#ifndef TREE_DAMAGE_LOOK_INCLUDED
#define TREE_DAMAGE_LOOK_INCLUDED

#include "OverheatBurstWave.hlsl"

// 4스테이지(용암 숲) 나무가 깎일수록 달라지는 모습. 렌더링 전용이다.
// - 돌처럼 생긴 나무: 금이 가며 그 틈으로 용암 빛이 새어 나온다(깎일수록 금이 밑동 쪽 한 자리에서 수정 조각을 따라 뻗어 나간다).
// - 잿불 나무(침엽수 모양): 금 대신 잎 층 아래 그늘에서 숯불이 붙어 아래층부터 위로 번진다.
// 나무마다 깎인 정도(_DamageLook)와 나무 루트 위치(_DamageLookPivot)를 MaterialPropertyBlock으로 받는다(TreeVisualComponent.SetDamageLook, 넣는 쪽은 TreeObj).
// 값의 부호가 모습을 고른다: 양수 = 금, 음수 = 숯불, 0 = 없음. 크기(0~1)가 깎인 정도다.
// 켜고 끄는 것은 전역값 _TreeDamageLookOn(0/1)을 따른다. StageAmbienceShaderGlobals가 스테이지 데이터의 열기 차오름
// 켜짐과 같은 값으로 채우며(본편 4스테이지·로그라이크 용암 숲에서만 1), 다른 스테이지에서는 값이 들어와도 아무것도 바꾸지 않는다.
// 열기 차오름(_TreeHeatChargeA)은 로그라이크에서 꺼지므로 따로 둔다. TreeHeatCharge.hlsl 뒤에 include 한다.
//
// [비용] 무늬는 픽셀마다 계산하지 않고 미리 구워 둔 작은 텍스처에서 한 번 읽는다.
// - 금: 나무 그림마다 구운 금 모양(_TreeCrackMaskTex, TreeCrackMaskTexture.cs가 그림의 조각·외곽선을 보고 만든다).
//   깎인 나무의 재질 사본에 그 나무 그림의 것이 들어간다(TreeVisualComponent). 모양이 여러 가지라 나무 밑동 위치로 하나를 고른다.
// - 숯불: 덩이·깜빡임 박자 무늬(_TreeDamagePatternTex, TreeDamagePatternTexture.cs가 만든다). 나무 밑동 위치로 무늬를 민다.
// 나무 한 그루에 한 번이면 되는 값(무늬 위치, 켜짐 여부, 열기 물결)은 정점 단계(ComputeTreeDamageFx)에서 구해 넘기므로,
// 깎이지 않은 나무의 픽셀은 비교 한 번으로 끝난다.

// 스프라이트 텍스처 크기는 _MainTex_TexelSize를 선언해 받지 않고 텍스처에서 직접 읽는다(TreeDamageSpriteSize).
// 그 값을 머티리얼 묶음(UnityPerMaterial) 밖에 선언하면 나무 셰이더 전체가 SRP 배처와 맞지 않게 되고,
// 묶음 안에 넣으면 스프라이트마다 다른 텍스처 대신 머티리얼의 값을 읽게 된다. 깎인 나무의 픽셀에서만 읽는다.

// 숯불 무늬(가로세로로 이어 붙는다). 텍스처 픽셀 좌표로 그대로 읽는다(필터 없음). R·G는 쓰지 않는다.
// B: 숯불 덩이 무늬(0~1, 뭉글뭉글한 값)
// A: 숯불 깜빡임 박자(0~1, 두 픽셀 묶음마다 다르다)
TEXTURE2D(_TreeDamagePatternTex);
// 나무 그림의 금 모양(그림 크기의 가로 TreeCrackVariantCount배, 모양이 가로로 이어 붙어 있다). 그림 픽셀 좌표로 그대로 읽는다.
// R(x255): 아래 2비트 = 종류(0 = 금 아님, 1 = 금의 심, 2 = 심 옆의 달궈진 가장자리, 3 = 외곽선을 건너는 이음매),
//          위 6비트 = 열기(0~TreeCrackHeatLevels -> 0~1, 수관 금은 1, 밑동 금은 수관에서 멀어질수록 식는다)
// G: 이 픽셀이 보이기 시작하는 깎임 정도(0~1)
// B: 금을 따라 시작 자리에서 잰 거리(픽셀, /255) - 빛이 바깥으로 흐르는 박자
// A: 금 한 줄마다 고유한 값(0~1) - 빛이 흐르는 박자를 줄마다 다르게
// 머티리얼 묶음(UnityPerMaterial) 밖의 텍스처라 SRP 배처와 맞는다. 깎인 나무의 픽셀에서만 읽는다.
TEXTURE2D(_TreeCrackMaskTex);
// 깎인 모습 켜짐(0/1). 위 설명 참고.
float _TreeDamageLookOn;
// 무늬 텍스처 한 변의 크기(픽셀). TreeDamagePatternTexture.Size와 같아야 한다.
static const float TreeDamagePatternSize = 144.0;

// ── 금 ──
// 금은 그림의 한 자리(수관 아래쪽 가운데, 도끼가 닿는 쪽)에서 갈라져 깎일수록 바깥으로 뻗어 나간다.
// 모양은 그림의 수정 조각을 따라 미리 구워 둔다(조각을 따라 지나가고, 외곽선을 건너는 자리는 한 톤 가라앉은 붉은 이음매로
// 이어져 금 줄기가 끊기지 않으면서도 앞 조각 뒤로 지나가는 것처럼 보이며, 다음 조각에서 어긋나 나온다).
// 나무 그림에는 원래 빛나는 점이 많이 박혀 있어서(하이라이트 그림), 금은 한 자리에서 이어져 자라는 1픽셀 줄기로 그리고
// 빛이 줄기를 따라 바깥으로 흐르게 해 제자리에서 반짝이는 점들과 구분한다.
// 금 모양 가짓수. TreeCrackMaskTexture.VariantCount와 같아야 한다.
static const int TreeCrackVariantCount = 4;
// 금 열기 단계 수. TreeCrackMaskTexture.HeatLevels와 같아야 한다.
static const float TreeCrackHeatLevels = 63.0;
// 식은 금(열기 0)의 밝기 배율. 밑동 금은 수관에서 내려오며 식어 어둡고 붉게 사그라든다.
static const float TreeCrackCoolBrightness = 0.55;
// 외곽선을 건너는 이음매의 밝기(갓 난 틈 색 기준). 블룸 임계값 아래의 붉은 주황이라 심처럼 번지지 않는다.
static const float TreeCrackSeamBrightness = 0.75;
// 금의 끝이 처음 보이기 시작해 다 드러날 때까지 필요한 깎임 폭(금 끝이 흐릿하게 자라난다)
static const float TreeCrackFadeIn = 0.08;
// 금이 드러난 뒤 이만큼 더 깎이면 갓 난 붉은 틈에서 하얗게 달군 심으로 다 달아오른다(오래된 금일수록 밝다).
static const float TreeCrackAgeSpan = 0.25;
// 이보다 많이 깎이면 금이 더 밝아지고 가장자리가 짙어지며 빛이 빠르게 흐른다(쓰러지기 직전)
static const float TreeCrackCritical = 0.75;
// 금 심(가장 밝은 줄)과 갓 난 틈의 빛 색(선형). 심은 나무의 주황·분홍 점과 구분되게 흰 쪽으로 달군 노랑이고
// 블룸 임계값(1.345)을 넘겨 번진다. 갓 난 틈은 임계값 아래의 붉은 주황이다.
static const half3 TreeCrackCoreColor = half3(1.75, 1.45, 0.7);
static const half3 TreeCrackEdgeColor = half3(1.3, 0.34, 0.04);
// 심 옆 가장자리의 색(선형). 어두운 진홍이라 빛이 아니라 달궈진 돌로 보이고, 심을 1픽셀 줄로 또렷하게 둔다.
static const half3 TreeCrackRimColor = half3(0.52, 0.136, 0.016);
// 가장자리를 바탕에 섞는 정도(평소 / 쓰러지기 직전)
static const float TreeCrackRimBlend = 0.6;
static const float TreeCrackRimBlendCritical = 0.9;

// ── 숯불 ──
// 숯불이 붙을 수 있는 픽셀의 밝기 상한과, 숯불의 심으로 가장 밝게 타는 픽셀의 밝기 상한(최대 채널).
// 잿불 나무 그림은 여덟 가지 색(그림 파일 값 0.09~0.28)을 쓴다. 어두운 세 색(0.15 아래)이 그늘·틈·어두운 바탕이고
// 그중 가장 어두운 두 색(0.11 아래)이 층 아래 그늘과 틈이다. 밝은 다섯 색(잎 층의 면)은 그대로 둔다.
// 선형 색 공간에서는 텍스처 값이 선형으로 들어오므로 같은 경계를 선형 값으로 적어 둔다(픽셀마다 변환하지 않는다).
#if defined(UNITY_COLORSPACE_GAMMA)
static const float TreeEmberShadeLimit = 0.15;
static const float TreeEmberCoreShade = 0.11;
#else
static const float TreeEmberShadeLimit = 0.0196;
static const float TreeEmberCoreShade = 0.01165;
#endif
// 나무 그림이 차지하는 세로 범위(텍스처 좌표). 아래에서 위로 번지는 순서를 정한다.
static const float TreeEmberBottomV = 0.25;
static const float TreeEmberTopV = 0.875;
// 이보다 많이 깎이면 나무 전체가 달아오르고 깜빡임이 빨라진다(쓰러지기 직전)
static const float TreeEmberCritical = 0.75;
// 숯불 심과 그 둘레의 빛 색(선형). 심은 블룸 임계값(1.345)을 넘겨 번진다.
static const half3 TreeEmberCoreColor = half3(2.5, 1.05, 0.2);
static const half3 TreeEmberWarmColor = half3(0.5, 0.1, 0.02);

float2 TreeDamageHash2(float2 _p)
{
    float3 p3 = frac(float3(_p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

// [정점 단계] 나무 한 그루에 한 번만 구하면 되는 값. 정점마다 같은 값이 나오므로 보간 없이(nointerpolation) 넘긴다.
// _pivotWorld: 렌더러(오브젝트 원점) 월드 좌표(열기 물결용), _damageLook: 렌더러의 _DamageLook
// _damagePivot: 렌더러의 _DamageLookPivot(나무 루트 월드 좌표, 무늬 위치용)
// 무늬 위치는 렌더러 위치가 아니라 루트 위치로 정한다. 렌더러는 피격 흔들림(visualRoot 펀치, 최대 약 6픽셀)으로 움직이는데,
// 반 픽셀을 넘을 때마다 해시가 바뀌어 흔들리는 동안 금·숯불 무늬가 프레임마다 통째로 바뀌었다.
// 흔들리지 않을 때는 렌더러 위치와 루트 위치가 같아(렌더러가 루트 원점에 있다) 예전과 같은 무늬가 나온다.
// 반환 xy = 무늬 위치(픽셀), z = 열기 물결에 달궈진 정도(0~1, 단계), w = 깎인 모습(양수 금 / 음수 숯불 / 0 없음)
float4 ComputeTreeDamageFx(float2 _pivotWorld, float _damageLook, float2 _damagePivot)
{
    float4 fx = float4(0.0, 0.0, OverheatBurstFlareHeatAt(_pivotWorld), 0.0);

    if (_TreeDamageLookOn >= 0.5 && _damageLook != 0.0)
    {
        fx.xy = floor(TreeDamageHash2(floor(_damagePivot * 32.0 + 0.5)) * TreeDamagePatternSize);
        fx.w = _damageLook;
    }

    return fx;
}

// 스프라이트 텍스처 크기(픽셀). 아틀라스에 묶인 스프라이트면 아틀라스 크기다(_MainTex_TexelSize.zw와 같은 값).
float2 TreeDamageSpriteSize()
{
    uint width;
    uint height;
    _MainTex.GetDimensions(width, height);
    return float2(width, height);
}

// _texel: 그림 안 픽셀 좌표, _texSize: 스프라이트 텍스처 크기, _variantSeed: 나무마다 다른 무늬 위치 값(정수, 모양을 고른다)
half3 ApplyTreeDamageCracks(half3 _color, float2 _texel, float2 _texSize, float _variantSeed, float _damage)
{
    // 무늬 위치 값은 0 이상의 정수라 부호 없는 나머지로 고른다(부호 있는 정수 나머지는 느리다).
    uint variant = (uint)_variantSeed % (uint)TreeCrackVariantCount;
    int2 maskTexel = int2(_texel.x + variant * _texSize.x, _texel.y);

    // 금 모양이 들어오지 않은 재질(기본 텍스처)이면 그리지 않는다. 범위 밖 읽기 값은 그래픽 API마다 달라 직접 거른다.
    uint maskWidth;
    uint maskHeight;
    _TreeCrackMaskTex.GetDimensions(maskWidth, maskHeight);
    if (maskTexel.x >= (int)maskWidth || maskTexel.y >= (int)maskHeight)
    {
        return _color;
    }

    half4 mask = LOAD_TEXTURE2D(_TreeCrackMaskTex, maskTexel);

    // 아래 2비트 = 종류(0 = 금 아님, 1 = 심, 2 = 가장자리, 3 = 이음매), 위 6비트 = 열기
    uint code = (uint)round(mask.r * 255.0);
    uint kind = code & 3u;
    if (0u == kind)
    {
        return _color;
    }
    float heat = (float)(code >> 2) / TreeCrackHeatLevels;

    // 시작 자리에서 금을 따라 먼 픽셀일수록 늦게 나타난다(깎일수록 금이 이어져 뻗어 나간다).
    float appearAt = mask.g;
    float shown = saturate((_damage - appearAt) / TreeCrackFadeIn);
    if (shown <= 0.0)
    {
        return _color;
    }
    // 갓 난 금은 붉고, 시간이 지날수록(더 깎일수록) 하얗게 달아오른다. 그래서 금 끝은 가늘고 붉게, 시작 자리 쪽은 밝게 보인다.
    float age = saturate((_damage - appearAt) / TreeCrackAgeSpan);

    // 빛이 시작 자리에서 금을 따라 바깥으로 흘러 나간다(제자리에서 반짝이는 나무의 점들과 움직임이 다르다).
    // 쓰러지기 직전에는 더 밝고 빠르게 흐른다.
    float critical = saturate((_damage - TreeCrackCritical) / (1.0 - TreeCrackCritical));
    float flow = 0.8 + 0.2 * sin(mask.b * 255.0 * 0.35 - _Time.y * lerp(3.0, 10.0, critical) + mask.a * 6.2832);
    float glow = flow * lerp(0.85, 1.2, critical);

    // 식은 금은 하얗게 달아오르지 않고 붉은 틈인 채로 어두워진다(열기 1이면 그대로).
    if (1u == kind)
    {
        half3 core = lerp(TreeCrackEdgeColor, TreeCrackCoreColor, (half)(age * heat)) * (half)(glow * lerp(TreeCrackCoolBrightness, 1.0, heat));
        return lerp(_color, core, (half)shown);
    }

    // 이음매는 하얗게 달아오르지 않고 붉은 틈 색으로만 이어 준다.
    if (3u == kind)
    {
        half3 seam = TreeCrackEdgeColor * (half)(glow * TreeCrackSeamBrightness * lerp(TreeCrackCoolBrightness, 1.0, heat));
        return lerp(_color, seam, (half)shown);
    }

    float rimBlend = lerp(TreeCrackRimBlend, TreeCrackRimBlendCritical, critical);
    return lerp(_color, TreeCrackRimColor * (half)glow, (half)(shown * age * rimBlend * heat));
}

// _texSize: 스프라이트 텍스처 크기
half3 ApplyTreeDamageEmbers(half3 _color, float2 _uv, float2 _texSize, half4 _pattern, float _damage)
{
    float critical = saturate((_damage - TreeEmberCritical) / (1.0 - TreeEmberCritical));

    // 쓰러지기 직전에는 잎 층의 면까지 포함해 나무 전체가 붉게 달아오른다(곱해서 밝혀 그림의 명암은 남긴다).
    half3 color = _color * (half3)(1.0 + critical * float3(1.6, 0.45, 0.15));

    // 아래층부터 붙어 위로 번진다. 맨 아래는 조금만 깎여도 붙고, 맨 위는 깎임 0.4쯤부터 붙는다.
    float height = saturate((_uv.y - TreeEmberBottomV) / (TreeEmberTopV - TreeEmberBottomV));
    float fill = saturate(_damage * 1.5 - height * 0.6);

    // 덩이 무늬가 차오른 정도보다 낮은 곳에 숯불이 붙는다. 덩이 가운데일수록 밝고, 세 단계로 끊는다.
    // 다 깎여도 그늘의 일부는 꺼진 채로 남겨, 한 덩어리로 빛나지 않고 숯불처럼 얼룩지게 한다.
    // 숯불이 붙지 않는 픽셀이 대부분이라 텍스처를 읽기 전에 여기서 먼저 거른다.
    float glow = saturate((fill * 0.78 - _pattern.b) / 0.22);
    if (glow <= 0.0)
    {
        return color;
    }
    glow = ceil(glow * 3.0) / 3.0;

    // 그림의 어두운 픽셀(그늘·틈·어두운 바탕)에만 붙는다.
    half3 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, _uv).rgb;
    float shade = max(tex.r, max(tex.g, tex.b));
    if (shade > TreeEmberShadeLimit)
    {
        return color;
    }

    // 그림 둘레(바깥이 투명한 픽셀)는 뺀다. 둘레가 빛나면 속불이 아니라 테두리 빛으로 보인다.
    float2 texelSize = 1.0 / _texSize;
    float outside = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, _uv + float2(texelSize.x, 0.0)).a;
    outside = min(outside, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, _uv - float2(texelSize.x, 0.0)).a);
    outside = min(outside, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, _uv + float2(0.0, texelSize.y)).a);
    outside = min(outside, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, _uv - float2(0.0, texelSize.y)).a);
    if (outside < 0.5)
    {
        return color;
    }

    // 숨 쉬듯 일렁이다가, 쓰러지기 직전에는 빠르게 깜빡인다.
    float speed = lerp(2.0, 8.0, critical);
    float flicker = 0.78 + 0.22 * sin(_Time.y * speed + _pattern.a * 6.2832);

    // 가장 어두운 픽셀(틈)은 숯불의 심으로 밝게 타고, 그보다 밝은 바탕은 붉게 물들기만 한다.
    if (shade <= TreeEmberCoreShade)
    {
        return lerp(color, TreeEmberCoreColor * (half)(flicker * lerp(0.7, 1.1, critical)), (half)glow);
    }

    return lerp(color, TreeEmberWarmColor * (half)flicker, (half)(glow * 0.5));
}

// [픽셀 단계] _color: 조명까지 끝난 색, _uv: 픽셀에 맞춘 텍스처 좌표, _fx: ComputeTreeDamageFx가 넘긴 값
half3 ApplyTreeDamageLook(half3 _color, float2 _uv, float4 _fx)
{
    // 깎이지 않은 나무·다른 스테이지는 여기서 끝난다.
    if (_fx.w == 0.0)
    {
        return _color;
    }

    float2 texSize = TreeDamageSpriteSize();
    float2 spriteTexel = floor(_uv * texSize);

    if (_fx.w > 0.0)
    {
        return ApplyTreeDamageCracks(_color, spriteTexel, texSize, _fx.x, _fx.w);
    }

    // 텍스처 픽셀 좌표 + 나무마다 다른 무늬 위치 -> 숯불 무늬 텍스처 안으로 접는다.
    float2 texel = spriteTexel + _fx.xy;
    texel -= TreeDamagePatternSize * floor(texel / TreeDamagePatternSize);
    half4 pattern = LOAD_TEXTURE2D(_TreeDamagePatternTex, int2(texel));

    return ApplyTreeDamageEmbers(_color, _uv, texSize, pattern, -_fx.w);
}

#endif // TREE_DAMAGE_LOOK_INCLUDED
