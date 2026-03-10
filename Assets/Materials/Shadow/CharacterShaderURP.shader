Shader "Custom/CharacterShaderURP"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        
        // 캐릭터는 1번 스텐실을 사용하여 나무 그림자(Ref 2 제외)가 그려질 수 있게 함
        [IntRange] _StencilRef("Stencil Reference Value", Range(0, 255)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            // --- 캐릭터 스텐실 설정 ---
            // 캐릭터는 Ref 1을 기록합니다.
            // 그림자 셰이더는 Ref 2(나무)가 아닌 곳에만 그려지므로, 1인 캐릭터 위에는 그림자가 그려집니다.
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
