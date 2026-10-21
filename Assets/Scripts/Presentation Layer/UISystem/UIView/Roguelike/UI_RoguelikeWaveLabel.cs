using TMPro;
using UnityEngine;

/// <summary>
/// 로그라이크 전투 HUD의 웨이브 표시. 목표치 바 위 왼쪽 끝에 "웨이브 12/30"(30웨이브 이후는 "Unlimit-3")을 그린다.
/// 문구는 LocalizationManager 형식 문자열(LocKeys.RoguelikeUI.roguelikeHudWave / roguelikeHudUnlimit)로 만든다. 웨이브가 바뀔 때와 언어가 바뀔 때만 다시 만든다.
/// 글자는 임계 바 숫자와 같은 방식이다: 밝은 크림색 본문 1장 + 1px 짙은 갈색 외곽선 4장(네 방향 복사본), 모두 같은 문구.
///
/// [웨이브가 바뀔 때] 24Hz 틱으로 위로 { +2, +3, +1, 0 }px 튀어 오르고(스텝당 3틱), 처음 6틱은 본문이 흰색과 크림색으로 번갈아 번쩍인다.
///   정수 픽셀 이동이고, 시간은 unscaled라 ESC로 멈춰도 이어진다. 처음 표시될 때(HUD가 켜질 때)는 튀지 않는다.
/// </summary>
public class UI_RoguelikeWaveLabel : MonoBehaviour
{
    private const int TotalWaves = 30;
    private const float TicksPerSecond = 24f;
    private const int StepTicks = 3;
    private const int FlashTicks = 6;
    private static readonly int[] HopPattern = { 2, 3, 1, 0 };

    //내부 의존성 (프리팹에서 연결)
    [SerializeField] private RectTransform labelRoot;
    [SerializeField, Tooltip("앞의 넷은 1px 외곽선(네 방향), 마지막이 본문. 모두 같은 문구를 받는다.")]
    private TMP_Text[] texts = new TMP_Text[0];
    [SerializeField] private Color mainColor = new Color(1f, 0.945f, 0.79f, 1f);
    [SerializeField] private Color flashColor = Color.white;

    //외부 의존성
    private LocalizationManager localizationManager;

    //상태 변수
    private int wave;
    private bool bUnlimit;
    private int unlimitIndex;
    private bool bHasWave;
    private float clock;
    private float hopStartClock = -1f;
    private Vector2 basePosition;
    private bool bBasePositionCaptured;

    public void Initialize(LocalizationManager _localizationManager)
    {
        localizationManager = _localizationManager;
        RefreshText();
    }

    /// <summary>웨이브 표기. 언리밋이면 _unlimitIndex로 "Unlimit-N"을 그린다. 같은 웨이브가 다시 오면 아무것도 하지 않는다.</summary>
    public void SetWave(int _waveNumber, bool _bUnlimit, int _unlimitIndex)
    {
        bool bChanged = false == bHasWave || wave != _waveNumber || bUnlimit != _bUnlimit || unlimitIndex != _unlimitIndex;
        if (false == bChanged) return;

        bool bFirst = false == bHasWave;
        wave = _waveNumber;
        bUnlimit = _bUnlimit;
        unlimitIndex = _unlimitIndex;
        bHasWave = true;

        RefreshText();
        if (false == bFirst) PlayChanged();
    }

    /// <summary>언어가 바뀌었을 때 다시 그린다.</summary>
    public void RefreshText()
    {
        if (false == bHasWave) return;

        string label;
        if (true == bUnlimit) label = null != localizationManager ? localizationManager.GetFormatText(LocKeys.RoguelikeUI.roguelikeHudUnlimit, unlimitIndex) : string.Empty;
        else label = null != localizationManager ? localizationManager.GetFormatText(LocKeys.RoguelikeUI.roguelikeHudWave, wave, TotalWaves) : string.Empty;

        if (true == string.IsNullOrEmpty(label)) label = true == bUnlimit ? string.Concat("Unlimit-", unlimitIndex.ToString()) : string.Concat(wave.ToString(), "/", TotalWaves.ToString());

        for (int i = 0; i < texts.Length; i++)
        {
            if (null != texts[i]) texts[i].SetText(label);
        }
    }

    public void SetVisible(bool _bVisible)
    {
        gameObject.SetActive(_bVisible);
    }

    /// <summary>_dt초만큼 진행한다(Update가 부른다). 편집기 검증에서도 이 메서드로 시간대별 모습을 만들 수 있다.</summary>
    public void Step(float _dt)
    {
        clock += _dt;

        if (hopStartClock < 0f)
        {
            return;
        }

        int tick = (int)((clock - hopStartClock) * TicksPerSecond);
        int step = tick / StepTicks;

        if (HopPattern.Length <= step)
        {
            hopStartClock = -1f;
            ApplyHop(0, false);
            return;
        }

        ApplyHop(HopPattern[step], tick < FlashTicks && 0 == tick % 2);
    }

    // // 내부

    private void PlayChanged()
    {
        hopStartClock = clock;
        Step(0f);
    }

    private void ApplyHop(int _offset, bool _bFlash)
    {
        if (null != labelRoot)
        {
            if (false == bBasePositionCaptured)
            {
                basePosition = labelRoot.anchoredPosition;
                bBasePositionCaptured = true;
            }

            labelRoot.anchoredPosition = new Vector2(basePosition.x, basePosition.y + _offset);
        }

        if (0 < texts.Length && null != texts[texts.Length - 1]) texts[texts.Length - 1].color = true == _bFlash ? flashColor : mainColor;
    }

    private void Update()
    {
        Step(Mathf.Min(Time.unscaledDeltaTime, 1f / 20f));
    }

    private void OnDisable()
    {
        hopStartClock = -1f;
        ApplyHop(0, false);
    }
}
