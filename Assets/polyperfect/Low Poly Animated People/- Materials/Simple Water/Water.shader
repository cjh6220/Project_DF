// URP 포팅판. 원본(빌트인 surface 셰이더)은 Water.builtin.shader.bak 로 남겨 두었다.
//
// 빌트인 surface 셰이더는 URP에서 쓸 수 있는 패스가 없어 마젠타로 나온다.
// 동작은 그대로 옮겼다 — 정점 노이즈 파도 + 깊이 기반 가장자리 페이드 + PBR 반투명.
Shader "Custom/Water"
{
    Properties
    {
        _Color       ("Color", Color) = (1,1,1,1)
        [MainTexture] _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness  ("Smoothness", Range(0,1)) = 0.5
        _Metallic    ("Metallic", Range(0,1)) = 0.0

        _Amount      ("Extrusion Amount", Range(-1,1)) = 0.5
        _NoiseSpeed  ("Noise Speed", Float) = 0.5
        _NoiseSize   ("Noise Size", Float) = 0.5
        _InvFade     ("Soft Factor", Range(0.01,3.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                half   _Glossiness;
                half   _Metallic;
                float  _Amount;
                float  _NoiseSpeed;
                float  _NoiseSize;
                float  _InvFade;
            CBUFFER_END

            // ── 원본과 같은 값 노이즈 ─────────────────────────────
            float NoiseRandomValue(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
            }

            float NoiseInterpolate(float a, float b, float t)
            {
                return (1.0 - t) * a + (t * b);
            }

            float ValueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);

                float r0 = NoiseRandomValue(i + float2(0.0, 0.0));
                float r1 = NoiseRandomValue(i + float2(1.0, 0.0));
                float r2 = NoiseRandomValue(i + float2(0.0, 1.0));
                float r3 = NoiseRandomValue(i + float2(1.0, 1.0));

                float bottom = NoiseInterpolate(r0, r1, f.x);
                float top    = NoiseInterpolate(r2, r3, f.x);
                return NoiseInterpolate(bottom, top, f.y);
            }

            float SimpleNoise(float2 uv, float scale)
            {
                float t = 0.0;
                t += ValueNoise(uv * scale / 1.0) * 0.125;
                t += ValueNoise(uv * scale / 2.0) * 0.25;
                t += ValueNoise(uv * scale / 4.0) * 0.5;
                return t;
            }
            // ─────────────────────────────────────────────────────

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float4 screenPos   : TEXCOORD3;
                float  eyeDepth    : TEXCOORD4;
                float  fogCoord    : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // 파도 — 원본과 동일하게 오브젝트 공간 Y를 노이즈로 밀어 올린다
                float wave = SimpleNoise(IN.uv + (_Time.x * _NoiseSpeed), _NoiseSize);
                IN.positionOS.y += _Amount * wave;

                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   n = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS   = n.normalWS;
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.screenPos  = ComputeScreenPos(p.positionCS);
                OUT.eyeDepth   = -TransformWorldToView(p.positionWS).z;
                OUT.fogCoord   = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;

                // 가장자리 페이드 — 깊이 텍스처가 꺼져 있으면 rawZ가 0이라 그냥 건너뛴다
                float2 screenUV = IN.screenPos.xy / max(IN.screenPos.w, 1e-5);
                float  rawZ     = SampleSceneDepth(screenUV);
                float  fade     = 1.0;
                if (rawZ > 0.0)
                {
                    float sceneZ = LinearEyeDepth(rawZ, _ZBufferParams);
                    fade = 1.0 - saturate(_InvFade * (sceneZ - IN.eyeDepth));
                }

                SurfaceData surface = (SurfaceData)0;
                surface.albedo     = c.rgb;
                surface.metallic   = _Metallic;
                surface.smoothness = _Glossiness;
                surface.normalTS   = float3(0, 0, 1);
                surface.occlusion  = 1.0;
                surface.alpha      = c.a * fade;

                InputData inputData = (InputData)0;
                inputData.positionWS          = IN.positionWS;
                inputData.normalWS            = normalize(IN.normalWS);
                inputData.viewDirectionWS     = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord         = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord            = IN.fogCoord;
                inputData.bakedGI             = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = screenUV;
                inputData.shadowMask          = half4(1, 1, 1, 1);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a   = surface.alpha;
                return color;
            }
            ENDHLSL
        }

        // 그림자 — 파도 때문에 정점이 움직이므로 같은 변형을 적용해야 한다
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   shadowVert
            #pragma fragment shadowFrag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                half   _Glossiness;
                half   _Metallic;
                float  _Amount;
                float  _NoiseSpeed;
                float  _NoiseSize;
                float  _InvFade;
            CBUFFER_END

            float ShadowNoiseRandomValue(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
            }

            float ShadowValueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);
                float r0 = ShadowNoiseRandomValue(i + float2(0.0, 0.0));
                float r1 = ShadowNoiseRandomValue(i + float2(1.0, 0.0));
                float r2 = ShadowNoiseRandomValue(i + float2(0.0, 1.0));
                float r3 = ShadowNoiseRandomValue(i + float2(1.0, 1.0));
                float bottom = lerp(r0, r1, f.x);
                float top    = lerp(r2, r3, f.x);
                return lerp(bottom, top, f.y);
            }

            float ShadowSimpleNoise(float2 uv, float scale)
            {
                float t = 0.0;
                t += ShadowValueNoise(uv * scale / 1.0) * 0.125;
                t += ShadowValueNoise(uv * scale / 2.0) * 0.25;
                t += ShadowValueNoise(uv * scale / 4.0) * 0.5;
                return t;
            }

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            float4 shadowVert(ShadowAttributes IN) : SV_POSITION
            {
                IN.positionOS.y += _Amount * ShadowSimpleNoise(IN.uv + (_Time.x * _NoiseSpeed), _NoiseSize);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 shadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
