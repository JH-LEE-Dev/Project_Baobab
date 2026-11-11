#ifndef FOREST_BREATH_INCLUDED
#define FOREST_BREATH_INCLUDED

// 숲의 숨결. 화면 밖 먼 곳에서 출발한 느린 빛 물결 하나가 땅을 따라 화면을 쓸고 지나간다.
// 물결 자리는 StageForestBreath가 매 프레임 전역값으로 넘기고, 꽃 장식·물 셰이더가 같은 값을 읽는다.
// 숨쉬는 장식(StaticObj)은 StageForestBreath.Sample이 같은 식을 CPU에서 계산한다. 식을 바꾸면 둘 다 고친다.
// 물결이 없을 때는 _ForestBreathA.x가 0이라 아무것도 바꾸지 않는다.
// _ForestBreathA : 물결 진행 중(0/1), 물결 중심(월드 xy), 지금 반지름(땅 거리)
// _ForestBreathB : 차오름 폭, 잦아듦 폭, 앞머리 흔들림 폭(땅 거리), 흔들림 무늬 크기(월드)
// _ForestBreathC : 꽃 밝기, 물 반짝임 칸 비율(0~1), 물가 테두리 세기(0~1), 수면 옅은 밝아짐(0~1)
// _ForestBreathD : 물 반짝임 한 점 길이(땅 거리), 물결 전체 세기(0~1, 평소 1, 끝낼 때 서서히 줄인다), (안 씀), (안 씀)
// _ForestBreathE : 나무 별점 밝기, 나무 전체 밝기, 땅 달빛 띠 세기(0~1), 땅 이슬 반짝임 칸 비율(0~1)
// _ForestBreathF : 땅 이슬 반짝임 칸 크기(텍셀), 한 점 길이(땅 거리), 가장 밝을 때 밝기(HDR), (안 씀)
// _ForestBreathColor : 땅 달빛 띠·이슬 반짝임 색(선형)
// 땅 거리는 아이소메트릭 바닥 기준이라 화면 세로를 두 배로 친다(물결이 바닥에 누운 타원으로 퍼진다).

float4 _ForestBreathA;
float4 _ForestBreathB;
float4 _ForestBreathC;
float4 _ForestBreathD;
float4 _ForestBreathE;
float4 _ForestBreathF;
float4 _ForestBreathColor;

bool IsForestBreathActive()
{
    return _ForestBreathA.x > 0.5;
}

// 사인 없는 해시(CPU와 같은 값을 내기 위해)
float ForestBreathHash21(float2 _p)
{
    float3 p3 = frac(float3(_p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float ForestBreathNoise(float2 _p)
{
    float2 i = floor(_p);
    float2 f = frac(_p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = ForestBreathHash21(i);
    float b = ForestBreathHash21(i + float2(1.0, 0.0));
    float c = ForestBreathHash21(i + float2(0.0, 1.0));
    float d = ForestBreathHash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// 앞머리 흔들림을 빼고 잰 지난 거리(노이즈 없이 싸다). 흔들림은 ±_ForestBreathB.z 안이므로
// 진짜 지난 거리(ForestBreathPassed)는 늘 이 값 ±_ForestBreathB.z 안에 있다.
float ForestBreathRoughPassed(float2 _positionWS)
{
    float2 offset = _positionWS - _ForestBreathA.yz;
    offset.y *= 2.0;
    return _ForestBreathA.w - length(offset);
}

// 지난 거리가 (0, _maxPassed) 안에 들 수 있는 자리인지. false면 물결과 멀어 결과가 0으로 정해져 있으니 노이즈를 계산하지 않아도 된다.
bool ForestBreathMayReach(float2 _positionWS, float _maxPassed)
{
    float rough = ForestBreathRoughPassed(_positionWS);
    float margin = _ForestBreathB.z + 0.01;
    return rough > -margin && rough < _maxPassed + margin;
}

// 물결 앞머리가 이 자리를 지나간 뒤 얼마나 지났는지(땅 거리). 아직 안 닿았으면 0 이하.
float ForestBreathPassed(float2 _positionWS)
{
    float2 offset = _positionWS - _ForestBreathA.yz;
    offset.y *= 2.0;
    // 앞머리를 앞뒤로 흔들어 곧은 호가 아니라 숲 가장자리처럼 굽이진 선으로 만든다.
    float wobble = (ForestBreathNoise(_positionWS / max(_ForestBreathB.w, 0.5)) - 0.5) * 2.0 * _ForestBreathB.z;
    return _ForestBreathA.w - length(offset) - wobble;
}

// 물결이 지나간 거리(_passed)에서의 밝기(0~1). 빠르게 차올라 길게 잦아든다.
// 자리마다 물결 세기를 조금씩 달리해(0.65~1) 앞머리 전체가 한 덩어리로 밝아지지 않게 한다.
float ForestBreathProfile(float _passed, float2 _positionWS)
{
    float rise = max(_ForestBreathB.x, 0.05);
    float fall = max(_ForestBreathB.y, 0.05);
    if (_passed <= 0.0 || _passed >= rise + fall)
    {
        return 0.0;
    }

    float up = smoothstep(0.0, rise, _passed);
    float down = 1.0 - smoothstep(rise, rise + fall, _passed);
    float strength = lerp(0.65, 1.0, ForestBreathNoise(_positionWS / (max(_ForestBreathB.w, 0.5) * 1.7) + 40.0));
    return up * down * strength * saturate(_ForestBreathD.y);
}

// 이 자리의 숨결 밝기(0~1). 물결이 없으면 0.
float SampleForestBreath(float2 _positionWS)
{
    if (false == IsForestBreathActive())
    {
        return 0.0;
    }
    // 물결에서 먼 자리(화면 대부분)는 노이즈 없이 바로 0
    if (false == ForestBreathMayReach(_positionWS, max(_ForestBreathB.x, 0.05) + max(_ForestBreathB.y, 0.05)))
    {
        return 0.0;
    }

    return ForestBreathProfile(ForestBreathPassed(_positionWS), _positionWS);
}

// 물결이 지나갈 때 흩뿌려지는 반짝임(물·땅 공용). 텍셀 격자를 _cellSize 칸으로 나눠 칸마다 한 점을 두고,
// _coverage 비율의 칸만 물결 앞머리가 지난 뒤 칸마다 다른 때에 한 번 반짝인다(앞머리 가까이 몰려 앞머리가 반짝이며 지나간다).
// 반환: x = 이 텍셀이 점 한가운데일 때 밝기(0~1), y = 점에서 맨해튼 거리 1인 십자 팔일 때 밝기(0~1). 해당 없으면 0.
float2 ForestBreathGlint(float2 _texelId, float _cellSize, float _ppu, float _coverage, float _length, float _salt)
{
    float2 cell = floor(_texelId / _cellSize);
    if (ForestBreathHash21(cell + 23.9 + _salt) >= _coverage)
    {
        return 0.0;
    }

    float2 spot = cell * _cellSize + 1.0 + floor(float2(ForestBreathHash21(cell + 51.3 + _salt), ForestBreathHash21(cell + 77.1 + _salt)) * (_cellSize - 2.0));
    float2 d = abs(_texelId - spot);
    float manhattan = d.x + d.y;
    if (manhattan > 1.5)
    {
        return 0.0;
    }

    float2 spotWS = (spot + 0.5) / _ppu;
    // 늦게 반짝이는 점도 잦아듦 폭의 앞쪽 30% 안에서 반짝인다.
    float span = _ForestBreathB.x + _ForestBreathB.y * 0.3;
    float glintLength = max(_length, 0.05);
    // 물결에서 먼 점은 노이즈 없이 바로 끝낸다(반짝이는 때는 지난 거리 0 ~ span + 길이 안).
    if (false == ForestBreathMayReach(spotWS, span + glintLength))
    {
        return 0.0;
    }
    float passed = ForestBreathPassed(spotWS);
    float delay = ForestBreathHash21(cell + 61.7 + _salt) * span;
    float t = (passed - delay) / glintLength;
    if (t <= 0.0 || t >= 1.0)
    {
        return 0.0;
    }

    float fade = sin(t * 3.14159) * (0.45 + 0.55 * ForestBreathProfile(passed, spotWS)) * saturate(_ForestBreathD.y);
    return manhattan < 0.5 ? float2(fade, 0.0) : float2(0.0, fade);
}

#endif // FOREST_BREATH_INCLUDED
