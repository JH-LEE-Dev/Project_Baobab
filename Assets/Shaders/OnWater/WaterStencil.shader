Shader "Custom/WaterStencil"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        // 셰이더 타일 프레임 애니메이션(StageTileFrameAnimation이 켠 머티리얼 사본만 x = 1). 켜지 않으면 UV를 그대로 쓴다.
        [HideInInspector] _TileFrameGrid("Tile Frame Grid", Vector) = (0,0,0,0)
        // 스텐실에 쓰는 값과 쓰기 마스크. 기본은 물 비트(1)만 쓴다.
        // 4스테이지 용암은 나무 그림자 비트(4)까지 켠 머티리얼(5/5)을 써서 그림자가 용암 위에 그려지지 않게 한다.
        [HideInInspector] _WaterStencilRef("Stencil Ref", Float) = 1
        [HideInInspector] _WaterStencilWriteMask("Stencil Write Mask", Float) = 1
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
            // 스텐실 설정: 항상 통과하고, 픽셀이 그려질 때 참조값(Ref)으로 스텐실 버퍼를 교체
            Stencil
            {
                Ref [_WaterStencilRef]
                ReadMask 1
                WriteMask [_WaterStencilWriteMask]
                Comp Always
                Pass Replace
            }

            // 색상 출력을 0,0,0,0으로 하기 위해 블렌딩 설정 및 깊이 쓰기 비활성화
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
                
                // 알파 값이 _Cutoff 보다 작으면 픽셀을 폐기(Discard)하여 스텐실도 기록되지 않게 함
                clip(color.a - _Cutoff);

                // 요구사항에 따라 색상은 0,0,0,0으로 출력
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
