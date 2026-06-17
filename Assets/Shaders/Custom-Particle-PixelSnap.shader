Shader "Custom/2D/Particle-PixelSnap"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0
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
        ZWrite [_ZWrite]

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
            float4 _MainTex_TexelSize;

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

                // 1. 원본 화면 픽셀 좌표 구하기
                float3 vertexWS = TransformObjectToWorld(input.positionOS.xyz);
                float4 vertexCS = TransformWorldToHClip(vertexWS);
                
                float2 vertexNDC = vertexCS.xy / vertexCS.w;
                float2 vertexScreenPixel = (vertexNDC + float2(1.0, signY)) * 0.5 * _ScreenParams.xy;

                // 2. 완벽한 독립 정수 스냅
                // [원인 규명] 유니티 파티클 시스템에서 TransformObjectToWorld(0,0,0)은 개별 파티클의 중심이 아니라 
                // '파티클 시스템 오브젝트(에미터)'의 중심을 반환합니다. 
                // 즉, 기존 로직은 파티클의 크기가 아니라 '에미터로부터 떨어진 거리'를 기준으로 홀/짝 스냅을 잘못 적용하고 있었습니다!
                // 거리가 27일 때는 찢어지고, 28일 때는 안 찢어졌던 이유가 바로 이 때문입니다.
                //
                // [해결] 32x32 해상도를 정수배로 스케일링하면 픽셀 크기는 언제나 '짝수'가 됩니다.
                // 크기가 짝수인 사각형은 각 꼭짓점을 독립적으로 반올림(round)해도 수학적으로 절대 팻 픽셀이 발생하지 않습니다.
                vertexScreenPixel = round(vertexScreenPixel);
                
                // 다시 NDC 및 클립 공간으로 복원
                vertexNDC = vertexScreenPixel / _ScreenParams.xy * 2.0 - float2(1.0, signY);
                o.positionCS = vertexCS;
                o.positionCS.xy = vertexNDC * vertexCS.w;

                o.uv = input.uv;
                o.color = input.color * _Color;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                // Texture Sheet Animation 렌더링 시 부동소수점 오차로 인한 잘림을 방어하기 위한 미세 UV 조정
                float2 safeUV = input.uv + (_MainTex_TexelSize.xy * 0.001);
                
                half4 texColor = tex2D(_MainTex, safeUV);
                half4 finalColor = texColor * input.color;

                clip(finalColor.a - 0.01);
                return finalColor;
            }
            ENDHLSL
        }
    }
}
