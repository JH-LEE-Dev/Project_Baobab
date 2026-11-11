#ifndef OVERHEAT_BURST_WAVE_INCLUDED
#define OVERHEAT_BURST_WAVE_INCLUDED

// 과열이 가득 차는 순간 캐릭터 발밑에서 퍼져 나가는 열기 물결. 렌더링 전용이다.
// 전역값은 StageOverheatBurst가 물결이 도는 동안만 매 프레임 채우고, 끝나면 0으로 돌린다.
// 한 번도 채워지지 않은 곳(로그라이크·에디터 등)은 w가 0이라 아무것도 바꾸지 않는다.
// _OverheatBurstWave : xy = 진원(월드), z = 물결 앞머리까지의 거리(바닥 평면 거리), w = 세기(0~1)
float4 _OverheatBurstWave;

// 앞머리가 지나간 뒤 이 거리(바닥 평면 거리)만큼 달궈진 채로 남았다가 식는다.
static const float OverheatBurstBand = 2.6;
// 물결이 닿은 나무를 달구는 빛의 색 비율(선형, 빨강 > 초록 > 파랑이라 주황으로 물든다)과 세기.
// 나무 색에 곱하는 배율이 1 + 색 비율 × 세기 × 달궈진 정도다. 곱하기만 하므로 픽셀끼리의 명암 비율과 검은 외곽선이 그대로 남고,
// 밝은 픽셀일수록 더 밝아져 블룸 임계값(1.345)을 넘겨 번진다.
static const half3 OverheatBurstFlareColor = half3(2.7, 1.3, 0.3);
static const half OverheatBurstFlareGain = 5.0h;

// 바닥 평면 거리 - 아이소 화면에서는 세로가 절반으로 눌려 보이므로 세로 차이를 두 배로 잰다.
float OverheatBurstGroundDistance(float2 _worldPos)
{
    float2 delta = _worldPos - _OverheatBurstWave.xy;
    delta.y *= 2.0;
    return length(delta);
}

// 이 자리가 물결에 달궈진 정도(0~1). 앞머리가 닿는 순간 가장 뜨겁고 지나갈수록 식는다.
float OverheatBurstHeatAt(float2 _worldPos)
{
    if (_OverheatBurstWave.w <= 0.0)
    {
        return 0.0;
    }

    float behind = _OverheatBurstWave.z - OverheatBurstGroundDistance(_worldPos);
    if (behind < 0.0)
    {
        return 0.0;
    }

    return _OverheatBurstWave.w * saturate(1.0 - behind / OverheatBurstBand);
}

// 나무용 [정점 단계]: 이 나무가 물결에 달궈진 정도(0~1). _pivotWorld는 나무 밑동이라 나무 한 그루가 같은 값을 쓴다.
// 네 단계로 끊어 식힌다(픽셀 그림이라 부드럽게 옅어지는 것보다 단계로 떨어지는 편이 어울린다).
// 가장 가까운 단계로 맞춰서, 물결이 흩어지며 약해진 끝자락에서는 나무가 마지막 단계에 걸린 채 남지 않고 같이 꺼진다.
float OverheatBurstFlareHeatAt(float2 _pivotWorld)
{
    return floor(OverheatBurstHeatAt(_pivotWorld) * 4.0 + 0.5) / 4.0;
}

// 나무용 [픽셀 단계]: 물결이 닿은 나무가 통째로 달아올랐다가 식는다. _heat는 OverheatBurstFlareHeatAt이 구한 값.
half3 ApplyOverheatBurstFlare(half3 _color, float _heat)
{
    if (_heat <= 0.0)
    {
        return _color;
    }

    // 나무 그림의 명암은 남긴 채 달군다. 빛 색을 더하면 아주 어두운 4스테이지 나무 색(선형 0.02~0.05)이 그 값에 덮여
    // 명암 없는 납작한 덩어리가 되므로, 더하지 않고 곱한다.
    return _color * (1.0h + OverheatBurstFlareColor * (OverheatBurstFlareGain * (half)_heat));
}

#endif // OVERHEAT_BURST_WAVE_INCLUDED
