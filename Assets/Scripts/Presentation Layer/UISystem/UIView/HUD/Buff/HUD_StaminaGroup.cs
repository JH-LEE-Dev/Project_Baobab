using UnityEngine;

/// <summary>
/// 스태미나 바와 버프 줄을 하나의 HUD 단위로 묶는 조합 프리팹의 루트. 각 프리팹은 독립적으로 유지하고
/// 이 컴포넌트가 초기화 순서와 상호 주입(경고 틴트 동기화)만 중계한다.
/// </summary>
public class HUD_StaminaGroup : MonoBehaviour
{
    // 내부 의존성
    [SerializeField] private HUD_Stemina stamina;
    [SerializeField] private HUD_BuffBar buffBar;
    [SerializeField] private HUD_DashGauge dashGauge;

    public HUD_Stemina Stamina
    {
        get { return stamina; }
    }

    public HUD_DashGauge DashGauge
    {
        get { return dashGauge; }
    }

    public HUD_BuffBar BuffBar
    {
        get { return buffBar; }
    }

    public void Initialize(HUD_ScreenBlood _screenBlood)
    {
        if (null != stamina)
        {
            stamina.Initialize(_screenBlood);
        }

        if (null != buffBar)
        {
            buffBar.Initialize(stamina);
        }
    }
}
