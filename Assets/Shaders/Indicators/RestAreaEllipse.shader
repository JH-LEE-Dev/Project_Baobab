Shader "Custom/RestAreaEllipse"
{
    // 휴식 특성의 휴식 범위 타원. 오브젝트 스케일이 곧 타원 지름(2rx, 2ry)이고, UV 0~1 쿼드 위에 그린다.
    // 월드 좌표를 PPU 격자로 스냅해서 외곽선이 정수 픽셀에 맞게 끊긴다.
    //
    // 화면 캡처(_CameraSortingLayerTexture)를 읽지 않는다. 캡처에는 Objects 레이어(나무, 오프로드)가 이미 들어 있어서
    // 읽는 순간 사물까지 같이 일그러지기 때문이다. 바닥을 일렁이게 하지 않고 옅은 바탕과 빛 파동만 그린다.
    // 그래서 이 쿼드는 Indicators 레이어(Objects 아래)에 그대로 두면 항상 사물 밑에 깔린다.
    //
    // 바탕은 3단계로 끊고 경계만 4x4 순서 디더로 섞어 픽셀 아트 질감을 낸다(덮기, 알파). 파동은 바닥을 덮지 않고 밝히기만 한다(가산).
    // 덮기와 가산을 한 패스로 내기 위해 프리멀티플라이드 블렌드(One, OneMinusSrcAlpha)를 쓴다.
    //
    // 바닥에 놓인 원판처럼 보이도록 앞(화면 아래)·뒤(위)를 다르게 그린다.
    // 앞쪽: 외곽선이 가운데로 갈수록 한 단계씩 밝아지고, 안쪽 띠가 2px(짙은 줄 -> 중간 톤), 바깥에 옆면 1px + 그림자 1px.
    // 뒤쪽: 외곽선을 한 단계 어둡게, 안쪽 광채를 옅게. 옆면·그림자·두 번째 줄은 양옆으로 갈수록 단계적으로 사라진다.
    // 위·아래·좌·우 네 곳에는 외곽선 위 5px 밝은 마디와 바깥 돌기 1px로 된 표식을 둔다.
    Properties
    {
        [Header(Wave)]
        _WaveStrength("Wave Strength", Range(0, 1)) = 1
        _WavePeriod("Wave Interval (Seconds)", Float) = 4
        _WaveAlpha("Wave Additive Alpha", Range(0, 0.4)) = 0.1

        [Header(Inner)]
        _BaseAlpha("Base Alpha (Center)", Range(0, 0.15)) = 0.035
        _RimAlpha("Rim Alpha (Edge)", Range(0, 0.4)) = 0.2
        _RimStart("Rim Gradient Start (0 Center ~ 1 Edge)", Range(0, 1)) = 0.4

        [Header(Reveal)]
        _RevealWaveAlpha("First Wave Additive Alpha", Range(0, 0.3)) = 0.1

        [Header(Pixel)]
        _PixelsPerUnit("Pixels Per Unit", Float) = 32

        [Header(Runtime)]
        _Fade("Fade", Range(0, 1)) = 1
        _Reveal("Reveal Progress (First Wave)", Range(0, 1)) = 1
        _WaveGate("Wave Gate", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float _WaveStrength;
                float _WavePeriod;
                float _WaveAlpha;
                float _BaseAlpha;
                float _RimAlpha;
                float _RimStart;
                float _RevealWaveAlpha;
                float _PixelsPerUnit;
                float _Fade;
                float _Reveal;
                float _WaveGate;
            CBUFFER_END

            // 외곽 레이저선 코어(1px)와 보조선(안쪽 1px). 파동이 닿을수록 한 단계씩 밝아진다. 값은 sRGB.
            // 코어는 밝은 민트, 보조선은 짙은 녹색으로 명도 차를 크게 둬서 초록 바닥에서도 선이 묻히지 않는다.
            static const float3 kCore[4] =
            {
                float3(0.600, 0.941, 0.620), float3(0.675, 0.965, 0.690),
                float3(0.765, 0.984, 0.765), float3(0.855, 1.000, 0.835)
            };
            static const float3 kSub[4] =
            {
                float3(0.122, 0.478, 0.220), float3(0.165, 0.580, 0.275),
                float3(0.208, 0.651, 0.314), float3(0.271, 0.722, 0.369)
            };
            // 내부 바탕(맑은 민트 연두)과 파동 빛(밝은 연두). 값은 sRGB.
            // 바탕이 누런 쪽이면 어두운 바닥(4스테이지)에서 탁한 올리브색이 되므로 푸른 기를 조금 섞는다.
            static const float3 kBaseColor = float3(0.52, 0.90, 0.52);
            static const float3 kWaveColor = float3(0.84, 0.96, 0.52);
            // 네 방향 표식·앞 가운데 하이라이트(거의 흰 민트)와 앞쪽 그림자(짙은 녹흑). 값은 sRGB.
            static const float3 kMark = float3(0.94, 1.00, 0.90);
            static const float3 kShadow = float3(0.04, 0.16, 0.08);
            static const float kBayer4[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            // 앞뒤 구분 경계. front = -y / ry (화면 아래 +1, 위 -1).
            static const float kFrontLine = 0.2;      // 이보다 앞: 외곽선 +1단계, 광채 진하게
            static const float kBackLine = -0.2;      // 이보다 뒤: 외곽선 어둡게, 광채 옅게
            static const float kFrontLit = 0.75;      // 외곽선 +2단계
            static const float kFrontPeak = 0.93;     // 외곽선 거의 흰색
            static const float kSub2Line = 0.15;      // 안쪽 두 번째 줄이 생기는 지점
            static const float kFaceLine = 0.25;      // 바깥 옆면이 생기는 지점
            static const float kShadowLine = 0.45;    // 옆면 아래 그림자가 생기는 지점

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // 윤곽 바깥으로 옆면·그림자가 2px 더 나가고, 쿼드 가장자리에서 반 픽셀·중심 스냅으로 최대 1px 더 밀리므로
                // 쿼드를 모서리 쪽으로 3px 넓혀 잘리지 않게 한다.
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                worldPos.xy += float2(IN.positionOS.x < 0.0 ? -1.0 : 1.0, IN.positionOS.y < 0.0 ? -1.0 : 1.0) * (3.0 / max(_PixelsPerUnit, 1.0));
                OUT.positionHCS = TransformWorldToHClip(worldPos);
                OUT.positionWS = worldPos;
                return OUT;
            }

            // 프로젝트 색 공간이 Linear라 sRGB로 정한 팔레트 값을 선형으로 바꿔서 낸다.
            float3 SrgbToLinear(float3 _c)
            {
                return _c * (_c * (_c * 0.305306011 + 0.682171111) + 0.012522878);
            }

            // 프리멀티플라이드로 낸다.
            half4 OutColor(float3 _srgb, float _a)
            {
                return half4(SrgbToLinear(_srgb) * _a, _a);
            }

            float Lvl4(float _w)
            {
                return _w < 0.2 ? 0.0 : (_w < 0.45 ? 1.0 : (_w < 0.7 ? 2.0 : 3.0));
            }

            // 1px 타원 윤곽. PixelEllipse(midpoint)와 같은 픽셀 집합이다(반지름 192x96, 191x95에서 0픽셀 차이를 확인).
            // 가파른 구간(좌우 끝)은 행마다 x를, 완만한 구간(위아래)은 열마다 y를 반올림해 한 픽셀만 고르므로
            // 8방향으로만 이어지는 한 줄이 되고 L자 모서리 픽셀이 생기지 않는다(JAGGIES, DOUBLE PIXEL).
            // _aq는 중심 기준 |x|, |y| 픽셀 좌표. 가파른 구간 윤곽(행마다 x 하나)과 완만한 구간 윤곽(열마다 y 하나)을 따로 판정한다.
            bool IsSteep(float2 _aq, float2 _r)
            {
                return _aq.y * _r.x * _r.x < _aq.x * _r.y * _r.y;
            }

            bool SteepEdge(float2 _aq, float2 _r)
            {
                float t = 1.0 - (_aq.y / _r.y) * (_aq.y / _r.y);
                return IsSteep(_aq, _r) && 0.0 <= t && _aq.x == round(_r.x * sqrt(t));
            }

            bool ShallowEdge(float2 _aq, float2 _r)
            {
                float t = 1.0 - (_aq.x / _r.x) * (_aq.x / _r.x);
                return false == IsSteep(_aq, _r) && 0.0 <= t && _aq.y == round(_r.y * sqrt(t));
            }

            // 코어 윤곽 픽셀을 그 구간의 주축 방향으로 _n px 옮긴 자리(+ 안쪽, - 바깥쪽). 0이면 코어 자체.
            // 가파른 구간은 같은 행에서 x를, 완만한 구간은 같은 열에서 y를 옮긴다.
            // 반지름을 바꾼 타원을 따로 그리면 대각선에서 계단 위치가 어긋나 밝은 점과 짙은 점이 지퍼처럼 섞이고
            // 띠 두께가 들쭉날쭉해진다. 코어에서 바로 옮겨 오면 어디서나 코어와 나란한 띠가 된다.
            bool ShiftedEdge(float2 _aq, float2 _r, float _n)
            {
                return SteepEdge(_aq + float2(_n, 0.0), _r) || ShallowEdge(_aq + float2(0.0, _n), _r);
            }

            // 4x4 순서 디더 임계값(0~1).
            float Bayer4(float2 _pix)
            {
                int2 i = (int2)fmod(_pix + 1024.0, 4.0);
                return (kBayer4[i.y * 4 + i.x] + 0.5) / 16.0;
            }

            // 코어 윤곽까지의 대략적인 수직 거리(px). 윤곽 위 0, 안쪽 +, 바깥 -.
            float EdgeDistPx(float2 _pix, float2 _r)
            {
                float2 q = _pix / _r;
                float f = 1.0 - dot(q, q);
                float2 g = 2.0 * _pix / (_r * _r);
                return f / max(length(g), 1e-4);
            }

            // 중앙에서 퍼지는 빔 파동. 두 줄이 반 주기씩 엇갈리고, 앞쪽은 날카롭고 뒤쪽은 꼬리가 길다.
            float WaveAt(float _r)
            {
                float best = 0.0;
                float period = max(_WavePeriod, 0.05);
                [unroll]
                for (int k = 0; k < 2; k++)
                {
                    float ph = frac(_Time.y / (period * 2.0) + k * 0.5);
                    float head = (1.0 - pow(1.0 - ph, 1.3)) * 1.15;
                    float amp = min(1.0, ph * 4.0) * (1.0 - ph * ph);
                    float d = _r - head;
                    float s = d > 0.0 ? 0.1 : 0.26;
                    best = max(best, exp(-(d / s) * (d / s)) * amp * _WaveStrength);
                }
                return best;
            }

            // 열릴 때 한 번 지나가는 첫 파동. 진행도(_Reveal) 0~1 동안 중앙에서 외곽 너머까지 가며, 시작과 끝은 세기 0이다.
            float FirstWaveAt(float _r)
            {
                if (1.0 <= _Reveal) return 0.0;

                float x = saturate(_Reveal);
                float u = 1.0 - x;
                float head = (1.0 - u * u * u) * 1.05;
                float d = (_r - head) / 0.08;
                return exp(-d * d) * _RevealWaveAlpha * sin(PI * x);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 오브젝트 스케일 = 타원 지름. 반지름은 그 절반.
                float scaleX = length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x));
                float scaleY = length(float3(unity_ObjectToWorld[0].y, unity_ObjectToWorld[1].y, unity_ObjectToWorld[2].y));
                float2 radius = max(float2(scaleX, scaleY) * 0.5, 0.0001);
                float2 center = float2(unity_ObjectToWorld[0].w, unity_ObjectToWorld[1].w);

                // 중심도 픽셀 격자에 맞춰 두면 타원 윤곽이 좌우, 상하 대칭으로 깔끔하게 끊긴다.
                float ppu = max(_PixelsPerUnit, 1.0);
                float2 centerSnapped = (floor(center * ppu) + 0.5) / ppu;
                float2 snapped = (floor(IN.positionWS.xy * ppu) + 0.5) / ppu;
                // 윤곽은 PixelEllipse와 같은 픽셀(반지름 radiusPx). 채우는 영역은 윤곽 안쪽을 다 덮도록 반 픽셀 큰 연속 타원으로 정한다.
                float2 pix = round((snapped - centerSnapped) * ppu);
                float2 radiusPx = radius * ppu;
                float2 aq = abs(pix);
                float2 p = pix / (radiusPx + 0.5);
                float d2 = dot(p, p);

                float waveEdge = WaveAt(1.0) * _WaveGate;
                int lvlCore = (int)Lvl4(waveEdge * 1.1);
                int lvlSub = (int)Lvl4(waveEdge);

                // 앞뒤: 화면 아래(앞) +1 ~ 위(뒤) -1.
                float front = -pix.y / max(radiusPx.y, 1.0);
                bool bFront = front > kFrontLine;
                bool bBack = front < kBackLine;

                // 네 방향 표식: 바닥 위 원의 각도(타원 매개변수 각)가 0/90/180/270도인 자리에서 윤곽을 따라 5px.
                // 윤곽 길이 방향 거리(px) = 각도 차 x 그 자리의 호 길이 변화율.
                float th = atan2(pix.y / radiusPx.y, pix.x / radiusPx.x);
                float sn = sin(th);
                float cs = cos(th);
                float arcRate = sqrt(radiusPx.x * radiusPx.x * sn * sn + radiusPx.y * radiusPx.y * cs * cs);
                float quarter = th / (0.5 * PI);
                float markOffPx = (quarter - round(quarter)) * (0.5 * PI) * arcRate;
                bool bMark = abs(markOffPx) <= 2.5;
                bool bMarkTip = abs(markOffPx) <= 0.75;

                // 코어: 1px 윤곽. 앞 가운데로 갈수록 빛을 받아 구간 단위로 한 단계씩 밝아진다.
                // 프리멀티플라이드 블렌드라 색에 알파(_Fade)를 곱해서 낸다.
                if (ShiftedEdge(aq, radiusPx, 0.0))
                {
                    float3 c = kCore[lvlCore];
                    if (front > kFrontPeak) c = lerp(kCore[3], kMark, 0.6);
                    else if (front > kFrontLit) c = kCore[min(lvlCore + 2, 3)];
                    else if (bFront) c = kCore[min(lvlCore + 1, 3)];
                    else if (bBack) c = lerp(c, kSub[lvlSub], 0.35);
                    if (bMark) c = kMark;
                    return OutColor(c, _Fade);
                }

                // 코어 바로 바깥 1px: 표식 돌기 > 앞쪽 옆면 > 계단 끝 보간 픽셀.
                if (ShiftedEdge(aq, radiusPx, -1.0))
                {
                    if (bMarkTip) return OutColor(kCore[min(lvlCore + 1, 3)], _Fade);
                    if (front > kFaceLine)
                    {
                        float3 c = front > kFrontLit ? kSub[min(lvlSub + 2, 3)] : kSub[min(lvlSub + 1, 3)];
                        return OutColor(c * 0.85, _Fade);
                    }
                    // 실제 곡선이 가까이 지나가는 계단 끝 바깥 칸에 코어색을 반투명으로 둬 계단을 덜 각지게 한다.
                    if (EdgeDistPx(pix, radiusPx) > -0.62) return OutColor(kCore[lvlCore], 0.5 * _Fade);
                    clip(-1.0);
                }

                // 앞쪽 옆면 아래 그림자 1px.
                if (front > kShadowLine && ShiftedEdge(aq, radiusPx, -2.0))
                {
                    return OutColor(kShadow, 0.45 * _Fade);
                }

                clip(1.0 - d2);

                // 보조선: 코어를 1px 안쪽으로 옮긴 자리. 코어와 겹쳐 2px 띠(레이저 느낌)를 만든다.
                if (ShiftedEdge(aq, radiusPx, 1.0))
                {
                    float3 c = bMarkTip ? kCore[lvlCore] : kSub[lvlSub];
                    return OutColor(c, _Fade);
                }

                // 앞쪽만 안쪽 1px 더: 짙은 줄 -> 중간 톤으로 안쪽 벽이 바닥으로 풀리게 한다.
                if (front > kSub2Line && ShiftedEdge(aq, radiusPx, 2.0))
                {
                    return OutColor(lerp(kSub[lvlSub], kCore[0], 0.32), 0.9 * _Fade);
                }

                float r = sqrt(d2);

                // 바탕: 가운데(_BaseAlpha)는 옅게 두고 _RimStart부터 외곽(_RimAlpha)으로 갈수록 짙어져
                // 밝은 바닥에서도 범위의 경계가 읽힌다. 가운데의 모닥불·캐릭터 주변은 바닥이 그대로 보인다.
                // 3단계로 끊고 단계 경계만 순서 디더로 섞는다.
                float rimT = smoothstep(_RimStart, 1.0, r);
                float step3 = saturate(floor(rimT * rimT * 2.0 + 0.5 + (Bayer4(pix) - 0.5) * 0.9) / 2.0);
                float baseA = lerp(_BaseAlpha, _RimAlpha, step3);
                float3 baseCol = kBaseColor;

                // 보조선 안쪽 계단식 광채 띠: 2px 꽉 채우고 2px 체커로 흩어진다. 앞쪽은 진하게, 뒤쪽은 옅게.
                float ed = EdgeDistPx(pix, radiusPx);
                bool bChecker = fmod(pix.x + pix.y + 2048.0, 2.0) < 0.5;
                if (ed < 3.5 || (ed < 5.5 && bChecker))
                {
                    baseA = bBack ? 0.24 : (bFront ? 0.34 : 0.29);
                    baseCol = kCore[0];
                }
                baseA *= _Fade;

                // 파동: 덮지 않고 밝히기만 한다. 평소 파동은 다 드러난 뒤 _WaveGate로 켜지고, 첫 파동은 열리는 동안 한 번만 지나간다.
                // 첫 파동은 시작 세기가 0이라 _Fade를 곱하지 않는다(곱하면 드러나기 전 구간에서 거의 사라진다).
                float addA = WaveAt(r) * _WaveAlpha * _WaveGate * _Fade + FirstWaveAt(r);

                clip(max(baseA, addA) - 0.001);
                float3 rgb = SrgbToLinear(baseCol) * baseA + SrgbToLinear(kWaveColor) * addA;
                return half4(rgb, baseA);
            }
            ENDHLSL
        }
    }
}
