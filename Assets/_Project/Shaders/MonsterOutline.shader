// 몬스터 외곽선 — 바닥 색에 묻히지 않게 몸 둘레에 얇은 선을 두른다.
// 렌더러의 머티리얼 목록 끝에 덧붙여 쓴다 (MonsterOutline.Apply). 스킨드 메시도 그대로 된다.
// 두께는 화면 기준이라 멀리 있어도 같은 굵기 (_OutlineWidth 0.003 ≈ 1080p에서 약 1.6px).
Shader "Proto/MonsterOutline"
{
    Properties
    {
        _OutlineColor ("Color", Color) = (0.95, 0.8, 1, 1)
        _OutlineWidth ("Width", Float) = 0.003
        _DepthPush ("Depth Push (m)", Float) = 0.35
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float _DepthPush;
            CBUFFER_END

            // 접선(tangent) 칸에 '부드럽게 평균 낸 노멀'을 넣어 둔다 (w = 2로 표시). 접선은 스킨드 메시에서도 뼈를 따라 돌아간다.
            // 면이 꺾이는 곳마다 노멀이 갈라져 있으면 그 자리에서 선이 끊겨 몸 안쪽에 줄이 생긴다.
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert (Attributes i)
            {
                Varyings o;
                // 카메라에서 조금 뒤로 민다 — 얇은 천 · 팔다리에서 껍데기가 몸 앞으로 뚫고 나오지 않게.
                // 그러면 실루엣 바깥에만 선이 남는다.
                float3 vs = TransformWorldToView(TransformObjectToWorld(i.positionOS.xyz));
                vs.z -= _DepthPush;
                float4 cs = TransformWViewToHClip(vs);
                float3 nOS = abs(i.tangentOS.w) > 1.5 ? i.tangentOS.xyz : i.normalOS;
                float3 nWS = TransformObjectToWorldNormal(nOS);
                float2 nCS = mul((float3x3)UNITY_MATRIX_VP, nWS).xy;
                float len = max(length(nCS), 1e-5);
                float2 off = nCS / len * _OutlineWidth * cs.w * 2.0;
                off.x *= _ScreenParams.y / _ScreenParams.x;   // 가로세로 같은 굵기
                cs.xy += off;
                o.positionCS = cs;
                return o;
            }

            half4 frag (Varyings i) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
