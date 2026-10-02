// 카타나 칠하기 — 모션 팩 칼은 통짜 메시 하나에 재질 하나라 부위별 색이 없다.
// 칼 길이 방향 좌표로 부위를 나눠 칠한다:
//   칼자루 머리(카시라) · 손잡이(감은 끈 + 마름모 무늬) · 코등이(쓰바) · 날밑 고리(하바키) · 칼날(끝으로 갈수록 밝게)
// 경계값은 메시 단면 폭을 재서 찾았다 (Grru_Katana: 길이 축 Z, -0.187 ~ 1.131).
Shader "Proto/KatanaPaint"
{
    Properties
    {
        _AxisDir    ("Length Axis (object space)", Vector) = (0, 0, 1, 0)
        _PommelEnd  ("Pommel End", Float) = -0.155
        _HandleEnd  ("Handle End", Float) = 0.138
        _GuardEnd   ("Guard End", Float) = 0.178
        _HabakiEnd  ("Habaki End", Float) = 0.212
        _TipPos     ("Tip", Float) = 1.131

        _Pommel     ("Pommel", Color) = (0.12, 0.11, 0.12, 1)
        _Wrap       ("Handle Wrap (ito)", Color) = (0.06, 0.06, 0.09, 1)
        _Skin       ("Handle Diamonds (same)", Color) = (0.82, 0.78, 0.66, 1)
        _WrapScale  ("Wrap Pattern Density", Float) = 70
        _Guard      ("Guard (tsuba)", Color) = (0.20, 0.16, 0.10, 1)
        _Habaki     ("Habaki", Color) = (0.95, 0.72, 0.32, 1)
        _BladeBase  ("Blade Base", Color) = (0.58, 0.60, 0.64, 1)
        _BladeTip   ("Blade Tip", Color) = (0.86, 0.89, 0.94, 1)
        _Shine      ("Blade Shine", Range(0, 2)) = 1.1
        _Gloss      ("Blade Gloss", Range(4, 256)) = 64
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _AxisDir;
                float _PommelEnd, _HandleEnd, _GuardEnd, _HabakiEnd, _TipPos, _WrapScale, _Shine, _Gloss;
                float4 _Pommel, _Wrap, _Skin, _Guard, _Habaki, _BladeBase, _BladeTip;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 posOS : TEXCOORD0;
                float3 posWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fog : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.posOS = v.positionOS.xyz;
                o.posWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float3 ax = normalize(_AxisDir.xyz);
                float s = dot(i.posOS, ax);

                // 손잡이 끈 무늬 — 축을 따라 비스듬히 감긴 띠 두 개가 엇갈려 마름모가 생긴다
                float3 side = i.posOS - ax * s;
                float ang = atan2(side.y, side.x) / 6.2831853;
                float u = s * _WrapScale;
                float d1 = abs(frac(u + ang * 2.0) - 0.5), d2 = abs(frac(u - ang * 2.0) - 0.5);
                float skin = step(0.3, d1) * step(0.3, d2);          // 끈 사이로 보이는 마름모
                float3 handle = lerp(_Wrap.rgb, _Skin.rgb, skin);

                float bladeT = saturate((s - _HabakiEnd) / max(1e-3, _TipPos - _HabakiEnd));
                float3 blade = lerp(_BladeBase.rgb, _BladeTip.rgb, bladeT);

                float3 albedo = s < _PommelEnd ? _Pommel.rgb
                              : s < _HandleEnd ? handle
                              : s < _GuardEnd  ? _Guard.rgb
                              : s < _HabakiEnd ? _Habaki.rgb
                              : blade;
                float metal = s >= _HabakiEnd ? 1.0 : (s >= _HandleEnd && s < _HabakiEnd ? 0.6 : 0.0);

                float4 shadowCoord = TransformWorldToShadowCoord(i.posWS);
                Light L = GetMainLight(shadowCoord);
                float3 n = normalize(i.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(i.posWS));
                float ndl = saturate(dot(n, L.direction));
                float3 h = normalize(L.direction + v);
                float spec = pow(saturate(dot(n, h)), _Gloss) * _Shine * metal;
                float rim = pow(1.0 - saturate(dot(n, v)), 3.0) * 0.35 * metal;

                float3 ambient = SampleSH(n);
                float3 lit = albedo * (ambient + L.color * ndl * L.shadowAttenuation)
                           + L.color * spec * L.shadowAttenuation + rim * _BladeTip.rgb;
                lit = MixFog(lit, i.fog);
                return float4(lit, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
