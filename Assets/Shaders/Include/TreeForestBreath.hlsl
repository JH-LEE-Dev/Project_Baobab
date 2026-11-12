#ifndef TREE_FOREST_BREATH_INCLUDED
#define TREE_FOREST_BREATH_INCLUDED

// 숲의 숨결(ForestBreath.hlsl) 물결이 나무 밑동에 닿으면 나무 전체가 달빛을 받은 듯 살짝 밝아지고,
// 별점(나무 별빛 반짝임의 별점 색 픽셀)은 하나씩 차례로 반짝인다. 지나가면 원래대로 돌아온다.
// 나무 셰이더(Custom-Sprite-Default-Tree)가 TreeStarTwinkle.hlsl 뒤에 include 한다(별점 색 판정을 함께 쓴다).
// 물결이 없거나 꺼진 스테이지는 _ForestBreathA.x가 0이라 텍스처를 더 읽지도 않고 아무것도 바꾸지 않는다.
#include "ForestBreath.hlsl"

// _color: 조명까지 끝난 색, _uv: 픽셀에 맞춘 텍스처 좌표, _positionWS: 월드 위치, _pivotWS: 그림(나무) 기준점 월드 위치
half3 ApplyTreeForestBreath(half3 _color, float2 _uv, float2 _positionWS, float2 _pivotWS)
{
    if (false == IsForestBreathActive())
    {
        return _color;
    }

    // 물결은 땅을 따라 퍼지므로 밑동 자리로 잰다. 피격 흔들림에도 값이 바뀌지 않게 나무가 서는 격자로 되돌린다(별빛 반짝임과 같다).
    float2 pivotOnGrid = round(_pivotWS * float2(2.0, 4.0)) / float2(2.0, 4.0);
    // 별점이 늦게 반짝이는 몫까지 지나면 끝난다. 배수(1.5)는 StageForestBreath.TreeStarSpreadRatio와 같아야 한다(물결 끝 판정).
    float starSpread = _ForestBreathB.x * 1.5;
    float maxPassed = _ForestBreathB.x + _ForestBreathB.y + starSpread;
    // 물결에서 먼 나무는 노이즈 없이 바로 그대로 둔다.
    if (false == ForestBreathMayReach(pivotOnGrid, maxPassed))
    {
        return _color;
    }
    float passed = ForestBreathPassed(pivotOnGrid);
    if (passed <= 0.0 || passed >= maxPassed)
    {
        return _color;
    }

    float glow = _ForestBreathE.y * ForestBreathProfile(passed, pivotOnGrid);

    // 별점: 점마다 조금씩 늦게 차올라 나무 안에서 별이 하나둘 켜지듯 반짝인다.
    if (_ForestBreathE.x > 0.0 && IsTreeStarTwinkleEnabled())
    {
        half3 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, _uv).rgb;
#if !defined(UNITY_COLORSPACE_GAMMA)
        tex = pow(max(tex, 0.0h), 1.0h / 2.2h);
#endif
        half3 d = abs(tex - (half3)_TreeTwinkleB.rgb);
        if (max(d.r, max(d.g, d.b)) <= (half)_TreeTwinkleB.w)
        {
            float2 texelId = floor(_positionWS * 32.0);
            float delay = ForestBreathHash21(texelId + 71.3) * starSpread;
            float star = ForestBreathProfile(passed - delay, pivotOnGrid);
            glow += _ForestBreathE.x * star * star;
        }
    }

    return _color * (half)(1.0 + glow);
}

#endif // TREE_FOREST_BREATH_INCLUDED
