using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 절차적 픽셀 이펙트가 화면 프레임마다가 아니라 고정 박자(초당 Rate번)로 계산·다시 그리게 하는 공용 시계.
    /// 천천히 일렁이는 픽셀 이펙트(나무 불타기, 열기 폭발, 바닥에 놓인 과열 불씨)는 낱장 애니메이션처럼 끊어 그려도 눈으로 구분하기 어렵고,
    /// 이렇게 하면 화면 프레임이 높아져도 이펙트 계산량과 메쉬 올리기가 늘지 않는다(60프레임 화면이면 절반).
    /// 인스턴스마다 박자의 어긋남(_phase)을 달리 줘서, 쉬는 프레임이 한꺼번에 몰리지 않고 프레임마다 고르게 나뉜다.
    /// 지나간 시간은 인스턴스가 직접 모아 두었다가 계산하는 프레임에 한꺼번에 흘리므로 이펙트의 길이·속도는 그대로다.
    /// 부모(나무)의 흔들림을 따라가는 변환 맞추기와 화면 왜곡 값처럼 싼 일은 쉬는 프레임에도 각 이펙트가 계속 한다.
    /// </summary>
    public static class PixelVfxClock
    {
        /// <summary>화면에 보이는 이펙트의 초당 계산 횟수.</summary>
        public const float Rate = 30.0f;

        /// <summary>
        /// 화면 밖 이펙트의 초당 계산 횟수. 화면 밖에서는 그리지 않고 입자 계산만 이어 가므로 더 낮춘다.
        /// 다시 보이는지는 계산하는 프레임에만 판정하므로, 한 박자(1/15초) 동안 카메라가 움직이는 거리가
        /// 화면 안/밖 판정의 여유(PixelVfxCulling.MarginUnits)보다 작아야 한다(카메라 최고 속도 약 10유닛/초 기준 0.67유닛).
        /// </summary>
        public const float OffScreenRate = 15.0f;

        /// <summary>끄면 예전처럼 화면 프레임마다 계산한다.</summary>
        public static bool bFixedRate = true;

        /// <summary>
        /// 이 프레임이 이 인스턴스가 계산할 차례인지. _lastTick은 인스턴스가 들고 있는 마지막 박자 번호(재생을 시작할 때 int.MinValue로 둔다),
        /// _phase는 인스턴스마다 다른 0~1 값, _rate는 초당 횟수다. 박자 수가 바뀌면(화면 안/밖 전환) 번호가 달라져 그 프레임에 바로 한 번 계산한다.
        /// </summary>
        public static bool ShouldStep(ref int _lastTick, float _phase, float _rate)
        {
            if (false == bFixedRate) return true;

            int tick = Mathf.FloorToInt(Time.time * _rate + _phase);
            if (tick == _lastTick) return false;

            _lastTick = tick;
            return true;
        }

        /// <summary>인스턴스 번호에서 0~1 사이의 고른 어긋남 값을 만든다.</summary>
        public static float PhaseFromInstance(int _instanceId)
        {
            uint hash = unchecked((uint)_instanceId * 2654435761u);
            return ((hash >> 12) & 0x3FF) / 1024.0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            bFixedRate = true;
        }
    }
}
