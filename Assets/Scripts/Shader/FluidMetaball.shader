Shader "Custom/Fluid/GlobalMetaball2D"
{
    Properties
    {
        _FluidColor ("Fluid Color", Color) = (0.1, 0.45, 1.0, 0.85)
        _Threshold ("Threshold", Range(0.01, 10)) = 1.0
        _Softness ("Softness", Range(0.001, 2)) = 0.25
        _Alpha ("Alpha", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "GlobalMetaball2D"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            half4 _FluidColor;
            float _Threshold;
            float _Softness;
            float _Alpha;

            float _ParticleCount;
            float4 _Particles[512];

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);

                output.positionHCS = TransformWorldToHClip(worldPos);
                output.worldPos = worldPos;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float field = 0.0;
                int count = (int)_ParticleCount;

                [loop]
                for (int i = 0; i < 256; i++)
                {
                    if (i >= count)
                        break;

                    float2 p = _Particles[i].xy;
                    float radius = _Particles[i].z;

                    float2 d = input.worldPos.xy - p;
                    float distSq = dot(d, d);

                    field += (radius * radius) / max(distSq, 0.0001);
                }

                float alpha = smoothstep(_Threshold, _Threshold + _Softness, field);
                alpha *= _Alpha;

                if (alpha <= 0.001)
                    discard;

                half4 col = _FluidColor;
                col.a *= alpha;

                return col;
            }
            ENDHLSL
        }
    }
}