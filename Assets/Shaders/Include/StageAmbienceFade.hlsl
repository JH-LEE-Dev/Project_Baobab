#ifndef STAGE_AMBIENCE_FADE_INCLUDED
#define STAGE_AMBIENCE_FADE_INCLUDED

// 스테이지 분위기 연출 전체를 서서히 끄고 켜는 값(로그라이크 지형 전환 때만 쓴다).
// _StageAmbienceFadeOut은 StageAmbienceFade가 채운다. 0 = 연출 그대로(본편은 늘 0, 한 번도 쓰지 않은 전역값도 0),
// 1 = 연출이 모두 꺼진 모습. 연출 셰이더는 마지막에 이 값으로 원래 색과 연출을 입힌 색 사이를 고른다.
float _StageAmbienceFadeOut;

// 새 지형의 연출이 파도를 따라 섬 중심에서 번져 켜지는 원(로그라이크 지형 전환 때만 쓴다).
// xy = 중심(월드), z = 반지름(바닥 평면 거리 - 아이소는 세로를 두 배로 편다), w = 가장자리가 옅어지는 폭.
// w가 0 이하면 원을 쓰지 않는다(본편은 늘 0, 한 번도 쓰지 않은 전역값도 0).
float4 _StageAmbienceReveal;

// _positionWS 자리에서 연출이 보이는 정도(1 = 그대로, 0 = 꺼짐).
float StageAmbienceVisibility(float2 _positionWS)
{
    float visibility = 1.0 - saturate(_StageAmbienceFadeOut);
    if (_StageAmbienceReveal.w > 0.0)
    {
        float2 d = _positionWS - _StageAmbienceReveal.xy;
        float distance = length(float2(d.x, d.y * 2.0));
        visibility *= 1.0 - smoothstep(_StageAmbienceReveal.z - _StageAmbienceReveal.w, _StageAmbienceReveal.z, distance);
    }

    return visibility;
}

// 연출을 입히기 전 색(_base)과 입힌 색(_effected) 사이. 보이는 정도가 1이면 입힌 색을 계산 오차 없이 그대로 돌려준다.
half3 StageAmbienceFadeColor(half3 _base, half3 _effected, float2 _positionWS)
{
    if (_StageAmbienceFadeOut <= 0.0 && _StageAmbienceReveal.w <= 0.0)
    {
        return _effected;
    }

    return lerp(_base, _effected, (half)StageAmbienceVisibility(_positionWS));
}

#endif // STAGE_AMBIENCE_FADE_INCLUDED
