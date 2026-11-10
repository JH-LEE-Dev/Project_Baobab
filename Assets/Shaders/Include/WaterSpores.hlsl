#ifndef WATER_SPORES_INCLUDED
#define WATER_SPORES_INCLUDED

// 물 위 포자. 물 타일 그림의 수면색 픽셀 위에 연보라 포자 알갱이(텍셀 한 칸)가 하나씩 떠올라
// 바람 방향으로 천천히 떠가며 살짝 흔들리다 사라진다. 물 타일 셰이더(WaterTilemapShader)가 _MainTex 선언 뒤에 include 한다.
// 전역값은 StageAmbienceShaderGlobals가 스테이지 데이터대로 채운다. 꺼진 스테이지는 _WaterSporeA.x가 0이라
// 아무것도 바꾸지 않는다.
// _WaterSporeA : 켜짐(0/1), 포자 하나가 맡는 칸 크기(텍셀), 떠 있는 시간 비율(0~1), 가장 밝을 때 밝기(HDR 배수)
// _WaterSporeB : 떠가는 방향 x, y(정규화), 한 번 떠 있는 동안 가는 거리(텍셀), 주기(초)
// _WaterSporeC : 옆으로 흔들리는 폭(텍셀), 색 판정 허용 오차(0~1), (안 씀), (안 씀)
// _WaterSporeColor : 포자 색
// _WaterSporeSurfaceA/B : 물 타일 그림의 수면색 두 가지
#include "StageAmbienceCommon.hlsl"

float4 _WaterSporeA;
float4 _WaterSporeB;
float4 _WaterSporeC;
float4 _WaterSporeColor;
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
float4 ComputeWaterSpores(float2 _uv, float2 _positionWS, float4 _texelSize, float3 _texColor)
{
    if (false == WaterSporeMatches(_texColor, _WaterSporeSurfaceA.rgb) && false == WaterSporeMatches(_texColor, _WaterSporeSurfaceB.rgb))
    {
        return 0.0;
    }

    float ppu = StageAmbiencePixelsPerUnit();
    float2 centerWS = StageAmbienceTexelCenterWS(_positionWS, _uv, _texelSize);
    // 타일 텍셀 중심은 (정수 + 0.5) / ppu에 놓이므로 내림하면 텍셀 하나가 한 번호를 갖는다.
    float2 texelId = floor(centerWS * ppu);

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

    // 바람 방향을 칸마다 조금씩 틀어 떠가고, 옆으로 천천히 흔들린다. 칸 밖으로 나가지 않게 이동 거리만큼 안쪽에서 시작한다.
    float2 dir = _WaterSporeB.xy;
    float angle = (WaterSporeHash21(cell + 21.7) - 0.5) * 0.8;
    float ca = cos(angle);
    float sa = sin(angle);
    dir = float2(dir.x * ca - dir.y * sa, dir.x * sa + dir.y * ca);
    float2 side = float2(-dir.y, dir.x);

    float travel = min(max(_WaterSporeB.z, 0.0), cellSize - 3.0);
    float wobble = min(max(_WaterSporeC.x, 0.0), 2.0);
    float room = max(cellSize - 2.0 - travel - wobble * 2.0, 0.0);
    float2 start = cell * cellSize + 1.0 + wobble + travel * 0.5
                 + float2(WaterSporeHash21(cell + 31.3), WaterSporeHash21(cell + 47.9)) * room;
    float2 pos = start + dir * ((life - 0.5) * travel) + side * (sin(life * 6.2831853 + WaterSporeHash21(cell) * 6.2831853) * wobble);

    float2 d = abs(texelId - floor(pos));
    if (d.x + d.y > 0.5)
    {
        return 0.0;
    }

    // 떠오를 때와 가라앉을 때 옅고, 가운데에서 가장 밝아 블룸이 살짝 번진다.
    float fade = sin(life * 3.14159);
    float brightness = lerp(1.0, _WaterSporeA.w, fade * fade);
    return float4(_WaterSporeColor.rgb * brightness, saturate(fade * 1.6));
}

#endif // WATER_SPORES_INCLUDED
