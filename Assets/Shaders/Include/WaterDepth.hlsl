#ifndef WATER_DEPTH_INCLUDED
#define WATER_DEPTH_INCLUDED

// 물 깊이. 물 타일 그림의 수면색·옆면색 픽셀을 물가에서 멀어질수록 몇 단계로 깊은 색(곱하기)으로 칠한다.
// 단계 사이는 또렷하게 나누고 경계 근처만 텍셀 단위로 엇갈려(디더링) 픽셀아트 그대로다.
// 물 타일 셰이더(WaterTilemapShader)가 _MainTex 선언 뒤에 include 한다.
// 전역값은 StageAmbienceShaderGlobals가, 물가 지도 텍스처는 StageShoreMap이 채운다. 꺼진 스테이지는 _WaterDepthA.x가 0이라
// 아무것도 바꾸지 않는다.
// _WaterDepthA : 켜짐(0/1), 색 판정 허용 오차(0~1), 단계 수, 단계 경계가 엇갈리는 폭(0~1)
// _WaterDepthB : 깊어지기 시작하는 거리(칸), 가장 깊어지는 거리(칸), (안 씀), (안 씀)
// _WaterDepthSurface : 물 타일 그림의 수면색, _WaterDepthSide : 물 타일 그림의 옆면색
// _WaterDepthDeepColor : 가장 깊은 곳에서 곱하는 색
// _ShoreMapTex / _ShoreMapA·MapA·MapB·MapC : 물가 지도(칸마다 1 + 물가까지 칸 수 / 255). _ShoreMapA.x가 0이면 그리지 않는다.
#include "StageAmbienceCommon.hlsl"

float4 _WaterDepthA;
float4 _WaterDepthB;
float4 _WaterDepthSurface;
float4 _WaterDepthSide;
float4 _WaterDepthDeepColor;

TEXTURE2D(_ShoreMapTex);
SAMPLER(sampler_ShoreMapTex);
float4 _ShoreMapA;
float4 _ShoreMapMapA;
float4 _ShoreMapMapB;
float4 _ShoreMapMapC;

bool IsWaterDepthEnabled()
{
    return _WaterDepthA.x > 0.5 && _ShoreMapA.x > 0.5;
}

// 물가까지 칸 수(칸 사이는 부드럽게 이어진다).
float WaterDepthShoreDistance(float2 _positionWS)
{
    float2 delta = _positionWS - _ShoreMapMapB.xy;
    float2 cell = float2(dot(_ShoreMapMapA.xy, delta), dot(_ShoreMapMapA.zw, delta)) - _ShoreMapMapB.zw;
    float2 uv = (cell + 0.5) / max(_ShoreMapMapC.xy, 1.0);
    return SAMPLE_TEXTURE2D_LOD(_ShoreMapTex, sampler_ShoreMapTex, uv, 0).r * 255.0 - 1.0;
}

// 4x4 순서 디더링 문턱(0~1). 표 { 0, 8, 2, 10 / 12, 4, 14, 6 / 3, 11, 1, 9 / 15, 7, 13, 5 }와 같은 값을
// 배열 없이 셈으로 낸다(2x2 표 |2a - 3b|를 두 단계로 겹친다). 배열 인덱싱은 GPU에서 느린 경우가 많다.
float WaterDepthBayer(float2 _texelId)
{
    float2 p = _texelId - 4.0 * floor(_texelId * 0.25);
    float2 high = floor(p * 0.5);
    float2 low = p - 2.0 * high;
    float m = 4.0 * abs(2.0 * low.x - 3.0 * low.y) + abs(2.0 * high.x - 3.0 * high.y);
    return (m + 0.5) / 16.0;
}

bool WaterDepthMatches(float3 _color, float3 _key)
{
    float3 d = abs(_color - _key);
    return max(d.x, max(d.y, d.z)) < _WaterDepthA.y;
}

// 수면·옆면 픽셀이면 깊이를 칠한 색, 아니면 그대로. _color는 조명이 끝난 색, _texColor는 정점색을 곱하기 전 텍스처 색이다.
// 옆면은 맵 끝이나 물가 아래쪽에서만 보이므로 수면과 같은 깊이로 맞춰 따로 놀지 않게 한다.
// _centerWS: 텍셀 중심 월드 위치(StageAmbienceTexelCenterWS), _texelId: floor(_centerWS * ppu). 물 셰이더가 한 번만 구해 넘긴다.
half3 ApplyWaterDepthAt(half3 _color, float2 _centerWS, float2 _texelId, float3 _texColor)
{
    if (false == WaterDepthMatches(_texColor, _WaterDepthSurface.rgb) && false == WaterDepthMatches(_texColor, _WaterDepthSide.rgb))
    {
        return _color;
    }

    float2 centerWS = _centerWS;
    float2 texelId = _texelId;

    float depth = saturate((WaterDepthShoreDistance(centerWS) - _WaterDepthB.x) / max(_WaterDepthB.y - _WaterDepthB.x, 0.1));
    float bands = max(_WaterDepthA.z - 1.0, 1.0);
    float level = saturate(floor(depth * bands + 0.5 + (WaterDepthBayer(texelId) - 0.5) * _WaterDepthA.w) / bands);
    return _color * (half3)lerp(float3(1.0, 1.0, 1.0), _WaterDepthDeepColor.rgb, level);
}

half3 ApplyWaterDepth(half3 _color, float2 _uv, float2 _positionWS, float4 _texelSize, float3 _texColor)
{
    float2 centerWS = StageAmbienceTexelCenterWS(_positionWS, _uv, _texelSize);
    // 타일 텍셀 중심은 (정수 + 0.5) / ppu에 놓이므로 내림하면 텍셀 하나가 한 번호를 갖는다.
    return ApplyWaterDepthAt(_color, centerWS, floor(centerWS * StageAmbiencePixelsPerUnit()), _texColor);
}

#endif // WATER_DEPTH_INCLUDED
