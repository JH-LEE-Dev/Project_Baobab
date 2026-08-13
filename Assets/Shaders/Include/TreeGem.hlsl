#ifndef TREE_GEM_INCLUDED
#define TREE_GEM_INCLUDED

// 픽셀아트 나무를 보석/결정 재질처럼 보이게 하는 절차적 셰이딩.
// 노멀맵이나 별도의 하이라이트 스프라이트 없이, 월드 픽셀 좌표만으로 보로노이 면(facet)을
// 만들고 각 면에 고정된 가짜 법선을 부여한 뒤, 회전하는 가상 광원과 내적해 면 단위로
// 딱딱 끊어지는 반사를 만든다.
//
// 핵심: 면 배치는 시간에 따라 변하지 않고 고정되어 있고, 광원 방향만 회전한다.
// 면까지 같이 움직이면 그냥 노이즈로 보이고, 면이 고정되어야 "보석 면이 하나씩 빛을 받는" 느낌이 난다.
//
// 좌표계로 월드 픽셀 좌표를 쓰는 이유:
//  - 나무 셰이더가 이미 같은 격자(worldPos * ppu)로 스프라이트를 스냅하고 있어 면 경계가
//    아트의 픽셀 경계와 정확히 맞아떨어진다.
//  - 나무마다 월드 위치가 다르므로 별도 시드 없이도 개체별로 다른 면 배치를 갖는다.
//  - top/bottom 렌더러가 같은 필드를 공유해 나무 상하단 경계에서 면이 자연스럽게 이어진다.

// 가상 광원의 월드 위치. 유니티 라이트가 아니라 이 셰이더가 면 밝기를 계산할 때만 쓰는 값으로,
// C#에서 Shader.SetGlobalVector로 캐릭터 위치를 넣어준다 (GemLightSource.cs).
//   xy = 월드 위치, w = 유효 플래그 (1이면 이 위치를 광원으로 사용)
// 주의: SRP Batcher 호환을 위해 반드시 UnityPerMaterial CBUFFER 바깥에 있어야 하며,
// 셰이더의 Properties 블록에도 넣으면 안 된다(넣는 순간 머티리얼 프로퍼티로 취급된다).
float4 _GemLightWorldPos;

float2 GemHash2(float2 _p)
{
    float2 h = float2(dot(_p, float2(127.1, 311.7)), dot(_p, float2(269.5, 183.3)));
    return frac(sin(h) * 43758.5453);
}

// 보로노이 셀을 구한다.
// _cellId   : 이 픽셀이 속한 면의 고유 ID (면별 랜덤값 생성에 사용)
// _edgeDist : 면 경계까지의 거리 (컷 라인 그리기에 사용)
// _localPos : 면 중심 기준 상대 위치 (스파클 위치에 사용)
void GemVoronoi(float2 _p, out float2 _cellId, out float _edgeDist, out float2 _localPos)
{
    float2 baseCell = floor(_p);
    float2 f = _p - baseCell;

    float2 bestOffset = float2(0.0, 0.0);
    float2 bestSite = float2(0.0, 0.0);
    float bestDistSq = 8.0;

    // 1차 순회: 가장 가까운 사이트(면의 중심점)를 찾는다.
    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float2 offset = float2(x, y);
            float2 site = offset + GemHash2(baseCell + offset);
            float2 diff = site - f;
            float distSq = dot(diff, diff);

            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestOffset = offset;
                bestSite = site;
            }
        }
    }

    _cellId = baseCell + bestOffset;
    _localPos = f - bestSite;

    // 2차 순회: 이웃 사이트와의 수직이등분선까지의 거리가 곧 면 경계까지의 거리다.
    float edge = 8.0;
    [unroll]
    for (int y2 = -1; y2 <= 1; y2++)
    {
        [unroll]
        for (int x2 = -1; x2 <= 1; x2++)
        {
            float2 offset = bestOffset + float2(x2, y2);
            float2 site = offset + GemHash2(baseCell + offset);
            float2 diff = site - bestSite;
            float lenSq = dot(diff, diff);

            // 자기 자신(bestSite)은 건너뛴다.
            if (lenSq > 1e-5)
            {
                float2 mid = 0.5 * (bestSite + site);
                edge = min(edge, dot(mid - f, diff * rsqrt(lenSq)));
            }
        }
    }

    _edgeDist = edge;
}

// 파라미터가 많아 호출부 가독성을 위해 구조체로 묶는다.
struct TreeGemParams
{
    float amount;            // 보석 효과 전체 강도 (0 = 원본, 1 = 완전 보석)
    float3 gemColor;         // 보석의 고정 색. 면마다 달라지는 건 색이 아니라 밝기다.
    float facetSize;         // 면 하나의 크기 (픽셀 단위)
    float shadeSteps;        // 면 명암 계단 수
    float sweepSpeed;        // 캐릭터 광원이 없을 때 쓰는 시간 기반 회전 속도
    float lightFollow;       // 광원을 캐릭터 위치로 삼는 정도 (0 = 시간 회전, 1 = 캐릭터 추종)
    float lightHeight;       // 캐릭터 광원의 높이. 낮을수록 면 밝기 차이가 커진다.
    float facetRandomness;   // 면 법선의 무작위 정도 (0 = 형상만, 1 = 완전 무작위)
    float formBulge;         // 나무를 얼마나 둥근 덩어리로 볼지
    float formCenterY;       // 피봇 기준 나무 중심의 높이
    float deepShade;         // 빛을 등진 면이 어디까지 어두워지는지
    float whiteness;         // 빛을 정면으로 받은 면이 흰색에 얼마나 가까워지는지
    float facetVariation;    // 면마다의 미세한 밝기 편차
    float lumaInfluence;     // 원본 스프라이트 명암을 반영하는 정도
    float lumaBias;          // 전체 밝기 바닥값
    float specStrength;      // 가장 밝은 면에 얹는 가산 하이라이트 세기
    float edgeWidth;         // 면 경계선 두께
    float edgeBrightness;    // 면 경계선 밝기
    float sparkleRatio;      // 스파클이 생기는 면의 비율 (0~1)
    float sparkleSpeed;      // 스파클 점멸 속도
    float sparkleSize;       // 스파클 크기
    float sparkleBrightness; // 스파클 밝기
};

// _worldPos : 프래그먼트의 월드 좌표 (나무 셰이더가 이미 varying으로 들고 있는 값)
// _ppu      : Pixels Per Unit. 나무 셰이더의 픽셀 스냅과 반드시 같은 값을 넘겨야
//             면 경계가 아트의 픽셀 경계와 어긋나지 않는다.
float3 ApplyTreeGem(float3 _baseColor, float2 _worldPos, float2 _pivotPos, float _ppu, TreeGemParams _params)
{
    if (_params.amount <= 0.001)
    {
        return _baseColor;
    }

    // 픽셀 격자에 스냅한다. 이 단계가 없으면 면 경계가 부드럽게 뭉개져 픽셀아트 룩이 깨진다.
    float2 gemPixel = floor(_worldPos * _ppu);

    float facetSize = max(_params.facetSize, 1.0);
    float2 voronoiCoord = gemPixel / facetSize;

    float2 cellId;
    float edgeDist;
    float2 localPos;
    GemVoronoi(voronoiCoord, cellId, edgeDist, localPos);

    // 이 면의 중심 월드 좌표.
    // 형상 법선과 광원 방향은 반드시 이 값으로 계산해야 한다. 픽셀 좌표(_worldPos)로 계산하면
    // 면 안에서도 법선이 매끄럽게 변하고, 그 그라데이션이 명암 양자화를 거치며 동심원 띠로 나타난다.
    // 면 중심으로 계산해야 면 하나가 통째로 평평한 다각형으로 빛난다.
    // localPos = voronoiCoord - (면 중심) 이므로, 빼면 면 중심이 나온다.
    float2 facetCenterWorld = (voronoiCoord - localPos) * facetSize / _ppu;

    // 면마다 고정된 가짜 법선. 시간에 의존하지 않으므로 면 자체는 화면에 붙어 정지해 있다.
    float2 cellRandom = GemHash2(cellId + 17.3);
    float normalAngle = cellRandom.x * 6.2831853;
    float3 randomNormal = normalize(float3(cos(normalAngle), sin(normalAngle), lerp(0.35, 1.25, cellRandom.y)));

    // 나무를 하나의 둥근 덩어리로 본 형상 법선. 중심에서 바깥으로 갈수록 옆을 향한다.
    // 면 법선을 100% 무작위로 두면 빛이 어디서 오든 밝은 면과 어두운 면이 나무 전체에 흩어져
    // 그냥 노이즈로 보인다. 이 완만한 성분이 섞여야 "빛이 이쪽에서 온다"가 한눈에 읽힌다.
    float2 fromCenter = facetCenterWorld - (_pivotPos + float2(0.0, _params.formCenterY));
    float3 formNormal = normalize(float3(fromCenter * _params.formBulge, 1.0));

    float3 facetNormal = normalize(lerp(formNormal, randomNormal, saturate(_params.facetRandomness)));

    // 폴백 광원: 시간에 따라 천천히 회전한다. 캐릭터 위치가 들어오지 않을 때만 쓰인다.
    float lightAngle = _Time.y * _params.sweepSpeed;
    float3 sweepDir = normalize(float3(cos(lightAngle), sin(lightAngle), 0.75));

    // 캐릭터를 향하는 가상 광원. 캐릭터가 나무 주위를 돌면 빛나는 면도 따라 돈다.
    // 캐릭터가 나무 바로 위에 있으면 방향이 (0,0,1)에 가까워져 모든 면이 고르게 밝아지는데,
    // 이는 광원이 머리 위에 있는 상황과 같아서 자연스럽다.
    float2 toLight = _GemLightWorldPos.xy - facetCenterWorld;
    float3 followDir = normalize(float3(toLight, max(_params.lightHeight, 0.01)));

    // w가 0이면(= C#에서 아무것도 넣지 않은 상태) 시간 회전 광원으로 안전하게 되돌아간다.
    // sweepDir와 followDir는 z가 항상 양수라 서로 정반대가 될 수 없으므로 lerp 후 normalize가 안전하다.
    float follow = saturate(_GemLightWorldPos.w * _params.lightFollow);
    float3 lightDir = normalize(lerp(sweepDir, followDir, follow));

    float ndl = saturate(dot(facetNormal, lightDir));

    // 픽셀아트답게 명암을 N단계로 양자화한다.
    // 0과 1을 모두 포함하는 균등 N단계: round(x * (N-1)) / (N-1) -> 0, 1/(N-1), ..., 1
    float steps = max(_params.shadeSteps, 2.0);
    float shade = round(ndl * (steps - 1.0)) / (steps - 1.0);

    // 색은 _gemColor 하나로 고정하고, 면마다 달라지는 것은 밝기뿐이다.
    // 빛을 등진 면(deep) -> 기본색 -> 빛을 정면으로 받은 면(lit, 흰색 쪽)으로 이어지는 이 구간이
    // "면이 빛을 받아 반짝인다"는 인상을 만든다.
    float3 deepColor = _params.gemColor * _params.deepShade;
    float3 litColor = lerp(_params.gemColor, float3(1.0, 1.0, 1.0), saturate(_params.whiteness));

    float3 gemColor = lerp(deepColor, _params.gemColor, saturate(shade * 2.0));
    gemColor = lerp(gemColor, litColor, saturate(shade * 2.0 - 1.0));

    // 면마다 아주 약간의 밝기 편차만 준다. 완전히 균일하면 인공적으로 보인다.
    // cellRandom을 그대로 쓰면 가짜 법선과 상관관계가 생겨(정면을 보는 면일수록 밝아짐)
    // 편차가 편차답지 않게 되므로, 값을 섞어 분리한다.
    float jitterRandom = frac(cellRandom.x * 7.3 + cellRandom.y * 3.1);
    gemColor *= lerp(1.0 - _params.facetVariation, 1.0 + _params.facetVariation, jitterRandom);

    // 원본 스프라이트의 명암 구조를 곱해 픽셀아트 디테일이 뭉개지지 않게 한다.
    float luma = dot(_baseColor, float3(0.299, 0.587, 0.114));
    gemColor *= (luma * _params.lumaInfluence + _params.lumaBias);

    // 가장 밝은 면에만 집중되는 가산 하이라이트. 3제곱으로 좁혀야 번들거리지 않고 자연스럽다.
    gemColor += litColor * (shade * shade * shade) * _params.specStrength;

    // 면 경계(컷 라인). 이미 픽셀 격자에 스냅된 좌표에서 계산되므로 자동으로 각진 도트 라인이 된다.
    float edge = step(edgeDist, _params.edgeWidth);
    gemColor += edge * _params.edgeBrightness * litColor;

    // 스파클: 일부 면의 중심에서 십자 별빛이 주기적으로 터진다.
    float2 sparkleRandom = GemHash2(cellId + 91.7);
    float sparkleMask = step(sparkleRandom.x, _params.sparkleRatio);

    float pulse = saturate(sin(_Time.y * _params.sparkleSpeed + sparkleRandom.y * 6.2831853));
    pulse = pulse * pulse * pulse;

    float2 d = abs(localPos);
    float star = _params.sparkleSize / max(d.x + d.y * 5.0, 1e-4)
               + _params.sparkleSize / max(d.y + d.x * 5.0, 1e-4);
    gemColor += litColor * saturate(star) * pulse * sparkleMask * _params.sparkleBrightness;

    return lerp(_baseColor, gemColor, saturate(_params.amount));
}

#endif // TREE_GEM_INCLUDED
