using System;
using DG.Tweening;
using DG.Tweening.Core;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace PresentationLayer.UISystem.UIView.HUD.Equipment
{
    /// <summary>
    /// 도끼 HUD 왼쪽의 과열 게이지. 과열 수치(세로 바 + 숫자)와 그 수치로 붙는 공격력 · 도끼 공격속도 배율(아이콘 + x배율)을 보여 준다.
    /// 보일지 말지는 상위(UIView_HUD)가 정해 SetVisible로 알려 주고, 값은 Bind한 IOverheatComponent의 이벤트로 갱신한다(Update 폴링 없음).
    ///
    /// [연출] 모두 unscaled 시간.
    ///   등장   화면 아래 밖에서 바 → 공격력 줄 → 공격속도 줄 순서로 튀어 오른다(OutBack). 올라오며 길쭉해졌다가 도착하며 납작해지고 탄성 있게 제자리로.
    ///   퇴장   살짝 위로 움찔하며 납작해졌다가 길쭉해지며 화면 아래로 빨려 내려간다(InBack). 공격속도 줄 → 공격력 줄 → 바 순서.
    ///   수치↑  숫자 뽀잉, 바가 1px 단위로 차오른다. 배율 표시가 바뀐 줄은 줄째로 뽀잉.
    ///   단계↑  바가 출렁이며 하얗게 번쩍이고 불티가 한꺼번에 터진다. 최대치(MAX)가 되면 한 번 더 번쩍이고 숫자 색이 바뀐다.
    ///   최대치↑ 특성으로 최대치가 오르면 바가 위로 늘어났다 돌아오며 번쩍이고, 채움이 새 비율로 내려앉는다. MAX 숫자 색은 풀린다.
    ///
    /// [게이지 단계] 최대치 대비 비율로 나눈다(캐릭터 오라의 OverheatComponent.Stage와는 별개).
    ///   0 수치 0 / 1 잔불 1~24% / 2 달아오름 25~49% / 3 끓어오름 50~74% / 4 백열 75% 이상.
    ///   단계마다 stageSettings의 값(팔레트 상한 · 흐름 · 기포 · 달아오름 · 불티)을 바 셰이더(UI/HeatGaugeFill)와 불티(HUD_HeatSparks)에 넘긴다.
    /// </summary>
    public class HUD_HeatGauge : MonoBehaviour
    {
        /// <summary>게이지 단계 하나의 연출 값. 인덱스 = 단계(0은 쓰지 않음).</summary>
        [Serializable]
        public struct HeatStageSettings
        {
            [Tooltip("팔레트(0 어두움 ~ 7 흰색) 중 쓸 수 있는 가장 밝은 색")]
            public float paletteCap;
            [Tooltip("용암 노이즈가 위로 흐르는 속도")]
            public float flowSpeed;
            [Tooltip("기포 양(0이면 없음)")]
            public float bubbleAmount;
            [Tooltip("채움 위 빈칸 달아오름 세기(0~1). 4단계는 테두리 깜빡임도 이 값으로 정한다")]
            public float frameHeat;
            [Tooltip("채움 바닥의 기준 열(높을수록 바닥부터 밝다)")]
            public float heatBase;
            [Tooltip("초당 불티 수")]
            public float sparksPerSecond;
        }

        // HeatBar_Base(9x50)에서 채움이 들어가는 안쪽은 아래 2px · 위 2px을 뺀 46px이다.
        private const int FillInnerHeight = 46;

        private static readonly int SpriteUVRectId = Shader.PropertyToID("_SpriteUVRect");
        private static readonly int FillPxId = Shader.PropertyToID("_FillPx");
        private static readonly int RatioId = Shader.PropertyToID("_Ratio");
        private static readonly int StageId = Shader.PropertyToID("_Stage");
        private static readonly int IsMaxId = Shader.PropertyToID("_IsMax");
        private static readonly int FlashId = Shader.PropertyToID("_Flash");
        private static readonly int PaletteCapId = Shader.PropertyToID("_PaletteCap");
        private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
        private static readonly int BubbleAmountId = Shader.PropertyToID("_BubbleAmount");
        private static readonly int FrameHeatId = Shader.PropertyToID("_FrameHeat");
        private static readonly int HeatBaseId = Shader.PropertyToID("_HeatBase");

        // //외부 의존성
        [Header("Slide Targets")]
        [Tooltip("숨김 동안 통째로 끄는 대상(슬라이드 대상들의 부모)")]
        [SerializeField] private GameObject visualRoot;
        [Tooltip("바(바닥 · 채움 · 번쩍임 · 수치 숫자). pivot은 아래 가운데")]
        [SerializeField] private RectTransform barGroup;
        [SerializeField] private RectTransform damageRow;
        [SerializeField] private RectTransform speedRow;

        [Header("Bar")]
        [Tooltip("바 한 장(HeatBar_Base 스프라이트). 테두리 · 달아오름 · 용암 채움 · 번쩍임을 셰이더가 그린다")]
        [SerializeField] private Image fillImage;
        [Tooltip("UI/HeatGaugeFill 머티리얼. 실행 중에는 복제본을 만들어 쓴다")]
        [SerializeField] private Material heatMaterial;
        [SerializeField] private HUD_HeatSparks sparks;

        [Header("Numbers")]
        [SerializeField] private HUD_HeatNumber levelNumber;
        [SerializeField] private HUD_HeatNumber damageNumber;
        [SerializeField] private HUD_HeatNumber speedNumber;

        [Header("Colors")]
        [SerializeField] private Color maxNumberColor = new Color32(255, 140, 40, 255);
        [Tooltip("4단계(MAX 전) 수치 숫자 색")]
        [SerializeField] private Color hotNumberColor = new Color32(255, 226, 190, 255);

        [Header("Stages")]
        [Tooltip("인덱스 = 게이지 단계(0~4). 0은 과열 없음")]
        [SerializeField] private HeatStageSettings[] stageSettings =
        {
            new HeatStageSettings(),
            new HeatStageSettings { paletteCap = 3f, flowSpeed = 4f, bubbleAmount = 0f, frameHeat = 0f, heatBase = 0.05f, sparksPerSecond = 0.6f },
            new HeatStageSettings { paletteCap = 5f, flowSpeed = 9f, bubbleAmount = 0.5f, frameHeat = 0.35f, heatBase = 0.12f, sparksPerSecond = 2f },
            new HeatStageSettings { paletteCap = 6f, flowSpeed = 15f, bubbleAmount = 1f, frameHeat = 0.7f, heatBase = 0.2f, sparksPerSecond = 5f },
            new HeatStageSettings { paletteCap = 7f, flowSpeed = 24f, bubbleAmount = 1.6f, frameHeat = 1f, heatBase = 0.28f, sparksPerSecond = 11f },
        };
        [Tooltip("단계가 오를 때 터지는 불티 수 = 기본 + 단계 x 단계당")]
        [SerializeField] private int burstBaseCount = 4;
        [SerializeField] private int burstPerStage = 3;

        [Header("Slide")]
        [Tooltip("숨김 위치까지 내려가는 거리(px). 숫자 꼭대기까지 화면 밖으로 나갈 만큼")]
        [SerializeField] private float hiddenOffsetY = 96f;
        [SerializeField] private float slideStagger = 0.06f;
        [SerializeField] private float slideInDuration = 0.42f;
        [SerializeField] private float slideInOvershoot = 2.2f;
        [SerializeField] private Vector3 stretchScale = new Vector3(0.85f, 1.2f, 1f);
        [SerializeField] private Vector3 squashScale = new Vector3(1.22f, 0.8f, 1f);
        [SerializeField] private float landRecoverDuration = 0.45f;
        [SerializeField] private float exitAnticipationY = 5f;
        [SerializeField] private float exitAnticipationDuration = 0.08f;
        [SerializeField] private float exitDuration = 0.26f;

        [Header("Value Feedback")]
        [SerializeField] private float fillDuration = 0.25f;
        [SerializeField] private float numberPopScale = 1.4f;
        [SerializeField] private float rowPopScale = 1.2f;
        [SerializeField] private float popDuration = 0.32f;
        [SerializeField] private Vector3 stageBounceScale = new Vector3(1.3f, 0.86f, 1f);
        [SerializeField] private float stageBounceDuration = 0.5f;
        [Tooltip("최대치가 올랐을 때 바가 위로 늘어나는 크기")]
        [SerializeField] private Vector3 maxUpBounceScale = new Vector3(0.8f, 1.25f, 1f);
        [SerializeField] private float flashDuration = 0.3f;

        // //내부 의존성
        private IOverheatComponent overheatState;
        private RectTransform[] slideTargets;
        private Vector2[] slideBasePositions;
        private Sequence slideSequence;
        private Tween fillTween;
        private Tween flashTween;
        private Sequence levelPopSequence;
        private Sequence damagePopSequence;
        private Sequence speedPopSequence;
        private Sequence barBounceSequence;

        private Action<int> cachedOnLevelChanged;
        private Action<float> cachedOnMaxChanged;
        private DOGetter<float> cachedGetFillPixels;
        private DOSetter<float> cachedSetFillPixels;
        private DOGetter<float> cachedGetFlashAlpha;
        private DOSetter<float> cachedSetFlashAlpha;
        private TweenCallback cachedOnSlideOutCompleted;
        private TweenCallback cachedOnSlideInCompleted;
        private Material materialInstance;

        // //상태 변수
        private bool bInitialized = false;
        private bool bVisible = false;
        private float fillPixels = 0f;
        private float flashAlpha = 0f;
        private int shownLevel = -1;
        private int shownStage = -1;
        private int shownDamage = -1;
        private int shownSpeed = -1;
        private bool bShownMax = false;
        private float shownMaxValue = -1f;

        // 머티리얼에 마지막으로 넣은 값(같으면 세터 생략)
        private int appliedFillPx = -1;
        private int appliedStage = -1;
        private float appliedRatio = -1f;
        private int appliedMax = -1;
        private float appliedFlash = -1f;

        public bool IsVisible => bVisible;

        // //퍼블릭 초기화 및 제어 메서드

        public void Initialize()
        {
            if (true == bInitialized)
                return;

            cachedOnLevelChanged = OnOverheatLevelChanged;
            cachedOnMaxChanged = OnOverheatMaxChanged;
            cachedGetFillPixels = GetFillPixels;
            cachedSetFillPixels = SetFillPixels;
            cachedGetFlashAlpha = GetFlashAlpha;
            cachedSetFlashAlpha = SetFlashAlpha;
            cachedOnSlideOutCompleted = OnSlideOutCompleted;
            cachedOnSlideInCompleted = OnSlideInCompleted;

            // 프리팹에 배치된 위치가 "보이는" 위치다. 숨김 위치는 여기서 hiddenOffsetY만큼 아래.
            slideTargets = new RectTransform[] { barGroup, damageRow, speedRow };
            slideBasePositions = new Vector2[slideTargets.Length];

            for (int i = 0; i < slideTargets.Length; i++)
            {
                if (null != slideTargets[i])
                    slideBasePositions[i] = slideTargets[i].anchoredPosition;
            }

            if (null != levelNumber)
                levelNumber.Initialize();
            if (null != damageNumber)
                damageNumber.Initialize();
            if (null != speedNumber)
                speedNumber.Initialize();

            if (null != sparks)
                sparks.Initialize();

            // 게이지는 하나뿐이지만 공유 머티리얼 에셋을 덮어쓰지 않도록 복제본에 값을 쓴다.
            if (null != fillImage && null != heatMaterial)
            {
                materialInstance = new Material(heatMaterial);
                materialInstance.name = heatMaterial.name + " (Instance)";
                fillImage.material = materialInstance;

                if (null != fillImage.sprite)
                    materialInstance.SetVector(SpriteUVRectId, DataUtility.GetOuterUV(fillImage.sprite));
            }

            bInitialized = true;
            bVisible = false;

            SetFlashAlpha(0f);
            SnapToState();
            SyncImmediate();
        }

        /// <summary>과열 상태를 구독한다. 이전 구독은 풀고, 지금 값으로 연출 없이 맞춘다.</summary>
        public void Bind(IOverheatComponent _overheatState)
        {
            Initialize();
            Unbind();

            overheatState = _overheatState;

            if (null != overheatState)
            {
                overheatState.OverheatLevelChangedEvent += cachedOnLevelChanged;
                overheatState.OverheatMaxChangedEvent += cachedOnMaxChanged;
            }

            SyncImmediate();
        }

        public void Unbind()
        {
            if (null == overheatState)
                return;

            overheatState.OverheatLevelChangedEvent -= cachedOnLevelChanged;
            overheatState.OverheatMaxChangedEvent -= cachedOnMaxChanged;
            overheatState = null;
        }

        /// <summary>
        /// 게이지를 보이거나 숨긴다. _bAnimate가 false이거나 오브젝트가 꺼져 있으면 연출 없이 바로 그 상태가 된다.
        /// 같은 상태로 다시 불리면 연출은 다시 하지 않는다(연출 없는 호출이면 진행 중인 연출을 끝 상태로 맞춘다).
        /// </summary>
        public void SetVisible(bool _bVisible, bool _bAnimate)
        {
            Initialize();

            bool _bChanged = _bVisible != bVisible;
            bVisible = _bVisible;

            if (false == _bAnimate || false == gameObject.activeInHierarchy)
            {
                SnapToState();
                SyncImmediate();
                return;
            }

            if (false == _bChanged)
                return;

            if (true == bVisible)
                PlaySlideIn();
            else
                PlaySlideOut();
        }

        // //일반 비즈니스 로직 및 내부 메서드

        private void OnOverheatLevelChanged(int _level)
        {
            // 숨어 있거나 등장 · 퇴장 중이면 연출 없이 값만 맞춘다(숨김 상태에서 쌓인 변화는 등장할 때 그대로 보인다).
            if (false == bVisible || false == gameObject.activeInHierarchy || true == IsSliding())
            {
                SyncImmediate();
                return;
            }

            ApplyValues(true);
        }

        // 수치는 그대로여도 채움 비율과 MAX 여부가 바뀌므로 다시 그린다(연출 조건은 수치 변경과 같다).
        private void OnOverheatMaxChanged(float _maxValue)
        {
            OnOverheatLevelChanged(shownLevel);
        }

        private bool IsSliding()
        {
            return null != slideSequence && true == slideSequence.IsActive();
        }

        private void PlaySlideIn()
        {
            KillSlide();
            SyncImmediate();

            if (null != visualRoot)
                visualRoot.SetActive(true);

            slideSequence = DOTween.Sequence().SetUpdate(true);

            for (int i = 0; i < slideTargets.Length; i++)
            {
                RectTransform _target = slideTargets[i];

                if (null == _target)
                    continue;

                Vector2 _base = slideBasePositions[i];
                _target.anchoredPosition = new Vector2(_base.x, _base.y - hiddenOffsetY);
                _target.localScale = Vector3.one;

                float _delay = i * slideStagger;

                slideSequence.Insert(_delay, _target.DOAnchorPosY(_base.y, slideInDuration, true)
                    .SetEase(Ease.OutBack, slideInOvershoot));

                // 올라오는 동안 길쭉 → 오버슈트 꼭대기에서 납작 → 탄성 있게 원래 크기로(쫀득)
                slideSequence.Insert(_delay, _target.DOScale(stretchScale, slideInDuration * 0.3f)
                    .SetEase(Ease.OutQuad));
                slideSequence.Insert(_delay + slideInDuration * 0.3f, _target.DOScale(squashScale, slideInDuration * 0.2f)
                    .SetEase(Ease.InOutQuad));
                slideSequence.Insert(_delay + slideInDuration * 0.5f, _target.DOScale(Vector3.one, landRecoverDuration)
                    .SetEase(Ease.OutElastic));
            }

            slideSequence.OnComplete(cachedOnSlideInCompleted);
        }

        private void PlaySlideOut()
        {
            KillSlide();
            KillValueTweens();

            slideSequence = DOTween.Sequence().SetUpdate(true);

            // 등장의 역순(공격속도 줄 → 공격력 줄 → 바)으로 빠진다.
            int _last = slideTargets.Length - 1;

            for (int i = _last; i >= 0; i--)
            {
                RectTransform _target = slideTargets[i];

                if (null == _target)
                    continue;

                Vector2 _base = slideBasePositions[i];
                float _delay = (_last - i) * slideStagger;
                float _dropAt = _delay + exitAnticipationDuration;

                // 움찔(살짝 위로 + 납작) → 길쭉해지며 아래로 빨려 내려간다
                slideSequence.Insert(_delay, _target.DOAnchorPosY(_base.y + exitAnticipationY, exitAnticipationDuration, true)
                    .SetEase(Ease.OutQuad));
                slideSequence.Insert(_delay, _target.DOScale(squashScale, exitAnticipationDuration)
                    .SetEase(Ease.OutQuad));
                slideSequence.Insert(_dropAt, _target.DOAnchorPosY(_base.y - hiddenOffsetY, exitDuration, true)
                    .SetEase(Ease.InBack));
                slideSequence.Insert(_dropAt, _target.DOScale(stretchScale, exitDuration * 0.5f)
                    .SetEase(Ease.OutQuad));
            }

            slideSequence.OnComplete(cachedOnSlideOutCompleted);
        }

        private void OnSlideInCompleted()
        {
            slideSequence = null;
            SnapToState();
        }

        private void OnSlideOutCompleted()
        {
            slideSequence = null;
            SnapToState();
        }

        // 연출 없이 지금 상태(보임 = 제자리, 숨김 = 화면 아래 밖 + 끔)로 맞춘다.
        // 부모가 꺼지는 중(OnDisable)에는 자식 SetActive가 막히므로 _bApplyActive를 false로 넘겨 위치 · 크기만 맞춘다.
        private void SnapToState(bool _bApplyActive = true)
        {
            KillSlide();

            if (null != slideTargets)
            {
                for (int i = 0; i < slideTargets.Length; i++)
                {
                    RectTransform _target = slideTargets[i];

                    if (null == _target)
                        continue;

                    Vector2 _base = slideBasePositions[i];
                    _target.anchoredPosition = true == bVisible ? _base : new Vector2(_base.x, _base.y - hiddenOffsetY);
                    _target.localScale = Vector3.one;
                }
            }

            if (true == _bApplyActive && null != visualRoot)
                visualRoot.SetActive(bVisible);
        }

        // 진행 중인 값 연출을 모두 끊고 지금 값으로 바로 맞춘다.
        private void SyncImmediate()
        {
            KillValueTweens();
            ApplyValues(false);
        }

        private void ApplyValues(bool _bAnimate)
        {
            int _level = 0;
            int _stage = 0;
            bool _bMax = false;
            float _maxValue = 100f;
            float _damage = 1f;
            float _speed = 1f;

            if (null != overheatState)
            {
                _level = overheatState.OverheatLevel;
                _bMax = overheatState.IsMax;
                _maxValue = overheatState.MaxValue;
                _damage = overheatState.DamageMultiplier;
                _speed = overheatState.AxeAttackSpeedMultiplier;
            }

            _stage = GetGaugeStage(_level, _maxValue);
            int _damageHundredths = Mathf.RoundToInt(_damage * 100f);
            int _speedHundredths = Mathf.RoundToInt(_speed * 100f);

            if (null != levelNumber)
            {
                levelNumber.SetInt(_level);
                levelNumber.SetTint(true == _bMax ? maxNumberColor : (4 <= _stage ? hotNumberColor : Color.white));
            }

            if (null != damageNumber)
                damageNumber.SetMultiplier(_damageHundredths);

            if (null != speedNumber)
                speedNumber.SetMultiplier(_speedHundredths);

            ApplyStage(_stage, _level, _maxValue, _bMax);
            SetFillTarget(_level, _maxValue, _bAnimate);

            if (true == _bAnimate)
            {
                if (0 <= shownLevel && _level > shownLevel && null != levelNumber)
                    PlayPop((RectTransform)levelNumber.transform, ref levelPopSequence, numberPopScale);

                if (0 <= shownDamage && _damageHundredths != shownDamage)
                    PlayPop(damageRow, ref damagePopSequence, rowPopScale);

                if (0 <= shownSpeed && _speedHundredths != shownSpeed)
                    PlayPop(speedRow, ref speedPopSequence, rowPopScale);

                bool _bStageUp = 0 <= shownStage && _stage > shownStage;
                bool _bBecameMax = true == _bMax && false == bShownMax;
                bool _bMaxRaised = 0f < shownMaxValue && _maxValue > shownMaxValue;

                if (true == _bMaxRaised)
                    PlayBarBounce(maxUpBounceScale);
                else if (true == _bStageUp)
                    PlayBarBounce(stageBounceScale);

                if (true == _bStageUp && null != sparks)
                    sparks.Burst(burstBaseCount + _stage * burstPerStage);

                if (true == _bStageUp || true == _bBecameMax || true == _bMaxRaised)
                    PlayFlash();
            }

            shownLevel = _level;
            shownStage = _stage;
            shownDamage = _damageHundredths;
            shownSpeed = _speedHundredths;
            bShownMax = _bMax;
            shownMaxValue = _maxValue;
        }

        // 게이지 단계: 0 = 수치 0, 1 = 1~24%, 2 = 25~49%, 3 = 50~74%, 4 = 75% 이상(최대치 대비).
        private static int GetGaugeStage(int _level, float _maxValue)
        {
            if (0 >= _level)
                return 0;

            float _percent = 0f < _maxValue ? _level * 100f / _maxValue : 100f;

            if (75f <= _percent)
                return 4;
            if (50f <= _percent)
                return 3;
            if (25f <= _percent)
                return 2;
            return 1;
        }

        // 단계 · 비율 · MAX를 셰이더와 불티에 넘긴다. 바뀐 값만 쓴다.
        private void ApplyStage(int _stage, int _level, float _maxValue, bool _bMax)
        {
            if (_stage != appliedStage)
            {
                appliedStage = _stage;
                HeatStageSettings _settings = GetStageSettings(_stage);

                if (null != materialInstance)
                {
                    materialInstance.SetFloat(StageId, _stage);
                    materialInstance.SetFloat(PaletteCapId, _settings.paletteCap);
                    materialInstance.SetFloat(FlowSpeedId, _settings.flowSpeed);
                    materialInstance.SetFloat(BubbleAmountId, _settings.bubbleAmount);
                    materialInstance.SetFloat(FrameHeatId, _settings.frameHeat);
                    materialInstance.SetFloat(HeatBaseId, _settings.heatBase);
                }

                if (null != sparks)
                    sparks.SetStage(_stage, _settings.sparksPerSecond);
            }

            float _ratio = 0f < _maxValue ? Mathf.Clamp01(_level / _maxValue) : 0f;

            if (false == Mathf.Approximately(_ratio, appliedRatio))
            {
                appliedRatio = _ratio;

                if (null != materialInstance)
                    materialInstance.SetFloat(RatioId, _ratio);
            }

            int _max = true == _bMax ? 1 : 0;

            if (_max != appliedMax)
            {
                appliedMax = _max;

                if (null != materialInstance)
                    materialInstance.SetFloat(IsMaxId, _max);
            }
        }

        private HeatStageSettings GetStageSettings(int _stage)
        {
            if (null == stageSettings || 0 == stageSettings.Length)
                return new HeatStageSettings();

            return stageSettings[Mathf.Clamp(_stage, 0, stageSettings.Length - 1)];
        }

        // 과열이 1 이상이면 최소 1px은 보이게 한다.
        private void SetFillTarget(int _level, float _maxValue, bool _bAnimate)
        {
            int _target = 0;

            if (0 < _level)
            {
                float _ratio = 0f < _maxValue ? _level / _maxValue : 1f;
                _target = Mathf.Clamp(Mathf.RoundToInt(FillInnerHeight * _ratio), 1, FillInnerHeight);
            }

            if (null != fillTween && true == fillTween.IsActive())
                fillTween.Kill();
            fillTween = null;

            if (false == _bAnimate)
            {
                SetFillPixels(_target);
                return;
            }

            fillTween = DOTween.To(cachedGetFillPixels, cachedSetFillPixels, _target, fillDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        private float GetFillPixels()
        {
            return fillPixels;
        }

        // 1px 단위로만 차오른다(셰이더가 정수 px 높이로 채운다).
        private void SetFillPixels(float _pixels)
        {
            fillPixels = _pixels;

            int _px = Mathf.RoundToInt(_pixels);

            if (_px == appliedFillPx)
                return;

            appliedFillPx = _px;

            if (null != materialInstance)
                materialInstance.SetFloat(FillPxId, _px);

            if (null != sparks)
                sparks.SetFillPixels(_px);
        }

        private float GetFlashAlpha()
        {
            return flashAlpha;
        }

        // 채움 픽셀을 흰색으로 보간하는 양. 제곱해 넣어 처음에 확 밝고 빠르게 빠진다.
        private void SetFlashAlpha(float _alpha)
        {
            flashAlpha = _alpha;

            float _flash = _alpha * _alpha;

            if (Mathf.Approximately(_flash, appliedFlash))
                return;

            appliedFlash = _flash;

            if (null != materialInstance)
                materialInstance.SetFloat(FlashId, _flash);
        }

        private void PlayFlash()
        {
            if (null != flashTween && true == flashTween.IsActive())
                flashTween.Kill();

            SetFlashAlpha(1f);
            flashTween = DOTween.To(cachedGetFlashAlpha, cachedSetFlashAlpha, 0f, flashDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true);
        }

        private void PlayBarBounce(Vector3 _scale)
        {
            if (null == barGroup)
                return;

            KillSequence(ref barBounceSequence);
            barGroup.localScale = Vector3.one;

            barBounceSequence = DOTween.Sequence().SetUpdate(true);
            barBounceSequence.Append(barGroup.DOScale(_scale, stageBounceDuration * 0.2f).SetEase(Ease.OutQuad));
            barBounceSequence.Append(barGroup.DOScale(Vector3.one, stageBounceDuration * 0.8f).SetEase(Ease.OutElastic));
        }

        // HUD_EquipmentAxe 수리 뽀잉과 같은 형태: 빠르게 커졌다가 탄성 있게 제자리로.
        private void PlayPop(RectTransform _target, ref Sequence _sequence, float _scale)
        {
            if (null == _target)
                return;

            KillSequence(ref _sequence);
            _target.localScale = Vector3.one;

            _sequence = DOTween.Sequence().SetUpdate(true);
            _sequence.Append(_target.DOScale(Vector3.one * _scale, popDuration * 0.25f).SetEase(Ease.OutQuad));
            _sequence.Append(_target.DOScale(Vector3.one, popDuration * 0.75f).SetEase(Ease.OutElastic));
        }

        private void KillSlide()
        {
            if (null != slideSequence && true == slideSequence.IsActive())
                slideSequence.Kill();
            slideSequence = null;
        }

        private void KillValueTweens()
        {
            if (null != fillTween && true == fillTween.IsActive())
                fillTween.Kill();
            fillTween = null;

            if (null != flashTween && true == flashTween.IsActive())
                flashTween.Kill();
            flashTween = null;
            SetFlashAlpha(0f);

            KillSequence(ref levelPopSequence);
            KillSequence(ref damagePopSequence);
            KillSequence(ref speedPopSequence);
            KillSequence(ref barBounceSequence);

            if (null != levelNumber)
                levelNumber.transform.localScale = Vector3.one;

            // 슬라이드 중에는 줄 · 바의 크기를 슬라이드가 쥐고 있으므로 건드리지 않는다.
            if (false == IsSliding())
            {
                if (null != damageRow)
                    damageRow.localScale = Vector3.one;
                if (null != speedRow)
                    speedRow.localScale = Vector3.one;
                if (null != barGroup)
                    barGroup.localScale = Vector3.one;
            }
        }

        private static void KillSequence(ref Sequence _sequence)
        {
            if (null != _sequence && true == _sequence.IsActive())
                _sequence.Kill();
            _sequence = null;
        }

        // //유니티 라이프사이클 이벤트 함수

        private void OnDisable()
        {
            if (false == bInitialized)
                return;

            // HUD가 꺼질 때(마을, 어빌리티 트리 UI 등) 연출 도중이면 중간 모습으로 남지 않게 끝 상태로 맞춘다.
            SnapToState(false);
            SyncImmediate();
        }

        private void OnDestroy()
        {
            Unbind();
            KillSlide();
            KillValueTweens();

            if (null != materialInstance)
                Destroy(materialInstance);
        }
    }
}
