Shader "Custom/IsometricShadowURP"
{
    Properties
    {
        [MainColor] _BaseColor("Shadow Color", Color) = (0, 0, 0, 0.5)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+1" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            // --- 스텐실 설정 강제 적용 ---
            // 1. Ref 0, Comp Equal, ReadMask 7: 
            //    스텐실의 하위 3비트(1:캐릭터, 2:나무, 4:그림자)가 모두 0인 깨끗한 땅에만 그립니다.
            // 2. Pass IncrSat, WriteMask 7:
            //    그림자가 그려지면 값을 1 증가시켜 0이 아니게 만듭니다. (중복 방지)
            Stencil
            {
                Ref 0
                Comp Equal
                ReadMask 7
                WriteMask 7
                Pass IncrSat
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
