using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using PresentationLayer.DOTweenAnimationSystem;

public class UI_SpeechBubble : MonoBehaviour
{
    // 외부 의존성
    [SerializeField] private RectTransform bodyRect;
    [SerializeField] private RectTransform tailRect;
    [SerializeField] private TMP_Text speechText;
    [SerializeField] private ObjectMotionPlayer motionPlayer;

    [SerializeField] private string absolTag = "Absol";
    //[SerializeField] private string impactTag = "impact";

    // 내부 의존성
    private Transform targetTransform;
    private RectTransform rootRect;
    private Vector2 offset;
    private HashSet<int> shownIds;

    private int currentId = -1;
    private float playDuration;
    private float playTimer;
    private bool isPlaying;
    
    private bool enableLock = true;

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize()
    {
        rootRect = GetComponent<RectTransform>();

        if (null == shownIds)
            shownIds = new HashSet<int>(32);
        else
            shownIds.Clear();
            
        enableLock = true;
    }
    
    public void SetLockEnabled(bool _enable)
    {
        enableLock = _enable;
    }

    /// <summary>
    /// 특정 ID의 노출 기록을 삭제하여 다시 띄울 수 있도록 합니다.
    /// </summary>
    public void RemoveShownId(int _id)
    {
        if (null != shownIds)
            shownIds.Remove(_id);
    }

    /// <summary>
    /// 여러 ID의 노출 기록을 삭제하여 다시 띄울 수 있도록 합니다.
    /// </summary>
    public void RemoveShownIds(IReadOnlyList<int> _ids)
    {
        if (null == shownIds || null == _ids)
            return;

        for (int _i = 0; _i < _ids.Count; _i++)
            shownIds.Remove(_ids[_i]);
    }

    /// <summary>
    /// 모든 노출 기록을 삭제하여 모든 ID를 다시 띄울 수 있도록 합니다.
    /// </summary>
    public void RemoveAllShownIds()
    {
        if (null != shownIds)
            shownIds.Clear();
    }

    /// <summary>
    /// 씬 전환 또는 재도전 시 말풍선 연출 및 타이머 상태를 즉시 초기화하고 노출 기록을 비웁니다.
    /// </summary>
    public void ResetSpeechBubble()
    {
        Hide(_bSkip: true);
        isPlaying = false;
        playTimer = 0f;
        currentId = -1;
        RemoveAllShownIds();
    }



    /// <summary>
    /// 특정 ID를 노출 완료 상태로 인위적으로 등록하여 이후 말풍선이 띄워지지 않도록 차단합니다.
    /// </summary>
    public void AddShownId(int _id)
    {
        if (null != shownIds)
            shownIds.Add(_id);
    }

    /// <summary>
    /// 여러 ID를 노출 완료 상태로 인위적으로 등록하여 이후 말풍선들이 띄워지지 않도록 차단합니다.
    /// </summary>
    public void AddShownIds(IReadOnlyList<int> _ids)
    {
        if (null == shownIds || null == _ids)
            return;

        for (int _i = 0; _i < _ids.Count; _i++)
            shownIds.Add(_ids[_i]);
    }

    public void SetTarget(Transform _target, Vector2 _offset)
    {
        targetTransform = _target;
        offset = _offset;
    }

    public void SetText(string _text)
    {
        if (null == speechText)
            return;

        speechText.text = _text;
        UpdateLayout();
    }

    public void Show()
    {
        if (null == motionPlayer)
            return;

        motionPlayer.Play(absolTag, bReset: true);
    }

    public void Hide(bool _bSkip = false)
    {
        if (null == motionPlayer)
            return;

        motionPlayer.PlayBackward(absolTag, bReset: true, _skip: _bSkip);
    }

    public void Play(int _id, string _text, float _duration = 3f)
    {
        if (true == enableLock && false == shownIds.Add(_id))
            return;

        currentId = _id;
        SetText(_text);
        Show();

        playDuration = _duration;
        playTimer = 0f;
        isPlaying = true;
    }

    public void UpdateLayout()
    {
        if (null == bodyRect || null == tailRect)
            return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(bodyRect);

        Vector2 bodySize = bodyRect.rect.size;
        float targetX = bodyRect.localPosition.x;
        targetX += (0.5f - bodyRect.pivot.x) * bodySize.x;

        Vector3 tailPos = tailRect.localPosition;
        tailPos.x = targetX;
        tailRect.localPosition = tailPos;
    }

    // 유니티 이벤트 함수

    private void Update()
    {
        if (false == isPlaying)
            return;

        playTimer += Time.deltaTime;
        if (playDuration <= playTimer)
        {
            Hide();
            isPlaying = false;
            currentId = -1;
        }
    }

    private void LateUpdate()
    {
        if (null == targetTransform || null == rootRect)
            return;

        Vector2 newPos = targetTransform.position;
        newPos += offset;

        rootRect.position = newPos;
    }
}
