using UnityEngine;
using System;
using UnityEngine.Rendering;

public class Tent : MonoBehaviour, IShadowCaster
{
    public event Action<bool> TentInteractEvent;
    public event Action<bool> TentInteractStateChangedEvent;

    private const string PLAYER_TAG = "Player";

    private InputManager inputManager;

    private bool bCanInteract = false;
    private bool bInteract = false;

    private SpriteRenderer sr;

    [SerializeField] private GameObject outLineObject;
    [SerializeField] private GameObject basicObject;

    private CustomSortable customSortable;

    [Header("Shadow")]
    // 그림자 판정 타원. SpriteRenderer.bounds는 쓰지 않는다 - 그림자 스프라이트(BlackSmith_Shadow, 96x80px)에
    // 투명 여백이 많아 bounds가 실제로 보이는 그림자보다 한참 크기 때문이다.
    // 아래 값은 실제 불투명 픽셀 영역(x 4~94, y 10~54 / PPU 32)에서 측정한 것이다.
    //   보이는 그림자 중심 = Shadow 오브젝트 로컬 위치(-1,-1) + 픽셀 중심 보정(0.047, 0.391) = (-0.95, -0.61)
    //   보이는 그림자 반경 = (1.42, 0.70) -> 회전된 타원이 이 안에 들어오도록 단축 0.52 / 장축배율 1.8
    // Scene 뷰에서 이 오브젝트를 선택하면 판정 타원이 그려지니 눈으로 보고 조절하면 된다.
    [SerializeField] private Vector2 shadowEllipseCenter = new Vector2(-0.95f, -0.61f);
    [SerializeField, Min(0f)] private float shadowEllipseRadius = 0.52f;
    [SerializeField, Min(1f)] private float shadowEllipseLengthScale = 1.8f;

    // 타원 중심을 Position에 직접 담으므로 TopShadowOffset은 항상 0이다.
    // (TopShadowOffset은 회전 보정된 로컬 좌표계 값이라 그대로 넣으면 방향이 어긋난다.)
    public Vector2 Position => (Vector2)transform.position + shadowEllipseCenter;
    public float TopShadowRadius => shadowEllipseRadius;
    public Vector2 TopShadowOffset => Vector2.zero;
    public float ShadowLengthScaleOverride => shadowEllipseLengthScale;

#if UNITY_EDITOR
    // 판정 타원의 장축 방향. EnvironmentInteractionManager가 쓰는 로컬 좌표계와 같은 각도(그림자각 + 90도)다.
    // 그림자각은 게임 시작 시 한 번 계산된 뒤 고정되므로(LightingController), 현재 설정값 34도를 기준으로 그린다.
    private const float ShadowPreviewAngle = 34f + 90f;

    private void OnDrawGizmosSelected()
    {
        if (shadowEllipseRadius <= 0f) return;

        DrawShadowEllipseGizmo(transform.position, shadowEllipseCenter, shadowEllipseRadius, shadowEllipseLengthScale, ShadowPreviewAngle);
    }

    // 그림자 판정 타원을 Scene 뷰에 그린다. Tent와 ShopNPC가 같은 방식으로 사용한다.
    public static void DrawShadowEllipseGizmo(Vector3 _origin, Vector2 _center, float _radius, float _lengthScale, float _angle)
    {
        const int Segments = 40;

        Gizmos.color = Color.green;
        Vector3 _worldCenter = _origin + (Vector3)_center;
        Quaternion _rot = Quaternion.Euler(0f, 0f, _angle);
        float _short = _radius;
        float _long = _radius * _lengthScale;

        Vector3 _prev = _worldCenter + _rot * new Vector3(_short, 0f, 0f);
        for (int i = 1; i <= Segments; i++)
        {
            float _t = (float)i / Segments * Mathf.PI * 2f;
            Vector3 _point = _worldCenter + _rot * new Vector3(_short * Mathf.Cos(_t), _long * Mathf.Sin(_t), 0f);
            Gizmos.DrawLine(_prev, _point);
            _prev = _point;
        }
    }
#endif

    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;
        sr = basicObject.GetComponent<SpriteRenderer>();

        customSortable = GetComponentInChildren<CustomSortable>();
        customSortable.Initialize(transform);
        customSortable.SetSortingGroup(GetComponentInChildren<SortingGroup>());

        BindEvents();
    }

    public void Release()
    {
        ReleaseEvents();
    }

    private void BindEvents()
    {
        inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
        inputManager.inputReader.InteractionKeyPressedEvent += InteractionKeyPressed;
    }

    private void ReleaseEvents()
    {
        inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
    }

    private void InteractionKeyPressed()
    {
        if (!bCanInteract) return;

        if (bInteract == true)
        {
            bInteract = false;
            TentInteractEvent?.Invoke(false);
        }
        else
        {
            bInteract = true;
            TentInteractEvent?.Invoke(true);
        }
    }

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (_other.CompareTag(PLAYER_TAG))
        {
            outLineObject.SetActive(true);

            bCanInteract = true;
            TentInteractStateChangedEvent?.Invoke(true);
        }
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
        if (_other.CompareTag(PLAYER_TAG))
        {
            outLineObject.SetActive(false);

            bCanInteract = false;

            // TentUI가 열려 있는 채로 트리거를 벗어나는 경우(넉백 등), bInteract만 조용히 꺼버리면
            // 실제 UI는 열린 채로 남아 있는데 내부 상태만 "닫힘"이 되어버린다. 그러면 다음 E 입력이
            // bCanInteract==false 가드에 걸려 완전히 무시되므로("E를 눌러도 안 닫힘"), 정상적인
            // 닫기 이벤트를 그대로 발행해 UI도 함께 닫는다.
            if (bInteract == true)
            {
                bInteract = false;
                TentInteractEvent?.Invoke(false);
            }

            TentInteractStateChangedEvent?.Invoke(false);
        }
    }

    public void ResetTent()
    {
        bCanInteract = false;
        bInteract = false;
    }

    // TentUI가 E키 토글 경로(InteractionKeyPressed)를 거치지 않고 닫혔을 때(ESC·패드 Cancel 등
    // UIDepthController가 Hide()를 직접 호출하는 경로) bInteract가 true로 남아, 다음 E 입력이
    // "닫기"로 오인되어 무시되는 문제를 막는다. GameplayUICoordinator가 TentUIClosedSignal
    // (UIView_Tent.OnHide에서 닫히는 경로와 무관하게 항상 발행)을 받아 호출한다.
    // bCanInteract(상호작용 가능 범위)는 건드리지 않는다 - 플레이어가 여전히 텐트 앞에 서 있을 수 있다.
    public void SyncInteractStateOnExternalClose()
    {
        bInteract = false;
    }

    private void Update()
    {
        if (customSortable != null)
            customSortable.SetHeight(0f);
    }

    private void LateUpdate()
    {
        if (customSortable != null)
            customSortable.ManualLateUpdate();
    }
}
