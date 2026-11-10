#ifndef WATER_MAGMA_INCLUDED
#define WATER_MAGMA_INCLUDED

// 용암 속 온도 무늬. 물 타일 그림의 수면색 픽셀만, 천천히 흘러가며 모양이 바뀌는 큰 얼룩 무늬로 칠한다.
// 무늬 값이 낮은 곳은 식은 껍질 색(어두운 빨강), 높은 곳은 뜨거운 속 색(밝은 주황), 그 사이는 원래 수면색 그대로다.
// 텍셀 가운데에서 한 번만 정하므로 텍셀 안이 갈라지지 않고 픽셀아트처럼 딱 끊어진다.
// 물 타일 셰이더(WaterTilemapShader)가 _MainTex 선언 뒤에 include 한다(거품보다 먼저 칠해 거품이 위에 남는다).
// 전역값은 StageAmbienceShaderGlobals가 스테이지 데이터대로 채운다. 꺼진 스테이지는 _WaterMagmaA.x가 0이라
// 아무것도 바꾸지 않는다.
// _WaterMagmaA : 켜짐(0/1), 무늬 크기(월드), 모양이 바뀌는 속도, (안 씀)
// _WaterMagmaB : xy = 흘러가는 속도(월드/초), z = 식은 색 문턱, w = 뜨거운 색 문턱
// _WaterMagmaCoolColor : 식은 껍질 색
// _WaterMagmaHotColor : 뜨거운 속 색
// _WaterMagmaSurface : rgb = 물 타일 그림의 수면색, a = 색 판정 허용 오차(0~1)
#include "StageAmbienceCommon.hlsl"

float4 _WaterMagmaA;
float4 _WaterMagmaB;
float4 _WaterMagmaCoolColor;
float4 _WaterMagmaHotColor;
float4 _WaterMagmaSurface;

bool IsWaterMagmaEnabled()
{
    return _WaterMagmaA.x > 0.5;
}

float WaterMagmaHash31(float3 _p)
{
    float3 p3 = frac(_p * 0.1031);
    p3 += dot(p3, p3.zyx + 31.32);
    return frac((p3.x + p3.y) * p3.z);
}

// 시간 축까지 보간하는 값 노이즈. 무늬가 한쪽으로 흘러가기만 하지 않고 제자리에서도 천천히 모양이 바뀐다.
float WaterMagmaNoise(float3 _p)
{
    float3 i = floor(_p);
    float3 f = frac(_p);
    float3 u = f * f * (3.0 - 2.0 * f);

    float n000 = WaterMagmaHash31(i);
    float n100 = WaterMagmaHash31(i + float3(1.0, 0.0, 0.0));
    float n010 = WaterMagmaHash31(i + float3(0.0, 1.0, 0.0));
    float n110 = WaterMagmaHash31(i + float3(1.0, 1.0, 0.0));
    float n001 = WaterMagmaHash31(i + float3(0.0, 0.0, 1.0));
    float n101 = WaterMagmaHash31(i + float3(1.0, 0.0, 1.0));
    float n011 = WaterMagmaHash31(i + float3(0.0, 1.0, 1.0));
    float n111 = WaterMagmaHash31(i + float3(1.0, 1.0, 1.0));

    float a = lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y);
    float b = lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y);
    return lerp(a, b, u.z);
}

// rgb = 덮을 색, a = 덮는 비율(0이면 그대로). _texColor는 정점색을 곱하기 전 텍스처 색이다.
float4 ComputeWaterMagma(float2 _uv, float2 _positionWS, float4 _texelSize, float3 _texColor)
{
    float3 d = abs(_texColor - _WaterMagmaSurface.rgb);
    if (max(d.x, max(d.y, d.z)) >= _WaterMagmaSurface.a)
    {
        return 0.0;
    }

    float2 centerWS = StageAmbienceTexelCenterWS(_positionWS, _uv, _texelSize);
    // 아이소메트릭 바닥은 세로가 반으로 눌려 보이므로 세로를 두 배로 늘려 무늬가 바닥에 누운 둥근 얼룩으로 보이게 한다.
    float2 p = float2(centerWS.x, centerWS.y * 2.0) / _WaterMagmaA.y - _WaterMagmaB.xy * _Time.y;
    float t = _Time.y * _WaterMagmaA.z;
    float n = WaterMagmaNoise(float3(p, t)) * 0.65 + WaterMagmaNoise(float3(p * 2.3 + 17.1, t * 1.4 + 5.3)) * 0.35;

    if (n < _WaterMagmaB.z)
    {
        return float4(_WaterMagmaCoolColor.rgb, 1.0);
    }
    if (n > _WaterMagmaB.w)
    {
        return float4(_WaterMagmaHotColor.rgb, 1.0);
    }
    return 0.0;
}

#endif // WATER_MAGMA_INCLUDED
