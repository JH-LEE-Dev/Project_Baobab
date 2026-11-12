#ifndef WATER_SPORES_INCLUDED
#define WATER_SPORES_INCLUDED

// 물 위 포자. 물 타일 그림의 수면색 픽셀 위에 빛나는 포자 알갱이가 하나씩 떠올라
// 바람 방향으로 천천히 떠가며 살짝 흔들리다 사라진다. 물 타일 셰이더(WaterTilemapShader)가 _MainTex 선언 뒤에 include 한다.
// 전역값은 StageAmbienceShaderGlobals가 스테이지 데이터대로 채운다. 꺼진 스테이지는 _WaterSporeA.x가 0이라
// 아무것도 바꾸지 않는다.
// _WaterSporeA : 켜짐(0/1), 포자 하나가 맡는 칸 크기(텍셀), 떠 있는 시간 비율(0~1), 가장 밝을 때 밝기(HDR 배수)
// _WaterSporeB : 떠가는 방향 x, y(정규화), 한 번 떠 있는 동안 가는 거리(텍셀), 주기(초)
// _WaterSporeC : 옆으로 흔들리는 폭(텍셀), 색 판정 허용 오차(0~1), (안 씀), (안 씀)
// _WaterSporeD : 십자 빛 세기(0~1), 물빛 반경(텍셀), 물빛 진하기(0~1), 반짝 세기(0~1). 모두 0이면 예전처럼 텍셀 한 칸만 그린다.
// _WaterSporeColor : 포자 색
// _WaterSporeColorB : 두 번째 포자 색(칸마다 포자 색과 이 색 사이에서 정해진다. 같으면 한 색)
// _WaterSporeSurfaceA/B : 물 타일 그림의 수면색 두 가지
#include "StageAmbienceCommon.hlsl"

float4 _WaterSporeA;
float4 _WaterSporeB;
float4 _WaterSporeC;
float4 _WaterSporeD;
float4 _WaterSporeColor;
float4 _WaterSporeColorB;
float4 _WaterSporeSurfaceA;
float4 _WaterSporeSurfaceB;

bool IsWaterSporesEnabled()
{
    return _WaterSporeA.x > 0.5;
}

float WaterSporeHash21(float2 _p)
{
    float3 p3 = frac(float3(_p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

bool WaterSporeMatches(float3 _color, float3 _key)
{
    float3 d = abs(_color - _key);
    return max(d.x, max(d.y, d.z)) < _WaterSporeC.y;
}

// rgb = 덮을 색, a = 덮는 비율(0이면 그대로). _texColor는 정점색을 곱하기 전 텍스처 색이다.
// _texelId: 텍셀 번호 floor(텍셀 중심 월드 위치 * ppu). 물 셰이더가 한 번만 구해 넘긴다.
float4 ComputeWaterSporesAt(float2 _texelId, float3 _texColor)
{
    if (false == WaterSporeMatches(_texColor, _WaterSporeSurfaceA.rgb) && false == WaterSporeMatches(_texColor, _WaterSporeSurfaceB.rgb))
    {
        return 0.0;
    }

    float2 texelId = _texelId;

    float cellSize = max(floor(_WaterSporeA.y), 4.0);
    float2 cell = floor(texelId / cellSize);

    // 칸마다 제 주기·박자로 한 알이 떠올랐다 사라진다.
    float period = max(_WaterSporeB.w, 0.1) * (0.7 + 0.6 * WaterSporeHash21(cell + 3.1));
    float local = frac(_Time.y / period + WaterSporeHash21(cell + 9.4));
    float duty = max(_WaterSporeA.z, 0.0001);
    if (local >= duty)
    {
        return 0.0;
    }
    float life = local / duty;

    // 십자 빛·물빛·반짝일 때 늘어나는 빛줄기가 칸 밖으로 나가지 않게 그만큼 안쪽에서 시작한다.
    float armStrength = saturate(_WaterSporeD.x);
    float haloRadius = clamp(_WaterSporeD.y, 0.0, 3.0);
    float haloAlpha = saturate(_WaterSporeD.z);
    float twinkle = saturate(_WaterSporeD.w);
    float pad = max(1.0, max(ceil(haloRadius), twinkle > 0.0 ? 2.0 : 0.0));
    float reach = max(max(armStrength > 0.0 ? 1.0 : 0.5, haloRadius), twinkle > 0.0 ? 2.0 : 0.5);

    float travel = min(max(_WaterSporeB.z, 0.0), max(cellSize - 1.0 - 2.0 * pad, 0.0));
    float wobble = min(max(_WaterSporeC.x, 0.0), 2.0);
    float room = max(cellSize - 2.0 * pad - travel - wobble * 2.0, 0.0);
    float2 start = cell * cellSize + pad + wobble + travel * 0.5
                 + float2(WaterSporeHash21(cell + 31.3), WaterSporeHash21(cell + 47.9)) * room;
    float drift = (life - 0.5) * travel;

    // 칸 안 대부분의 픽셀은 포자와 멀다. 바람 방향 그대로 옮긴 자리와 실제 자리의 차이는
    // 방향을 튼 만큼(최대 0.4라디안이라 이동 거리의 2·sin 0.2 = 0.3973배 이하) + 옆 흔들림 폭 이하이므로,
    // 그보다 멀면 방향 회전·흔들림을 계산하지 않고 넘어간다(그려질 픽셀은 하나도 빠지지 않는다).
    float2 rough = abs(texelId - (start + _WaterSporeB.xy * drift));
    if (max(rough.x, rough.y) > reach + 1.01 + 0.3973 * abs(drift) + wobble)
    {
        return 0.0;
    }

    // 바람 방향을 칸마다 조금씩 틀어 떠가고, 옆으로 천천히 흔들린다.
    float2 dir = _WaterSporeB.xy;
    float angle = (WaterSporeHash21(cell + 21.7) - 0.5) * 0.8;
    float ca = cos(angle);
    float sa = sin(angle);
    dir = float2(dir.x * ca - dir.y * sa, dir.x * sa + dir.y * ca);
    float2 side = float2(-dir.y, dir.x);

    float2 pos = start + dir * drift + side * (sin(life * 6.2831853 + WaterSporeHash21(cell) * 6.2831853) * wobble);

    float2 d = abs(texelId - floor(pos));
    float manhattan = d.x + d.y;
    float chebyshev = max(d.x, d.y);
    if (manhattan > 0.5 && chebyshev > reach + 0.5)
    {
        return 0.0;
    }

    // 떠오를 때와 가라앉을 때 옅고, 가운데에서 가장 밝아 블룸이 살짝 번진다.
    float fade = sin(life * 3.14159);
    float3 sporeColor = lerp(_WaterSporeColor.rgb, _WaterSporeColorB.rgb, WaterSporeHash21(cell + 63.1));

    // 한 알이 떠 있는 동안 한 번, 정해진 순간에 별처럼 반짝인다(하얗게 밝아지며 빛줄기가 한 칸 더 뻗는다).
    float twinkleAt = 0.35 + 0.3 * WaterSporeHash21(cell + 77.7);
    float flash = twinkle * saturate(1.0 - abs(life - twinkleAt) / 0.07);
    flash *= flash;

    if (manhattan < 0.5)
    {
        float brightness = lerp(1.0, _WaterSporeA.w, fade * fade) * (1.0 + 0.8 * flash);
        float3 core = lerp(sporeColor, 1.0, 0.45 * flash);
        return float4(core * brightness, saturate(fade * 1.6));
    }

    // 솜털 빛: 밝게 떠 있을 때 큰 포자(칸의 절반쯤)는 상하좌우 한 칸, 그보다 옅게 대각선 한 칸까지 번져
    // 동그란 솜뭉치처럼 보인다. 작은 포자는 점 하나로 남아 크기가 섞인다.
    // 반짝이는 순간에는 크기와 관계없이 상하좌우로 두 칸 빛줄기가 뻗어 별처럼 보인다.
    float peak = smoothstep(0.35, 0.85, fade);
    float puff = armStrength * peak * step(WaterSporeHash21(cell + 88.3), 0.5);
    bool onAxis = min(d.x, d.y) < 0.5;
    if (onAxis && manhattan < 1.5)
    {
        float a = saturate(puff + flash * 0.6);
        if (a > 0.0)
        {
            float brightness = lerp(1.0, _WaterSporeA.w, peak * 0.6) * (1.0 + 0.5 * flash);
            return float4(lerp(sporeColor, 1.0, 0.3 * flash) * brightness, a);
        }
    }
    if (onAxis && manhattan < 2.5 && flash > 0.0)
    {
        return float4(lerp(sporeColor, 1.0, 0.3) * lerp(1.0, _WaterSporeA.w, 0.5), flash * 0.55);
    }
    if (chebyshev < 1.5 && manhattan > 1.5 && puff > 0.0)
    {
        return float4(sporeColor, puff * 0.45 * (1.0 - flash));
    }

    // 물빛: 포자 둘레 물이 포자 색으로 은은하게 물든다. 픽셀 그림답게 안쪽·바깥쪽 두 단계로 끊는다.
    if (haloAlpha > 0.0 && haloRadius > 0.0)
    {
        float dist = length(d);
        if (dist <= haloRadius + 0.25)
        {
            float band = dist <= haloRadius * 0.55 + 0.25 ? 1.0 : 0.5;
            return float4(sporeColor, haloAlpha * band * fade);
        }
    }

    return 0.0;
}

#endif // WATER_SPORES_INCLUDED
