namespace PresentationLayer.VFX
{
    /// <summary>
    /// 드론 레이저(VFX_DroneLaser)와 타격 이펙트(VFX_LaserHit)에 입히는 색 계열.
    /// Default는 기존 색(평소 노랑, 과열 파랑)을 그대로 쓰고, Stella는 스텔라 + 드론 조합의 푸른색이다(색 값은 과열 파랑과 같다).
    /// </summary>
    public enum EDroneLaserTint
    {
        Default = 0,
        Stella = 1,
    }
}
