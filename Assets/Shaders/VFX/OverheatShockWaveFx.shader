Shader "Custom/VFX/OverheatShockWaveFx"
{
    // 과열 충격파 곁들임 연출. 일반 충격파(ShockWaveFx)와 같은 짜임에 색만 푸른 불꽃이고, 마루 불꽃 혀와 불씨를 더했다.
    // ShockWaveVisualRunner가 충격파 원점에 사각형 쿼드 하나를 깔고, 픽셀(32 PPU 게임 격자)마다 아래 중 무엇이 그려지는지 여기서 정한다.
    // 위치는 전부 경과 시간(_T)과 퍼지는 값(_Geo)으로 계산한다(재생 상태 없음).
    // 1. 발밑 충격: 발동 순간 원점에 흰 원판이 번쩍이고 두꺼운 고리가 퍼지며 식는다(0.17초).
    // 2. 불티·파편: 파동 앞 가장자리가 지나간 자리에서 불티는 바깥으로 밀려 나가 감속하고, 돌 조각은 한 번 튀었다 떨어진다.
    // 3. 소멸 흩날림: 판정이 끝나면 사라지는 파동에서 작은 티끌이 떨어져 나와 바깥·옆·위로 흩날리며 식는다.
    // 4. 마루 불꽃 혀: 파동 앞 가장자리(마루)에서 화면 위로 뾰족한 불꽃 혀가 줄지어 솟아 15fps로 이글거린다.
    //    발사 순간 1.5배로 확 솟았다가 가라앉고, 본체가 식는 동안 함께 낮아지며 꺼진다(본체보다 오래 남지 않음).
    // 5. 불씨: 마루에서 `.` `+` `x` 불씨가 튀어 올라 흔들리며 날아가 식는다.
    // 발밑 충격 크기, 불티·티끌·불씨 개수, 불꽃 혀 높이는 충격파 범위(범위 강화·과열 유지시간)에 비례한다.
    // 색은 과열 3단계 아우라(VFX_OverheatAura) 불꽃 혀 팔레트와 같다.
    // 출력은 BrandWrapGlow와 같다(색 * lerp(_GlowMin, _GlowMax, 발광 세기), 알파 1).
    Properties
    {
        _GlowMin("Glow Min (HDR 발광 최소 배율, 발광 세기 0)", Float) = 0.8
        _GlowMax("Glow Max (HDR 발광 최대 배율, 발광 세기 1)", Float) = 3.0
        _ColorWhite("White (rgb, a = 발광 세기)", Vector) = (0.8392, 0.9804, 1, 1)
        _ColorLight("Light", Vector) = (0.4706, 1, 0.9255, 0.15)
        _ColorMid("Mid", Vector) = (0.3608, 0.7843, 1, 0)
        _ColorDark("Dark", Vector) = (0.1725, 0.4706, 1, 0)
        _ColorLilac("Lilac", Vector) = (0.4706, 0.3608, 1, 0)
        _Pebble("Pebble", Vector) = (0.46, 0.34, 0.22, 0)
        _PebbleDark("Pebble Dark", Vector) = (0.30, 0.22, 0.15, 0)

        // 러너가 매 프레임 넘기는 값
        _Center("원점 (xy 월드), 그림 반지름(z), 픽셀/유닛(w)", Vector) = (0, 0, 1, 32)
        _Geo("시작 안쪽 거리, 시작 바깥 거리, 퍼지는 속도, 판정 시간", Vector) = (0.1, 0.5, 4, 0.25)
        _Dir("방향(xy, 등각 보정), 반각(z, 라디안)", Vector) = (1, 0, 0.785, 0)
        _T("경과 시간", Float) = 0
        _Seed("Seed", Float) = 1
        _Tint("Tint (rgb)", Vector) = (1, 1, 1, 1)
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
        Cull Off

        Pass
        {
            Name "OverheatShockWaveFxPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float _GlowMin;
                float _GlowMax;
                float4 _ColorWhite;
                float4 _ColorLight;
                float4 _ColorMid;
                float4 _ColorDark;
                float4 _ColorLilac;
                float4 _Pebble;
                float4 _PebbleDark;
                float4 _Center;
                float4 _Geo;
                float4 _Dir;
                float _T;
                float _Seed;
                float4 _Tint;
            CBUFFER_END

            // 개수·발밑 파장 크기는 최대 범위 강화(앞 가장자리 반지름 REF_RADIUS) 기준이고, 범위에 비례해 조절한다.
            #define REF_RADIUS 5.75
            #define MIN_RANGE_SCALE 0.25
            #define MAX_RANGE_SCALE 1.5
            #define MOTE_COUNT 72
            #define FINALE_COUNT 30
            #define BURST_SECONDS 0.17
            // 흩날림은 러너가 판정 뒤로 남겨 두는 시간(FxTailSeconds 0.34초) 안에 끝나야 한다: 0.08 + 0.18 + 0.08
            #define FINALE_SPAWN 0.08
            #define FINALE_LIFE 0.18
            #define FINALE_LIFE_JITTER 0.08
            // 불꽃이 이글거리는 속도(초당 그림 수)
            #define FLICKER_FPS 15.0
            // 마루 불꽃 혀: 최대 높이(게임 픽셀, 범위 배율 1 기준), 뿌리가 되는 마루 두께(게임 픽셀)
            #define TONGUE_PX 16.0
            #define CREST_PX 5.0
            // 양옆 끝에서 불꽃 혀(밑동 줄 포함)가 점으로 옅어지는 폭(게임 픽셀): 퍼지는 동안 / 끝무렵. 본체의 SIDE_SOFT_PX·SIDE_SOFT_LATE_PX와 같다.
            #define SIDE_SOFT_PX 12.0
            #define SIDE_SOFT_LATE_PX 56.0
            // 본체가 식는 구간: 판정 시간 + (visualExtraDuration 0.1 - visualFadeOutDuration 0.15)부터 0.15초(과열 충격파 프리팹 값과 맞춤).
            // 불꽃 혀는 이 구간에 본체와 함께 낮아지며 꺼져 본체보다 오래 남지 않는다.
            #define BODY_FADE_START_OFFSET -0.05
            #define BODY_FADE_SECONDS 0.15
            // 불씨: 생성은 판정 시간 + 0.04초까지, 수명 0.22 + 0.08초(FxTailSeconds 0.34초 안에 끝남)
            #define EMBER_COUNT 56
            #define EMBER_SPAWN_TAIL 0.04
            #define EMBER_LIFE 0.22
            #define EMBER_LIFE_JITTER 0.08
            // 빨리 거르기(결과 동일): 각 효과가 이 픽셀에 닿을 수 없으면 계산을 건너뛴다. 아래 거리는 모두 등각 좌표(세로 2배) 기준 상한이다.
            // 입자 위치와 그 입자가 칠하는 픽셀 중심의 어긋남 상한(게임 픽셀, 크기 3칸·바닥 내림 포함 7.9 → 넉넉히 10)
            #define CULL_SLACK_PX 10.0
            // 입자 수명 상한(+0.001)과, 출발점에서 날아갈 수 있는 거리 상한(+0.05)
            // 흩날림 1.15: 퍼짐 √(1.4² + 0.7²) / 5 = 0.31, 떠오름 1.0 × 0.26 → (0.31 + 0.26) × 2
            // 불꽃 혀 1.95: 높이 16 × 1.3 × 1.5 = 31.2픽셀 = 0.975 → × 2
            // 모든 효과가 닿는 가장 먼 거리(앞 가장자리 바깥): 불티 출발점(앞 가장자리 - 0.1) + 1.38 × 2 + 0.78 = 3.44 → 3.52
            #define MOTE_MAX_LIFE 0.381
            #define MOTE_REACH 3.52
            // 입자 하나를 볼 때는 출발점에서 바깥 방향(등각 좌표에서 출발 각도 방향, 바깥 거리 / 타원 보정)으로만 길게 가고
            // 옆·뒤로는 떠오름만큼만 벗어나므로 띠로 좁힌다: 바깥 거리 상한 3.8 × 0.36 = 1.37(+0.01), 떠오름 0.36 × 2(+0.05)
            #define MOTE_OUT_REACH 1.38
            #define MOTE_SIDE_REACH 0.78
            #define EMBER_MAX_LIFE 0.301
            // 불씨 하나: 바깥 2.0 / 6 = 0.33(+0.01), 옆·뒤로는 좌우 0.05 · 위 0.62 × 2 → 1.25
            #define EMBER_OUT_REACH 0.34
            #define EMBER_SIDE_REACH 1.25
            #define FINALE_MAX_LIFE 0.261
            #define FINALE_REACH 1.2
            #define TONGUE_REACH 2.0

            float Hash(float _n)
            {
                return frac(sin(_n * 12.9898 + _Seed * 78.233) * 43758.5453);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS.xy;
                return output;
            }

            half4 Output(float4 _color, float _glow)
            {
                float glow = lerp(_GlowMin, _GlowMax, saturate(_glow));
                return half4((half3)(_color.rgb * _Tint.rgb * glow), 1.0h);
            }

            // 등각 타원 위 점(원점 기준 월드). _radius는 가로 반지름(월드), _theta는 등각 보정 공간의 각도.
            float2 EllipsePoint(float _theta, float _radius)
            {
                return float2(cos(_theta), sin(_theta) * 0.5) * _radius;
            }

            // 시각 _t의 파동 앞 가장자리 가로 반지름(월드). 판정 시간이 지나면 멈춘다.
            float FrontRadius(float _t)
            {
                return _Geo.y + _Geo.z * clamp(_t, 0.0, _Geo.w);
            }

            // 충격파 범위 배율: 판정이 끝날 때 앞 가장자리 반지름 / 최대 범위 강화(범위 I·II 모두) 때의 반지름.
            // 범위 강화가 늘리는 것은 퍼지는 속도이므로 실제로 퍼진 거리로 잰다(로그라이크 범위 보너스도 같은 식으로 반영).
            float RangeScale()
            {
                return clamp(FrontRadius(_Geo.w) / REF_RADIUS, MIN_RANGE_SCALE, MAX_RANGE_SCALE);
            }

            // 최대 범위 기준 개수 _count를 범위 배율만큼 줄이거나 늘린다(테두리 길이에 비례해 밀도를 같게).
            int ScaledCount(int _count, float _scale)
            {
                return max(1, (int)round(_count * _scale));
            }

            // 본체가 식은 정도(0 = 그대로, 1 = 다 식음). 러너의 본체 알파(1 - SmoothStep)와 같은 곡선이다.
            float BodyCool(float _t, float _duration)
            {
                return smoothstep(0.0, 1.0, saturate((_t - (_duration + BODY_FADE_START_OFFSET)) / BODY_FADE_SECONDS));
            }

            // 4x4 순서 디더 문턱값(0~1). 본체(OverheatShockWaveArc)의 Bayer4와 같은 표·같은 픽셀 기준이라 두 층의 점이 맞물린다.
            float Bayer4(int _x, int _y)
            {
                int x = _x & 3;
                int y = _y & 3;
                int v = (y == 0) ? (x == 0 ? 0 : x == 1 ? 8 : x == 2 ? 2 : 10)
                      : (y == 1) ? (x == 0 ? 12 : x == 1 ? 4 : x == 2 ? 14 : 6)
                      : (y == 2) ? (x == 0 ? 3 : x == 1 ? 11 : x == 2 ? 1 : 9)
                      :            (x == 0 ? 15 : x == 1 ? 7 : x == 2 ? 13 : 5);
                return (v + 0.5) / 16.0;
            }

            // 각도 _theta가 진행 방향에서 벌어진 정도(라디안, 0 ~ π)
            float AngleFromDir(float _theta, float _dirAngle)
            {
                float delta = _theta - _dirAngle;
                return abs(atan2(sin(delta), cos(delta)));
            }

            // 두 각도의 차이(라디안, 0 ~ π)
            float AngleGap(float _a, float _b)
            {
                float delta = _a - _b;
                delta -= 6.2831853 * round(delta * 0.15915494);
                return abs(delta);
            }

            // 출발 각도 쪽으로만 멀리 가고 옆으로는 _side까지만 벗어나는 입자가 원점 거리 _rho인 이 점에 닿으려면, 출발 각도와 이 점의 각도 차이가
            // 이 값 이하여야 한다(옆 거리 = _rho × sin(각도 차이)). 점이 1.5 × _side 안쪽이면 어느 각도든 닿을 수 있어 거르지 않는다.
            float AngleTolerance(float _rho, float _side)
            {
                return _rho > 1.5 * _side ? asin(saturate(_side / _rho)) + 0.002 : 10.0;
            }

            // 출발점이 부채꼴 띠(등각 좌표, 반지름 _rlo ~ _rhi, 진행 방향 ± _a)에 있는 입자가 바깥으로 _out, 안쪽으로 _in, 옆으로 _side보다
            // 더 벗어날 수 없을 때, 이 점(원점 거리 _rho, 진행 방향에서 벌어진 각 _spread)이 그 입자들에 닿을 수 없으면 true.
            // 띠 안의 점과의 거리는 반지름 차이와 쐐기(방향 ± _a)까지 거리보다 짧을 수 없다. 바깥 이동은 출발 각도 그대로라 쐐기 거리를 늘리지 않는다.
            bool OutsideReach(float _rho, float _spread, float _a, float _rlo, float _rhi, float _in, float _out, float _side)
            {
                float wedge = 0.0;
                if (_a < 3.14159)
                {
                    float excess = _spread - _a;
                    if (excess > 0.0) wedge = excess >= 1.5707963 ? _rho : _rho * sin(excess);
                }
                return _rho - _rhi > _out || _rlo - _rho > _in || wedge > _side;
            }

            // 마루 불꽃 혀의 높이 배율: 0.06초 만에 1.5배로 솟았다가 0.2초까지 1로 가라앉고, 본체가 식는 구간에 함께 0으로 낮아진다.
            float TongueLife(float _t, float _duration)
            {
                float grow = _t < 0.06 ? 1.5 * _t / 0.06 : lerp(1.5, 1.0, saturate((_t - 0.06) / 0.14));
                return grow * (1.0 - BodyCool(_t, _duration));
            }

            // 게임 픽셀 열 _px의 불꽃 혀 모양(0~1): 폭 9칸 큰 혀(밑동이 통통하고 끝이 뾰족함)와 폭 5칸 작은 혀 두 겹, 길이는 칸·박자마다 새로 뽑는다.
            float TongueProfile(float _px, float _step)
            {
                float offsetA = floor(_Seed * 7.0);
                float cellA = floor((_px + offsetA) / 9.0);
                float uA = (_px + offsetA - cellA * 9.0 + 0.5) / 9.0;
                float ampA = 0.45 + 0.55 * Hash(cellA * 1.37 + _step * 0.71);
                float cellB = floor((_px + offsetA + 4.0) / 5.0);
                float uB = (_px + offsetA + 4.0 - cellB * 5.0 + 0.5) / 5.0;
                float ampB = 0.3 + 0.7 * Hash(cellB * 2.91 + _step * 0.53 + 40.0);
                float shapeA = pow(saturate(1.0 - abs(uA * 2.0 - 1.0)), 0.75);
                float shapeB = saturate(1.0 - abs(uB * 2.0 - 1.0));
                return max(shapeA * ampA, shapeB * ampB * 0.6);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float ppu = _Center.w;
                float t = _T;
                float duration = _Geo.w;
                float2 origin = _Center.xy;
                int2 pixel = (int2)floor(input.positionWS * ppu);
                int2 rel = pixel - (int2)floor(origin * ppu);
                bool bChecker = 0 == ((rel.x + rel.y) & 1);
                float dirAngle = atan2(_Dir.y, _Dir.x);
                float halfAngle = _Dir.z;
                float rangeScale = RangeScale();
                bool bFullCircle = halfAngle >= 3.1;
                float step = floor(t * FLICKER_FPS);

                // 빨리 거르기용: 이 픽셀 중심의 등각 좌표(세로 2배), 원점 거리, 진행 방향에서 벌어진 각도
                float2 cullQ = (float2(rel) + 0.5) / ppu;
                float2 cullP = float2(cullQ.x, cullQ.y * 2.0);
                float cullRho = length(cullP);
                float cullSlack = CULL_SLACK_PX / ppu;
                // 모든 효과가 닿을 수 있는 가장 먼 반지름(흙먼지·불티 출발점 + 날아가는 거리) 밖이면 바로 버린다(발밑 충격 고리는 2.6 미만이라 이 안이다).
                if (cullRho > FrontRadius(t) + MOTE_REACH + cullSlack) discard;
                float cullPhi = atan2(cullP.y, cullP.x);
                float cullSpread = AngleGap(cullPhi, dirAngle);
                float particleAngle = halfAngle * 0.95;

                // 5. 불씨(맨 위)
                int emberCount = ScaledCount(EMBER_COUNT, rangeScale);
                float emberSide = EMBER_SIDE_REACH + cullSlack;
                float emberTolerance = AngleTolerance(cullRho, emberSide);
                // 살아 있는 불씨의 출발 반지름: 마루(출발 시각) - 0.05 ~ 0.15, 출발 시각은 지금부터 수명 상한 전까지
                if (false == OutsideReach(cullRho, cullSpread, particleAngle, FrontRadius(t - EMBER_MAX_LIFE) - 0.15, FrontRadius(t) - 0.05,
                        emberSide, EMBER_OUT_REACH * 2.0 + emberSide, emberSide))
                [loop] for (int e = 0; e < emberCount; e++)
                {
                    // 출발 각도가 이 픽셀 쪽이 아니면 건너뛴다.
                    float e1 = Hash(e * 3.1 + 101.0);
                    float theta = dirAngle + (e1 * 2.0 - 1.0) * halfAngle * 0.95;
                    if (AngleGap(cullPhi, theta) > emberTolerance) continue;
                    float e2 = Hash(e * 5.7 + 102.0);
                    float spawn = e2 * (duration + EMBER_SPAWN_TAIL);
                    float age = t - spawn;
                    if (age < 0.0 || age > EMBER_MAX_LIFE) continue;
                    float e3 = Hash(e * 7.3 + 103.0);
                    // 출발점에서 이 픽셀까지 날아갈 수 없으면 건너뛴다(출발 각도 방향 띠 밖).
                    float2 emberDir = float2(cos(theta), sin(theta));
                    float emberAlong = dot(cullP, emberDir) - (FrontRadius(spawn) - 0.05 - 0.1 * e3);
                    if (abs(cullP.x * emberDir.y - cullP.y * emberDir.x) > emberSide || emberAlong < -emberSide
                        || emberAlong > EMBER_OUT_REACH / sqrt(emberDir.x * emberDir.x + 0.25 * emberDir.y * emberDir.y) + emberSide) continue;

                    float e4 = Hash(e * 9.1 + 104.0);
                    float e5 = Hash(e * 2.3 + 105.0);
                    float life = EMBER_LIFE + EMBER_LIFE_JITTER * e4;
                    if (age < 0.0 || age > life) continue;

                    float2 start = origin + EllipsePoint(theta, FrontRadius(spawn) - 0.05 - 0.1 * e3);
                    float2 outward = normalize(float2(cos(theta), sin(theta) * 0.5));
                    float drag = (1.0 - exp(-6.0 * age)) / 6.0;
                    // 바깥으로 조금, 위로 크게 튀어 감속하고, 좌우로 흔들리며 계속 떠오른다.
                    float2 p = start + outward * (0.6 + 1.4 * e3) * drag
                        + float2(sin(age * 18.0 + e1 * 6.28) * 0.05, (1.4 + 1.6 * e5) * drag + 0.4 * age);
                    float u = age / life;

                    int2 d = pixel - (int2)floor(p * ppu);
                    int ax = abs(d.x);
                    int ay = abs(d.y);
                    // 모양: 0 = 점, 1 = 십자, 2 = 엑스. 식어 가면 점으로 줄어든다.
                    int shape = u > 0.55 ? 0 : (e4 < 0.35 ? 1 : (e4 < 0.6 ? 2 : 0));
                    bool bCenter = 0 == ax && 0 == ay;
                    bool bArm = (1 == shape && 1 == ax + ay) || (2 == shape && 1 == ax && 1 == ay);
                    if (false == bCenter && false == bArm) continue;
                    if (u > 0.75 && false == bChecker) continue;
                    float heat = u + (bArm ? 0.25 : 0.0);
                    if (heat < 0.15) return Output(_ColorWhite, 0.8);
                    if (heat < 0.45) return Output(_ColorLight, 0.2);
                    if (heat < 0.7) return Output(_ColorMid, 0.0);
                    return Output(heat < 0.9 ? _ColorDark : _ColorLilac, 0.0);
                }

                // 3. 소멸 흩날림: 사라지는 파동에서 푸른 티끌이 제각각 떨어져 나와 바깥·옆·위로 감속하며 흩날리고, 색이 식으며 체커로 사라진다.
                float finaleReach = FINALE_REACH + cullSlack;
                if (t > duration && false == OutsideReach(cullRho, cullSpread, particleAngle, FrontRadius(duration) * 0.8, FrontRadius(duration), finaleReach, finaleReach, finaleReach))
                {
                    float endRadius = FrontRadius(duration);
                    int finaleCount = ScaledCount(FINALE_COUNT, rangeScale);
                    float finaleTolerance = AngleTolerance(cullRho, finaleReach);
                    [loop] for (int k = 0; k < finaleCount; k++)
                    {
                        float f4 = Hash(k * 8.9 + 390.0);
                        // 테두리 선을 따라 줄 서지 않게 각도·반경(파동 두께 안)을 제각각 뽑고, 방향도 바깥·옆·위로 흩는다.
                        float theta = dirAngle + (f4 * 2.0 - 1.0) * halfAngle * 0.95;
                        // 출발 각도가 이 픽셀 쪽이 아니면 건너뛴다.
                        if (AngleGap(cullPhi, theta) > finaleTolerance) continue;
                        float f2 = Hash(k * 4.1 + 330.0);
                        float start = duration + f2 * FINALE_SPAWN;
                        float age = t - start;
                        if (age < 0.0 || age > FINALE_MAX_LIFE) continue;
                        float f1 = Hash(k * 2.3 + 300.0);
                        // 출발점에서 이 픽셀까지 날아갈 수 없으면 건너뛴다.
                        float2 finaleFar = cullP - endRadius * (0.8 + 0.2 * f1) * float2(cos(theta), sin(theta));
                        if (dot(finaleFar, finaleFar) > finaleReach * finaleReach) continue;

                        float f3 = Hash(k * 6.7 + 360.0);
                        float f5 = Hash(k * 3.7 + 420.0);
                        float life = FINALE_LIFE + FINALE_LIFE_JITTER * f3;
                        if (age < 0.0 || age > life) continue;
                        float2 outward = normalize(float2(cos(theta), sin(theta) * 0.5));
                        float2 side = float2(-outward.y, outward.x);
                        float drag = 1.0 - exp(-5.0 * age);
                        float2 drift = outward * (0.2 + 1.2 * f3) + side * (f5 * 2.0 - 1.0) * 0.7;
                        float rise = 0.3 + 0.7 * f1;
                        float2 p = origin + EllipsePoint(theta, endRadius * (0.8 + 0.2 * f1)) + drift * drag / 5.0 + float2(0.0, rise * age);
                        float u = age / life;
                        int2 d = pixel - (int2)floor(p * ppu);
                        int size = (u < 0.45 && f5 > 0.35) ? 2 : 1;
                        if (d.x < 0 || d.y < 0 || d.x >= size || d.y >= size) continue;
                        if (u > 0.55 && false == bChecker) continue;
                        return Output(u < 0.45 ? _ColorMid : _ColorDark, 0.0);
                    }
                }

                // 4. 마루 불꽃 혀: 이 픽셀 바로 아래(같은 열)에서 가장 가까운 마루 점을 뿌리로 삼는다.
                float tongueLife = TongueLife(t, duration);
                // 혀는 마루 띠(안쪽 타원 ~ 앞 가장자리, 진행 방향 ± 반각 × 0.97 안)의 뿌리에서 위로만 솟는다.
                float tongueReach = TONGUE_REACH + cullSlack;
                if (tongueLife > 0.0 && false == OutsideReach(cullRho, cullSpread, bFullCircle ? 4.0 : halfAngle * 0.97,
                        max(0.0, FrontRadius(t) - CREST_PX / ppu), FrontRadius(t), tongueReach, tongueReach, tongueReach))
                {
                    float2 q = (float2(rel) + 0.5) / ppu;
                    float front = FrontRadius(t);
                    float crestInner = max(0.0, front - CREST_PX / ppu);
                    float x2 = q.x * q.x;
                    if (front * front > x2)
                    {
                        // 이 열에서 마루(바깥 타원과 안쪽 타원 사이)의 세로 구간: 위쪽 [yi, yo], 아래쪽 [-yo, -yi]
                        float yo = 0.5 * sqrt(front * front - x2);
                        float yi = crestInner * crestInner > x2 ? 0.5 * sqrt(crestInner * crestInner - x2) : 0.0;
                        float rootY = -1e5;
                        if (q.y >= yi) rootY = min(q.y, yo);
                        else if (q.y >= -yo) rootY = min(q.y, -yi);

                        float rise = (q.y - rootY) * ppu; // 뿌리에서 위로 몇 픽셀인지(0이면 마루 위라 본체가 그린다)
                        // 위쪽 마루의 바깥 1칸은 본체 앞 가장자리와 반 칸씩 어긋나 비는 때가 있어, 혀 밑동으로 같이 채운다.
                        bool bRootRow = q.y >= yi && rise <= 0.5 && (yo - q.y) * ppu < 1.5;
                        if (rootY > -1e4 && (rise > 0.5 || bRootRow))
                        {
                            float theta = atan2(rootY * 2.0, q.x);
                            float spread = AngleFromDir(theta, dirAngle);
                            float taper = bFullCircle ? 1.0 : saturate((halfAngle * 0.97 - spread) / 0.3);
                            // 양옆 끝은 본체와 같은 점 밀도로 옅어져, 마루 선이 끝에서 뚝 끊기지 않는다.
                            if (false == bFullCircle)
                            {
                                float tangentScale = sqrt(sin(theta) * sin(theta) + 0.25 * cos(theta) * cos(theta));
                                float edgePx = (halfAngle - spread) * front * ppu * tangentScale;
                                float softPx = lerp(SIDE_SOFT_PX, SIDE_SOFT_LATE_PX, BodyCool(t, duration));
                                if (saturate(edgePx / softPx) < Bayer4(rel.x, rel.y)) taper = 0.0;
                            }
                            float heightPx = TONGUE_PX * lerp(0.75, 1.3, saturate(rangeScale)) * TongueProfile((float)pixel.x, step) * taper * tongueLife;
                            if (rise < heightPx)
                            {
                                float f = rise / heightPx + BodyCool(t, duration) * 0.4;
                                // 혀 끝은 체커로 흩어진다(빈 칸은 아래 불티·발밑 충격이 그릴 수 있게 넘긴다).
                                // 밑동은 마루와 같은 백열·민트라 마루에서 이어져 솟고, 위로 갈수록 식어 라일락 끝이 된다.
                                if (f <= 0.9 || bChecker)
                                {
                                    if (f < 0.12) return Output(_ColorWhite, 0.7);
                                    if (f < 0.36) return Output(_ColorLight, 0.15);
                                    if (f < 0.6) return Output(_ColorMid, 0.0);
                                    if (f < 0.82) return Output(_ColorDark, 0.0);
                                    return Output(_ColorLilac, 0.0);
                                }
                            }
                        }
                    }
                }

                // 2. 불티·파편
                int moteCount = ScaledCount(MOTE_COUNT, rangeScale);
                float moteSide = MOTE_SIDE_REACH + cullSlack;
                float moteTolerance = AngleTolerance(cullRho, moteSide);
                // 살아 있는 불티의 출발 반지름: 마루(출발 시각) - 0.1, 출발 시각은 지금부터 수명 상한 전까지
                // 바깥으로는 바깥 거리 / 타원 보정(0.5 이상) + 떠오름, 안쪽·옆으로는 떠오름만큼만 벗어난다.
                if (false == OutsideReach(cullRho, cullSpread, particleAngle, FrontRadius(t - MOTE_MAX_LIFE) - 0.1, FrontRadius(t) - 0.1,
                        moteSide, MOTE_OUT_REACH * 2.0 + moteSide, moteSide))
                [loop] for (int i = 0; i < moteCount; i++)
                {
                    // 출발 각도가 이 픽셀 쪽이 아니면 건너뛴다.
                    float h1 = Hash(i * 3.1 + 1.0);
                    float theta = dirAngle + (h1 * 2.0 - 1.0) * halfAngle * 0.95;
                    if (AngleGap(cullPhi, theta) > moteTolerance) continue;
                    float h2 = Hash(i * 5.7 + 2.0);
                    float spawn = h2 * duration * 0.95;
                    float age = t - spawn;
                    if (age < 0.0 || age > MOTE_MAX_LIFE) continue;
                    // 출발점에서 이 픽셀까지 날아갈 수 없으면 건너뛴다(출발 각도 방향 띠 밖).
                    float2 moteDir = float2(cos(theta), sin(theta));
                    float moteAlong = dot(cullP, moteDir) - (FrontRadius(spawn) - 0.1);
                    if (abs(cullP.x * moteDir.y - cullP.y * moteDir.x) > moteSide || moteAlong < -moteSide
                        || moteAlong > MOTE_OUT_REACH / sqrt(moteDir.x * moteDir.x + 0.25 * moteDir.y * moteDir.y) + moteSide) continue;

                    float h3 = Hash(i * 7.3 + 3.0);
                    float h4 = Hash(i * 9.1 + 4.0);
                    bool bPebble = h3 > 0.62;
                    float life = bPebble ? 0.26 + 0.1 * h4 : 0.22 + 0.16 * h4;
                    if (age < 0.0 || age > life) continue;

                    float2 start = origin + EllipsePoint(theta, FrontRadius(spawn) - 0.1);
                    float2 outward = normalize(float2(cos(theta), sin(theta) * 0.5));
                    float u = age / life;
                    float2 p;
                    if (bPebble)
                    {
                        // 바깥으로 날며 한 번 튀었다 떨어진다(높이는 화면 위로).
                        float speed = 2.2 + 1.6 * h4;
                        float up = 2.6 + 1.4 * h1;
                        float height = max(0.0, up * age - 11.0 * age * age);
                        p = start + outward * speed * age + float2(0.0, height);
                    }
                    else
                    {
                        // 빠르게 밀려 나가다 감속하며 살짝 떠오른다.
                        float speed = 3.4 + 2.6 * h4;
                        float dist = speed * (1.0 - exp(-7.0 * age)) / 7.0;
                        p = start + outward * dist + float2(0.0, 0.5 * age);
                    }

                    int2 d = pixel - (int2)floor(p * ppu);
                    int size = bPebble ? 2 : (u < 0.35 ? 3 : (u < 0.7 ? 2 : 1));
                    if (d.x < 0 || d.y < 0 || d.x >= size || d.y >= size) continue;
                    if (u > 0.75 && false == bChecker) continue;
                    if (bPebble) return Output(0 == d.x + d.y ? _Pebble : _PebbleDark, 0.0);
                    return Output(u < 0.3 ? _ColorMid : _ColorDark, 0.0);
                }

                // 1. 발밑 충격
                if (t < BURST_SECONDS)
                {
                    float2 p = (float2(rel) + 0.5) / ppu;
                    float d = length(float2(p.x, p.y * 2.0));
                    float u = t / BURST_SECONDS;
                    // 원판·고리 크기와 고리 두께는 충격파 범위에 맞춘다(최대 범위 강화일 때 1배).
                    float discRadius = (0.42 * (1.0 - smoothstep(0.0, 0.05, t)) + 0.12) * lerp(0.6, 1.0, saturate(rangeScale));
                    if (t < 0.05 && d < discRadius)
                    {
                        return Output(_ColorWhite, d < discRadius * 0.6 ? 1.0 : 0.7);
                    }
                    float ringRadius = (0.25 + 1.25 * (1.0 - (1.0 - u) * (1.0 - u))) * rangeScale;
                    float ringThickness = ((2.0 + 4.0 * saturate(rangeScale)) * (1.0 - u) + 2.0) / ppu;
                    if (abs(d - ringRadius) < ringThickness * 0.5 + 0.5 / ppu)
                    {
                        if (u < 0.3) return Output(_ColorWhite, 0.9);
                        if (u < 0.55) return Output(_ColorLight, 0.3);
                        if (bChecker) return Output(u < 0.8 ? _ColorMid : _ColorDark, 0.0);
                    }
                }

                discard;
                return half4(0.0h, 0.0h, 0.0h, 0.0h);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
