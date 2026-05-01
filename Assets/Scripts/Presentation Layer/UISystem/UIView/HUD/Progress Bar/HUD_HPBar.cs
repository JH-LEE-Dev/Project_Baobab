using System;
using PresentationLayer.ObjectSystem;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;

/// <summary>
/// HUD에서 HP 바 등 캐릭터의 상태를 추적하며 표시하는 프로그레스 바입니다.
/// </summary>
public class HUD_HPBar : HUD_ProgressBar, IPoolable
{
    // //내부 의존성
    private RectTransform rect;
    private Camera mainCam;

    private float activeTimer = 0.0f;
    private float showYOffset = 0.0f;
    private bool isTimerActive = false;

    private Action<HUD_HPBar> onHideCallback;

    private GameObject targetObj;

    private Tween chargeTween;

    // //퍼블릭 초기화 및 제어 메서드

    public override void Initialize()
    {
        base.Initialize();

        if (null != progressSlider)
            rect = progressSlider.GetComponent<RectTransform>();

        if (null == mainCam)
            mainCam = Camera.main;
    }

    public void UpdateTargetObj(GameObject _target)
    {
        targetObj = _target;
    }

    public void UpdateYOffset(float _in)
    {
        showYOffset = _in;
    }

    /// <summary>
    /// 지정된 시간 동안 활성화하고, 종료 시 실행할 콜백을 등록합니다.
    /// </summary>
    public void TriggerActiveForDuration(float _duration, Action<HUD_HPBar> _onHide = null)
    {
        if (0.0f >= _duration)
        {
            onHideCallback = _onHide;
            OnHide();
            return;
        }

        chargeTween?.Kill();
        activeTimer = _duration;
        isTimerActive = true;
        onHideCallback = _onHide;
        OnShow();
    }

    public void OnShow()
    {
        if (false == gameObject.activeSelf)
            gameObject.SetActive(true);
    }

    public void OnHide()
    {
        if (true == gameObject.activeSelf)
        {
            chargeTween?.Kill();
            chargeTween = null;

            isTimerActive = false;
            activeTimer = 0.0f;
            gameObject.SetActive(false);

            // 반납 등을 위한 콜백 실행
            onHideCallback?.Invoke(this);
            onHideCallback = null;
        }
    }

    // //IPoolable 구현부

    public void OnSpawn()
    {
        Initialize();
    }

    public void OnDespawn()
    {
        chargeTween?.Kill();
        chargeTween = null;
        isTimerActive = false;
        activeTimer = 0.0f;
        onHideCallback = null;
    }

    // //유니티 이벤트 함수

    private void Update()
    {
        if (false == isTimerActive)
            return;

        activeTimer -= Time.deltaTime;

        if (0.0f >= activeTimer)
            OnHide();
    }

    private void LateUpdate()
    {
        if (true == isTimerActive && null != targetObj)
        {
            Vector3 newPos = targetObj.transform.position;
            newPos.y += showYOffset;

            if (null != rect)
                rect.position = GlobalUI.SnapToScreenPixel(newPos, Camera.main);
        }
    }

    private void OnDestroy()
    {
        chargeTween?.Kill();
    }
}
