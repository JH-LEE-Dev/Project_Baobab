using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 유니티 기본 Scroll Rect를 사용하지 않고 개별 커스텀 로직으로 상하 드래그 스크롤을 구현한 컴포넌트입니다.
/// 가비지 컬렉션(GC) 할당을 0으로 유지하며 Yoda 표기법을 준수합니다.
/// </summary>
public class UI_CustomScroll : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
{
    [Header("References")]
    [SerializeField, Tooltip("스크롤될 내용물 (예: Content)")] 
    private RectTransform content;
    [SerializeField, Tooltip("내용물이 보여지는 부모 뷰포트 (보통 이 스크립트가 달린 패널)")] 
    private RectTransform viewport;

    [Header("Settings")]
    [SerializeField, Tooltip("마우스 휠 스크롤 감도")] 
    private float scrollSensitivity = 25f;
    [SerializeField, Tooltip("드래그 이동 감도")] 
    private float dragSensitivity = 1f;

    [Header("Scrollbar (Optional)")]
    [SerializeField, Tooltip("연동할 수직 스크롤바")]
    private Scrollbar verticalScrollbar;

    // 내부 상태
    private Vector2 lastPointerPosition;
    private bool isSyncingScrollbar = false;

    private void Awake()
    {
        if (null == viewport)
        {
            viewport = GetComponent<RectTransform>();
        }

        if (null != verticalScrollbar)
        {
            verticalScrollbar.onValueChanged.AddListener(OnScrollbarValueChanged);
        }
    }

    private void Start()
    {
        UpdateScrollbarSize();
        SyncScrollbarFromContent();
    }

    private void OnEnable()
    {
        UpdateScrollbarSize();
        SyncScrollbarFromContent();
    }

    public void OnBeginDrag(PointerEventData _eventData)
    {
        if (null == viewport) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewport, 
            _eventData.position, 
            _eventData.pressEventCamera, 
            out lastPointerPosition
        );
    }

    public void OnDrag(PointerEventData _eventData)
    {
        if (null == content || null == viewport) return;

        Vector2 _localPointerPosition;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewport, 
            _eventData.position, 
            _eventData.pressEventCamera, 
            out _localPointerPosition))
        {
            Vector2 _delta = _localPointerPosition - lastPointerPosition;
            
            // 상하 스크롤만 적용
            float _newY = content.anchoredPosition.y + (_delta.y * dragSensitivity);
            
            // 위아래 경계 제한 계산
            _newY = ClampYPosition(_newY);
            
            // 구조체(Vector2) 할당은 스택에 생성되므로 GC가 발생하지 않음
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, _newY);
            lastPointerPosition = _localPointerPosition;

            SyncScrollbarFromContent();
        }
    }

    public void OnScroll(PointerEventData _eventData)
    {
        if (null == content || null == viewport) return;

        // 휠을 내리면 scrollDelta.y는 음수가 나오므로 이동 방향에 맞게 빼줌
        float _newY = content.anchoredPosition.y - (_eventData.scrollDelta.y * scrollSensitivity);
        
        _newY = ClampYPosition(_newY);
        
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, _newY);

        SyncScrollbarFromContent();
    }

    private float ClampYPosition(float _targetY)
    {
        // ※ 주의: 이 로직은 Content의 Pivot이 Y=1 (Top)일 때 완벽히 작동합니다.
        float _contentHeight = content.rect.height;
        float _viewportHeight = viewport.rect.height;

        // 컨텐츠가 뷰포트보다 작거나 같으면 스크롤 불필요 (맨 위 고정)
        if (_contentHeight <= _viewportHeight)
        {
            return 0f;
        }

        // 최대 이동 가능 거리는 (컨텐츠 전체 높이 - 보이는 뷰포트 높이)
        float _maxY = _contentHeight - _viewportHeight;
        
        // 범위 밖으로 넘어가지 않도록 클램핑
        if (0f > _targetY) return 0f;
        if (_targetY > _maxY) return _maxY;

        return _targetY;
    }

    private void OnScrollbarValueChanged(float _value)
    {
        if (true == isSyncingScrollbar || null == content || null == viewport) return;

        float _contentHeight = content.rect.height;
        float _viewportHeight = viewport.rect.height;
        float _maxY = _contentHeight - _viewportHeight;

        if (0f >= _maxY) return;

        // 스크롤바 value 1이 맨 위(Y=0), value 0이 맨 아래(Y=_maxY) 라고 가정
        float _targetY = (1f - _value) * _maxY;
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, _targetY);
    }

    private void SyncScrollbarFromContent()
    {
        if (null == verticalScrollbar || null == content || null == viewport) return;

        float _contentHeight = content.rect.height;
        float _viewportHeight = viewport.rect.height;
        float _maxY = _contentHeight - _viewportHeight;

        isSyncingScrollbar = true;

        if (0f >= _maxY)
        {
            verticalScrollbar.value = 1f;
        }
        else
        {
            float _currentY = content.anchoredPosition.y;
            // Y=0일때 value 1, Y=_maxY일때 value 0
            verticalScrollbar.value = 1f - Mathf.Clamp01(_currentY / _maxY);
        }

        isSyncingScrollbar = false;
    }

    public void UpdateScrollbarSize()
    {
        if (null == verticalScrollbar || null == content || null == viewport) return;

        if (true == content.gameObject.activeInHierarchy)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        float _contentHeight = content.rect.height;
        float _viewportHeight = viewport.rect.height;

        if (0f >= _contentHeight || 0f >= _viewportHeight)
        {
            verticalScrollbar.size = 1f;
            verticalScrollbar.interactable = false;
            return;
        }

        float _sizeRatio = _viewportHeight / _contentHeight;
        
        if (_sizeRatio >= 1f)
        {
            // 뷰포트가 컨텐츠보다 크거나 같으면 스크롤 불필요
            verticalScrollbar.size = 1f;
            verticalScrollbar.interactable = false;
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
        }
        else
        {
            // 뷰포트가 컨텐츠보다 작으면 스크롤 필요 (최소 핸들 크기 0.1f 보장)
            verticalScrollbar.size = Mathf.Clamp(_sizeRatio, 0.1f, 1f);
            verticalScrollbar.interactable = true;

            // 컨텐츠 높이가 줄어들었을 경우 현재 위치가 범위를 벗어날 수 있으므로 클램핑 보정
            float _newY = ClampYPosition(content.anchoredPosition.y);
            if (_newY != content.anchoredPosition.y)
            {
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, _newY);
            }
        }

        SyncScrollbarFromContent();
    }

    public void SetContent(RectTransform _newContent)
    {
        if (content == _newContent) return;
        content = _newContent;
        UpdateScrollbarSize();
        SyncScrollbarFromContent();
    }

    public void ResetScrollPosition()
    {
        if (null == content) return;
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
        SyncScrollbarFromContent();
    }

    public void EnsureVisible(RectTransform _target)
    {
        if (null == _target || null == viewport) return;

        // 뷰포트 내부의 요소가 아닌 고정 UI(전체 초기화 버튼, 적용/닫기 버튼 등)는 스크롤하지 않고 무시한다.
        if (false == _target.IsChildOf(viewport)) return;

        if (null == content || false == content.gameObject.activeInHierarchy || false == _target.IsChildOf(content))
        {
            Transform _curr = _target;
            while (null != _curr && _curr.parent != viewport && null != _curr.parent)
            {
                _curr = _curr.parent;
            }

            if (null != _curr && _curr is RectTransform _contentRt)
            {
                SetContent(_contentRt);
            }
        }

        if (null == content) return;

        float _contentHeight = content.rect.height;
        if (0f >= _contentHeight)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            _contentHeight = content.rect.height;
        }

        float _viewportHeight = viewport.rect.height;
        if (_contentHeight <= _viewportHeight) return;

        Vector3 _targetWorldPos = _target.TransformPoint(_target.rect.center);
        Vector3 _targetInViewport = viewport.InverseTransformPoint(_targetWorldPos);

        float _targetHalfHeight = _target.rect.height * 0.5f;
        float _targetTop = _targetInViewport.y + _targetHalfHeight;
        float _targetBottom = _targetInViewport.y - _targetHalfHeight;

        float _viewportTop = viewport.rect.yMax;
        float _viewportBottom = viewport.rect.yMin;

        float _scrollDelta = 0f;
        const float _padding = 10f;

        if (_targetTop > _viewportTop - _padding)
        {
            _scrollDelta = _targetTop - (_viewportTop - _padding);
        }
        else if (_viewportBottom + _padding > _targetBottom)
        {
            _scrollDelta = _targetBottom - (_viewportBottom + _padding);
        }

        if (0f != _scrollDelta)
        {
            float _newY = ClampYPosition(content.anchoredPosition.y - _scrollDelta);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, _newY);
            SyncScrollbarFromContent();
        }
    }

    private void OnDestroy()
    {
        if (null != verticalScrollbar)
        {
            verticalScrollbar.onValueChanged.RemoveListener(OnScrollbarValueChanged);
        }
    }
}
