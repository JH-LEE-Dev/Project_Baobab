using UnityEngine;
using UnityEngine.InputSystem;

public class UI_TentAbilityComponent : MonoBehaviour
{
    private const float DefaultZoom = 1f;
    private const float MinZoom = 0.5f;
    private const float MaxZoom = 3f;
    private const float ZoomStep = 0.1f;
    private const float ZoomFollowSpeed = 18f;

    private ISkillSystemProvider skillSystemProvider;
    private Canvas rootCanvas;
    private bool isDragging;
    private Vector2 previousMousePosition;
    private float currentZoom = DefaultZoom;
    private float targetZoom = DefaultZoom;

    [Header("UI References")]
    [SerializeField] private RectTransform abilityBackground;
    [SerializeField] private RectTransform moveTarget;

    // 추후 특성 UI 구현에 사용할 의존성을 초기화한다.
    public void Initialize(ISkillSystemProvider _skillSystemProvider)
    {
        skillSystemProvider = _skillSystemProvider;
        rootCanvas = GetComponentInParent<Canvas>();
        Close();
    }

    // 능력 버튼을 눌렀을 때 호출될 특성 UI 열기 진입점이다.
    public void Open()
    {
        if (abilityBackground == null)
            return;

        abilityBackground.gameObject.SetActive(true);
        ResetView();
    }

    // 테스트용 텍스처의 위치와 확대 값을 초기화
    private void ResetView()
    {
        if (moveTarget == null)
            return;

        isDragging = false;
        currentZoom = DefaultZoom;
        targetZoom = DefaultZoom;
        moveTarget.anchoredPosition = Vector2.zero;
        moveTarget.localScale = Vector3.one * currentZoom;
    }


    // 능력 UI를 닫고 드래그 상태를 초기화
    public void Close()
    {
        isDragging = false;

        if (abilityBackground != null)
            abilityBackground.gameObject.SetActive(false);
    }

    // 능력 UI가 열려 있는 동안 드래그 이동과 휠 줌을 처리
    public void Tick()
    {
        if (abilityBackground == null || abilityBackground.gameObject.activeSelf == false || moveTarget == null)
            return;

        HandlePan();
        HandleZoom();
        UpdateZoomAnimation();
    }

    // 좌, 우, 휠 클릭 드래그로 테스트용 텍스처를 이동시킨다.
    private void HandlePan()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        bool canDrag =
            mouse.leftButton.isPressed ||
            mouse.rightButton.isPressed ||
            mouse.middleButton.isPressed;

        Vector2 currentMousePosition = mouse.position.ReadValue();

        if (canDrag == false)
        {
            isDragging = false;
            return;
        }

        if (isDragging == false)
        {
            isDragging = true;
            previousMousePosition = currentMousePosition;
            return;
        }

        Vector2 delta = currentMousePosition - previousMousePosition;
        previousMousePosition = currentMousePosition;

        float scaleFactor = 1f;
        if (rootCanvas != null)
            scaleFactor = Mathf.Max(rootCanvas.rootCanvas.scaleFactor, 0.0001f);

        moveTarget.anchoredPosition += delta / scaleFactor;
    }

    // 마우스 휠로 테스트용 텍스처를 확대 및 축소한다.
    private void HandleZoom()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        float scrollY = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scrollY, 0f))
            return;

        targetZoom += Mathf.Sign(scrollY) * ZoomStep;
        targetZoom = Mathf.Clamp(targetZoom, MinZoom, MaxZoom);
    }

    /// 목표 줌 값을 향해 현재 줌을 빠르게 보간
    private void UpdateZoomAnimation()
    {
        currentZoom = Mathf.Lerp(currentZoom, targetZoom, 1f - Mathf.Exp(-ZoomFollowSpeed * Time.unscaledDeltaTime));

        if (Mathf.Abs(currentZoom - targetZoom) < 0.001f)
            currentZoom = targetZoom;

        moveTarget.localScale = Vector3.one * currentZoom;
    }
}
