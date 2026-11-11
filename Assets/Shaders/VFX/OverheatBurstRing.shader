Shader "Custom/VFX/OverheatBurstRing"
{
    // 과열이 가득 차는 순간 캐릭터 발밑에서 바닥을 따라 퍼져 나가는 불 고리. 렌더링 전용이다.
    // StageOverheatBurst가 진원에 놓은 쿼드 하나에 그리고, 고리의 위치·세기는 나무와 같은 전역값
    // (_OverheatBurstWave, OverheatBurstWave.hlsl)을 읽으므로 고리가 닿는 순간 나무가 같이 달아오른다.
    // 텍스처를 쓰지 않는다. 월드 픽셀 격자(1 = 32픽셀)에 맞춰 그려 주변 픽셀 그림과 격자가 어긋나지 않는다.
    Properties
    {
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

        Blend One One
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "OverheatBurstRingPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Include/OverheatBurstWave.hlsl"

            // 고리 앞머리(가장 밝은 띠)와 그 뒤 불띠의 두께(바닥 평면 거리)
            static const float RingFrontWidth = 0.16;
            static const float RingFlameWidth = 0.5;
            // 불띠 뒤로 잔불이 흩어지며 사라지는 길이(바닥 평면 거리)
            static const float RingTailLength = 2.2;
            // 앞머리가 울퉁불퉁하게 일그러지는 폭(바닥 평면 거리)
            static const float RingWobble = 0.55;
            static const half3 RingFrontColor = half3(3.0, 2.2, 0.9);
            static const half3 RingFlameColor = half3(2.4, 1.0, 0.22);
            static const half3 RingTailColor = half3(1.0, 0.24, 0.04);

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 worldPos : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz).xy;
                return output;
            }

            float RingHash(float2 _p)
            {
                float3 p3 = frac(float3(_p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // 뭉글뭉글한 값(0~1). 고리 앞머리를 불길처럼 일그러뜨린다.
            float RingBlobNoise(float2 _p)
            {
                float2 cell = floor(_p);
                float2 f = _p - cell;
                float a = RingHash(cell);
                float b = RingHash(cell + float2(1.0, 0.0));
                float c = RingHash(cell + float2(0.0, 1.0));
                float d = RingHash(cell + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                if (_OverheatBurstWave.w <= 0.0)
                {
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);
                }

                // 월드 픽셀 격자에 맞춘다.
                float2 pixel = floor(input.worldPos * 32.0);
                float2 worldPos = (pixel + 0.5) / 32.0;

                float wobble = (RingBlobNoise(pixel / 9.0) - 0.5) * RingWobble;
                float behind = _OverheatBurstWave.z - (OverheatBurstGroundDistance(worldPos) + wobble);
                if (behind < 0.0 || behind > RingTailLength)
                {
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);
                }

                // 흩어짐: 세기가 줄수록 고리가 덩어리째 조각나 빠진다(통째로 옅어지거나 한꺼번에 꺼지지 않는다).
                // 덩어리 무늬(몇 픽셀 크기)와 두 픽셀 묶음 무늬를 섞어, 큰 틈이 먼저 벌어지고 가장자리가 자잘하게 부서진다.
                float strength = _OverheatBurstWave.w;
                float scatter = RingBlobNoise(pixel / 5.0 + 17.0) * 0.6 + RingHash(floor(pixel / 2.0) + 41.0) * 0.4;
                if (scatter >= strength)
                {
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);
                }

                // 식어 가는 색: 세기가 절반 아래로 내려가면 가장 밝은 앞머리가 불띠 색으로, 불띠가 잔불 색으로 한 단계씩 식는다.
                // 밝기도 두 단계로만 줄인다(픽셀 그림이라 부드럽게 옅어지는 것보다 단계로 떨어지는 편이 어울린다).
                bool bCooling = strength < 0.5;
                half brightness = bCooling ? 0.75h : 1.0h;

                if (behind < RingFrontWidth)
                {
                    return half4((bCooling ? RingFlameColor : RingFrontColor) * brightness, 0.0h);
                }

                if (behind < RingFlameWidth)
                {
                    return half4((bCooling ? RingTailColor : RingFlameColor) * brightness, 0.0h);
                }

                // 잔불: 뒤로 갈수록 픽셀이 듬성듬성 빠지며 사라진다.
                float gone = (behind - RingFlameWidth) / (RingTailLength - RingFlameWidth);
                if (RingHash(pixel + 3.0) < gone * 1.15)
                {
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);
                }

                return half4(RingTailColor * brightness, 0.0h);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
