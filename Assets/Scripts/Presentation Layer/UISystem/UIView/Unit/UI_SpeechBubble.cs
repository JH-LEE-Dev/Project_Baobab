using UnityEngine;
using UnityEngine.UI;
using TMPro;
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

    private int currentId = -1;
    private float playDuration;
    private float playTimer;
    private bool isPlaying;

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize()
    {
        rootRect = GetComponent<RectTransform>();
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

    public void Hide()
    {
        if (null == motionPlayer)
            return;

        motionPlayer.PlayBackward(absolTag, bReset: true);
    }

    public void Play(int _id, string _text, float _duration = 3f)
    {
        if (isPlaying && currentId == _id)
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
        if (playTimer >= playDuration)
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
