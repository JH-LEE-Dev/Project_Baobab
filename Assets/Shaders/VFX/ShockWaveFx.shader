Shader "Custom/VFX/ShockWaveFx"
{
    // 충격파 곁들임 연출. ShockWaveVisualRunner가 충격파 원점에 사각형 쿼드 하나를 깔고, 픽셀(32 PPU 게임 격자)마다
    // 아래 세 가지 중 무엇이 그려지는지 여기서 정한다. 위치는 전부 경과 시간(_T)과 퍼지는 값(_Geo)으로 계산한다(재생 상태 없음).
    // 1. 발밑 충격: 발동 순간 원점에 흰 원판이 번쩍이고 두꺼운 고리가 퍼지며 식는다(0.17초).
    // 2. 흙먼지·파편: 파동 앞 가장자리가 지나간 자리에서 먼지는 바깥으로 밀려 나가 감속하고, 돌 조각은 한 번 튀었다 떨어진다.
    // 3. 소멸 흩날림: 판정이 끝나면 사라지는 파동에서 작은 티끌이 떨어져 나와 바깥·옆·위로 흩날리며 식는다.
    // 발밑 충격 크기와 흙먼지·티끌 개수는 충격파 범위(범위 강화)에 비례한다.
    // 출력은 BrandWrapGlow와 같다(색 * lerp(_GlowMin, _GlowMax, 발광 세기), 알파 1).
    Properties
    {
        _GlowMin("Glow Min (HDR 발광 최소 배율, 발광 세기 0)", Float) = 1.0
        _GlowMax("Glow Max (HDR 발광 최대 배율, 발광 세기 1)", Float) = 2.4
        _ColorWhite("White (rgb, a = 발광 세기)", Vector) = (1, 1, 1, 1)
        _ColorLight("Light", Vector) = (1, 0.9255, 0.698, 0.0784)
        _ColorMid("Mid", Vector) = (1, 0.7608, 0.3216, 0)
        _ColorDark("Dark", Vector) = (0.8392, 0.4706, 0.1569, 0)
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
            Name "ShockWaveFxPass"

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
            #define MAX_RANGE_SCALE 1.3
            #define MOTE_COUNT 72
            #define FINALE_COUNT 30
            #define BURST_SECONDS 0.17
            // 흩날림은 러너가 판정 뒤로 남겨 두는 시간(FxTailSeconds 0.34초) 안에 끝나야 한다: 0.08 + 0.18 + 0.08
            #define FINALE_SPAWN 0.08
            #define FINALE_LIFE 0.18
            #define FINALE_LIFE_JITTER 0.08

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

                // 3. 소멸 흩날림(맨 위): 사라지는 파동에서 금빛 티끌이 제각각 떨어져 나와 바깥·옆·위로 감속하며 흩날리고, 색이 식으며 체커로 사라진다.
                if (t > duration)
                {
                    float endRadius = FrontRadius(duration);
                    int finaleCount = ScaledCount(FINALE_COUNT, rangeScale);
                    [loop] for (int k = 0; k < finaleCount; k++)
                    {
                        float f1 = Hash(k * 2.3 + 300.0);
                        float f2 = Hash(k * 4.1 + 330.0);
                        float f3 = Hash(k * 6.7 + 360.0);
                        float f4 = Hash(k * 8.9 + 390.0);
                        float f5 = Hash(k * 3.7 + 420.0);
                        float start = duration + f2 * FINALE_SPAWN;
                        float life = FINALE_LIFE + FINALE_LIFE_JITTER * f3;
                        float age = t - start;
                        if (age < 0.0 || age > life) continue;
                        // 테두리 선을 따라 줄 서지 않게 각도·반경(파동 두께 안)을 제각각 뽑고, 방향도 바깥·옆·위로 흩는다.
                        float theta = dirAngle + (f4 * 2.0 - 1.0) * halfAngle * 0.95;
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

                // 2. 흙먼지·파편
                int moteCount = ScaledCount(MOTE_COUNT, rangeScale);
                [loop] for (int i = 0; i < moteCount; i++)
                {
                    float h1 = Hash(i * 3.1 + 1.0);
                    float h2 = Hash(i * 5.7 + 2.0);
                    float h3 = Hash(i * 7.3 + 3.0);
                    float h4 = Hash(i * 9.1 + 4.0);
                    float spawn = h2 * duration * 0.95;
                    float age = t - spawn;
                    bool bPebble = h3 > 0.62;
                    float life = bPebble ? 0.26 + 0.1 * h4 : 0.22 + 0.16 * h4;
                    if (age < 0.0 || age > life) continue;

                    float theta = dirAngle + (h1 * 2.0 - 1.0) * halfAngle * 0.95;
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
