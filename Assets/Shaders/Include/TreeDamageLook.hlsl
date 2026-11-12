#ifndef TREE_DAMAGE_LOOK_INCLUDED
#define TREE_DAMAGE_LOOK_INCLUDED

#include "OverheatBurstWave.hlsl"

// 4스테이지(용암 숲) 나무가 깎일수록 달라지는 모습. 렌더링 전용이다.
// - 돌처럼 생긴 나무: 금이 가며 그 틈으로 용암 빛이 새어 나온다(깎일수록 금이 한 자리에서 뻗어 나가고, 많이 깎이면 굵어진다).
// - 잿불 나무(침엽수 모양): 금 대신 잎 층 아래 그늘에서 숯불이 붙어 아래층부터 위로 번진다.
// 나무마다 깎인 정도(_DamageLook)를 MaterialPropertyBlock으로 받는다(TreeVisualComponent.SetDamageLook, 넣는 쪽은 TreeObj).
// 값의 부호가 모습을 고른다: 양수 = 금, 음수 = 숯불, 0 = 없음. 크기(0~1)가 깎인 정도다.
// 켜고 끄는 것은 전역값 _TreeDamageLookOn(0/1)을 따른다. StageAmbienceShaderGlobals가 스테이지 데이터의 열기 차오름
// 켜짐과 같은 값으로 채우며(본편 4스테이지·로그라이크 용암 숲에서만 1), 다른 스테이지에서는 값이 들어와도 아무것도 바꾸지 않는다.
// 열기 차오름(_TreeHeatChargeA)은 로그라이크에서 꺼지므로 따로 둔다. TreeHeatCharge.hlsl 뒤에 include 한다.
//
// [비용] 무늬(금의 경계선, 숯불 덩이, 깜빡임 박자)는 픽셀마다 계산하지 않고 미리 구워 둔 작은 텍스처
// (_TreeDamagePatternTex, TreeDamagePatternTexture.cs가 만든다)에서 한 번 읽는다. 나무 한 그루에 한 번이면 되는 값
// (무늬 위치, 켜짐 여부, 열기 물결)은 정점 단계(ComputeTreeDamageFx)에서 구해 넘기므로, 깎이지 않은 나무의 픽셀은
// 비교 한 번으로 끝난다. 같은 그림을 쓰는 나무끼리 무늬가 겹치지 않게 나무 밑동 위치로 무늬를 민다.

// 스프라이트 텍스처 크기는 _MainTex_TexelSize를 선언해 받지 않고 텍스처에서 직접 읽는다(TreeDamageSpriteSize).
// 그 값을 머티리얼 묶음(UnityPerMaterial) 밖에 선언하면 나무 셰이더 전체가 SRP 배처와 맞지 않게 되고,
// 묶음 안에 넣으면 스프라이트마다 다른 텍스처 대신 머티리얼의 값을 읽게 된다. 깎인 나무의 픽셀에서만 읽는다.

// 미리 구운 무늬(가로세로로 이어 붙는다). 텍스처 픽셀 좌표로 그대로 읽는다(필터 없음).
// R: 0 = 금 아님, 그 밖 = (금의 심까지 거리 + 1) / 255 (심 = 1, 심 옆 = 2, 그 옆 = 3). 심은 어디서나 1픽셀 굵기로 이어진다.
// G: 금 한 줄마다 고유한 값(0~1) - 뻗는 빠르기·빛이 흐르는 박자
// B: 숯불 덩이 무늬(0~1, 뭉글뭉글한 값)
// A: 숯불 깜빡임 박자(0~1, 두 픽셀 묶음마다 다르다)
TEXTURE2D(_TreeDamagePatternTex);
// 깎인 모습 켜짐(0/1). 위 설명 참고.
float _TreeDamageLookOn;
// 무늬 텍스처 한 변의 크기(픽셀). TreeDamagePatternTexture.Size와 같아야 한다.
static const float TreeDamagePatternSize = 144.0;

// ── 금 ──
// 금은 그림의 한 자리(수관 아래쪽 가운데, 도끼가 닿는 쪽)에서 시작해 깎일수록 바깥으로 뻗어 나간다.
// 나무 그림에는 원래 빛나는 점이 많이 박혀 있어서(하이라이트 그림), 금이 여기저기 따로 나타나면 그 점들과 구분되지 않는다.
// 그래서 한 자리에서 이어져 자라는 줄기로 그리고, 밝은 심 양옆에 붉은 가장자리를 붙여 3픽셀 굵기의 줄로 보이게 한다.
// 금이 시작하는 자리(그림 안 비율)와, 거기서 이 거리(픽셀)까지 뻗으면 다 뻗은 것으로 본다.
static const float2 TreeCrackOrigin = float2(0.5, 0.3);
static const float TreeCrackReach = 36.0;
// 금의 끝이 처음 보이기 시작해 다 밝아질 때까지 필요한 깎임 폭(금 끝이 흐릿하게 자라난다)
static const float TreeCrackFadeIn = 0.1;
// 이보다 많이 깎이면 금의 심이 2픽셀로 굵어진다(쓰러지기 바로 전 단계부터)
static const float TreeCrackWide = 0.85;
// 이보다 많이 깎이면 금이 더 밝아지고 빛이 빠르게 흐른다(쓰러지기 직전)
static const float TreeCrackCritical = 0.75;
// 금 심(가장 밝은 줄)과 가장자리의 빛 색(선형). 심은 나무의 주황·분홍 점과 구분되게 흰 쪽으로 달군 노랑이고
// 블룸 임계값(1.345)을 넘겨 번진다. 가장자리는 임계값 아래의 붉은 주황이라 번지지 않고 금의 윤곽을 또렷하게 잡는다.
static const half3 TreeCrackCoreColor = half3(1.75, 1.45, 0.7);
static const half3 TreeCrackEdgeColor = half3(1.3, 0.34, 0.04);

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
// _pivotWorld: 나무 밑동(오브젝트 원점) 월드 좌표, _damageLook: 렌더러의 _DamageLook
// 반환 xy = 무늬 위치(픽셀), z = 열기 물결에 달궈진 정도(0~1, 단계), w = 깎인 모습(양수 금 / 음수 숯불 / 0 없음)
float4 ComputeTreeDamageFx(float2 _pivotWorld, float _damageLook)
{
    float4 fx = float4(0.0, 0.0, OverheatBurstFlareHeatAt(_pivotWorld), 0.0);

    if (_TreeDamageLookOn >= 0.5 && _damageLook != 0.0)
    {
        fx.xy = floor(TreeDamageHash2(floor(_pivotWorld * 32.0 + 0.5)) * TreeDamagePatternSize);
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

// _texel: 그림 안 픽셀 좌표(무늬 위치를 더하기 전), _texSize: 스프라이트 텍스처 크기
half3 ApplyTreeDamageCracks(half3 _color, float2 _texel, float2 _texSize, half4 _pattern, float _damage)
{
    // 금의 심까지 거리(0 = 심). 금이 아닌 픽셀은 음수다.
    float edge = round(_pattern.r * 255.0) - 1.0;
    if (edge < 0.0)
    {
        return _color;
    }

    // 금이 시작하는 자리에서 먼 픽셀일수록 늦게 나타난다(깎일수록 금이 이어져 뻗어 나간다).
    // 금 한 줄마다 조금씩 빠르고 느려서 줄기마다 길이가 다르다. 맨 끝은 깎임 0.65쯤에 닿는다.
    float edgeRandom = _pattern.g;
    float spread = saturate(length(_texel + 0.5 - _texSize * TreeCrackOrigin) / TreeCrackReach);
    float appearAt = 0.02 + spread * 0.5 + edgeRandom * 0.12;
    float shown = saturate((_damage - appearAt) / TreeCrackFadeIn);
    if (shown <= 0.0)
    {
        return _color;
    }

    // 심은 1픽셀 줄, 쓰러지기 바로 전에는 2픽셀로 굵어진다. 그 바깥 1픽셀은 달궈진 가장자리.
    float coreWidth = 1.0 + step(TreeCrackWide, _damage);
    float core = step(edge, coreWidth - 0.5);
    float rim = step(edge, coreWidth + 0.5) * (1.0 - core);
    if (core + rim <= 0.0)
    {
        return _color;
    }

    // 빛이 시작 자리에서 금을 따라 바깥으로 흘러 나간다(제자리에서 반짝이는 나무의 점들과 움직임이 다르다).
    // 쓰러지기 직전에는 더 밝고 빠르게 흐른다.
    float critical = saturate((_damage - TreeCrackCritical) / (1.0 - TreeCrackCritical));
    float flow = 0.8 + 0.2 * sin(spread * 9.0 - _Time.y * lerp(3.0, 10.0, critical) + edgeRandom * 6.2832);
    float glow = flow * lerp(0.85, 1.2, critical);

    half3 color = _color;
    color = lerp(color, TreeCrackCoreColor * (half)glow, (half)(core * shown));
    color = lerp(color, TreeCrackEdgeColor * (half)glow, (half)(rim * shown * 0.85));
    return color;
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

    // 텍스처 픽셀 좌표 + 나무마다 다른 무늬 위치 -> 무늬 텍스처 안으로 접는다.
    float2 texSize = TreeDamageSpriteSize();
    float2 spriteTexel = floor(_uv * texSize);
    float2 texel = spriteTexel + _fx.xy;
    texel -= TreeDamagePatternSize * floor(texel / TreeDamagePatternSize);
    half4 pattern = LOAD_TEXTURE2D(_TreeDamagePatternTex, int2(texel));

    if (_fx.w > 0.0)
    {
        return ApplyTreeDamageCracks(_color, spriteTexel, texSize, pattern, _fx.w);
    }

    return ApplyTreeDamageEmbers(_color, _uv, texSize, pattern, -_fx.w);
}

#endif // TREE_DAMAGE_LOOK_INCLUDED
