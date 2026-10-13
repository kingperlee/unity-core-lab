Shader "UnityCoreLab/Rendering/OverdrawFillRate"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.2, 0.72, 1.0, 1.0)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend", Float) = 0
        [Toggle] _ZWrite("Depth Write", Float) = 1
        [IntRange] _FragmentIterations("Fragment Work Iterations", Range(1, 64)) = 8
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _FragmentIterations;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 value = input.uv * 2.0 - 1.0;
                float signal = 0.0;
                [loop]
                for (int iteration = 0;
                     iteration < (int)_FragmentIterations;
                     iteration++)
                {
                    value = frac(
                        value * 1.6180339 +
                        float2(0.137, 0.173) * (iteration + 1.0));
                    signal += dot(value, value);
                }

                half modulation = (half)(0.9 + frac(signal * 0.13) * 0.1);
                return half4(
                    _BaseColor.rgb * modulation,
                    _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
