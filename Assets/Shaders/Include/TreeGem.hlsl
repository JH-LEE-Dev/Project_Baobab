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

// 색상환 전체를 도는 색. 무지개 보석에만 쓴다.
// URP 버전에 따라 Color.hlsl 포함 여부가 달라질 수 있어 외부 의존성 없이 직접 계산한다.
float3 GemHueToRgb(float _hue)
{
    return saturate(abs(frac(_hue + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
}

// 보로노이 셀을 구한다.
// _cellId   : 이 픽셀이 속한 면의 고유 ID (면별 랜덤값 생성에 사용)
// _localPos : 면 중심 기준 상대 위치 (면 중심 좌표와 스파클 위치를 구하는 데 사용)
//
// 면 경계까지의 거리는 구하지 않는다. 경계선을 인위적으로 긋지 않고 면끼리의 밝기 차이로만
// 경계가 드러나게 하므로 필요가 없고, 그 계산이 해시 9회를 더 먹어서 전체 비용의 절반 가까이였다.
void GemVoronoi(float2 _p, out float2 _cellId, out float2 _localPos)
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
}

// 파라미터가 많아 호출부 가독성을 위해 구조체로 묶는다.
struct TreeGemParams
{
    float amount;            // 보석 효과 전체 강도 (0 = 원본, 1 = 완전 보석)
    float3 gemColor;         // 보석의 기준 색
    float3 gemColorB;        // 영롱함을 만드는 두 번째 색. 이 둘 사이에서만 색이 오간다.
    float iridescence;       // 두 색 사이를 얼마나 오갈지 (0 = 단색)
    float rainbowAmount;     // 무지개 비중 (0 = 위 두 색만, 1 = 완전 무지개)
    float rainbowHueBase;    // 무지개의 중심 색상
    float rainbowHueRange;   // 색상이 퍼지는 범위. 좁을수록 조화롭고, 1이면 색상환 전체.
    float rainbowSaturation; // 무지개 채도. 낮출수록 파스텔이 되고 빛에 반응하는 게 잘 보인다.
    float facetSize;         // 면 하나의 크기 (픽셀 단위)
    float shadeSteps;        // 면 명암 계단 수
    float sweepSpeed;        // 캐릭터 광원이 없을 때 쓰는 시간 기반 회전 속도
    float lightFollow;       // 광원을 캐릭터 위치로 삼는 정도 (0 = 시간 회전, 1 = 캐릭터 추종)
    float lightHeight;       // 캐릭터 광원의 고도(거리와 무관). 낮을수록 스치듯 비춰 면 대비가 커진다.
    float facetRandomness;   // 면 법선의 무작위 정도 (0 = 형상만, 1 = 완전 무작위)
    float formBulge;         // 나무를 얼마나 둥근 덩어리로 볼지
    float formCenterY;       // 피봇 기준 나무 중심의 높이
    float deepShade;         // 빛을 등진 면이 어디까지 어두워지는지
    float whiteness;         // 플래시가 터진 면의 색이 흰색에 얼마나 가까운지
    float flashThreshold;    // 이 값보다 광원과 정렬된 면만 번쩍인다. 높을수록 소수의 면만 튄다.
    float flashStrength;     // 번쩍이는 면이 플래시 색으로 갈아타는 정도
    float facetVariation;    // 면마다의 미세한 밝기 편차
    float lumaInfluence;     // 원본 스프라이트 명암을 반영하는 정도
    float lumaBias;          // 전체 밝기 바닥값
    float specStrength;      // 가장 밝은 면에 얹는 가산 하이라이트 세기
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
    float2 localPos;
    GemVoronoi(voronoiCoord, cellId, localPos);

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
    float2 formCenter = _pivotPos + float2(0.0, _params.formCenterY);
    float2 fromCenter = facetCenterWorld - formCenter;
    float3 formNormal = normalize(float3(fromCenter * _params.formBulge, 1.0));

    float3 facetNormal = normalize(lerp(formNormal, randomNormal, saturate(_params.facetRandomness)));

    // 폴백 광원: 시간에 따라 천천히 회전한다. 캐릭터 위치가 들어오지 않을 때만 쓰인다.
    float lightAngle = _Time.y * _params.sweepSpeed;
    float3 sweepDir = normalize(float3(cos(lightAngle), sin(lightAngle), 0.75));

    // 캐릭터를 향하는 가상 광원. 캐릭터가 나무 주위를 돌면 빛나는 면도 따라 돈다.
    // 방향은 면 중심이 아니라 "나무 중심" 기준으로 한 번만 구한다. 면마다 따로 구하면
    // 캐릭터가 가까이 왔을 때 면끼리 방향이 크게 갈라져 점광원이 코앞에 있는 그림이 되고,
    // 면별 밝기 대비가 뭉개진다. 나무 전체가 하나의 방향광을 받아야 특정 면이 또렷하게 튄다.
    // 거리와 무관하게 "방향"만 취한다. 이것이 핵심이다.
    // toLight를 정규화하지 않고 그대로 쓰면 광원의 고도가 거리에 따라 멋대로 변한다.
    //   멀면  -> toLight가 커져 방향이 거의 수평 -> 대부분 +z를 향하는 면들이 깜깜해진다
    //   가까우면 -> toLight가 0에 수렴해 방향이 (0,0,1) -> 모든 면이 정면광을 받아 새하얘진다
    // 방위각만 캐릭터를 따라가고 고도는 _lightHeight로 고정해야 거리에 상관없이 일정하게 보인다.
    float2 toLight = _GemLightWorldPos.xy - formCenter;
    float distSq = dot(toLight, toLight);

    // 캐릭터가 나무와 사실상 같은 위치일 때만 방향이 정의되지 않으므로 그때만 기본값을 쓴다.
    float2 horizontalDir = distSq > 1e-6 ? toLight * rsqrt(distSq) : float2(0.0, -1.0);
    float3 followDir = normalize(float3(horizontalDir, max(_params.lightHeight, 0.01)));

    // w가 0이면(= C#에서 아무것도 넣지 않은 상태) 시간 회전 광원으로 안전하게 되돌아간다.
    // sweepDir와 followDir는 z가 항상 양수라 서로 정반대가 될 수 없으므로 lerp 후 normalize가 안전하다.
    float follow = saturate(_GemLightWorldPos.w * _params.lightFollow);
    float3 lightDir = normalize(lerp(sweepDir, followDir, follow));

    // 기본 명암은 형상이 섞인 법선으로 구한다. 나무 전체가 어느 쪽에서 빛을 받는지 읽히게 하는 항.
    float ndl = saturate(dot(facetNormal, lightDir));

    // 플래시 판정은 형상을 뺀 "순수 무작위 법선"으로만 한다.
    // 형상 법선이 섞인 값으로 판정하면 빛을 받는 쪽 면들이 비슷한 값을 가져 한꺼번에 문턱을 넘고,
    // 결과적으로 면이 아니라 커다란 흰 덩어리가 생긴다. 면마다 제각각인 법선으로 판정해야
    // 흩어진 소수의 면만 튄다.
    float ndlFlash = saturate(dot(randomNormal, lightDir));

    // 픽셀아트답게 명암을 N단계로 양자화한다.
    // 0과 1을 모두 포함하는 균등 N단계: round(x * (N-1)) / (N-1) -> 0, 1/(N-1), ..., 1
    float steps = max(_params.shadeSteps, 2.0);
    float shade = round(ndl * (steps - 1.0)) / (steps - 1.0);

    // 색은 _gemColor 하나로 고정하고, 면마다 달라지는 것은 밝기뿐이다.
    // 어두운 면부터 밝은 면까지 하나의 연속된 밝기 램프로 잇는다. 이렇게 해야 면들이 넓은
    // 밝기 범위에 골고루 퍼져서 자연스럽게 빛난다.
    // 대부분을 좁은 구간에 몰아넣고 소수만 밝게 빼면 "특정 면만 튀는" 인공적인 그림이 된다.
    // litColor도 순백이 아니라 옅은 보석색이어야 재질감이 유지된다(_Whiteness로 조절).
    // 영롱함: 면이 빛을 받는 각도에 따라 색이 미묘하게 옮겨간다(보석의 분산광).
    // 색상환을 통째로 도는 대신 두 색 사이에서만 움직여야, 무지개가 아니라 한 덩어리 보석의
    // 결로 읽힌다. 각도(ndl)에 면 고유값(cellRandom.y)을 섞어 이웃한 면끼리도 색이 갈리게 한다.
    float iridT = frac(ndl * 1.7 + cellRandom.y);
    float3 tintColor = lerp(_params.gemColor, _params.gemColorB, iridT * saturate(_params.iridescence));

    // 무지개 보석.
    float rainbow = saturate(_params.rainbowAmount);
    if (rainbow > 0.001)
    {
        const float3 lumaWeights = float3(0.299, 0.587, 0.114);

        // 색상을 나무 중심 기준 "방향각"으로 돌린다.
        // 면마다 색상환에서 무작위로 뽑으면 이웃한 면끼리 전혀 다른 색이 되어 뒤죽박죽으로 보인다.
        // 그렇다고 선형 그라데이션을 쓰면 면적이 넓은 중앙부가 전부 기준 색 하나에 몰려버린다.
        // 방향각을 쓰면 이웃끼리는 인접한 색이면서(조화) 색이 고르게 퍼진다(다양성).
        float angle = atan2(fromCenter.y, fromCenter.x) * 0.15915494 + 0.5; // 1/(2*PI)로 0~1 정규화
        float t = frac(angle + (cellRandom.x - 0.5) * 0.06); // 면별 미세한 흔들림. 너무 매끈해지지 않게만.
        float hue = frac(_params.rainbowHueBase + t * _params.rainbowHueRange);

        // 채도를 낮춰 파스텔로 만든다. 채도가 높으면 색 차이가 밝기 차이를 완전히 덮어버려서
        // 캐릭터 광원에 반응하는 것이 눈에 보이지 않는다.
        float3 rainbowHue = lerp(float3(1.0, 1.0, 1.0), GemHueToRgb(hue), saturate(_params.rainbowSaturation));

        // 밝기는 휘도 기준으로 gemColor에 맞춘다. 최대 성분으로 맞추면 색상환을 도는 동안
        // 노란색이 파란색보다 몇 배 밝아져서 특정 색 구간에서만 하얗게 터진다.
        float targetLuma = dot(_params.gemColor, lumaWeights);
        float hueLuma = max(dot(rainbowHue, lumaWeights), 1e-4);

        tintColor = lerp(tintColor, rainbowHue * (targetLuma / hueLuma), rainbow);
    }

    float3 deepColor = tintColor * _params.deepShade;
    float3 litColor = lerp(tintColor, float3(1.0, 1.0, 1.0), saturate(_params.whiteness));

    float3 gemColor = lerp(deepColor, litColor, shade);

    // 면마다 아주 약간의 밝기 편차만 준다. 완전히 균일하면 인공적으로 보인다.
    // cellRandom을 그대로 쓰면 가짜 법선과 상관관계가 생겨(정면을 보는 면일수록 밝아짐)
    // 편차가 편차답지 않게 되므로, 값을 섞어 분리한다.
    float jitterRandom = frac(cellRandom.x * 7.3 + cellRandom.y * 3.1);
    gemColor *= lerp(1.0 - _params.facetVariation, 1.0 + _params.facetVariation, jitterRandom);

    // 원본 스프라이트의 명암 구조를 곱해 픽셀아트 디테일이 뭉개지지 않게 한다.
    float luma = dot(_baseColor, float3(0.299, 0.587, 0.114));
    gemColor *= (luma * _params.lumaInfluence + _params.lumaBias);

    // 플래시: 광원과 거의 정확히 정렬된 소수의 면만 한 단계 더 밝아진다.
    // 위 램프가 룩의 본체이고 이건 그 위에 얹는 양념이다. 흰색으로 완전히 빼지 않고
    // 밝은 보석색(litColor) 쪽으로만 올려야 재질감이 유지된다.
    // 양자화된 shade가 아니라 원본 ndl로 판정해야, 광원이 움직일 때 면마다 문턱을 넘는 순간이
    // 제각각 어긋나면서 하나씩 반짝이는 느낌이 살아난다.
    float flash = step(_params.flashThreshold, ndlFlash);

    gemColor = lerp(gemColor, litColor, flash * saturate(_params.flashStrength));
    gemColor += litColor * flash * _params.specStrength;

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
