using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전리품 설명 툴팁을 표시하고 화면 밖으로 이탈하지 않도록 보정해 주는 클래스입니다.
/// </summary>
public class HUD_LootTooltip : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text descriptionText;
    
    [Header("Settings")]
    [SerializeField] private float animationDuration = 0.15f;
    [SerializeField] private float yOffset = 10f; // 슬롯과 툴팁 사이의 간격

    private Tween fadeTween;
    private Tween scaleTween;
    
    public void Initialize()
    {
        if (null == canvasGroup)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (null == canvasGroup)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
        
        if (null == rectTransform)
        {
            rectTransform = GetComponent<RectTransform>();
        }
        
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 대상 트랜스폼의 위치를 기준으로 툴팁을 띄웁니다.
    /// </summary>
    public void ShowTooltip(RectTransform _targetSlot, string _descriptionText)
    {
        Debug.Log($"[HUD_LootTooltip] ShowTooltip called. Target: {_targetSlot?.name}, Description: {_descriptionText}");
        if (null == _targetSlot) return;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        // 0. 자동 레이아웃 간섭 완벽 차단
        LayoutElement _layoutElement = GetComponent<LayoutElement>();
        if (null == _layoutElement) _layoutElement = gameObject.AddComponent<LayoutElement>();
        _layoutElement.ignoreLayout = true;

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

        // 1. 애니메이션/찌그러짐 등 스케일 오염 완전 초기화
        rectTransform.localScale = Vector3.one;

        // 2. 텍스트 할당 및 사이즈 강제 계산
        descriptionText.text = _descriptionText;
        descriptionText.ForceMeshUpdate(true);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

        // --- 위치 계산 기본 준비 ---
        Canvas _canvas = GetComponentInParent<Canvas>();
        Camera _cam = (null != _canvas && RenderMode.ScreenSpaceOverlay != _canvas.renderMode) ? _canvas.worldCamera : null;

        Vector3[] _targetCorners = new Vector3[4];
        _targetSlot.GetWorldCorners(_targetCorners);
        // 0: BottomLeft, 1: TopLeft, 2: TopRight, 3: BottomRight

        Vector3 _targetTopCenter = (_targetCorners[1] + _targetCorners[2]) * 0.5f;
        Vector3 _targetBottomCenter = (_targetCorners[0] + _targetCorners[3]) * 0.5f;
        Vector3 _upDir = (_targetCorners[1] - _targetCorners[0]).normalized;
        float _worldOffsetY = yOffset * rectTransform.lossyScale.y;

        // --- 1차 배치 (타겟 위쪽) ---
        // 피벗을 툴팁 밑바닥 중앙(0.5, 0)으로 설정하면 Y축 계산이 무의미해짐 (직접 위치 지정 가능)
        rectTransform.pivot = new Vector2(0.5f, 0f);
        rectTransform.position = _targetTopCenter + (_upDir * _worldOffsetY);

        // --- 2차 검사: 화면 위쪽으로 나가는지? (전리품 가림 방지) ---
        Vector3[] _tooltipCorners = new Vector3[4];
        rectTransform.GetWorldCorners(_tooltipCorners);
        
        Vector2 _screenTR = RectTransformUtility.WorldToScreenPoint(_cam, _tooltipCorners[2]); // 우측 상단
        float _screenPadding = 15f;

        // 카메라가 실제로 그리는 영역을 화면 경계로 삼는다. 크롭(Pillarbox)이 켜진 해상도에서
        // Screen 크기를 쓰면 툴팁이 검은 띠까지 밀려난다. 크롭이 없으면 결과가 같다.
        Rect _viewRect = GlobalUI.GetViewRect();

        if (_viewRect.yMax - _screenPadding < _screenTR.y)
        {
            // 위로 나가면 타겟의 아래쪽으로 즉시 재배치
            rectTransform.pivot = new Vector2(0.5f, 1f); // 피벗을 툴팁 천장 중앙으로 변경
            rectTransform.position = _targetBottomCenter - (_upDir * _worldOffsetY);
            
            // 변경된 위치로 코너 다시 업데이트
            rectTransform.GetWorldCorners(_tooltipCorners);
        }

        // --- 3차 검사: 좌/우 화면 이탈 픽셀 계산 (화면 밖 이탈 방지) ---
        Vector2 _screenBL = RectTransformUtility.WorldToScreenPoint(_cam, _tooltipCorners[0]); // 좌측 하단
        _screenTR = RectTransformUtility.WorldToScreenPoint(_cam, _tooltipCorners[2]);

        float _shiftX = 0f;
        if (_viewRect.xMin + _screenPadding > _screenBL.x)
        {
            _shiftX = (_viewRect.xMin + _screenPadding) - _screenBL.x; // 우측으로 밀어내야 할 픽셀 수
        }
        else if (_viewRect.xMax - _screenPadding < _screenTR.x)
        {
            _shiftX = (_viewRect.xMax - _screenPadding) - _screenTR.x; // 좌측으로 밀어내야 할 픽셀 수 (음수)
        }

        // 밀어내야 할 양이 있다면, 순수 월드 벡터로 치환해서 더해줌
        if (0f != _shiftX)
        {
            Vector3 _worldPosZero, _worldPosShifted;
            RectTransform _rootRect = null != _canvas ? _canvas.GetComponent<RectTransform>() : rectTransform;
            
            RectTransformUtility.ScreenPointToWorldPointInRectangle(_rootRect, Vector2.zero, _cam, out _worldPosZero);
            RectTransformUtility.ScreenPointToWorldPointInRectangle(_rootRect, new Vector2(_shiftX, 0), _cam, out _worldPosShifted);
            
            Vector3 _worldDeltaX = _worldPosShifted - _worldPosZero;
            rectTransform.position += _worldDeltaX;
        }

        // --- UX 애니메이션 연출 ---
        if (null != fadeTween && true == fadeTween.IsActive()) fadeTween.Kill();
        if (null != scaleTween && true == scaleTween.IsActive()) scaleTween.Kill();

        rectTransform.localScale = new Vector3(0.8f, 0.8f, 1f);
        canvasGroup.alpha = 0f;

        fadeTween = canvasGroup.DOFade(1f, animationDuration).SetEase(Ease.OutQuad);
        scaleTween = rectTransform.DOScale(1f, animationDuration).SetEase(Ease.OutBack);
    }

    public void HideTooltip()
    {
        if (false == gameObject.activeInHierarchy)
        {
            return;
        }

        if (null != fadeTween && true == fadeTween.IsActive()) fadeTween.Kill();
        if (null != scaleTween && true == scaleTween.IsActive()) scaleTween.Kill();

        fadeTween = canvasGroup.DOFade(0f, animationDuration * 0.7f).SetEase(Ease.InQuad).OnComplete(() =>
        {
            gameObject.SetActive(false);
        });
        scaleTween = rectTransform.DOScale(0.8f, animationDuration * 0.7f).SetEase(Ease.InQuad);
    }
}
