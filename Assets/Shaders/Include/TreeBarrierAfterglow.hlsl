#ifndef TREE_BARRIER_AFTERGLOW_INCLUDED
#define TREE_BARRIER_AFTERGLOW_INCLUDED

// 3스테이지 배리어가 깨진 직후의 잔광. 렌더링 전용이다.
// 껍질이 깨지는 순간 나무 그림(수관·밑동)의 테두리(바깥쪽 1~2픽셀)가 배리어와 같은 푸른빛으로 밝게 빛나고, 나무 전체가 살짝 푸르게
// 들떴다가 약 1초에 걸쳐 원래대로 돌아온다. "이제 때릴 수 있는 나무"가 바로 보이게 하는 것이 목적이다.
// 나무마다 세기(_BarrierAfterglow, 0~1)를 MaterialPropertyBlock으로 받는다(TreeVisualComponent.SetBarrierAfterglow,
// 넣는 쪽은 VFX_TreeBarrier). 기본값 0이면 비교 한 번으로 끝나고 색을 바꾸지 않으므로 다른 나무·스테이지는 그대로다.
// 스프라이트 크기는 TreeDamageLook.hlsl의 TreeDamageSpriteSize로 읽는다(SRP 배처 호환 - 그 파일 설명 참고).
// TreeDamageLook.hlsl 뒤에 include 한다.

// 배리어 껍질 셰이더(TreeBarrier.shader)의 테두리·채움 색과 같다.
static const half3 TreeBarrierAfterglowRimColor = half3(0.67h, 0.86h, 1.0h);
static const half3 TreeBarrierAfterglowFillColor = half3(0.36h, 0.57h, 0.9h);
// 테두리 빛의 HDR 배율(블룸이 살짝 번지는 정도)
static const half TreeBarrierAfterglowRimHDR = 1.8h;
// 바깥에서 두 번째 픽셀의 테두리 세기(테두리가 두께 1픽셀로만 보이지 않고 안쪽으로 살짝 번지게)
static const half TreeBarrierAfterglowInnerRim = 0.4h;
// 나무 전체가 푸르게 들뜨는 정도
static const half TreeBarrierAfterglowFill = 0.12h;
// 가장 밝을 때 테두리 픽셀이 테두리 빛으로 덮이는 정도
static const half TreeBarrierAfterglowRimMix = 0.8h;

// 그 자리가 비어 있는지(투명) - 1 = 비어 있음.
// 이웃은 그림을 그린 것과 같은 샘플러로 읽는다. 텍셀 번호(floor)로 따로 읽으면 바람 흔들림으로 그림이 살짝 기울 때
// 텍셀 경계에 걸린 줄에서 그림과 이웃 판정이 한 칸 어긋나 수관 안쪽에 줄무늬가 생긴다.
half TreeBarrierAfterglowEmpty(float2 _uv)
{
    return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, _uv, 0).a < 0.5h ? 1.0h : 0.0h;
}

// _color: HDR 배율까지 끝난 색, _uv: 픽셀에 맞춘 텍스처 좌표, _glow: 잔광 세기(0~1)
half3 ApplyTreeBarrierAfterglow(half3 _color, float2 _uv, float _glow)
{
    if (_glow <= 0.0) return _color;

    // 그림 가장자리의 반투명 잔털 픽셀은 밝히지 않는다(테두리 밖으로 튀어나온 점처럼 보인다).
    if (TreeBarrierAfterglowEmpty(_uv) > 0.5h) return _color;

    float2 d = 1.0 / TreeDamageSpriteSize();

    // 위아래좌우 이웃이 비어 있으면 바깥 테두리, 두 칸 떨어진 이웃이 비어 있으면 그 안쪽 줄
    half edge1 = max(
        max(TreeBarrierAfterglowEmpty(_uv + float2(d.x, 0.0)), TreeBarrierAfterglowEmpty(_uv - float2(d.x, 0.0))),
        max(TreeBarrierAfterglowEmpty(_uv + float2(0.0, d.y)), TreeBarrierAfterglowEmpty(_uv - float2(0.0, d.y))));
    half rim = edge1;
    if (edge1 < 0.5h)
    {
        half edge2 = max(
            max(TreeBarrierAfterglowEmpty(_uv + float2(2.0 * d.x, 0.0)), TreeBarrierAfterglowEmpty(_uv - float2(2.0 * d.x, 0.0))),
            max(TreeBarrierAfterglowEmpty(_uv + float2(0.0, 2.0 * d.y)), TreeBarrierAfterglowEmpty(_uv - float2(0.0, 2.0 * d.y))));
        rim = edge2 * TreeBarrierAfterglowInnerRim;
    }

    half g = (half)saturate(_glow);
    half3 col = _color + TreeBarrierAfterglowFillColor * (TreeBarrierAfterglowFill * g);
    return lerp(col, TreeBarrierAfterglowRimColor * TreeBarrierAfterglowRimHDR, rim * g * TreeBarrierAfterglowRimMix);
}

#endif
