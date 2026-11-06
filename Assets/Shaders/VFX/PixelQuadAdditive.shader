Shader "Custom/VFX/PixelQuadAdditive"
{
    Properties
    {
        _Intensity ("Intensity (alpha multiplier)", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha One
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "PixelQuadAdditivePass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                return output;
            }

            // 정점 색(RGB)에 정점 알파 x _Intensity를 곱해 배경에 더한다. 바닥 불빛처럼 은은하게 밝히는 픽셀 쿼드용이다.
            // _Intensity는 메쉬를 다시 만들지 않고 전체 세기만 바꿀 때 MaterialPropertyBlock으로 넣는다(기본 1).
            half4 frag(Varyings input) : SV_Target
            {
                return half4(input.color.rgb, input.color.a * _Intensity);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
