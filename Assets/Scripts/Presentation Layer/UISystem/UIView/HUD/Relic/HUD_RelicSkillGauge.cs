using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전리품 발동형 스킬(유성우/지진) 임시 원형 HUD 게이지.
/// 보임/숨김은 해금 및 현재 맵 상태로 제어하고, Radial360 채움 + 발동 시 바운스/섬광을 재생한다.
/// 실제 리소스가 준비되면 프리팹의 원형 프레임/채움/아이콘 스프라이트만 교체한다.
/// </summary>
public class HUD_RelicSkillGauge : MonoBehaviour
{
    // 외부 의존성
    [Header("UI Ref")]
    [Tooltip("보임/숨김 및 스케일 연출 대상. 피벗은 가운데.")]
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private Image fillImage;
    [Tooltip("발동 순간 겹치는 흰 섬광 오버레이.")]
    [SerializeField] private Image flashImage;

    [Header("Color")]
    [SerializeField] private Color chargingColor = new Color32(110, 210, 255, 255);
    [SerializeField] private Color readyColor = new Color32(255, 236, 138, 255);
    [SerializeField] private Color activeDrainColor = new Color32(170, 236, 255, 255);

    [Header("Motion")]
    [SerializeField] private float enterDuration = 0.35f;
    [Range(0f, 1f)][SerializeField] private float enterFromScale = 0.4f;
    [SerializeField] private Ease enterEase = Ease.OutBack;
    [SerializeField] private float enterOvershoot = 2.2f;
    [SerializeField] private float bounceHeight = 3f;
    [SerializeField] private float bounceTime = 0.25f;
    [SerializeField] private Ease bounceEase = Ease.OutBack;
    [SerializeField] private float bounceOvershoot = 2.2f;
    [SerializeField] private float flashTime = 0.14f;

    // 내부 상태
    private bool bUnlocked = false;
    private bool bBaseCached = false;
    private Vector2 rootBasePos;
    private float targetRatio = 0f;
    private float activeDrainDuration = 0f;
    private float activeDrainElapsed = 0f;
    private bool bActiveDraining = false;
    private float enterAge = -1f;
    private float bounceAge = -1f;
    private float flashAge = -1f;

    public bool IsUnlocked
    {
        get { return bUnlocked; }
    }

    public void SetUnlocked(bool _bUnlocked)
    {
        CacheBase();

        if (_bUnlocked == bUnlocked)
        {
            return;
        }

        bUnlocked = _bUnlocked;

        if (null != visualRoot)
        {
            visualRoot.gameObject.SetActive(_bUnlocked);
        }

        if (true == _bUnlocked)
        {
            enterAge = 0f;
            ApplyEnterScale(0f);
        }
        else
        {
            enterAge = -1f;
            ClearEffects();
        }
    }

    public void SetCharge(int _current, int _max, bool _bSilent = false)
    {
        CacheBase();

        int _safeMax = 0 < _max ? _max : 1;
        float _nextRatio = Mathf.Clamp01((float)_current / _safeMax);
        bool _bIncreased = _nextRatio > targetRatio + 0.0001f;

        bActiveDraining = false;
        activeDrainDuration = 0f;
        activeDrainElapsed = 0f;
        targetRatio = _nextRatio;

        ApplyFill(_nextRatio, 1f <= _nextRatio ? readyColor : chargingColor);

        if (true == _bSilent || false == bUnlocked)
        {
            return;
        }

        if (true == _bIncreased)
        {
            StartBounce();

            if (1f <= _nextRatio)
            {
                StartFlash();
            }
        }
    }

    public void PlayTriggered(float _drainDuration = 0f)
    {
        CacheBase();

        if (false == bUnlocked)
        {
            targetRatio = 0f;
            ApplyFill(0f, chargingColor);
            return;
        }

        StartBounce();
        StartFlash();

        if (0.05f < _drainDuration)
        {
            bActiveDraining = true;
            activeDrainDuration = _drainDuration;
            activeDrainElapsed = 0f;
            targetRatio = 1f;
            ApplyFill(1f, activeDrainColor);
        }
        else
        {
            bActiveDraining = false;
            targetRatio = 0f;
            ApplyFill(0f, chargingColor);
        }
    }

    private void Awake()
    {
        CacheBase();

        if (null != visualRoot)
        {
            visualRoot.gameObject.SetActive(bUnlocked);
        }

        ApplyFill(0f, chargingColor);

        if (null != flashImage)
        {
            flashImage.enabled = false;
        }
    }

    private void Update()
    {
        if (false == bUnlocked)
        {
            return;
        }

        float _dt = Time.deltaTime;
        UpdateEnter(_dt);
        UpdateBounce(_dt);
        UpdateFlash(_dt);
        UpdateActiveDrain(_dt);
    }

    private void CacheBase()
    {
        if (true == bBaseCached || null == visualRoot)
        {
            return;
        }

        rootBasePos = visualRoot.anchoredPosition;
        bBaseCached = true;
    }

    private void UpdateEnter(float _dt)
    {
        if (0f > enterAge)
        {
            return;
        }

        enterAge += _dt;
        float _duration = Mathf.Max(0.01f, enterDuration);

        if (enterAge >= _duration)
        {
            enterAge = -1f;
            visualRoot.localScale = Vector3.one;
            return;
        }

        ApplyEnterScale(enterAge / _duration);
    }

    private void ApplyEnterScale(float _t)
    {
        if (null == visualRoot)
        {
            return;
        }

        float _k = DOVirtual.EasedValue(0f, 1f, Mathf.Clamp01(_t), enterEase, enterOvershoot, 0f);
        float _s = Mathf.LerpUnclamped(enterFromScale, 1f, _k);
        visualRoot.localScale = new Vector3(_s, _s, 1f);
    }

    private void StartBounce()
    {
        bounceAge = 0f;
    }

    private void UpdateBounce(float _dt)
    {
        if (0f > bounceAge || null == visualRoot)
        {
            return;
        }

        bounceAge += _dt;
        float _duration = Mathf.Max(0.01f, bounceTime);

        if (bounceAge >= _duration)
        {
            bounceAge = -1f;
            visualRoot.anchoredPosition = rootBasePos;
            return;
        }

        float _t = bounceAge / _duration;
        float _offsetY = DOVirtual.EasedValue(bounceHeight, 0f, _t, bounceEase, bounceOvershoot, 0f);
        visualRoot.anchoredPosition = new Vector2(rootBasePos.x, rootBasePos.y + _offsetY);
    }

    private void StartFlash()
    {
        if (null == flashImage)
        {
            return;
        }

        flashAge = 0f;
        flashImage.enabled = true;
    }

    private void UpdateFlash(float _dt)
    {
        if (0f > flashAge || null == flashImage)
        {
            return;
        }

        flashAge += _dt;

        if (flashAge >= flashTime)
        {
            flashAge = -1f;
            flashImage.enabled = false;
        }
    }

    private void UpdateActiveDrain(float _dt)
    {
        if (false == bActiveDraining)
        {
            return;
        }

        activeDrainElapsed += _dt;

        if (activeDrainElapsed >= activeDrainDuration)
        {
            bActiveDraining = false;
            targetRatio = 0f;
            ApplyFill(0f, chargingColor);
            return;
        }

        float _remainRatio = Mathf.Clamp01(1f - (activeDrainElapsed / activeDrainDuration));
        ApplyFill(_remainRatio, activeDrainColor);
    }

    private void ApplyFill(float _ratio, Color _color)
    {
        if (null == fillImage)
        {
            return;
        }

        fillImage.fillAmount = Mathf.Clamp01(_ratio);
        fillImage.color = _color;
    }

    private void ClearEffects()
    {
        bounceAge = -1f;
        flashAge = -1f;
        bActiveDraining = false;

        if (null != visualRoot)
        {
            visualRoot.anchoredPosition = rootBasePos;
            visualRoot.localScale = Vector3.one;
        }

        if (null != flashImage)
        {
            flashImage.enabled = false;
        }
    }
}
