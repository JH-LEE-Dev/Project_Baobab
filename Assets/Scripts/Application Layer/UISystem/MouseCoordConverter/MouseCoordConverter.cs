using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class MouseCoordConverter : MonoBehaviour
{
    // 외부 의존성
    [SerializeField] private Camera uiCamera;        // 렌더 텍스처를 그리는 UI 전용 카메라
    [SerializeField] private RenderTexture targetRT; // 쿼드에 입혀진 렌더 텍스처

    // 내부 의존성
    private GraphicRaycaster uiRaycaster;
    private PointerEventData pointerEventData;
    private List<RaycastResult> raycastResults;
    private GameObject lastHoveredObject;

    public void Awake()
    {
        // 씬 내의 "Canvas" 이름을 가진 객체를 찾아 GraphicRaycaster 획득
        GameObject _canvasObj = GameObject.Find("Canvas");

        if (null != _canvasObj)
        {
            uiRaycaster = _canvasObj.GetComponent<GraphicRaycaster>();
        }

        pointerEventData = new PointerEventData(EventSystem.current);
        raycastResults = new List<RaycastResult>(16);
    }

    private void Update()
    {
        // 런타임에 생성된 캔버스 대응 (Canvas(Clone))
        if (null == uiRaycaster)
        {
            GameObject _canvasObj = GameObject.Find("Canvas(Clone)");

            if (null != _canvasObj)
            {
                uiRaycaster = _canvasObj.GetComponent<GraphicRaycaster>();
            }
        }

        if (null == uiRaycaster || Mouse.current == null || targetRT == null)
            return;

        ProcessInteraction();
    }

    private void ProcessInteraction()
    {
        // 1. 현재 화면(Screen) 마우스 좌표 획득
        Vector2 _mousePos = Mouse.current.position.ReadValue();

        // 2. 화면 비율(0~1)로 변환
        float _normalizedX = _mousePos.x / Screen.width;
        float _normalizedY = _mousePos.y / Screen.height;

        // 3. 렌더 텍스처의 픽셀 좌표로 직접 매핑
        Vector2 _pixelPos = new Vector2(
            _normalizedX * targetRT.width,
            _normalizedY * targetRT.height
        );

        // 4. UI 이벤트 시뮬레이션
        SimulateUIEvents(_pixelPos);
    }

    private void SimulateUIEvents(Vector2 _pixelPos)
    {
        pointerEventData.position = _pixelPos;
        raycastResults.Clear();
        uiRaycaster.Raycast(pointerEventData, raycastResults);

        GameObject _currentHovered = (raycastResults.Count > 0) ? raycastResults[0].gameObject : null;

        // Hover (Enter / Exit) 처리
        if (_currentHovered != lastHoveredObject)
        {
            if (null != lastHoveredObject)
            {
                ExecuteEvents.ExecuteHierarchy(lastHoveredObject, pointerEventData, ExecuteEvents.pointerExitHandler);
            }

            if (null != _currentHovered)
            {
                ExecuteEvents.ExecuteHierarchy(_currentHovered, pointerEventData, ExecuteEvents.pointerEnterHandler);
            }

            lastHoveredObject = _currentHovered;
        }

        // Click 처리
        if (null != _currentHovered)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                ExecuteEvents.Execute(_currentHovered, pointerEventData, ExecuteEvents.pointerDownHandler);
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                ExecuteEvents.Execute(_currentHovered, pointerEventData, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(_currentHovered, pointerEventData, ExecuteEvents.pointerClickHandler);
            }
        }
    }

    private void ClearHoverState()
    {
        if (null == lastHoveredObject)
            return;

        ExecuteEvents.Execute(lastHoveredObject, pointerEventData, ExecuteEvents.pointerExitHandler);
        lastHoveredObject = null;
    }
}
