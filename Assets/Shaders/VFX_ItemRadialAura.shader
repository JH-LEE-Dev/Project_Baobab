Shader "Custom/VFX/URP2D_ItemRadialAura"
{
    Properties
    {
        [Header(Color and Bloom Intensity)]
        [HDR] _CoreColor ("Core Center Color", Color) = (3.5, 3.2, 2.0, 1.0)
        [HDR] _BeamColor ("Primary Beam Color", Color) = (2.5, 1.8, 0.3, 1.0)
        [HDR] _OuterColor ("Outer Glow Color", Color) = (1.2, 0.5, 0.05, 1.0)
        _Intensity ("Overall Intensity Multiplier", Float) = 1.0
        _BloomMultiplier ("Bloom Intensity Multiplier", Range(0.5, 10.0)) = 1.5

        [Header(Pixel Perfect Settings)]
        _PixelateEnabled ("Enable Pixel Style (1: ON, 0: OFF)", Float) = 1.0
        _PixelResolution ("Pixel Grid Resolution (PPU)", Range(8.0, 128.0)) = 32.0
        _ColorBandingSteps ("Color Banding Steps", Range(1.0, 16.0)) = 4.0

        [Header(Ray Count and Expanding Fan Width)]
        _RayCount ("Ray Count (Exact Number)", Range(1.0, 32.0)) = 6.0
        _AngleJitter ("Angle Irregular Jitter", Range(0.0, 0.8)) = 0.4
        _BeamMinWidth ("Beam Min Width (Narrow Fan)", Range(0.04, 0.35)) = 0.12
        _BeamMaxWidth ("Beam Max Width (Wide Fan)", Range(0.15, 0.8)) = 0.45
        _BeamBlur ("Beam Blur Softness (Sharp vs Blur)", Range(0.01, 1.0)) = 0.22

        [Header(Rotation and Dynamics)]
        _RotationSpeed ("Rotation Speed (-20 to 20)", Range(-20.0, 20.0)) = 1.2
        _SpeedVariation ("Per-Ray Speed Variation", Range(0.0, 1.0)) = 0.45
        _FlickerSpeed ("Shimmer Frequency Speed", Float) = 3.0
        _FlickerAmount ("Shimmer Flashing Depth", Range(0.0, 1.0)) = 0.4

        [Header(One Shot Burst and Staggered Lifecycles)]
        _EnableBurstMode ("Enable One-Shot Burst Mode", Float) = 1.0
        _BurstProgress ("Burst Progress (0.0 to 1.0)", Range(0.0, 1.0)) = 0.0
        _StaggerSpread ("Staggered Fadeout Spread", Range(0.0, 0.4)) = 0.25

        [Header(Radial Reach and Outer Soft Alpha Fadeout)]
        _Center ("Effect Center UV", Vector) = (0.5, 0.5, 0, 0)
        _OuterRadius ("Max Beam Reach", Range(0.2, 1.0)) = 0.48
        _RadialSoftness ("Outer Tip Soft Fadeout Ratio", Range(0.1, 0.9)) = 0.55
        _DistanceFalloff ("Distance Falloff Power", Range(0.1, 3.0)) = 0.9
        _CoreGlowRadius ("Core Glow Radial Size", Range(0.0, 0.3)) = 0.15
        _InnerRadius ("Inner Hole Radius", Range(0.0, 0.3)) = 0.0
        _InnerFade ("Inner Softness Width", Range(0.001, 0.2)) = 0.05
        _EdgeSoftness ("Quad Edge Soft Fade Width", Range(0.01, 0.3)) = 0.08

        [Header(Rendering and Blend Mode)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1.0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1.0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 0.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline"="UniversalPipeline" 
            "IgnoreProjector"="True"
        }
        
        Blend [_SrcBlend] [_DstBlend]
        Cull [_Cull]
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "ItemRadialAura"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _BeamColor;
                float4 _OuterColor;
                float4 _Center;
                float _Intensity;
                float _BloomMultiplier;
                float _PixelateEnabled;
                float _PixelResolution;
                float _ColorBandingSteps;
                float _RayCount;
                float _AngleJitter;
                float _BeamMinWidth;
                float _BeamMaxWidth;
                float _BeamBlur;
                float _RotationSpeed;
                float _SpeedVariation;
                float _FlickerSpeed;
                float _FlickerAmount;
                float _EnableBurstMode;
                float _BurstProgress;
                float _StaggerSpread;
                float _EdgeSoftness;
                float _OuterRadius;
                float _RadialSoftness;
                float _DistanceFalloff;
                float _CoreGlowRadius;
                float _InnerRadius;
                float _InnerFade;
            CBUFFER_END

            // Procedural pseudo-random hash function
            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                bool isPixelMode = (_PixelateEnabled > 0.5);

                // 1. 픽셀 그리드 양자화 (Pixel Perfect Snapping)
                float2 localPos = (input.uv - _Center.xy) * 2.0; // [-1, +1]

                if (isPixelMode)
                {
                    float halfRes = max(2.0, _PixelResolution * 0.5);
                    localPos = (floor(localPos * halfRes) + 0.5) / halfRes;
                }

                // 2. 극좌표계 변환 (양자화된 픽셀 좌표로부터 계산)
                float radius = length(localPos) * 0.5; // [0, 0.5]
                float pixelAngle = atan2(localPos.y, localPos.x); // [-PI, PI]

                // 3. 버스트 진행도에 따른 셰이더 내부 픽셀 확장(Growth) 및 전체 페이드 계산
                float globalReachMultiplier = 1.0;
                float globalFade = 1.0;

                if (_EnableBurstMode > 0.5)
                {
                    // 초반(0.0 ~ 0.35) 픽셀이 중심에서 외곽으로 뻗어나가는 확장 모션 (Pixel Growth)
                    float popProgress = saturate(_BurstProgress / 0.35);
                    globalReachMultiplier = 1.0 - pow(1.0 - popProgress, 3.0); // Cubic Ease Out

                    // 후반(0.7 ~ 1.0) 부드러운 소멸 곡선 (절벽 끊김 방지)
                    if (_BurstProgress > 0.65)
                    {
                        float fadeProgress = saturate((1.0 - _BurstProgress) / 0.35);
                        globalFade = smoothstep(0.0, 1.0, fadeProgress);
                    }
                }

                // 4. 정확한 광선 개수 및 각도 스텝
                int exactRayCount = (int)clamp(floor(_RayCount + 0.5), 1.0, 32.0);
                float sectorStep = TWO_PI / (float)exactRayCount;

                // 5. 개별 부채꼴 광선 루프 연산
                float totalRays = 0.0;

                for (int k = 0; k < 32; ++k)
                {
                    if (k >= exactRayCount) break;

                    float kF = (float)k;

                    // 고유 해시 추출
                    float hJitter = Hash11(kF * 17.77 + 5.19);
                    float hSpeed  = Hash11(kF * 43.21 + 8.76);
                    float hWidth  = Hash11(kF * 37.71 + 1.23);
                    float hDelay  = Hash11(kF * 71.13 + 3.47);
                    float hLife   = Hash11(kF * 93.31 + 4.81);
                    float hReach  = Hash11(kF * 53.19 + 6.19);

                    // 광선별 고유 생성 각도 (불규칙 지터)
                    float jitter = (hJitter - 0.5) * _AngleJitter * sectorStep;
                    float initialAngle = kF * sectorStep + jitter;

                    // 광선별 개별 회전 속도 (유기적 편차)
                    float speedMult = lerp(1.0 - _SpeedVariation, 1.0 + _SpeedVariation, hSpeed);
                    float raySpeed = _RotationSpeed * speedMult;

                    // 생성된 이후 회전 시작
                    float rotDelta = 0.0;
                    if (_EnableBurstMode > 0.5)
                    {
                        rotDelta = _BurstProgress * raySpeed * TWO_PI;
                    }
                    else
                    {
                        rotDelta = _Time.y * raySpeed;
                    }
                    float currentRayAngle = initialAngle + rotDelta;

                    // 픽셀 각도와의 최단 각도 거리 ([-PI, PI] 연속 계산)
                    float angleDiff = fmod(pixelAngle - currentRayAngle + 3.0 * PI, TWO_PI) - PI;
                    float absAngleDiff = abs(angleDiff);

                    // 동적 도달 거리 (버스트 초반에 픽셀이 뻗어나감)
                    float baseReach = lerp(0.85, 1.2, hReach) * _OuterRadius;
                    float currentReach = baseReach * globalReachMultiplier;
                    float innerReach = currentReach * saturate(1.0 - _RadialSoftness);

                    float radialFade = 0.0;
                    if (radius < currentReach)
                    {
                        radialFade = smoothstep(currentReach, innerReach, radius);
                        radialFade = pow(radialFade, _DistanceFalloff);
                    }

                    // 외곽으로 갈수록 넓어지는 부채꼴(Expanding Fan) 빔 폭
                    float widthWeight = pow(hWidth, 1.3);
                    float baseWidth = lerp(_BeamMinWidth, _BeamMaxWidth, widthWeight);
                    float fanExpand = lerp(0.8, 1.25, saturate(radius / max(0.001, currentReach)));
                    float beamWidthRad = baseWidth * sectorStep * fanExpand;

                    float normDist = absAngleDiff / max(0.0001, beamWidthRad);
                    float edgeFactor = 0.0;

                    if (isPixelMode)
                    {
                        // 픽셀 모드: 계단화된 도트 경계 (32 PPU 그리드 스냅과 결합)
                        if (normDist <= 1.0)
                        {
                            float steps = max(1.0, _ColorBandingSteps);
                            edgeFactor = ceil(saturate(1.0 - normDist) * steps) / steps;
                            radialFade = ceil(radialFade * steps) / steps;
                        }
                    }
                    else
                    {
                        // 아날로그 모드: 부드러운 블러 그라디언트
                        float blurFeather = max(0.005, _BeamBlur * 0.5);
                        edgeFactor = smoothstep(1.0, max(0.0, 1.0 - blurFeather * 2.0), normDist);
                    }

                    float wedge = edgeFactor * radialFade;

                    // 생명주기 및 단발성 시차 소멸
                    float lifeEnvelope = 1.0;
                    if (_EnableBurstMode > 0.5)
                    {
                        float birthTime = hDelay * _StaggerSpread;
                        float lifeDuration = lerp(0.55, 0.95, hLife) * (1.0 - _StaggerSpread);

                        if (_BurstProgress < birthTime)
                        {
                            lifeEnvelope = 0.0;
                        }
                        else
                        {
                            float localAge = saturate((_BurstProgress - birthTime) / max(0.001, lifeDuration));
                            lifeEnvelope = sin(localAge * PI);
                            lifeEnvelope = pow(saturate(lifeEnvelope), 1.5);
                        }
                    }
                    else
                    {
                        float flashCycle = sin(_Time.y * _FlickerSpeed * lerp(0.8, 2.0, hWidth) + hDelay * 12.0);
                        float flash = saturate(flashCycle * 0.5 + 0.5);
                        lifeEnvelope = lerp(1.0, flash, _FlickerAmount);
                    }

                    totalRays += wedge * lifeEnvelope;
                }

                // 6. 마스킹 및 단면 처리
                float innerMask = smoothstep(_InnerRadius, _InnerRadius + _InnerFade, radius);
                float totalAura = totalRays * innerMask * globalFade;

                // 7. 중앙 방사형 원형 코어 글로우 (Center Core Glow)
                float activeCoreRadius = _CoreGlowRadius * globalReachMultiplier;
                float coreFactor = saturate(1.0 - (radius / max(0.001, activeCoreRadius)));
                coreFactor = pow(coreFactor, 1.8);

                float coreLife = 1.0;
                if (_EnableBurstMode > 0.5)
                {
                    float coreProgress = sin(_BurstProgress * PI);
                    coreLife = saturate(coreProgress * 1.5) * globalFade;
                }

                if (isPixelMode)
                {
                    float steps = max(1.0, _ColorBandingSteps);
                    coreFactor = ceil(coreFactor * steps) / steps;
                }

                float coreGlow = coreFactor * coreLife;

                // 8. 쿼드 외곽 가장자리 페이드
                float edgeDistX = min(input.uv.x, 1.0 - input.uv.x);
                float edgeDistY = min(input.uv.y, 1.0 - input.uv.y);
                float quadEdgeFade = saturate(min(edgeDistX, edgeDistY) / max(0.001, _EdgeSoftness));
                quadEdgeFade = smoothstep(0.0, 1.0, quadEdgeFade);

                totalAura *= quadEdgeFade;

                // 9. HDR 컬러 합성 + Bloom 배율 증폭
                half3 beamRgb = lerp(_OuterColor.rgb, _BeamColor.rgb, saturate(totalRays * 1.2));
                float totalIntensity = _Intensity * _BloomMultiplier;
                half3 finalRgb = (beamRgb * totalAura + _CoreColor.rgb * coreGlow) * totalIntensity * input.color.rgb;

                // 10. 알파(Alpha) 출력 (부드러운 소멸 보장)
                half finalAlpha = saturate(totalAura + coreGlow) * quadEdgeFade * input.color.a * _BeamColor.a;

                if (finalAlpha <= 0.0005)
                {
                    discard;
                }

                return half4(finalRgb, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
