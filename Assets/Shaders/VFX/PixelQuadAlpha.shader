Shader "Custom/VFX/PixelQuadAlpha"
{
    // PixelQuadBuffer로 만든 픽셀 쿼드를 정점 색(RGBA) 그대로 반투명하게 그린다. BrandWrapGlow와 달리 정점 알파가 투명도이고 발광(HDR 배율)이 없다.
    // 먼지처럼 "거의 보일락 말락"하는 반투명 이펙트용(VFX_DashDust). 한 인스턴스 안의 칸은 합집합으로 만들어 겹치지 않으므로 알파가 이중으로 쌓이지 않는다.
    Properties
    {
        [HideInInspector] _MainTex ("Unused", 2D) = "white" {}
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

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "PixelQuadAlphaPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return input.color;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
