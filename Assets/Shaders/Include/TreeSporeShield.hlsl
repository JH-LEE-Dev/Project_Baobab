#ifndef TREE_SPORE_SHIELD_INCLUDED
#define TREE_SPORE_SHIELD_INCLUDED

// 2스테이지 포자막 상태 표현. 렌더링 전용이다.
// 포자막 그림(나무 셰이더를 쓰는 Top/Bottom Shield 렌더러)에서 하늘색 결정 픽셀만 골라
//  - 막이 깎인 만큼 결정이 어둡고 탁해지고(x)
//  - 막을 때리면 결정이 하얗게 번쩍이고(y)
//  - 막이 다시 차오르는 동안 아래에서 위로 빛 띠가 거듭 훑어 올라가고(z)
//  - 막이 가득 차는 순간 결정이 한 번 밝게 맥동한다(w).
// 포자막 렌더러에만 MaterialPropertyBlock으로 값(_SporeShieldFx)을 넣는다(TreeVisualComponent.SetSporeShieldFx,
// 넣는 쪽은 StageSporeShieldFx). 기본값 0이면 비교 한 번으로 끝나고 색을 바꾸지 않으므로 다른 렌더러·스테이지는 그대로다.
// 포자막 그림은 수관 전체를 그대로 베낀 위에 결정을 얹은 그림이라, 결정이 아닌 픽셀은 건드리지 않는다.

// 결정 색 판정(sRGB 0~1): 초록이 빨강보다 높을수록 결정이다. 2스테이지 세 나무(포자 솜·솜털 균사·방울솔) 그림을 재 보면
// 결정과 그 흰 점·테두리는 초록-빨강이 0.024~0.31이고, 수관·밑동의 보라·분홍·흰 점은 모두 -0.008 이하다.
static const half TreeSporeShieldCyanMin = 0.015h;
static const half TreeSporeShieldCyanFull = 0.045h;

// 막이 다 깎였을 때 결정이 어두워지는 정도와 탁해지는(회색 쪽으로) 정도
static const half TreeSporeShieldDepleteDim = 0.6h;
static const half TreeSporeShieldDepleteDesat = 0.45h;
static const float TreeSporeShieldDepleteCurve = 0.6;

// 차오름 빛 띠: 그림 세로(텍스처 좌표) 범위, 한 번 훑는 시간(초), 띠 반폭, 띠 색(선형 HDR), 띠가 없을 때도 살짝 들뜨는 정도
static const float TreeSporeShieldSweepBottomV = 0.2;
static const float TreeSporeShieldSweepTopV = 0.92;
static const float TreeSporeShieldSweepPeriod = 0.9;
static const float TreeSporeShieldSweepHalfWidth = 0.07;
static const half3 TreeSporeShieldSweepColor = half3(0.45h, 1.25h, 1.2h);
static const half TreeSporeShieldRegenLift = 0.18h;

// 번쩍임 색(선형 HDR, 블룸 문턱을 넘겨 번진다)과 가득 참 맥동 색
static const half3 TreeSporeShieldFlashColor = half3(1.7h, 2.3h, 2.2h);
static const half TreeSporeShieldFlashMix = 0.85h;
// 맥동은 결정 색을 유지한 채 밝기를 올리고(흰색으로 바래지 않게) 하늘색을 조금 더한다.
static const half TreeSporeShieldPulseGain = 0.9h;
static const half3 TreeSporeShieldPulseColor = half3(0.1h, 0.35h, 0.33h);

// 결정 픽셀이면 1(가장자리 색은 0~1 사이), 아니면 0
half TreeSporeShieldCrystalMask(float2 _uv)
{
    half3 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, _uv).rgb;
#if !defined(UNITY_COLORSPACE_GAMMA)
    // 판정용이라 근사로 충분하다(Color.hlsl을 끌어오지 않는다).
    tex = pow(max(tex, 0.0h), 1.0h / 2.2h);
#endif
    return smoothstep(TreeSporeShieldCyanMin, TreeSporeShieldCyanFull, tex.g - tex.r);
}

// _color: HDR 배율까지 끝난 색, _uv: 픽셀에 맞춘 텍스처 좌표,
// _fx: x = 깎인 정도(0~1), y = 번쩍임(0~1), z = 차오르는 중(0~1), w = 가득 참 맥동(0~1)
half3 ApplyTreeSporeShield(half3 _color, float2 _uv, float4 _fx)
{
    if (all(_fx == 0.0)) return _color;

    half mask = TreeSporeShieldCrystalMask(_uv);
    if (mask <= 0.0h) return _color;

    half3 col = _color;

    // 깎인 만큼 어둡고 탁하게. 결정은 HDR(1.7배)로 블룸 문턱을 넘어 있어 조금 어두워져서는 그대로 하얗게 보이므로,
    // 앞쪽에서 빨리 어두워지게 곡선을 준다(절반 깎이면 다 깎인 것의 약 2/3만큼).
    half deplete = (half)pow(saturate(_fx.x), TreeSporeShieldDepleteCurve);
    half luma = dot(col, half3(0.299h, 0.587h, 0.114h));
    col = lerp(col, luma.xxx, TreeSporeShieldDepleteDesat * deplete) * (1.0h - TreeSporeShieldDepleteDim * deplete);

    // 차오르는 동안 아래에서 위로 훑는 빛 띠
    half regen = (half)saturate(_fx.z);
    if (regen > 0.0h)
    {
        float bandV = lerp(TreeSporeShieldSweepBottomV, TreeSporeShieldSweepTopV, frac(_Time.y / TreeSporeShieldSweepPeriod));
        half band = (half)saturate(1.0 - abs(_uv.y - bandV) / TreeSporeShieldSweepHalfWidth);
        col = col * (1.0h + TreeSporeShieldRegenLift * regen) + TreeSporeShieldSweepColor * (band * band * regen);
    }

    // 가득 참 맥동
    half pulse = (half)saturate(_fx.w);
    col = col * (1.0h + TreeSporeShieldPulseGain * pulse) + TreeSporeShieldPulseColor * pulse;

    // 맞은 순간 번쩍임
    col = lerp(col, TreeSporeShieldFlashColor, (half)saturate(_fx.y) * TreeSporeShieldFlashMix);

    return lerp(_color, col, mask);
}

#endif // TREE_SPORE_SHIELD_INCLUDED
