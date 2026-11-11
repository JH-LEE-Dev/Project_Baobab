#ifndef WATER_BUBBLES_INCLUDED
#define WATER_BUBBLES_INCLUDED

// 물(용암) 위 거품. 물 타일 그림의 수면색 픽셀 위에서 거품이 아래에서 올라오듯 점처럼 생겨 둥글게 부풀고,
// 다 부풀면 톡 터지며 끊어진 고리 조각이 한 칸 퍼졌다 사라진다. 칸마다 제 박자로 되풀이하고, 매번 칸 안의 다른 자리에서 올라온다.
// 물 타일 셰이더(WaterTilemapShader)가 _MainTex 선언 뒤에 include 한다.
// 전역값은 StageAmbienceShaderGlobals가 스테이지 데이터대로 채운다. 꺼진 스테이지는 _WaterBubbleA.x가 0이라
// 아무것도 바꾸지 않는다.
// _WaterBubbleA : 켜짐(0/1), 거품 하나가 맡는 칸 크기(텍셀), 거품이 있는 시간 비율(0~1), 주기(초)
// _WaterBubbleB : 다 부푼 반지름 최소, 최대(텍셀), 그중 터지는 시간 비율(0~1), 하이라이트 밝기(HDR 배수)
// _WaterBubbleBodyColor : 거품 몸 색(터질 때 흩어지는 조각도 이 색)
// _WaterBubbleEdgeColor : 거품 아래쪽 테두리(그늘)와 막 올라올 때 점의 색
// _WaterBubbleHighlightColor : 거품 윗쪽 하이라이트 색
// _WaterBubbleSurface : rgb = 물 타일 그림의 수면색, a = 색 판정 허용 오차(0~1)
// _WaterBubbleC : x = 섬을 둘러싼 바다 칸에서 거품이 올라오는 되풀이 비율(0~1, 1이면 어디서나 같은 빈도)
// 바다 칸 판정은 용암 바다 지도(LavaSeaMap.hlsl, StageLavaSeaMap이 채운다)를 쓴다. 지도가 꺼져 있으면 어디서나 같은 빈도다.
#include "StageAmbienceCommon.hlsl"
#include "LavaSeaMap.hlsl"

float4 _WaterBubbleA;
float4 _WaterBubbleB;
float4 _WaterBubbleC;
float4 _WaterBubbleBodyColor;
float4 _WaterBubbleEdgeColor;
float4 _WaterBubbleHighlightColor;
float4 _WaterBubbleSurface;

bool IsWaterBubblesEnabled()
{
    return _WaterBubbleA.x > 0.5;
}

float WaterBubbleHash21(float2 _p)
{
    float3 p3 = frac(float3(_p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// rgb = 덮을 색, a = 덮는 비율(0이면 그대로). _texColor는 정점색을 곱하기 전 텍스처 색이다.
float4 ComputeWaterBubbles(float2 _uv, float2 _positionWS, float4 _texelSize, float3 _texColor)
{
    float3 d = abs(_texColor - _WaterBubbleSurface.rgb);
    if (max(d.x, max(d.y, d.z)) >= _WaterBubbleSurface.a)
    {
        return 0.0;
    }

    float ppu = StageAmbiencePixelsPerUnit();
    float2 centerWS = StageAmbienceTexelCenterWS(_positionWS, _uv, _texelSize);
    // 타일 텍셀 중심은 (정수 + 0.5) / ppu에 놓이므로 내림하면 텍셀 하나가 한 번호를 갖는다.
    float2 texelId = floor(centerWS * ppu);

    float cellSize = max(floor(_WaterBubbleA.y), 6.0);
    // 칸이 바둑판처럼 줄지어 보이지 않게 가로줄마다 칸을 옆으로 무작위만큼 민다.
    float row = floor(texelId.y / cellSize);
    float rowShift = floor(WaterBubbleHash21(float2(row, 57.3)) * cellSize);
    float2 cell = float2(floor((texelId.x + rowShift) / cellSize), row);

    // 칸마다 제 주기·박자로 되풀이한다. 몇 번째 되풀이인지로 자리·크기를 매번 다시 고른다.
    float period = max(_WaterBubbleA.w, 0.1) * (0.75 + 0.5 * WaterBubbleHash21(cell + 3.1));
    float phase = _Time.y / period + WaterBubbleHash21(cell + 9.4);
    float cycle = floor(phase);
    float local = phase - cycle;
    float duty = max(_WaterBubbleA.z, 0.0001);
    if (local >= duty)
    {
        return 0.0;
    }
    float life = local / duty;

    float2 seed = cell + cycle * float2(7.13, 3.71);
    float maxRadius = lerp(_WaterBubbleB.x, max(_WaterBubbleB.x, _WaterBubbleB.y), WaterBubbleHash21(seed + 21.7));
    // 터지는 고리(다 부푼 반지름 + 1, 두께 반 칸)까지 칸 안에 들어오도록 그만큼 안쪽 텍셀에 가운데를 둔다.
    float edge = ceil(maxRadius + 1.0);
    float room = max(cellSize - 1.0 - edge * 2.0, 0.0);
    float2 cellOrigin = float2(cell.x * cellSize - rowShift, cell.y * cellSize);
    float2 center = cellOrigin + edge + floor(float2(WaterBubbleHash21(seed + 31.3), WaterBubbleHash21(seed + 47.9)) * (room + 1.0)) + 0.5;
    // 섬을 둘러싼 바다에서는 되풀이 중 일부에서만 올라온다(거품 가운데 칸 기준이라 한 거품은 통째로 보이거나 빠진다).
    if (IsLavaSeaMapEnabled() && _WaterBubbleC.x < 1.0 && WaterBubbleHash21(seed + 63.1) >= _WaterBubbleC.x && IsLavaSeaCell(center / ppu))
    {
        return 0.0;
    }

    float2 offset = (texelId + 0.5) - center;
    float dist = length(offset);

    float popFraction = saturate(_WaterBubbleB.z);
    float growEnd = 1.0 - popFraction;

    if (life < growEnd)
    {
        // 올라오며 부푼다: 처음에는 어두운 점 하나였다가 둥글게 커진다.
        float grow = life / max(growEnd, 0.0001);
        float radius = lerp(0.5, maxRadius, grow * (2.0 - grow));
        if (dist > radius + 0.25)
        {
            return 0.0;
        }

        if (radius < 1.0)
        {
            return float4(_WaterBubbleEdgeColor.rgb, 1.0);
        }

        // 윗쪽 왼편 한 칸은 하이라이트, 아래쪽 테두리는 그늘, 나머지는 몸 색.
        float2 highlight = floor(float2(-0.45, 0.45) * radius + 0.5);
        if (all(abs(floor(offset) - highlight) < 0.5) && radius >= 1.5)
        {
            return float4(_WaterBubbleHighlightColor.rgb * _WaterBubbleB.w, 1.0);
        }
        if (dist > radius - 0.85 && offset.y < 0.0)
        {
            return float4(_WaterBubbleEdgeColor.rgb, 1.0);
        }
        return float4(_WaterBubbleBodyColor.rgb, 1.0);
    }

    // 톡 터진다: 다 부푼 크기에서 몸 색 고리가 한 칸 퍼지며 조각조각 흩어지고 옅어진다.
    float pop = (life - growEnd) / max(popFraction, 0.0001);
    float ringRadius = maxRadius + pop;
    if (abs(dist - ringRadius) > 0.5)
    {
        return 0.0;
    }
    // 고리를 끊어 튀는 조각처럼 보이게 한다(터질 때마다 끊기는 자리가 바뀐다).
    if (WaterBubbleHash21(texelId + seed * 1.37) < 0.45)
    {
        return 0.0;
    }
    float fade = 1.0 - pop;
    return float4(_WaterBubbleBodyColor.rgb, fade * fade);
}

#endif // WATER_BUBBLES_INCLUDED
