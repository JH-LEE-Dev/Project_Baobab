using System;
using UnityEngine;

public class UIView_OverUIPopup : UIView
{
    // 이벤트
    public event Action CompanyLogoProductionCompletedEvent;

    // 내부 의존성
    [Header("Opening Production")]
    [SerializeField] private UI_OpeningProduction openingProduction;

    [Header("Tutorial Quest")]
    [SerializeField] private UI_TutorialQuest tutorialQuest;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);
        if (null != openingProduction)
        {
            openingProduction.Initialize(_ctx?.localizationManager);
        }

        if (null != tutorialQuest)
        {
            tutorialQuest.Initialize(_ctx?.localizationManager);
        }
    }

    public override void SetupUI()
    {
        base.SetupUI();
    }

    public void PlayCompanyLogo()
    {
        if (null != openingProduction)
        {
            openingProduction.PlayIntroScene(OnIntroSceneCompleted);
        }
    }

    /// <summary>
    /// MainMenu → Dungeon 튜토리얼 인트로(로고 연출 → 하차 → HUD 복귀)가 전부 끝났음을 알린다.
    /// HUD가 완전히 다 올라온 시점에 GameplayUICoordinator가 호출한다.
    /// 인트로 이후로 이어지는 UI 연출은 여기서 시작하면 된다.
    /// </summary>
    public void IntroProductionEnded()
    {
    }

    /// <summary>
    /// 튜토리얼 스텝이 시작됨 - 해당 스텝의 안내 UI를 여기서 띄우면 된다.
    /// </summary>
    public void TutorialStepStarted(TutorialStep _step)
    {
        if (null != tutorialQuest)
        {
            tutorialQuest.OnTutorialStepStarted(_step);
        }
    }

    /// <summary>
    /// 튜토리얼 스텝이 완료됨 - 해당 스텝의 안내 UI를 여기서 내리면 된다.
    /// </summary>
    public void TutorialStepCompleted(TutorialStep _step)
    {
        if (null != tutorialQuest)
        {
            tutorialQuest.OnTutorialStepCompleted(_step);
        }
    }

    public override void Release()
    {
        base.Release();
        CompanyLogoProductionCompletedEvent = null;

        if (null != tutorialQuest)
        {
            tutorialQuest.ResetQuest();
        }
    }

    public override void Refresh()
    {
        base.Refresh();
    }

    protected override void OnShow()
    {
        base.OnShow();
    }

    protected override void OnHide()
    {
        base.OnHide();
        if (null != openingProduction)
        {
            openingProduction.StopOpeningProduction();
        }

        if (null != tutorialQuest)
        {
            tutorialQuest.ResetQuest();
        }
    }

    private void OnIntroSceneCompleted()
    {
        CompanyLogoProductionCompletedEvent?.Invoke();
    }

    protected override void Awake()
    {
        base.Awake();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        CompanyLogoProductionCompletedEvent = null;

        if (null != tutorialQuest)
        {
            tutorialQuest.ResetQuest();
        }
    }
}
