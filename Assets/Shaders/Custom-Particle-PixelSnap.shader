Shader "Custom/2D/Particle-PixelSnap"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags 
        { 
            "Queue" = "Transparent" 
            "IgnoreProjector" = "True" 
            "RenderType" = "Transparent" 
            "PreviewType" = "Plane"
            "RenderPipeline" = "UniversalPipeline" 
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                half4 color         : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                half4 color         : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize; // (1/width, 1/height, width, height)

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            Varyings UnlitVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                #if UNITY_UV_STARTS_AT_TOP
                float signY = -1.0;
                #else
                float signY = 1.0;
                #endif

                // ──────────────────────────────────────────────
                // [버텍스 위치 스냅] 팻픽셀(ㄱ,ㄴ) 방지
                // ──────────────────────────────────────────────
                // 1. 스크린 픽셀 좌표 산출
                float3 vertexWS = TransformObjectToWorld(input.positionOS.xyz);
                float4 vertexCS = TransformWorldToHClip(vertexWS);
                float2 vertexNDC = vertexCS.xy / vertexCS.w;
                float2 screenPixel = (vertexNDC + float2(1.0, signY)) * 0.5 * _ScreenParams.xy;

                // 2. floor 스냅
                //    round()는 4개 꼭짓점이 각자 다른 방향으로 스냅될 수 있어
                //    쿼드 크기가 ±1px 변동됩니다 (팻픽셀의 원인).
                //
                //    floor()는 항상 같은 방향(음의 무한대)으로 스냅하므로
                //    수학적으로 floor(a + n) - floor(a) = n (n이 정수)이 보장됩니다.
                //    → 쿼드의 스크린 픽셀 크기가 정수인 한, 크기가 절대 변하지 않습니다.
                screenPixel = floor(screenPixel);

                // 3. NDC / 클립 공간으로 복원
                vertexNDC = screenPixel / _ScreenParams.xy * 2.0 - float2(1.0, signY);
                o.positionCS = vertexCS;
                o.positionCS.xy = vertexNDC * vertexCS.w;

                o.uv = input.uv;
                o.color = input.color * _Color;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                // ──────────────────────────────────────────────
                // [텍셀 중심 스냅] ㅡ 현상 방지
                // ──────────────────────────────────────────────
                // GPU의 하드웨어 UV 보간에는 부동소수점 오차가 있습니다.
                // 홀수 배율에서 텍셀 경계가 픽셀 중심과 정확히 겹치면,
                // 오차에 의해 인접 텍셀을 잘못 샘플링하여 가로줄(ㅡ)이 생깁니다.
                //
                // 이를 방지하기 위해 UV를 해당 텍셀의 정중앙으로 명시적 스냅합니다.
                // Point 필터링에 의존하지 않고 셰이더에서 직접 보정하므로
                // 어떤 배율에서든 정확한 텍셀을 보장합니다.
                //
                // _MainTex_TexelSize: (1/width, 1/height, width, height)
                //   .zw = 전체 텍스처 해상도 (예: 96, 32)
                //   .xy = 텍셀 1개의 UV 크기 (예: 1/96, 1/32)
                float2 texelCoord = input.uv * _MainTex_TexelSize.zw; // UV → 텍셀 좌표
                texelCoord = floor(texelCoord) + 0.5;                  // 텍셀 정중앙으로 스냅
                float2 snappedUV = texelCoord * _MainTex_TexelSize.xy; // 텍셀 좌표 → UV

                half4 texColor = tex2D(_MainTex, snappedUV);
                half4 finalColor = texColor * input.color;

                clip(finalColor.a - 0.01);
                return finalColor;
            }
            ENDHLSL
        }
    }
}
