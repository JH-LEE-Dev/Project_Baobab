#ifndef PIXEL_OUTLINE_INCLUDED
#define PIXEL_OUTLINE_INCLUDED

// 픽셀 스냅 아웃라인 공용 헬퍼.
//
// 아웃라인은 스프라이트 텍스처를 8방향으로 샘플링해 그린다. 그래서 아트가 스프라이트 경계에
// 닿아 있으면(BlackSmith 하단처럼) 테두리를 그릴 자리가 쿼드 바깥이라 그 부분이 잘려 나간다.
// 이를 막기 위해 버텍스에서 쿼드를 바깥으로 몇 픽셀 넓혀 테두리 한 줄이 들어갈 여백을 만든다.
//
// 주의할 점 두 가지:
//  1. 그냥 넓히면 UV 보간도 같이 늘어나 픽셀아트가 미세하게 확대되고 월드 픽셀 스냅이 깨진다.
//     그래서 "넓히기 전" 월드 좌표를 함께 넘겨 프래그먼트에서 원본 UV 매핑을 복원한다.
//  2. 넓혀서 생긴 여백은 텍스처 밖이라, Clamp 래핑 탓에 가장자리 텍셀이 그대로 늘어나 보인다.
//     그래서 모든 샘플링을 텍스처 범위로 마스킹해 여백을 "비어 있음"으로 취급한다.

// Pixel Perfect Camera 의 Assets PPU 와 반드시 같아야 한다. (640x360 / PPU 32)
#define PIXEL_OUTLINE_PPU 32.0

// 스프라이트 쿼드를 중심에서 바깥으로 _expandPixels 픽셀(월드 기준)만큼 넓힌다.
//
// unity_RendererBounds 는 렌더러에 따라 피벗 기준 대칭 박스로 들어오는 경우가 있어
// 중심/크기 값 자체는 믿을 수 없다. 다만 어떤 경우든 쿼드 안의 한 점이긴 하므로,
// "각 버텍스가 어느 방향 코너인가"(부호)를 정하는 용도로만 쓴다.
float3 ExpandOutlineQuad(float3 _positionOS, float _expandPixels)
{
    if (_expandPixels <= 0.0)
        return _positionOS;

    float3 minOS = TransformWorldToObject(unity_RendererBounds_Min.xyz);
    float3 maxOS = TransformWorldToObject(unity_RendererBounds_Max.xyz);
    float2 insideOS = (minOS.xy + maxOS.xy) * 0.5;

    // 확장량은 월드 기준 픽셀이므로, 오브젝트 축의 월드 스케일로 나눠 오브젝트 공간 길이로 바꾼다.
    float2 axisScale = float2(
        length(TransformObjectToWorldDir(float3(1, 0, 0), false)),
        length(TransformObjectToWorldDir(float3(0, 1, 0), false)));
    float2 expandOS = (_expandPixels / PIXEL_OUTLINE_PPU) / max(axisScale, 1e-5);

    float3 result = _positionOS;
    result.xy += sign(_positionOS.xy - insideOS) * expandOS;
    return result;
}

// 텍스처 범위 밖을 샘플링하면 0을 돌려준다.
// 넓혀서 생긴 여백이 Clamp 래핑으로 가장자리 텍셀을 복제하는 것을 막아,
// 그 자리가 "비어 있음"으로 판정되어 아웃라인이 그려지게 한다.
// 분기 안에서 샘플링하면 미분이 망가지므로 샘플은 무조건 하고 결과만 마스킹한다.
half SampleOutlineAlpha(TEXTURE2D_PARAM(_map, _mapSampler), float2 _uv)
{
    half alpha = SAMPLE_TEXTURE2D(_map, _mapSampler, _uv).a;
    return (all(_uv >= 0.0) && all(_uv <= 1.0)) ? alpha : 0.0h;
}

// 여백이면 0, 원본 스프라이트 안이면 1.
half InsideSpriteUV(float2 _uv)
{
    return (all(_uv >= 0.0) && all(_uv <= 1.0)) ? 1.0h : 0.0h;
}

#endif // PIXEL_OUTLINE_INCLUDED
