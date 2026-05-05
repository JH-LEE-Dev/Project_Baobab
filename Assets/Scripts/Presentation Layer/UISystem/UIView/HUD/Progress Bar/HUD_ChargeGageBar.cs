using UnityEngine;
using PresentationLayer.DOTweenAnimationSystem;
using DG.Tweening;
using PresentationLayer.ObjectSystem;

/// <summary>
/// HUD에서 차지 게이지를 표시하는 바입니다.
/// OnShow/OnHide 시 ObjectMotionPlayer를 사용하여 모션을 재생합니다.
/// </summary>
public class HUD_ChargeGageBar : HUD_ProgressBar, IPoolable
{
    // //외부 의존성
    [SerializeField] private ObjectMotionPlayer motionPlayer;

    private RectTransform rect;
    private Camera mainCam;

    // //내부 의존성
    private float showYOffset = 0.0f;
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

        if (null == motionPlayer)
            motionPlayer = GetComponent<ObjectMotionPlayer>();

        if (null != motionPlayer)
            motionPlayer.Initialize();
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
    /// 지정된 시간 동안 게이지를 채우고 OnShow/OnHide 모션을 연동합니다.
    /// </summary>
    public void SetCharge(float _duration)
    {
        if (null == progressSlider)
            return;

        chargeTween?.Kill();

        progressSlider.value = 0.0f;

        OnShow();

        chargeTween = progressSlider.DOValue(progressSlider.maxValue, _duration)
            .SetEase(Ease.Linear)
            .OnComplete(OnHide);
    }

    public void OnShow()
    {
        if (null != motionPlayer)
            motionPlayer.Play("Show", bReset: true);
    }

    public void OnHide()
    {
        if (null != motionPlayer)
            motionPlayer.PlayBackward("Show", bReset: true);
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
        targetObj = null;
    }

    // //유니티 이벤트 함수

    private void LateUpdate()
    {
        if (null != targetObj)
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
