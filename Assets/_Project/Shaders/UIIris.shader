// 원형 화면 전환. 반지름 밖을 검게 칠한다.
// UGUI(Screen Space Overlay)에서 쓰는 셰이더라 파이프라인과 무관하게 동작한다.
Shader "Proto/UI/Iris"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0,0,0,1)
        _Radius ("Radius", Float) = 1.25
        _Soft ("Edge Softness", Float) = 0.006
        _Aspect ("Aspect", Float) = 1.777
        _RingColor ("Ring Color", Color) = (1,0.62,0.25,1)
        _RingWidth ("Ring Width", Float) = 0.006
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            fixed4 _Color;
            fixed4 _RingColor;
            float _Radius, _Soft, _Aspect, _RingWidth;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 d = i.uv - 0.5;
                d.x *= _Aspect;
                float dist = length(d);

                // 원 밖 = 검정
                float outside = smoothstep(_Radius - _Soft, _Radius + _Soft, dist);
                // 경계에 얇은 주황 테 — 조여 들어가는 원이 눈에 읽히게
                float ring = (1 - smoothstep(_RingWidth, _RingWidth + _Soft, abs(dist - _Radius))) * step(0.001, _Radius);

                fixed3 col = lerp(_Color.rgb, _RingColor.rgb, ring * (1 - outside));
                float a = saturate(outside + ring * _RingColor.a) * i.color.a;
                return fixed4(col, a);
            }
            ENDCG
        }
    }
}
