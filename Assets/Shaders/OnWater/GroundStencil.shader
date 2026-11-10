Shader "Custom/GroundStencil"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        // 셰이더 타일 프레임 애니메이션(StageTileFrameAnimation이 켠 머티리얼 사본만 x = 1). 켜지 않으면 UV를 그대로 쓴다.
        [HideInInspector] _TileFrameGrid("Tile Frame Grid", Vector) = (0,0,0,0)
        // 스텐실 쓰기 마스크. 기본은 물·육지 비트(3)만 쓴다.
        // 4스테이지는 용암이 켠 나무 그림자 비트(4)도 육지에서 다시 끄도록 7을 쓴 머티리얼을 쓴다(육지 위 그림자는 그대로).
        [HideInInspector] _GroundStencilWriteMask("Stencil Write Mask", Float) = 3
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        Pass
        {
            // 스텐실 설정: 
            // 1. Ref 2 (binary 10), WriteMask 3 (binary 11) 설정
            // 2. 이를 통해 육지 비트(2)를 쓰면서 동시에 물 비트(1)를 0으로 덮어씀 (무효화)
            Stencil
            {
                Ref 2
                ReadMask 3
                WriteMask [_GroundStencilWriteMask]
                Comp Always
                Pass Replace
            }

            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Include/TileFrameAnimation.hlsl"

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
                float4 _BaseMap_ST;
                half _Cutoff;
                float4 _TileFrameGrid;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 셰이더 타일 프레임 애니메이션(켠 머티리얼만): 지금 프레임 그림 자리로 UV를 옮겨 읽는다.
                IN.uv = TileFrameAnimationUV(IN.uv, _TileFrameGrid);
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                
                // 알파 값이 있는 픽셀에만 스텐실을 기록하기 위해 clip 사용
                clip(color.a - _Cutoff);

                // 색상은 0,0,0,0으로 출력
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
