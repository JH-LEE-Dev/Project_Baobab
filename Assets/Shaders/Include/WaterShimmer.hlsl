#ifndef WATER_SHIMMER_INCLUDED
#define WATER_SHIMMER_INCLUDED

// 물 반짝임 연출. 물 타일 셰이더(WaterTilemapShader, WaterURP)가 _MainTex 선언 뒤에 include 한다.
// 물 타일 그림의 수면색 픽셀만 골라 그 위에 반짝임을 덧그리고, 켜져 있으면 테두리색 픽셀에 달빛 물결을 덧그린다.
// 전역값은 StageAmbienceShaderGlobals가 스테이지 데이터대로 채운다. 꺼진 스테이지는 _WaterShimmerA.x가 0이라
// 아무것도 바꾸지 않는다.
// _WaterShimmerA : 켜짐(0/1), 반짝임 칸 크기(텍셀), 반짝임이 켜져 있는 비율(0~1), 반짝임 밝기(HDR)
// _WaterShimmerB : 십자 팔 진하기(0~1), (안 씀), 색 판정 허용 오차(0~1), 반짝임 주기(초)
// _WaterShimmerSurfaceColor : 물 타일 그림의 수면색
// _WaterShimmerFoamColor : 반짝임 색
// _WaterShimmerRimA : 테두리 물결 세기(0이면 꺼짐), 빛띠 간격(월드), 주기(초), 빛띠 폭 비율(0~1)
// _WaterShimmerRimB : 빛띠가 흘러가는 방향(정규화된 xy), (안 씀), (안 씀)
// _WaterShimmerRimColor : 물 타일 그림의 테두리색
// _WaterShimmerRimGlowColor : 빛띠가 지날 때 테두리가 물드는 색
// 숲의 숨결(ForestBreath.hlsl) 물결이 지나가는 동안에는 물결을 따라 수면에 반짝임이 흩뿌려지고 테두리가 함께 물든다.
#include "StageAmbienceCommon.hlsl"
#include "ForestBreath.hlsl"

float4 _WaterShimmerA;
float4 _WaterShimmerB;
float4 _WaterShimmerSurfaceColor;
float4 _WaterShimmerFoamColor;
float4 _WaterShimmerRimA;
float4 _WaterShimmerRimB;
float4 _WaterShimmerRimColor;
float4 _WaterShimmerRimGlowColor;

bool IsWaterShimmerEnabled()
{
    return _WaterShimmerA.x > 0.5;
}

float WaterShimmerHash21(float2 _p)
{
    _p = frac(_p * float2(123.34, 456.21));
    _p += dot(_p, _p + 45.32);
    return frac(_p.x * _p.y);
}

bool WaterShimmerMatches(float3 _color, float3 _key)
{
    float3 d = abs(_color - _key);
    return max(d.x, max(d.y, d.z)) < _WaterShimmerB.z;
}

// 수면 반짝임 한 점을 그린다. _fade는 0~1(가장 밝을 때 가운데가 블룸 문턱을 넘어 은은히 번지고 십자 팔이 옅게 비친다).
float4 WaterShimmerSpot(float2 _texelId, float2 _spot, float _fade)
{
    float2 d = abs(_texelId - _spot);
    float manhattan = d.x + d.y;
    if (manhattan < 0.5)
    {
        float brightness = lerp(1.0, _WaterShimmerA.w, _fade);
        return float4(_WaterShimmerFoamColor.rgb * brightness, _fade);
    }
    // 십자 팔은 가장 밝을 때만 옅게 비친다.
    if (manhattan < 1.5)
    {
        return float4(_WaterShimmerFoamColor.rgb, _fade * _fade * _WaterShimmerB.x);
    }

    return 0.0;
}

// 숲의 숨결 물결이 지나갈 때 수면에 흩뿌려지는 반짝임(평소 반짝임과 다른 자리, ForestBreathGlint).
float4 ComputeForestBreathWaterSparkle(float2 _texelId, float _cellSize, float _ppu)
{
    float2 glint = ForestBreathGlint(_texelId, _cellSize, _ppu, _ForestBreathC.y, _ForestBreathD.x, 0.0);
    if (glint.x > 0.0)
    {
        return float4(_WaterShimmerFoamColor.rgb * lerp(1.0, _WaterShimmerA.w, glint.x), glint.x);
    }
    if (glint.y > 0.0)
    {
        return float4(_WaterShimmerFoamColor.rgb, glint.y * glint.y * _WaterShimmerB.x);
    }

    return 0.0;
}

// rgb = 덮을 색, a = 덮는 비율(0이면 그대로).
// _centerWS: 텍셀 중심 월드 위치(StageAmbienceTexelCenterWS), _texelId: floor(_centerWS * ppu). 물 셰이더가 한 번만 구해 넘긴다.
// _texColor는 정점색을 곱하기 전 텍스처 색이다.
float4 ComputeWaterShimmerAt(float2 _centerWS, float2 _texelId, float3 _texColor)
{
    if (false == IsWaterShimmerEnabled())
    {
        return 0.0;
    }

    float ppu = StageAmbiencePixelsPerUnit();
    float2 centerWS = _centerWS;
    float time = _Time.y;

    // 수면색 픽셀에만 그린다(흙·장식은 그대로, 물가 테두리는 아래 달빛 물결만).
    bool isSurface = WaterShimmerMatches(_texColor, _WaterShimmerSurfaceColor.rgb);
    if (false == isSurface)
    {
        // 물가 테두리 달빛 물결: 옅은 빛띠가 월드를 가로질러 천천히 지나가서, 테두리 선 위에서는 빛이 선을 따라 미끄러지는 것처럼 보인다.
        if (_WaterShimmerRimA.x > 0.0 && WaterShimmerMatches(_texColor, _WaterShimmerRimColor.rgb))
        {
            // 빛띠가 곧은 줄로 보이지 않게 수직 방향 위치로 조금 휜다. 텍셀 중심으로 계산해 텍셀 하나가 한 색이다.
            float2 direction = _WaterShimmerRimB.xy;
            float along = dot(centerWS, direction);
            float across = dot(centerWS, float2(-direction.y, direction.x));
            float bend = sin(across * 0.45) * 1.2 + sin(across * 1.3 + 1.7) * 0.4;
            float band = frac((along + bend) / _WaterShimmerRimA.y - time / _WaterShimmerRimA.z);
            float width = _WaterShimmerRimA.w;
            float rimAlpha = 0.0;
            if (band < width)
            {
                float glow = sin(band / width * 3.14159);
                rimAlpha = glow * glow * _WaterShimmerRimA.x;
            }
            // 숲의 숨결 물결이 지나가는 동안 테두리 전체가 물결을 따라 물든다(평소 빛띠와 겹치면 더 밝은 쪽).
            if (IsForestBreathActive() && _ForestBreathC.z > 0.0)
            {
                rimAlpha = max(rimAlpha, SampleForestBreath(centerWS) * _ForestBreathC.z);
            }
            if (rimAlpha > 0.0)
            {
                return float4(_WaterShimmerRimGlowColor.rgb, rimAlpha);
            }
        }
        return 0.0;
    }

    // 수면 반짝임: 칸마다 한 점이 제 주기대로 잠깐 반짝이고, 가장 밝을 때 십자로 퍼진다.
    // 켜져 있는 비율이 0이면 평소 수면 반짝임은 끄고 테두리 물결(과 숲의 숨결)만 쓴다.
    float cellSize = max(floor(_WaterShimmerA.y), 3.0);
    // 타일 텍셀 중심은 (정수 + 0.5) / ppu에 놓이므로 내림하면 텍셀 하나가 한 번호를 갖는다(반올림하면 경계에서 뒤집힌다).
    float2 texelId = _texelId;
    if (_WaterShimmerA.z > 0.0)
    {
        float2 cell = floor(texelId / cellSize);
        float period = max(_WaterShimmerB.w, 0.1) * (0.7 + 0.6 * WaterShimmerHash21(cell + 3.1));
        float local = frac(time / period + WaterShimmerHash21(cell + 9.4));
        float duty = max(_WaterShimmerA.z, 0.0001);
        // 꺼져 있는 칸(대부분)은 점 자리를 구하지 않고 넘어간다.
        if (local < duty)
        {
            float2 spot = cell * cellSize + 1.0 + floor(float2(WaterShimmerHash21(cell), WaterShimmerHash21(cell + 17.7)) * (cellSize - 2.0));
            // 켜지는 동안 부드럽게 밝아졌다 사그라든다.
            float4 sparkle = WaterShimmerSpot(texelId, spot, sin(local / duty * 3.14159));
            if (sparkle.a > 0.0)
            {
                return sparkle;
            }
        }
    }

    // 숲의 숨결 물결이 지나가는 동안 수면에 반짝임이 흩뿌려지고, 켜 두면 수면 전체가 아주 옅게 밝아진다.
    if (IsForestBreathActive())
    {
        float4 breathSparkle = ComputeForestBreathWaterSparkle(texelId, cellSize, ppu);
        if (breathSparkle.a > 0.0)
        {
            return breathSparkle;
        }
        if (_ForestBreathC.w > 0.0)
        {
            return float4(_WaterShimmerRimGlowColor.rgb, SampleForestBreath(centerWS) * _ForestBreathC.w);
        }
    }

    return 0.0;
}

float4 ComputeWaterShimmer(float2 _uv, float2 _positionWS, float4 _texelSize, float3 _texColor)
{
    float2 centerWS = StageAmbienceTexelCenterWS(_positionWS, _uv, _texelSize);
    return ComputeWaterShimmerAt(centerWS, floor(centerWS * StageAmbiencePixelsPerUnit()), _texColor);
}

#endif // WATER_SHIMMER_INCLUDED
