Shader "Custom/WorldUIUnlit"
{
    Properties
    {
        [MainColor] _Color("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _MainTex("Base Map", 2D) = "white" {}
        
        // UI 필수 프로퍼티
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        ColorMask [_ColorMask]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 rawScreenPos : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _MainTex_ST;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                
                // 1. 오브젝트 좌표를 클립 공간으로 변환
                float4 clipPos = TransformObjectToHClip(IN.positionOS.xyz);
                
                // 2. [핵심] 정점 좌표를 물리 픽셀 격자에 강제 스냅
                // 화면 해상도를 기준으로 정점이 픽셀 경계선에 딱 맞게 위치를 조정합니다.
                float2 screenPos = (clipPos.xy / clipPos.w + 1.0) * 0.5 * _ScreenParams.xy;
                screenPos = round(screenPos); // 물리 픽셀 단위로 반올림
                clipPos.xy = (screenPos / (_ScreenParams.xy * 0.5) - 1.0) * clipPos.w;

                OUT.positionCS = clipPos;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                OUT.rawScreenPos = clipPos;
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 가상 해상도 설정 (640x360)
                float2 targetRes = float2(640, 360);
                float2 screenRes = _ScreenParams.xy;
                float2 ratio = screenRes / targetRes;

                // 3. 내부 픽셀 샘플링 스냅
                // 화면상의 좌표를 가상 해상도의 픽셀 중앙으로 옮겨 포인트 필터 효과를 냄
                float2 screenPos = IN.positionCS.xy;
                float2 snappedScreenPos = (floor(screenPos / ratio) + 0.5) * ratio;
                
                // 화면 오차만큼 UV 보정
                float2 delta = snappedScreenPos - screenPos;
                float2 snappedUV = IN.uv + delta.x * ddx(IN.uv) + delta.y * ddy(IN.uv);

                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, snappedUV) * IN.color;

                // 픽셀 경계선에서 반투명하게 뭉개지는 것 방지
                if (color.a < 0.5) discard; 

                return color;
            }
            ENDHLSL
        }
    }
}
