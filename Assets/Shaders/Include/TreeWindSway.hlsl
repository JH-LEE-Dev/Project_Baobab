#ifndef TREE_WIND_SWAY_INCLUDED
#define TREE_WIND_SWAY_INCLUDED

// 트리 바람 흔들림 버텍스 변위 함수.
// 오브젝트의 월드 위치를 해시하여 개체별 고유 위상을 생성하고,
// 사인파 조합으로 자연스러운 흔들림을 만든다.
// 모든 트리 셰이더(본체, 그림자, 아웃라인, 스텐실)가 이 함수를 공유하여
// 동일한 sway 결과를 보장한다. 지진 떨림(ApplyQuakeShake)도 같은 자리에서 더한다.

// 오브젝트 월드 위치를 해시하여 0~2π 범위의 고유 위상값을 생성한다.
// 같은 트리의 모든 렌더러가 동일한 위상을 갖도록 월드 좌표를 정수 단위로 스냅한다.
float ComputeSwayPhase(float3 _objectWorldPos)
{
    float2 snapped = round(_objectWorldPos.xy);
    return frac(sin(dot(snapped, float2(12.9898, 78.233))) * 43758.5453) * 6.2832;
}

// 지진 나무 떨림(QuakeTileRipple이 지진 동안만 채우는 전역값). 물결 앞머리가 나무 피봇에 닿으면 잠깐 바르르 떤다.
// 지진이 없으면 _QuakeShakeB.x·_QuakeShakeD.x가 0이라 버텍스를 그대로 돌려준다(바람 흔들림과 무관하게 동작).
// _QuakeShakeA : 진원 x, 진원 y, 앞머리 거리(바닥 평면 거리 - 세로 차이를 두 배로 편 거리), 피해 범위(0이면 없음)
// _QuakeShakeB : 떨림 세기(0 = 꺼짐), 앞머리 속도(거리/초), 물결이 닿는 거리, 피해 범위 경계 폭(떨림 각도가 범위 안→밖으로 바뀌는 폭, 범위가 한가운데)
// _QuakeShakeC : 범위 안 떨림 각도(도), 범위 밖 떨림 각도(도, 물결이 닿는 거리로 갈수록 0), 떨림 시간(초), 떨림 진동수(Hz)
// _QuakeShakeD : x = 물결 위 나무 높이 풀기(1 = 켜짐, QuakeObjectRide가 나무를 옮겨 그리는 동안)
float4 _QuakeShakeA;
float4 _QuakeShakeB;
float4 _QuakeShakeC;
float4 _QuakeShakeD;

// QuakeObjectRide가 물결 위 나무를 칸 높이만큼 옮겨 그리는 동안, 나무 루트 z에 (옮긴 픽셀 × 16 + 지진에 맞아 튀는 픽셀) × 이 단위를 담는다.
// 나무는 z 0에 놓이고 렌더러는 모두 루트 자리라 피봇 z로 바로 푼다. 원래 자리로 위상·진원 거리를 구해야 옮기는 동안 위상이 튀지 않고
// 거리가 피해 판정(나무 자리)과 같다.
#define QUAKE_RIDE_Z_STEP (1.0 / 4096.0)
#define QUAKE_RIDE_CODE_BASE 16.0

// 그림자처럼 바닥에 붙어 있어야 하는 셰이더는 include 전에 0으로 정의해 튀지 않고 떨기만 한다.
#ifndef TREE_QUAKE_POP_SCALE
#define TREE_QUAKE_POP_SCALE 1.0
#endif

// _objectWorldPos(그리는 피봇)에서 물결에 옮기기 전 자리(_groundPos)와 지금 튀는 높이(_popY, 월드)를 푼다. 지진이 아니면 그대로·0.
void DecodeQuakeRide(float3 _objectWorldPos, out float3 _groundPos, out float _popY)
{
    _groundPos = _objectWorldPos;
    _popY = 0.0;

    if (_QuakeShakeD.x > 0.5)
    {
        float code = round(_objectWorldPos.z / QUAKE_RIDE_Z_STEP);
        float pop = code - QUAKE_RIDE_CODE_BASE * floor(code / QUAKE_RIDE_CODE_BASE);
        float ride = (code - pop) / QUAKE_RIDE_CODE_BASE;
        _groundPos.y -= ride / 32.0;
        _groundPos.z = 0.0;
        _popY = pop / 32.0;
    }
}

// _groundPos는 물결에 옮기기 전 피봇(DecodeQuakeRide), _popY는 튀는 높이.
float3 ApplyQuakeShake(float3 _positionOS, float3 _groundPos, float _popY)
{
    float3 result = _positionOS;

    if (_QuakeShakeB.x > 0.0)
    {
        float2 offset = _groundPos.xy - _QuakeShakeA.xy;
        offset.y *= 2.0;
        float dist = length(offset);
        // 앞머리가 이 나무에 닿은 뒤 지난 시간(초).
        float t = (_QuakeShakeA.z - dist) / max(_QuakeShakeB.y, 0.1);
        float shiverSeconds = max(_QuakeShakeC.z, 0.01);

        if (t > 0.0 && t < shiverSeconds)
        {
            float strike = _QuakeShakeA.w;
            float halfEdge = 0.5 * max(_QuakeShakeB.w, 0.01);
            float inside = strike > 0.0 ? 1.0 - smoothstep(strike - halfEdge, strike + halfEdge, dist) : 0.0;
            float reachFade = saturate(1.0 - dist / max(_QuakeShakeB.z, 0.1));
            float shiverDeg = lerp(_QuakeShakeC.y * reachFade, _QuakeShakeC.x, inside) * _QuakeShakeB.x;

            float envelope = 1.0 - t / shiverSeconds;
            envelope *= envelope;
            float angle = radians(shiverDeg) * envelope * sin(_Time.y * 6.2832 * _QuakeShakeC.w + ComputeSwayPhase(_groundPos));
            float sinR, cosR;
            sincos(angle, sinR, cosR);

            result.x = _positionOS.x * cosR - _positionOS.y * sinR;
            result.y = _positionOS.x * sinR + _positionOS.y * cosR;
        }
    }

    // 지진에 맞은 나무가 튀는 높이(QuakeObjectRide가 맞은 시각으로 정한다).
    result.y += _popY * TREE_QUAKE_POP_SCALE;

    return result;
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
    float3 result = _positionOS;

    // 지진 물결에 옮겨 그리는 동안에도 위상은 원래 자리로 구한다(평소에는 _objectWorldPos 그대로).
    float3 groundPos;
    float popY;
    DecodeQuakeRide(_objectWorldPos, groundPos, popY);

    if (_enableWindSway >= 0.5)
    {
        float phase = ComputeSwayPhase(groundPos);
        float time = _Time.y;

        float mainWave = sin(time * _mainSpeed + phase);
        float detailWave = sin(time * _detailSpeed + phase * 1.73) * _detailWeight;
        float sway = mainWave + detailWave;

        // 오브젝트 피봇 기준 Z축 회전 (2D 평면)
        float rotAngle = -sway * radians(_rotAmplitudeDeg);
        float sinR, cosR;
        sincos(rotAngle, sinR, cosR);

        result.x = _positionOS.x * cosR - _positionOS.y * sinR;
        result.y = _positionOS.x * sinR + _positionOS.y * cosR;

        // X축 위치 오프셋
        result.x += sway * _posAmplitude;
    }

    return ApplyQuakeShake(result, groundPos, popY);
}

#endif // TREE_WIND_SWAY_INCLUDED
