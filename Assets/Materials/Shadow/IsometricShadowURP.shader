Shader "Custom/IsometricShadowURP"
{
    Properties
    {
        [MainColor] _BaseColor("Shadow Color", Color) = (0, 0, 0, 0.5)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        
        // --- 추가 설정 ---
        [IntRange] _TreeStencilRef("Tree Stencil Reference (나무 구분용)", Range(0, 255)) = 2
    }

    SubShader
    {
        // 일반 Transparent보다 큐를 조금 높여서, 정렬 순서가 겹칠 때 캐릭터 위에 그려지도록 유도
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+1" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            // --- 스텐실 설정 ---
            // 나무(Ref 2)가 이미 그려진 곳에는 그림자를 그리지 않음 (NotEqual)
            // 그림자가 캐릭터 위에 덮이려면 그림자 객체의 Sorting Order가 캐릭터보다 높아야 함
            Stencil
            {
                Ref [_TreeStencilRef]
                Comp NotEqual
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
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
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                
                // 알파 테스트: 투명한 영역은 스텐실을 채우지 않도록 자름
                clip(texColor.a - 0.1);

                return half4(_BaseColor.rgb, texColor.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
