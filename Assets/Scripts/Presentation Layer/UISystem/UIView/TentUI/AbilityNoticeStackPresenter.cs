using System.Collections.Generic;
using DG.Tweening;
using NaughtyAttributes;
using UnityEngine;

public class AbilityNoticeStackPresenter : MonoBehaviour
{
    [SerializeField] private AbilityNoticeItem noticePrefab;
    [SerializeField] private RectTransform noticeParent;
    [SerializeField] private RectTransform[] noticePivots = new RectTransform[5];
    [SerializeField] private int prewarmCount = 7;
    [SerializeField] private float lifeTime = 3.0f;
    [SerializeField] private float showDuration = 0.32f;
    [SerializeField] private float hideDuration = 0.28f;
    [SerializeField] private float moveDuration = 0.32f;
    [SerializeField] private float entryOffsetX = 26.0f;
    [SerializeField] private float exitOffsetY = 14.0f;
    [SerializeField] private float refreshDuration = 0.45f;
    [SerializeField] private Vector2 refreshSquashScale = new Vector2(1.4f, 0.7f);
    [SerializeField] private Vector2 refreshRecoilScale = new Vector2(0.8f, 1.3f);
    [SerializeField, Range(1, 5)] private int refreshBounceCount = 2;
    [SerializeField, Range(0.0f, 1.0f)] private float refreshBounceDamping = 0.25f;
    [SerializeField] private Ease showEase = Ease.OutCubic;
    [SerializeField] private Ease hideEase = Ease.OutCubic;
    [SerializeField] private Ease moveEase = Ease.OutCubic;
    [SerializeField] private Ease refreshSquashEase = Ease.OutQuad;
    [SerializeField] private Ease refreshRestoreEase = Ease.OutBack;
    [SerializeField] private string debugNoticeFormat = "공격력 {0}%";
    [SerializeField] private int debugNoticeStep = 20;

    private readonly List<AbilityNoticeItem> noticePool = new List<AbilityNoticeItem>(7);
    private readonly List<AbilityNoticeItem> activeNotices = new List<AbilityNoticeItem>(5);
    private int debugNoticeValue;

    private int MaxVisibleCount
    {
        get { return noticePivots == null ? 0 : noticePivots.Length; }
    }

    private void Awake()
    {
        BindReferencesIfNeeded();
        PrewarmPool();
    }

    private void Update()
    {
        TickActiveNotices();
    }

    private void OnDisable()
    {
        ResetAllNotices();
    }

    public void ShowNotice(string _message)
    {
        ShowNotice(null, _message);
    }

    public void ShowNotice(string _key, string _message)
    {
        BindReferencesIfNeeded();
        PrewarmPool();

        if (noticePrefab == null || MaxVisibleCount <= 0)
            return;

        if (string.IsNullOrEmpty(_key) == false && TryRefreshActiveNotice(_key, _message))
            return;

        if (activeNotices.Count >= MaxVisibleCount)
            RemoveNoticeAt(0);

        AbilityNoticeItem noticeItem = GetReusableNotice();
        if (noticeItem == null)
            return;

        activeNotices.Add(noticeItem);
        noticeItem.Show(_key, _message, noticePivots[activeNotices.Count - 1], entryOffsetX, showDuration, lifeTime, showEase);
    }

    public void OnNoticeReturned(AbilityNoticeItem _noticeItem)
    {
    }

    [Button("AbilityNotice노출기능")]
    public void DebugShowAbilityNotice()
    {
        if (Application.isPlaying == false)
        {
            Debug.LogWarning("[AbilityNoticeStackPresenter] AbilityNotice test is available in Play Mode.");
            return;
        }

        debugNoticeValue += debugNoticeStep;
        ShowNotice(string.Format(debugNoticeFormat, debugNoticeValue));
    }

    private void TickActiveNotices()
    {
        if (activeNotices.Count == 0)
            return;

        float deltaTime = Time.unscaledDeltaTime;
        for (int i = 0; i < activeNotices.Count; i++)
        {
            AbilityNoticeItem noticeItem = activeNotices[i];
            if (noticeItem == null)
            {
                activeNotices.RemoveAt(i);
                i--;
                ReflowActiveNotices(i);
                continue;
            }

            if (noticeItem.Tick(deltaTime))
            {
                RemoveNoticeAt(i);
                i--;
            }
        }
    }

    private void RemoveNoticeAt(int _index)
    {
        if (_index < 0 || _index >= activeNotices.Count)
            return;

        AbilityNoticeItem noticeItem = activeNotices[_index];
        activeNotices.RemoveAt(_index);

        if (noticeItem != null)
            noticeItem.Hide(exitOffsetY, hideDuration, hideEase);

        ReflowActiveNotices(_index);
    }

    private bool TryRefreshActiveNotice(string _key, string _message)
    {
        for (int i = 0; i < activeNotices.Count; i++)
        {
            AbilityNoticeItem noticeItem = activeNotices[i];
            if (noticeItem == null || noticeItem.IsVisible == false || noticeItem.IsHiding)
                continue;

            if (noticeItem.NoticeKey != _key)
                continue;

            activeNotices.RemoveAt(i);
            activeNotices.Add(noticeItem);
            noticeItem.Refresh(_message, lifeTime, refreshDuration, refreshSquashScale, refreshRecoilScale, refreshBounceCount, refreshBounceDamping, refreshSquashEase, refreshRestoreEase);
            ReflowActiveNotices(i);
            return true;
        }

        return false;
    }

    private void ReflowActiveNotices(int _startIndex)
    {
        int startIndex = Mathf.Max(0, _startIndex);
        for (int i = startIndex; i < activeNotices.Count; i++)
        {
            AbilityNoticeItem noticeItem = activeNotices[i];
            if (noticeItem == null || i >= MaxVisibleCount)
                continue;

            noticeItem.MoveTo(noticePivots[i], moveDuration, moveEase);
        }
    }

    private AbilityNoticeItem GetReusableNotice()
    {
        for (int i = 0; i < noticePool.Count; i++)
        {
            AbilityNoticeItem noticeItem = noticePool[i];
            if (noticeItem != null && noticeItem.IsReusable)
                return noticeItem;
        }

        return CreateNoticeItem();
    }

    private void PrewarmPool()
    {
        if (noticePrefab == null)
            return;

        int targetCount = Mathf.Max(prewarmCount, MaxVisibleCount);
        while (noticePool.Count < targetCount)
            CreateNoticeItem();
    }

    private AbilityNoticeItem CreateNoticeItem()
    {
        if (noticePrefab == null)
            return null;

        RectTransform parentTransform = noticeParent != null ? noticeParent : transform as RectTransform;
        AbilityNoticeItem noticeItem = Instantiate(noticePrefab, parentTransform);
        noticeItem.name = noticePrefab.name;
        noticeItem.Initialize(this);
        noticePool.Add(noticeItem);
        return noticeItem;
    }

    private void ResetAllNotices()
    {
        activeNotices.Clear();

        for (int i = 0; i < noticePool.Count; i++)
        {
            AbilityNoticeItem noticeItem = noticePool[i];
            if (noticeItem != null)
                noticeItem.ResetForPool();
        }
    }

    private void BindReferencesIfNeeded()
    {
        if (noticeParent == null)
            noticeParent = transform as RectTransform;

        if (noticePivots == null || noticePivots.Length == 0)
            noticePivots = new RectTransform[5];

        for (int i = 0; i < noticePivots.Length; i++)
        {
            if (noticePivots[i] != null)
                continue;

            Transform pivotTransform = transform.Find("AbilityBG/AbilityNoticePivot_" + i);
            if (pivotTransform == null)
                pivotTransform = transform.Find("AbilityNoticePivot_" + i);

            noticePivots[i] = pivotTransform as RectTransform;
        }
    }
}
