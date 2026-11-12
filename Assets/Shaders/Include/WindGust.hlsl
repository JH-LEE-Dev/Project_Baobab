#ifndef WIND_GUST_INCLUDED
#define WIND_GUST_INCLUDED

// 돌풍 연출. 돌풍 덩어리가 바람 방향으로 지나가면 그 자리의 나무(TreeWindSway)가 잠깐 바람 쪽으로 눕고,
// 잔디(GroundWindGustLit)는 풀잎이 하나둘씩 바람 쪽으로 1텍셀 누웠다가 일어선다.
// 전역값은 StageAmbienceShaderGlobals가 스테이지 데이터대로 채운다. 꺼진 스테이지는 _WindGustB.x가 0이라
// 아무것도 바꾸지 않는다.
// _WindGustA : 바람 방향 x, y(정규화), 이동 속도(월드/초), 돌풍 간격(월드)
// _WindGustB : 켜짐(0/1), 돌풍 앞쪽 반폭(월드, 뒤쪽 꼬리는 2.5배), 덩어리 크기(월드, 바람과 수직 방향), 덩어리 비율(0~1)
// _WindGustC : 누운 풀잎 밝기 증가 비율, 나무 추가 기울기(도), 나무 떨림 속도, 나무 떨림 비율
// _WindGustD : 잔디 덩어리 크기(월드), 풀잎이 눕기 시작하는 가장 약한 돌풍 세기, 풀잎마다 문턱이 퍼지는 폭, 돌풍 무리 길이(월드, 0이면 늘 같은 세기)
// _WindGustE : 홀로 떨어진 작은 덩어리 넓히는 배수(1~2, 1 이하면 그대로), (안 씀), (안 씀), (안 씀)
float4 _WindGustA;
float4 _WindGustB;
float4 _WindGustC;
float4 _WindGustD;
float4 _WindGustE;

// 곱셈·frac만 쓰는 해시. sin 해시는 GPU마다 sin 정밀도가 달라 덩어리가 켜지고 꺼지는 자리가 기기·CPU 계산과 어긋나므로 쓰지 않는다
// (StageWindPetals가 같은 식을 CPU에서 계산해 꽃가루를 돌풍 자리에 띄운다).
float WindGustHash11(float _x)
{
    float p = frac(_x * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float WindGustValueNoise(float _x)
{
    float i = floor(_x);
    float f = frac(_x);
    float u = f * f * (3.0 - 2.0 * f);
    return lerp(WindGustHash11(i), WindGustHash11(i + 1.0), u);
}

bool IsWindGustEnabled()
{
    return _WindGustB.x > 0.5;
}

// _posWS 자리의 지금 돌풍 세기(0~1).
float SampleWindGust(float2 _posWS)
{
    if (false == IsWindGustEnabled())
    {
        return 0.0;
    }

    float2 dir = _WindGustA.xy;
    float2 perp = float2(-dir.y, dir.x);
    float along = dot(_posWS, dir);
    float across = dot(_posWS, perp);

    // 앞머리를 일렁이게 해서 나무들이 한 줄로 동시에 눕지 않게 한다.
    // 짧은 물결을 하나 더 얹어 앞머리가 곧은 세로줄로 읽히지 않게 한다.
    along += sin(across * 0.45) * 0.9 + sin(across * 1.1 + 1.7) * 0.35 + sin(across * 2.3 + 0.6) * 0.45;

    float spacing = max(_WindGustA.w, 1.0);
    float s = along - _Time.y * _WindGustA.z;
    float idx = floor(s / spacing);
    float local = s - idx * spacing - spacing * 0.5;

    // 진행 방향(+) 쪽은 짧게 서고, 지나간 뒤(-)는 길게 빠진다.
    float halfWidth = max(_WindGustB.y, 0.01);
    float t = local >= 0.0 ? local / halfWidth : -local / (halfWidth * 2.5);
    float band = saturate(1.0 - t);
    band = band * band * (3.0 - 2.0 * band);
    // 돌풍 띠 밖(화면 대부분)은 아래 덩어리·무리 계산 없이 끝낸다. 결과는 곱이 0이라 같다.
    if (band <= 0.0)
    {
        return 0.0;
    }

    // 돌풍 무리: 몇 번 몰아치다가 한동안 잠잠해진다. 돌풍 하나는 통째로 불거나 안 불거나 해서,
    // 바람처럼 보이지 않는 약한 돌풍은 없다. 쉬는 돌풍이면 덩어리 계산 없이 끝낸다.
    float group = 1.0;
    if (_WindGustD.w > 0.0)
    {
        float groupNoise = WindGustValueNoise((idx + 0.5) * spacing / _WindGustD.w + 3.7);
        if (groupNoise <= 0.3)
        {
            return 0.0;
        }
        group = lerp(0.8, 1.0, smoothstep(0.3, 0.7, groupNoise));
    }

    // 돌풍마다 가로로 끊긴 덩어리가 다르게 생긴다. 덩어리는 고르게 센 것만 만들어,
    // 나무 한두 그루만 살짝 건드리는 가늘고 약한 덩어리는 생기지 않게 한다.
    float cut = 1.0 - _WindGustB.w;
    float patchX = across / max(_WindGustB.z, 0.5) + idx * 7.31;
    float patchCell = floor(patchX);
    float patchF = frac(patchX);
    float patchA = WindGustHash11(patchCell) > cut ? 1.0 : 0.0;
    float patchB = WindGustHash11(patchCell + 1.0) > cut ? 1.0 : 0.0;
    float patch = lerp(patchA, patchB, patchF * patchF * (3.0 - 2.0 * patchF));
    patch = smoothstep(0.3, 0.7, patch);

    // 작은 돌풍 넓히기: 양옆이 비어 홀로 떨어진 덩어리만 같은 모양 그대로 옆으로 늘린다(이어진 큰 덩어리는 그대로).
    // 홀로 떨어진 덩어리는 덩어리 번호 자리(정수 좌표)를 가운데로 양옆 1칸까지 퍼지므로, 거리를 배수로 나눠 늘린다.
    float smallScale = _WindGustE.x;
    if (smallScale > 1.0)
    {
        [unroll]
        for (int k = -1; k <= 2; k++)
        {
            float cellK = patchCell + k;
            bool isolated = WindGustHash11(cellK) > cut && WindGustHash11(cellK - 1.0) <= cut && WindGustHash11(cellK + 1.0) <= cut;
            float dist = abs(patchX - cellK) / smallScale;
            if (isolated && dist < 1.0)
            {
                float fall = dist * dist * (3.0 - 2.0 * dist);
                patch = max(patch, smoothstep(0.3, 0.7, 1.0 - fall));
            }
        }
    }

    return band * patch * group;
}

// 정수 좌표용 해시(큰 좌표에서도 무늬가 깨지지 않는다).
float WindGustHash21(float2 _p)
{
    float3 p3 = frac(float3(_p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float WindGustValueNoise2D(float2 _p)
{
    float2 i = floor(_p);
    float2 f = frac(_p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = WindGustHash21(i);
    float b = WindGustHash21(i + float2(1.0, 0.0));
    float c = WindGustHash21(i + float2(0.0, 1.0));
    float d = WindGustHash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// 잔디용 돌풍 세기. 나무와 같은 돌풍을 바람과 함께 흘러가는 작은 덩어리로 다시 쪼개,
// 돌풍 안에서도 군데군데만 풀이 눕게 한다(곧은 띠가 한꺼번에 지나가 보이지 않게).
float SampleWindGustGrass(float2 _posWS)
{
    float gust = SampleWindGust(_posWS);
    if (gust <= 0.0)
    {
        return 0.0;
    }

    float2 drift = _WindGustA.xy * (_Time.y * _WindGustA.z);
    float clump = WindGustValueNoise2D((_posWS - drift) / max(_WindGustD.x, 0.1));
    clump = smoothstep(0.35, 0.75, clump);
    return gust * clump;
}

#endif // WIND_GUST_INCLUDED
