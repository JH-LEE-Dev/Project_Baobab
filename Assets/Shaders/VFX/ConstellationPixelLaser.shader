Shader "Custom/VFX/ConstellationPixelLaser"
{
    Properties
    {
        [Header(Color and HDR Settings)]
        [HDR] _CoreColor ("Core Color (중심 코어)", Color) = (1.2, 1.35, 1.5, 1.0)
        [HDR] _HeadColor ("Impact Head Color (선단 헤드 발광)", Color) = (1.3, 1.5, 1.8, 1.0)
        [HDR] _TailColor ("Tail Dissolve Color (꼬리 성운 잔상)", Color) = (0.15, 0.35, 0.7, 1.0)
        _HeadWidth ("Impact Head Width (선단 헤드 폭)", Range(0.005, 0.1)) = 0.025

        [Header(Progress and Trail Settings)]
        _Progress ("Progress (레이저 전진 진행도)", Range(0.0, 2.0)) = 1.0
        _TailLength ("Tail Length (유성 꼬리 길이)", Range(0.05, 1.5)) = 0.85

        [Header(Emission and Alpha Control)]
        _EmissionBoost ("Emission Boost (전체 발광 배율)", Float) = 1.0
        _AlphaFactor ("Alpha Factor (알파 트윈 스케일)", Range(0.0, 1.0)) = 1.0
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
            Name "ConstellationPixelLaserPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _HeadColor;
                half4 _TailColor;

                float _HeadWidth;
                float _Progress;
                float _TailLength;

                float _EmissionBoost;
                float _AlphaFactor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                // 정갈한 정통 32 PPU 일직선 픽셀 도트 변환 (웨이브 완전 제거)
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half safeAlpha = _AlphaFactor > 0.001h ? (half)_AlphaFactor : 1.0h;

                // 1. 유성(Shooting Star) 헤드 및 꼬리(Trail) 마스킹
                float headPos = _Progress;
                float tailLength = max(0.05f, _TailLength);
                float tailPos = headPos - tailLength;

                // 선단 헤드 앞쪽은 아직 도달하지 않았으므로 즉시 폐기 (GPU 부하 절감)
                if (input.uv.x > headPos)
                {
                    discard;
                }

                // 꼬리 뒤쪽은 이미 지나갔으므로 완전 소멸 폐기
                if (input.uv.x < tailPos)
                {
                    discard;
                }

                // 2. 도트 기본 성좌 발광 색상
                half3 finalRgb = input.color.rgb;
                half finalAlpha = input.color.a;

                // 3. 꼬리 구간 (tailPos ~ headPos) 점진적 페이드아웃 알파 (유선형 파워 커브)
                float trailFactor = saturate((input.uv.x - tailPos) / tailLength);
                float softTrail = pow(trailFactor, 1.25f); // 뭉툭한 절단 방지, 자연스러운 유선형 감쇄

                // 4. 선단 헤드 별빛 플래시 vs 코스믹 성운 꼬리 그라데이션
                float distToHead = abs(input.uv.x - headPos);
                if (distToHead < max(0.005f, _HeadWidth) && headPos < 0.999f)
                {
                    float headIntensity = (1.0f - (distToHead / max(0.005f, _HeadWidth))) * 0.8f;
                    finalRgb += _HeadColor.rgb * (half)headIntensity;
                    finalAlpha = 1.0h;
                }
                else
                {
                    // Cosmic Starlight Gradient: 헤드 쪽 네온 시안 -> 꼬리 끝자락의 신비로운 바이올렛 성운으로 점진적 색상 전이
                    half3 gradientRgb = lerp(_TailColor.rgb, finalRgb, (half)softTrail);
                    finalRgb = lerp(gradientRgb, _CoreColor.rgb, 0.25h);
                    finalAlpha *= (half)softTrail;
                }

                // 5. 전체 발광 및 알파 트윈
                finalRgb *= (half)_EmissionBoost;
                finalAlpha *= safeAlpha;

                return half4(finalRgb, saturate(finalAlpha));
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
