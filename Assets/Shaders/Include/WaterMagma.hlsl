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
// 섬을 둘러싼 바다만 따로 칠하는 값(용암 바다 지도 LavaSeaMap.hlsl가 켜졌을 때만 쓰인다. 섬 안 웅덩이는 위 값 그대로):
// _WaterMagmaSeaA : 켜짐(0/1), 먼 바다 값이 다 걸리는 해안 거리(칸), 바다 무늬 크기(월드), 먼 바다 어둡게 덮는 비율(0~1)
// _WaterMagmaSeaB : x = 먼 바다 식은 곳 문턱 올림, y = 먼 바다 뜨거운 곳 문턱 올림
// _WaterMagmaSeaFarColor : 먼 바다를 덮는 식은 색
// _WaterMagmaCrustA : 굳은 껍질 켜짐(0/1), 판 크기(월드), 해안 쪽 깔리는 비율, 먼 바다 깔리는 비율
// _WaterMagmaCrustB : xy = 흘러가는 속도(월드/초), z = 판 사이 틈 넓이, w = 빛나는 가장자리 넓이(판 크기 대비)
// _WaterMagmaCrustColorA/B : 껍질 바탕색·얼룩색, _WaterMagmaCrustRimColor : 빛나는 가장자리 색
#include "StageAmbienceCommon.hlsl"
#include "LavaSeaMap.hlsl"
#include "StageAmbienceFade.hlsl"

float4 _WaterMagmaA;
float4 _WaterMagmaB;
float4 _WaterMagmaCoolColor;
float4 _WaterMagmaHotColor;
float4 _WaterMagmaSurface;
float4 _WaterMagmaSeaA;
float4 _WaterMagmaSeaB;
float4 _WaterMagmaSeaFarColor;
float4 _WaterMagmaCrustA;
float4 _WaterMagmaCrustB;
float4 _WaterMagmaCrustColorA;
float4 _WaterMagmaCrustColorB;
float4 _WaterMagmaCrustRimColor;

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

float2 WaterMagmaHash22(float2 _p)
{
    float3 p3 = frac(float3(_p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

// WaterMagmaNoise를 정수 z 한 층에서만 읽는 것과 똑같은 값(z 쪽 보간이 0이라 해시를 반만 쓴다). 시간에 따라 안 바뀌는 무늬용.
float WaterMagmaNoiseLayer(float2 _p, float _z)
{
    float2 i = floor(_p);
    float2 f = frac(_p);
    float2 u = f * f * (3.0 - 2.0 * f);

    float n00 = WaterMagmaHash31(float3(i, _z));
    float n10 = WaterMagmaHash31(float3(i + float2(1.0, 0.0), _z));
    float n01 = WaterMagmaHash31(float3(i + float2(0.0, 1.0), _z));
    float n11 = WaterMagmaHash31(float3(i + float2(1.0, 1.0), _z));
    return lerp(lerp(n00, n10, u.x), lerp(n01, n11, u.x), u.y);
}

// 두 값을 한 번에 내는 2D 값 노이즈(모서리마다 해시 하나로 두 값). 판 가장자리 비틀기용.
float2 WaterMagmaNoise2(float2 _p)
{
    float2 i = floor(_p);
    float2 f = frac(_p);
    float2 u = f * f * (3.0 - 2.0 * f);

    float2 n00 = WaterMagmaHash22(i);
    float2 n10 = WaterMagmaHash22(i + float2(1.0, 0.0));
    float2 n01 = WaterMagmaHash22(i + float2(0.0, 1.0));
    float2 n11 = WaterMagmaHash22(i + float2(1.0, 1.0));
    return lerp(lerp(n00, n10, u.x), lerp(n01, n11, u.x), u.y);
}

// 바다에 떠다니는 굳은 껍질. rgb = 칠할 색, a = 1이면 칠함(0이면 껍질 없음).
// 판은 보로노이 칸 하나다. 칸마다 무작위 값이 깔리는 비율보다 작으면 판이 있고, 판 사이 틈은 그대로 용암이 보인다.
// 판 가운데 자리의 해안 거리로 깔리는 비율을 정하므로 판이 통째로 있거나 없고, 해안 쪽으로 흘러오면 가장자리부터 줄어들며 녹아 사라진다.
float4 ComputeWaterMagmaCrust(float2 _centerWS)
{
    float size = _WaterMagmaCrustA.y;
    // 무늬와 같이 세로를 두 배로 늘려 판이 바닥에 누워 보이게 한다.
    float2 drift = _WaterMagmaCrustB.xy * _Time.y;
    float2 p = float2(_centerWS.x - drift.x, (_centerWS.y - drift.y) * 2.0) / size;
    // 판 가장자리가 반듯한 다각형으로 보이지 않게 자리를 노이즈로 살짝 비튼다(판과 같이 흘러간다).
    float2 warp = WaterMagmaNoise2(p * 1.3 + 9.2) - 0.5;
    p += warp * 0.45;
    float2 cell = floor(p);
    float2 local = p - cell;

    float f1 = 8.0;
    float f2 = 8.0;
    float2 nearestId = cell;
    float2 nearestPoint = 0.0;
    float2 nearestOffset = 0.0;
    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float2 g = float2(x, y);
            float2 o = WaterMagmaHash22(cell + g) * 0.8 + 0.1;
            float2 r = g + o - local;
            float dd = dot(r, r);
            if (dd < f1)
            {
                f2 = f1;
                f1 = dd;
                nearestId = cell + g;
                nearestPoint = cell + g + o;
                nearestOffset = r;
            }
            else if (dd < f2)
            {
                f2 = dd;
            }
        }
    }
    // 판 경계까지의 거리(대략). 경계에서 0, 판 가운데로 갈수록 커진다.
    float edge = sqrt(f2) - sqrt(f1);

    // 판 가운데 자리의 해안 거리로 이 판이 깔리는지 정한다.
    float2 pointWS = float2(nearestPoint.x * size + drift.x, nearestPoint.y * size * 0.5 + drift.y);
    float farFactor = saturate(LavaSeaShoreDistance(pointWS) / _WaterMagmaSeaA.y);
    float coverage = lerp(_WaterMagmaCrustA.z, _WaterMagmaCrustA.w, farFactor);
    // 판마다 무작위 값 두 개: x = 깔리는지 정하는 값, y = 줄어드는 정도
    float2 plateRandom = WaterMagmaHash22(nearestId + 41.7);
    float margin = coverage - plateRandom.x;
    if (margin <= 0.0)
    {
        return 0.0;
    }
    // 깔리는 비율 문턱 가까이 있는 판(해안 쪽으로 흘러온 판)은 틈을 넓혀 작게 줄인다.
    // 판마다 줄어드는 정도를 달리해 크기가 들쭉날쭉하고 사이가 벌어지게 한다.
    float melt = 1.0 - saturate(margin / 0.12);
    float gap = _WaterMagmaCrustB.z + plateRandom.y * 0.3 + melt * 0.7;
    if (edge < gap)
    {
        return 0.0;
    }
    // 가장자리: 판 가운데보다 위쪽은 스며 나온 용암 빛, 아래쪽은 용암에 잠긴 판 옆면(어두운 두께)으로 칠한다.
    bool lowerSide = nearestOffset.y > 0.0;
    if (lowerSide && edge < gap + _WaterMagmaCrustB.w * 1.5)
    {
        return float4(_WaterMagmaCrustColorA.rgb * 0.45, 1.0);
    }
    if (false == lowerSide && edge < gap + _WaterMagmaCrustB.w)
    {
        return float4(_WaterMagmaCrustRimColor.rgb, 1.0);
    }

    // 판 위 얼룩(판과 같이 흘러간다)
    float spots = WaterMagmaNoiseLayer(p * 3.1 + nearestId * 7.3, 0.0);
    return float4(spots > 0.58 ? _WaterMagmaCrustColorB.rgb : _WaterMagmaCrustColorA.rgb, 1.0);
}

// rgb = 덮을 색, a = 덮는 비율(0이면 그대로). _texColor는 정점색을 곱하기 전 텍스처 색이다.
// _onCrust는 굳은 껍질(빛나는 가장자리 포함)을 칠했으면 참이다. 껍질 위에는 거품을 올리지 않는다.
float4 ComputeWaterMagmaCore(float2 _uv, float2 _positionWS, float4 _texelSize, float3 _texColor, out bool _onCrust)
{
    _onCrust = false;
    float3 d = abs(_texColor - _WaterMagmaSurface.rgb);
    if (max(d.x, max(d.y, d.z)) >= _WaterMagmaSurface.a)
    {
        return 0.0;
    }

    float2 centerWS = StageAmbienceTexelCenterWS(_positionWS, _uv, _texelSize);

    // 섬을 둘러싼 바다면 무늬 크기·문턱·덮는 색을 해안 거리에 따라 바꾸고, 굳은 껍질을 띄운다.
    bool sea = _WaterMagmaSeaA.x > 0.5 && IsLavaSeaMapEnabled() && IsLavaSeaCell(centerWS);
    float scale = _WaterMagmaA.y;
    float coolThreshold = _WaterMagmaB.z;
    float hotThreshold = _WaterMagmaB.w;
    float darken = 0.0;
    if (sea)
    {
        if (_WaterMagmaCrustA.x > 0.5)
        {
            float4 crust = ComputeWaterMagmaCrust(centerWS);
            if (crust.a > 0.5)
            {
                _onCrust = true;
                return crust;
            }
        }

        float farFactor = saturate(LavaSeaShoreDistance(centerWS) / _WaterMagmaSeaA.y);
        scale = _WaterMagmaSeaA.z;
        coolThreshold += farFactor * _WaterMagmaSeaB.x;
        hotThreshold = max(coolThreshold, hotThreshold + farFactor * _WaterMagmaSeaB.y);
        darken = farFactor * _WaterMagmaSeaA.w;
    }

    // 아이소메트릭 바닥은 세로가 반으로 눌려 보이므로 세로를 두 배로 늘려 무늬가 바닥에 누운 둥근 얼룩으로 보이게 한다.
    float2 p = float2(centerWS.x, centerWS.y * 2.0) / scale - _WaterMagmaB.xy * _Time.y;
    float t = _Time.y * _WaterMagmaA.z;
    float n = WaterMagmaNoise(float3(p, t)) * 0.65 + WaterMagmaNoise(float3(p * 2.3 + 17.1, t * 1.4 + 5.3)) * 0.35;

    if (n < coolThreshold)
    {
        return float4(lerp(_WaterMagmaCoolColor.rgb, _WaterMagmaSeaFarColor.rgb, darken), 1.0);
    }
    if (n > hotThreshold)
    {
        return float4(lerp(_WaterMagmaHotColor.rgb, _WaterMagmaSeaFarColor.rgb, darken), 1.0);
    }
    // 그 사이는 원래 수면색 그대로(먼 바다는 식은 색으로 그만큼 덮는다).
    return float4(_WaterMagmaSeaFarColor.rgb, darken);
}

// 로그라이크 지형 전환 때 서서히 끄고 켠다(StageAmbienceFade.hlsl, 본편은 늘 그대로).
float4 ComputeWaterMagma(float2 _uv, float2 _positionWS, float4 _texelSize, float3 _texColor, out bool _onCrust)
{
    float4 magma = ComputeWaterMagmaCore(_uv, _positionWS, _texelSize, _texColor, _onCrust);
    magma.a *= StageAmbienceVisibility(_positionWS);
    return magma;
}

#endif // WATER_MAGMA_INCLUDED
