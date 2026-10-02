// 원경 배경판 — 카메라 앞 일정 거리에 세운 그림 벽 한 장.
// 카메라가 내려다보고 안개가 짙어서, 그림을 맨 뒤에 두면 안개 낀 땅·나무에 전부 가려진다.
// 그래서 그림을 장면 안(안개가 짙어지는 거리)에 세운다 — 그보다 가까운 것은 그림 앞에,
// 먼 것(어차피 안개에 묻힌 것)은 그림 뒤로 사라진다. 그림은 안개를 받지 않는다.
// 아래쪽은 안개색으로 녹아들어 땅과 이어진다.
Shader "Proto/Backdrop"
{
    Properties
    {
        _MainTex   ("Image", 2D) = "black" {}
        _Tint      ("Tint", Color) = (1, 1, 1, 1)
        _FadeColor ("Fade Color (= 안개색, 스크립트가 넣는다)", Color) = (0.5, 0.5, 0.5, 1)
        _Haze      ("Haze (안개를 얼마나 덮나)", Range(0, 1)) = 0.25
        _FadeBottom ("Bottom Fade (아래에서 이만큼 안개로)", Range(0, 1)) = 0.35
        _Alpha     ("Alpha", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+400" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        ZWrite On
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "Backdrop"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST, _Tint, _FadeColor;
                float _Haze, _FadeBottom, _Alpha;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb * _Tint.rgb;
                c = lerp(c, _FadeColor.rgb, _Haze);
                float f = saturate(1.0 - i.uv.y / max(_FadeBottom, 1e-3));
                c = lerp(c, _FadeColor.rgb, f * f * (3.0 - 2.0 * f));
                return float4(c, 1.0);
            }
            ENDHLSL
        }
    }
}
