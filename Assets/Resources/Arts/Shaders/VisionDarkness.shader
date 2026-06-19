Shader "Custom/VisionDarkness"
{
    // 플레이어(_Center) 주변 _Radius 안쪽은 투명, 바깥쪽은 _Color(검정)으로 어둡게.
    // 월드 좌표 거리 기반이라 파이프라인/렌더러 종류와 무관하게 동작한다.
    Properties
    {
        _Color  ("Dark Color", Color) = (0,0,0,1)
        _Center ("Center (world xy)", Vector) = (0,0,0,0)
        _Radius ("Radius", Float) = 4
        _Soft   ("Soft Edge", Float) = 1.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 worldXY : TEXCOORD0; };

            float4 _Color;
            float4 _Center;
            float  _Radius;
            float  _Soft;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 ws = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(ws);
                OUT.worldXY = ws.xy;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float d = distance(IN.worldXY, _Center.xy);
                float a = smoothstep(_Radius, _Radius + max(0.001, _Soft), d);
                return half4(_Color.rgb, _Color.a * a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
