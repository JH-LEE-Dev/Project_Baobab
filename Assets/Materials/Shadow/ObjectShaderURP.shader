Shader "Custom/ObjectShaderURP"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        
        // 그림자 셰이더의 _TreeStencilRef와 일치시켜야 함 (기본값 2)
        [IntRange] _StencilRef("Stencil Reference Value", Range(0, 255)) = 2
    }

    SubShader
    {
        // 나무의 불투명한 부분만 스텐실을 기록하기 위해 TransparentCutout 사용
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            // --- 스텐실 설정 추가 ---
            // 나무가 그려지는 픽셀에 2를 기록하여, 그림자 셰이더가 이 영역을 피하게 만듦
            Stencil
            {
                Ref [_StencilRef]
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Cutoff;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                
                // 알파 테스트: 텍스처의 투명한 영역은 스텐실 버퍼에 쓰지 않고 버림
                clip(color.a - _Cutoff);
                
                return color;
            }
            ENDHLSL
        }
    }
}
