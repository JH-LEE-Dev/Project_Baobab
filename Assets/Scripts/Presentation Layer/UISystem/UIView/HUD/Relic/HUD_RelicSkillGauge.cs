using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전리품 발동형 스킬(유성우/지진) 구슬 HUD 게이지.
/// 보임/숨김은 해금 및 현재 맵 상태로 제어하고, 스택이 찰수록 구슬이 아래에서 위로 차오른다(정수 도트 행으로 스냅).
/// 유성우는 내리는 동안 게이지가 0까지 천천히 줄고, 지진은 쿨타임 동안 구슬 위에 음영이 덮였다가 위에서부터 아래로 걷힌다.
/// </summary>
public class HUD_RelicSkillGauge : MonoBehaviour
{
    // 외부 의존성
    [Header("UI Ref")]
    [Tooltip("보임/숨김 및 스케일 연출 대상. 피벗은 가운데.")]
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private Image fillImage;
    [SerializeField] private Material meteorFillMaterial;
    [Tooltip("쿨타임 음영. 채움 스프라이트(구슬 안쪽 원)를 반투명 검정으로 겹친다. 쿨타임이 없는 스킬은 비워 둔다.")]
    [SerializeField] private Image cooldownImage;
    [Tooltip("발동 순간 겹치는 흰 섬광 오버레이.")]
    [SerializeField] private Image flashImage;

    [Header("Fill Shape")]
    [Tooltip("채움 스프라이트 안에서 채움 픽셀이 시작하는 행(아래부터)과 높이, 스프라이트 높이(px)")]
    [SerializeField] private int fillContentY = 22;
    [SerializeField] private int fillContentHeight = 20;
    [SerializeField] private int spriteSize = 64;

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
    // 쿨타임이 끝나는 시각(Time.time 기준, 0 미만이면 쿨타임 없음). 게임 쪽 쿨타임도 Time.deltaTime으로 줄어 일시정지 때 함께 멈춘다.
    private float cooldownEndTime = -1f;
    private float cooldownTotal = 0f;
    private int cooldownShownPx = -1;
    private HUD_MeteorShowerFill meteorFill;

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
            UpdateCooldown();
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

        if (meteorFill == null)
            ApplyFill(_nextRatio, 1f <= _nextRatio ? readyColor : chargingColor);
        else if (_bSilent || !bUnlocked)
            meteorFill.Reset(_nextRatio);
        else
            meteorFill.Charge(_nextRatio);

        if (true == _bSilent || false == bUnlocked)
        {
            return;
        }

        if (true == _bIncreased)
        {
            StartBounce();

            if (1f <= _nextRatio && meteorFill == null)
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
        if (meteorFill == null || _drainDuration <= 0.05f)
            StartFlash();

        if (0.05f < _drainDuration)
        {
            bActiveDraining = true;
            activeDrainDuration = _drainDuration;
            activeDrainElapsed = 0f;
            targetRatio = 1f;
            if (meteorFill != null)
                meteorFill.Trigger();
            else
                ApplyFill(1f, activeDrainColor);
        }
        else
        {
            bActiveDraining = false;
            targetRatio = 0f;
            ApplyFill(0f, chargingColor);
        }
    }

    // 남은 쿨타임과 전체 쿨타임(초). 남은 시간이 0 이하이면 음영을 걷는다.
    public void SetCooldown(float _remaining, float _total)
    {
        if (0f >= _remaining || 0f >= _total)
        {
            cooldownEndTime = -1f;
            cooldownTotal = 0f;
        }
        else
        {
            cooldownTotal = _total;
            cooldownEndTime = Time.time + Mathf.Min(_remaining, _total);
        }

        UpdateCooldown();
    }

    private void Awake()
    {
        CacheBase();

        if (meteorFillMaterial != null && fillImage != null && fillImage.sprite != null)
            meteorFill = new HUD_MeteorShowerFill(fillImage, meteorFillMaterial, fillContentY, fillContentHeight);

        if (null != visualRoot)
        {
            visualRoot.gameObject.SetActive(bUnlocked);
        }

        ApplyFill(0f, chargingColor);
        UpdateCooldown();

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
        meteorFill?.Tick(_dt);
        UpdateCooldown();
    }

    private void OnDestroy()
    {
        meteorFill?.Dispose();
    }

    // 음영 높이 = 남은 쿨타임 비율. 아래쪽을 기준으로 위 가장자리가 내려가며 걷힌다.
    // 남은 시간이 조금이라도 있으면 1행은 남기도록 올림한다(끝나는 순간 정확히 사라짐).
    private void UpdateCooldown()
    {
        if (null == cooldownImage)
        {
            return;
        }

        int _px = 0;

        if (0f <= cooldownEndTime && 0f < cooldownTotal)
        {
            float _remain = cooldownEndTime - Time.time;

            if (0f < _remain)
            {
                _px = Mathf.Clamp(Mathf.CeilToInt(_remain / cooldownTotal * fillContentHeight), 1, fillContentHeight);
            }
            else
            {
                cooldownEndTime = -1f;
            }
        }

        if (_px == cooldownShownPx)
        {
            return;
        }

        cooldownShownPx = _px;
        cooldownImage.enabled = 0 < _px;
        cooldownImage.fillAmount = (float)(fillContentY + _px) / Mathf.Max(1, spriteSize);
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
        if (meteorFill != null)
            meteorFill.Drain(_remainRatio, activeDrainElapsed);
        else
            ApplyFill(_remainRatio, activeDrainColor);
    }

    private void ApplyFill(float _ratio, Color _color)
    {
        if (meteorFill != null)
        {
            meteorFill.Reset(_ratio);
            return;
        }

        if (null == fillImage)
        {
            return;
        }

        // 채움 높이를 정수 도트 행으로 스냅한다(구슬 안쪽 원의 아래 행부터 차오름)
        float _px = Mathf.Round(Mathf.Clamp01(_ratio) * fillContentHeight);
        fillImage.fillAmount = (fillContentY + _px) / Mathf.Max(1, spriteSize);
        fillImage.color = _color;
    }

    private void ClearEffects()
    {
        bounceAge = -1f;
        flashAge = -1f;
        bActiveDraining = false;
        meteorFill?.Reset(0f);

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
