Shader "Custom/VFX/ConstellationPixelLaser"
{
    Properties
    {
        [Header(Color and HDR Settings)]
        [HDR] _CoreColor ("Core Color (중심 코어)", Color) = (1.2, 1.35, 1.5, 1.0)
        [HDR] _HeadColor ("Impact Head Color (선단 탄두 코어 발광)", Color) = (1.3, 1.5, 1.8, 1.0)
        [HDR] _TailColor ("Tail Dissolve Color (꼬리 성운 잔상)", Color) = (0.15, 0.35, 0.7, 1.0)

        [Header(Progress and Trail Settings)]
        _Progress ("Progress (레이저 전진 진행도)", Range(0.0, 2.0)) = 1.0
        _TailLength ("Tail Length (유성 꼬리 길이)", Range(0.05, 1.5)) = 0.85

        [Header(Warhead Wave Spark Settings)]
        _HeadParams ("Head: x=최대굵기px y=테이퍼길이px z=탄두구간px w=마름모한계", Vector) = (7, 30, 7, 1.25)
        _WaveParams ("Wave: x=진폭px y=파동길이px z=주기px w=끝점앵커px", Vector) = (4, 44, 10, 14)
        _SparkParams ("Spark: x=튀는거리px y=수명px z=굵은도트발광배율", Vector) = (9, 28, 0.4, 0)

        [Header(Arrival Settings)]
        _Arrive ("Arrive (도착 후 경과 0~1, 스크립트가 지정)", Range(0.0, 1.0)) = 0.0
        _ArriveParams ("Arrive: x=스파크반경px y=별맥동px z=별떨림px w=끝점잔재정리반경px", Vector) = (10, 2, 1, 48)

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

            // 32 PPU 픽셀 격자 - 모든 도트 중심과 굵기는 이 격자의 정수 픽셀 단위로만 움직인다.
            static const float PixelsPerUnit = 32.0;
            static const float PixelUnit = 1.0 / 32.0;

            struct Attributes
            {
                float4 positionOS : POSITION;   // 도트 중심(쿼드 4정점이 동일)
                float2 uv : TEXCOORD0;          // x = 변 위 진행 위치(0~1), y = 도트별 난수 시드
                float2 corner : TEXCOORD1;      // 코너 오프셋(로컬 단위, 1픽셀 기준)
                float4 aux : TEXCOORD2;         // xy = 진행 방향 법선, z = 변 호 길이(px), w = 종류(0 도트, 1 십자별 팔, 2 스파크)
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 shape : TEXCOORD1;       // 코너 부호(±1) - 프래그먼트에서 마름모 마스크에 쓴다
                float4 info : TEXCOORD2;        // x = 탄두 구간 계수(0~1), y = 굵기 px, z = 종류, w = 미사용
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _HeadColor;
                half4 _TailColor;

                float4 _HeadParams;
                float4 _WaveParams;
                float4 _SparkParams;
                float4 _ArriveParams;

                float _Arrive;
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

                float normT = input.uv.x;
                float seed = input.uv.y;
                float arcPx = max(input.aux.z, 1.0);
                float kind = input.aux.w;

                // 빔 선단에서 이 도트까지 뒤로 떨어진 거리(px). 음수 = 선단이 아직 도달하지 않은 도트(프래그먼트가 버린다)
                float dist = (_Progress - normT) * arcPx;

                float sizePx = 1.0;
                float2 offsetPx = float2(0.0, 0.0);
                float2 cornerPx = input.corner;     // 십자별 팔은 이 값을 도착 연출로 늘였다 줄인다
                float armScale = 1.0;               // 십자별 팔의 숨김(0)/표시(1)
                float headFactor = 0.0;

                // 도착 후 경과(0~1). 0이면 비행 중이라 아래 도착 연출이 전부 꺼진다.
                float arrive = _Arrive;
                float collapse = saturate(arrive / 0.6);    // 탄두 소멸은 도착 연출의 앞 60% 동안 끝난다

                // 끝점 잔재 정리: 끝 별에서 _ArriveParams.w px 안의 도트/별은 도착 진행 20~70% 사이에 도트별 난수 순서로 탈락한다.
                // 끝 별에서 먼 도트가 먼저 빠져 잔재가 끝 별 쪽으로 빨려 들듯 사라진다.
                float distToEnd = (1.0 - normT) * arcPx;
                float clearD = saturate(distToEnd / max(_ArriveParams.w, 0.001));
                float clearTime = 0.2 + 0.5 * (1.0 - clearD) + (seed - 0.5) * 0.2;
                bool bClearHidden = arrive > 0.0 && distToEnd < _ArriveParams.w && arrive > clearTime;

                if (kind < 0.5)
                {
                    // 굵기 테이퍼: 선단에서 가장 굵고(_HeadParams.x px) 꼬리 쪽으로 홀수 픽셀 단위로 1px까지 가늘어진다
                    float taperT = saturate(1.0 - dist / max(_HeadParams.y, 0.001));
                    float halfSteps = floor((_HeadParams.x - 1.0) * 0.5 * taperT + 0.5);

                    // 도착 소멸(사악): 굵은 도트가 홀수 픽셀 단위로 얇아지고, 도트별 난수 순서로 하나씩 탈락한다(디더).
                    bool bCollapseHidden = false;
                    if (collapse > 0.0 && halfSteps >= 1.0)
                    {
                        bCollapseHidden = seed < collapse * 0.85;
                        halfSteps = floor(halfSteps * (1.0 - collapse) + 0.5);
                    }
                    sizePx = (bCollapseHidden || bClearHidden) ? 0.0 : 1.0 + 2.0 * halfSteps;

                    // 선단 바로 뒤 몇 픽셀은 마름모 탄두(흰 코어 + 테두리)
                    headFactor = saturate(1.0 - dist / max(_HeadParams.z, 0.001));

                    // 파동: 선단 뒤로만 이어지는 진폭 묶음(패킷). 선단에 고정된 위상이라 묶음이 선단과 함께 달린다.
                    // 진폭은 선단에서 0(탄두는 곧게 날아간다) -> 파동 길이 중간에서 최대 -> 끝에서 0으로 수렴하는 sin 엔벨로프다.
                    // 양 끝 별 근처는 진폭을 눌러 빔이 별 중심에 정확히 붙게 한다.
                    float waveEnv = sin(saturate(dist / max(_WaveParams.y, 0.001)) * 3.14159265) * step(0.0, dist);
                    float anchor = saturate(min(normT, 1.0 - normT) * arcPx / max(_WaveParams.w, 0.001));
                    float phase = dist / max(_WaveParams.z, 0.001);
                    float tri = abs(frac(phase) * 2.0 - 1.0) * 2.0 - 1.0;
                    offsetPx = input.aux.xy * round(tri * waveEnv * anchor * _WaveParams.x * (1.0 - collapse));
                }
                else if (kind < 1.5)
                {
                    // 끝 별(시드가 음수인 십자별): 도착하면 팔이 늘었다 줄었다 맥동하고 시간 단위로 ±px 떨리다가,
                    // 도착 연출 뒤 40% 동안 팔이 줄어 사라진다 - 마지막 프레임에 한꺼번에 사라지지 않게 한다.
                    if (seed < 0.0 && arrive > 0.0)
                    {
                        float pulse = max(sin(arrive * 3.14159265 * 3.0), 0.0) * (1.0 - arrive);
                        float fadeK = saturate((arrive - 0.6) / 0.4);
                        float lenAdd = round(_ArriveParams.y * pulse) - round(2.0 * fadeK);
                        if (abs(cornerPx.x) > abs(cornerPx.y))
                        {
                            cornerPx.x += sign(cornerPx.x) * lenAdd * PixelUnit;
                        }
                        else
                        {
                            cornerPx.y += sign(cornerPx.y) * lenAdd * PixelUnit;
                        }
                        armScale = fadeK >= 1.0 ? 0.0 : 1.0;

                        float tick = floor(arrive * 16.0);
                        float s = abs(seed);
                        float2 h = frac(sin(float2(tick * 12.9898 + s * 78.233, tick * 39.346 + s * 11.135)) * 43758.5453);
                        offsetPx = round((h * 2.0 - 1.0) * _ArriveParams.z * (1.0 - arrive));
                    }
                    else if (seed >= 0.0 && bClearHidden)
                    {
                        armScale = 0.0; // 끝점 근처 중간 십자별도 끝점 잔재 정리에 포함
                    }
                }
                else if (kind > 2.5)
                {
                    // 도착 스파크: 도착 직후 끝 별 중심에서 8방향으로 튀어나가며(이즈 아웃) 도트마다 다른 수명으로 꺼진다.
                    // 초반 잠깐은 3px, 이후 1px. 도착 전이나 수명이 끝난 뒤에는 크기 0으로 숨긴다.
                    float bt = saturate(arrive / 0.7);
                    float burstLife = (0.55 + 0.45 * frac(seed * 5.3)) * 0.75;
                    if (arrive > 0.0 && arrive < burstLife)
                    {
                        float r = _ArriveParams.x * (1.0 - (1.0 - bt) * (1.0 - bt)) * (0.6 + 0.4 * frac(seed * 3.1));
                        offsetPx = input.aux.xy * round(r);
                        sizePx = bt < 0.2 ? 3.0 : 1.0;
                    }
                    else
                    {
                        sizePx = 0.0;
                    }
                }
                else
                {
                    // 스파크: 선단이 지난 직후부터 옆으로 튀어나가며 도트마다 다른 수명으로 사라진다. 그 외에는 크기 0으로 숨긴다.
                    float life = max(_SparkParams.y, 0.001);
                    // 도착 후에는 선단이 끝점에 멈춰 dist가 얼어붙으므로, 도착 경과를 수명 진행에 더해 스파크가 튀어나가며 꺼지게 한다.
                    float p = dist / life + arrive * 1.7;
                    float lifeLimit = 0.35 + 0.65 * frac(seed * 7.13);
                    if (dist >= 0.0 && p < lifeLimit)
                    {
                        float side = seed < 0.5 ? -1.0 : 1.0;
                        float rise = p * _SparkParams.x * (0.5 + frac(seed * 3.7));
                        offsetPx = input.aux.xy * side * round(rise + 1.0);
                    }
                    else
                    {
                        sizePx = 0.0;
                    }
                }

                // 도트 중심을 월드 픽셀 칸 중앙으로 스냅한 뒤 정수 픽셀 크기로 쿼드를 펼친다(십자별 팔은 고정 크기)
                float3 centerWS = TransformObjectToWorld(input.positionOS.xyz);
                centerWS += TransformObjectToWorldDir(float3(offsetPx, 0.0), false) * PixelUnit;
                centerWS.xy = (floor(centerWS.xy * PixelsPerUnit) + 0.5) * PixelUnit;

                float cornerScale = kind > 0.5 && kind < 1.5 ? armScale : sizePx;
                float3 cornerWS = TransformObjectToWorldDir(float3(cornerPx * cornerScale, 0.0), false);

                output.positionHCS = TransformWorldToHClip(centerWS + cornerWS);
                output.uv = input.uv;
                output.shape = sign(input.corner);
                output.info = float4(headFactor, sizePx, kind, 0.0);
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

                float headFactor = input.info.x;
                float sizePx = input.info.y;
                float kind = input.info.z;

                // 2. 스파크는 선단 코어 색 1픽셀 점
                if (kind > 1.5)
                {
                    return half4(_HeadColor.rgb * (half)_EmissionBoost, 1.0h);
                }

                // 3. 탄두 구간의 굵은 도트는 마름모로 깎는다(작은 도트/꼬리는 정사각형 그대로)
                float diamond = abs(input.shape.x) + abs(input.shape.y);
                bool bWarhead = kind < 0.5 && headFactor > 0.0 && sizePx > 3.5;
                if (bWarhead && diamond > _HeadParams.w)
                {
                    discard;
                }

                // 4. 도트 기본 성좌 발광 색상 + 꼬리 구간(tailPos ~ headPos) 점진적 페이드아웃 알파
                half3 finalRgb = input.color.rgb;
                half finalAlpha = input.color.a;

                float trailFactor = saturate((input.uv.x - tailPos) / tailLength);
                float softTrail = pow(trailFactor, 1.25f);

                half3 gradientRgb = lerp(_TailColor.rgb, finalRgb, (half)softTrail);
                half3 bodyRgb = lerp(gradientRgb, _CoreColor.rgb, 0.25h);
                half bodyAlpha = finalAlpha * (half)softTrail;

                // 5. 탄두: 중심부는 코어 색, 바깥은 테두리 색으로 선단에 가까울수록 진하게 덮는다
                if (bWarhead)
                {
                    half3 warheadRgb = diamond < 0.55 ? _HeadColor.rgb : _CoreColor.rgb;
                    finalRgb = lerp(bodyRgb, warheadRgb, (half)headFactor);
                    finalAlpha = lerp(bodyAlpha, 1.0h, (half)headFactor);
                }
                else
                {
                    finalRgb = bodyRgb;
                    finalAlpha = bodyAlpha;
                }

                // 6. 굵은 도트(탄두/테이퍼)는 면적이 커서 블룸이 번지기 쉽다 - 굵을수록 발광을 낮춰 픽셀 형태가 뭉개지지 않게 한다
                float thickFactor = kind < 0.5 ? saturate((sizePx - 1.0) / max(_HeadParams.x - 1.0, 1.0)) : 0.0;
                finalRgb *= (half)lerp(1.0, _SparkParams.z, thickFactor);

                // 7. 전체 발광 및 알파 트윈
                finalRgb *= (half)_EmissionBoost;
                finalAlpha *= safeAlpha;

                return half4(finalRgb, saturate(finalAlpha));
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
