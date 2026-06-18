#ifndef TREE_WIND_SWAY_INCLUDED
#define TREE_WIND_SWAY_INCLUDED

// 트리 바람 흔들림 버텍스 변위 함수.
// 오브젝트의 월드 위치를 해시하여 개체별 고유 위상을 생성하고,
// 사인파 조합으로 자연스러운 흔들림을 만든다.
// 모든 트리 셰이더(본체, 그림자, 아웃라인, 스텐실)가 이 함수를 공유하여
// 동일한 sway 결과를 보장한다.

// 오브젝트 월드 위치를 해시하여 0~2π 범위의 고유 위상값을 생성한다.
// 같은 트리의 모든 렌더러가 동일한 위상을 갖도록 월드 좌표를 정수 단위로 스냅한다.
float ComputeSwayPhase(float3 _objectWorldPos)
{
    float2 snapped = round(_objectWorldPos.xy);
    return frac(sin(dot(snapped, float2(12.9898, 78.233))) * 43758.5453) * 6.2832;
}

// 바람 흔들림 버텍스 변위를 적용한다.
// _positionOS      : 오브젝트 공간 버텍스 위치
// _objectWorldPos  : 오브젝트 피봇의 월드 위치 (위상 해시용)
// _enableWindSway  : 0 = 비활성, 1 = 활성
// _posAmplitude    : X축 위치 진폭 (월드 단위)
// _rotAmplitudeDeg : Z축 회전 진폭 (도 단위)
// _mainSpeed       : 주파형 속도
// _detailSpeed     : 세부파형 속도
// _detailWeight    : 세부파형 가중치
float3 ApplyWindSway(
    float3 _positionOS,
    float3 _objectWorldPos,
    float _enableWindSway,
    float _posAmplitude,
    float _rotAmplitudeDeg,
    float _mainSpeed,
    float _detailSpeed,
    float _detailWeight)
{
    if (_enableWindSway < 0.5)
        return _positionOS;

    float phase = ComputeSwayPhase(_objectWorldPos);
    float time = _Time.y;

    float mainWave = sin(time * _mainSpeed + phase);
    float detailWave = sin(time * _detailSpeed + phase * 1.73) * _detailWeight;
    float sway = mainWave + detailWave;

    // 오브젝트 피봇 기준 Z축 회전 (2D 평면)
    float rotAngle = -sway * radians(_rotAmplitudeDeg);
    float sinR, cosR;
    sincos(rotAngle, sinR, cosR);

    float3 result;
    result.x = _positionOS.x * cosR - _positionOS.y * sinR;
    result.y = _positionOS.x * sinR + _positionOS.y * cosR;
    result.z = _positionOS.z;

    // X축 위치 오프셋
    result.x += sway * _posAmplitude;

    return result;
}

#endif // TREE_WIND_SWAY_INCLUDED
