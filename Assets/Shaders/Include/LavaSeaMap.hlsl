#ifndef LAVA_SEA_MAP_INCLUDED
#define LAVA_SEA_MAP_INCLUDED

// 섬을 둘러싼 용암 바다 지도. StageLavaSeaMap이 맵을 만들 때 칸마다 값을 담아 전역으로 넘긴다.
// 바다가 아니면 0, 바다면 1 + 해안까지 칸 수(최대 255)를 0~1로 담은 텍스처다.
// 물 타일 셰이더의 거품(WaterBubbles.hlsl)과 온도 무늬(WaterMagma.hlsl)가 include 한다.
// 지도를 쓰는 연출이 없는 스테이지는 _LavaSeaA.x가 0이라 IsLavaSeaMapEnabled가 거짓이다.
// _LavaSeaA : x = 켜짐(0/1)
// _LavaSeaMapA : 월드 → 칸 좌표 2x2 역행렬(m00, m01, m10, m11)
// _LavaSeaMapB : xy = 칸 (0,0) 가운데의 월드 위치, zw = 텍스처 왼쪽 아래 칸 번호
// _LavaSeaMapC : xy = 텍스처 크기(칸 수)

TEXTURE2D(_LavaSeaTex);
SAMPLER(sampler_LavaSeaTex);
float4 _LavaSeaA;
float4 _LavaSeaMapA;
float4 _LavaSeaMapB;
float4 _LavaSeaMapC;

bool IsLavaSeaMapEnabled()
{
    return _LavaSeaA.x > 0.5;
}

// 월드 위치의 칸 좌표(텍스처 왼쪽 아래 칸이 0)
float2 LavaSeaCell(float2 _positionWS)
{
    float2 delta = _positionWS - _LavaSeaMapB.xy;
    return float2(dot(_LavaSeaMapA.xy, delta), dot(_LavaSeaMapA.zw, delta)) - _LavaSeaMapB.zw;
}

// 월드 위치가 섬을 둘러싼 바다 칸인지(가장 가까운 칸 가운데 기준).
bool IsLavaSeaCell(float2 _positionWS)
{
    int2 texel = int2(floor(LavaSeaCell(_positionWS) + 0.5));
    int2 size = int2(_LavaSeaMapC.xy);
    if (any(texel < 0) || any(texel >= size))
    {
        return false;
    }
    return LOAD_TEXTURE2D(_LavaSeaTex, texel).r > 0.5 / 255.0;
}

// 해안에서 떨어진 칸 수. 칸 사이는 부드럽게 이어지고, 땅에 가까울수록 0, 땅 쪽으로 넘어가면 음수(최소 -1)다.
float LavaSeaShoreDistance(float2 _positionWS)
{
    float2 uv = (LavaSeaCell(_positionWS) + 0.5) / max(_LavaSeaMapC.xy, 1.0);
    return SAMPLE_TEXTURE2D_LOD(_LavaSeaTex, sampler_LavaSeaTex, uv, 0).r * 255.0 - 1.0;
}

#endif // LAVA_SEA_MAP_INCLUDED
