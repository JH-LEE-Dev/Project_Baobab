#ifndef TREE_STAR_TWINKLE_INCLUDED
#define TREE_STAR_TWINKLE_INCLUDED

#include "StageAmbienceFade.hlsl"

// 나무 별빛 반짝임. 나무 그림에서 별점 색 픽셀만 골라, 점마다 다른 박자로 잠깐 밝아졌다 평소 밝기로 돌아오게 한다.
// 그림의 색을 바꾸지 않고 밝기만 곱한다. 점 하나는 텍스처 픽셀 하나라서, 텍셀 자리(월드 픽셀 격자)로 박자를 정한다.
// 나무 셰이더(Custom-Sprite-Default-Tree)가 _MainTex 선언 뒤에 include 한다.
// 전역값은 StageAmbienceShaderGlobals가 스테이지 데이터대로 채운다. 꺼진 스테이지는 _TreeTwinkleA.x가 0이라
// 텍스처를 더 읽지도 않고 아무것도 바꾸지 않는다.
// _TreeTwinkleA : 켜짐(0/1), 평소 밝기 배율, 가장 밝을 때 배율, 평균 주기(초)
// _TreeTwinkleB : 별점 색(sRGB rgb), 판정 허용 오차
// _TreeTwinkleC : 밝아져 있는 시간 비율(0~1), 뜨거운 픽셀 최소 빨강(0이면 끔), 뜨거운 픽셀 빨강이 초록·파랑보다 큰 최소 차, 나무 단위 박자(0/1)
//                 뜨거운 픽셀 판정을 켜면 별점 색이 아니어도 빨강이 강한 픽셀(불씨·용암빛)이 함께 반짝인다.
//                 나무 단위 박자를 켜면 점마다가 아니라 그림(나무) 하나가 한 박자로 함께 밝아진다(숨쉬기).
float4 _TreeTwinkleA;
float4 _TreeTwinkleB;
float4 _TreeTwinkleC;

bool IsTreeStarTwinkleEnabled()
{
    return _TreeTwinkleA.x > 0.5;
}

float TreeStarTwinkleHash21(float2 _p)
{
    float3 p3 = frac(float3(_p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// _color: 조명까지 끝난 색, _uv: 픽셀에 맞춘 텍스처 좌표, _positionWS: 월드 위치, _pivotWS: 그림(나무) 기준점 월드 위치
half3 ApplyTreeStarTwinkle(half3 _color, float2 _uv, float2 _positionWS, float2 _pivotWS)
{
    if (false == IsTreeStarTwinkleEnabled())
    {
        return _color;
    }

    half3 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, _uv).rgb;
#if !defined(UNITY_COLORSPACE_GAMMA)
    // 판정용이라 근사로 충분하다(Color.hlsl을 끌어오지 않는다).
    tex = pow(max(tex, 0.0h), 1.0h / 2.2h);
#endif
    half3 d = abs(tex - (half3)_TreeTwinkleB.rgb);
    bool isStar = max(d.r, max(d.g, d.b)) <= (half)_TreeTwinkleB.w;
    bool isHot = _TreeTwinkleC.y > 0.0 && tex.r >= (half)_TreeTwinkleC.y && tex.r - max(tex.g, tex.b) >= (half)_TreeTwinkleC.z;
    if (false == isStar && false == isHot)
    {
        return _color;
    }

    // 텍셀 하나가 한 번호를 갖도록 월드 픽셀 격자로 내림한다(나무 셰이더가 UV를 텍셀 중심에 맞춰 둔다).
    // 나무 단위 박자면 그림 기준점으로 번호를 정해 그림 전체가 같은 박자다.
    // 나무는 가로 0.5·세로 0.25 격자 위에 서 있고 피격 흔들림(옆으로 0.1)은 격자 반 칸보다 작으므로,
    // 기준점을 그 격자로 되돌려 쓰면 흔들리는 동안에도 박자가 바뀌지 않는다(서 있을 때는 값이 그대로다).
    float2 pivotOnGrid = round(_pivotWS * float2(2.0, 4.0)) / float2(2.0, 4.0);
    float2 texelId = _TreeTwinkleC.w > 0.5 ? floor(pivotOnGrid * 32.0) : floor(_positionWS * 32.0);
    float period = max(_TreeTwinkleA.w, 0.1) * (0.7 + 0.6 * TreeStarTwinkleHash21(texelId + 3.1));
    float local = frac(_Time.y / period + TreeStarTwinkleHash21(texelId + 9.4));
    float duty = max(_TreeTwinkleC.x, 0.01);
    float pulse = local < duty ? sin(local / duty * 3.14159) : 0.0;
    // 로그라이크 지형 전환 때 서서히 끄고 켠다(StageAmbienceFade.hlsl, 본편은 늘 그대로).
    return StageAmbienceFadeColor(_color, _color * (half)lerp(_TreeTwinkleA.y, _TreeTwinkleA.z, pulse), _positionWS);
}

#endif // TREE_STAR_TWINKLE_INCLUDED
