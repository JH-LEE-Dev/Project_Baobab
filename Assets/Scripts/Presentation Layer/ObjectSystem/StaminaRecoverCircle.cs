using UnityEngine;

public class StaminaRecoverCircle : MonoBehaviour
{
    private Transform charTransform;
    private Character character;
    private PHealthComponent pHealthComponent;

    [Header("Ellipse Settings")]
    public float radiusX = 3f;
    public float radiusY = 1.5f;

    private bool bIsCharacterInside = false;

    // [주의] 이 컴포넌트는 지금 한 줄도 실행되지 않는다. 아래 GetComponent가 항상 null을 돌려주고
    // Update()의 첫 줄 가드에서 매 프레임 즉시 반환하기 때문이다. "휴식"을 개방할 때 반드시 함께 처리할 것.
    //
    // 원인: OffroadVehicleObj가 넘겨주는 _charTransform은 캐릭터 루트가 아니라 character.centerTransform,
    //       즉 자식 피벗이다(InDungeonObjectManager.ReadyPortal -> OffroadVehicleObj.Initialize).
    //       Character 컴포넌트는 그 부모에 붙어 있으므로 자식에서 GetComponent<Character>()를 하면 null이다.
    //       같은 인자를 받는 RepairBox.Initialize()는 _characterTransform.parent.GetComponent<Character>()로
    //       올바르게 해결하고 있으니 그쪽과 맞추면 된다.
    //
    // 개방 시 함께 확인할 것 두 가지:
    //  1) 위 참조를 고치면 특성과 무관하게 "소모 정지"(SetStaminaDecrease(false))가 즉시 켜진다.
    //     staminaRecoverAmount는 회복량에만 쓰이고 소모 정지에는 관여하지 않으므로, 미개방 상태에서
    //     차량 주변이 안전지대가 되어버린다. 개방 여부 게이트가 별도로 필요하다.
    //  2) bStaminaDecrease는 소유자 구분이 없는 단일 bool이라 InDungeonObjectManager.GameEnd()/
    //     AbortGameEnd(true)와 Character.StartDecreaseStamina()도 같은 값을 건드린다. 아래 로직은
    //     타원 경계를 넘는 순간에만 값을 쓰므로, 그 사이 누군가 true로 덮어쓰면 플레이어가 원 안에
    //     있는데도 다시 소모가 시작되고 밖으로 나갔다 들어오기 전까지 복구되지 않는다.
    //     (InputReader의 escLockOwners/inventoryLockOwners 같은 소유자별 잠금이 정석)
    public void Initialize(Transform _charTransform)
    {
        charTransform = _charTransform;
        if (charTransform != null)
        {
            character = charTransform.GetComponent<Character>();
            if (character != null)
            {
                pHealthComponent = character.pHealthComponent as PHealthComponent;
            }
        }
    }

    private void Update()
    {
        if (charTransform == null || character == null || pHealthComponent == null) return;

        Vector3 diff = charTransform.position - transform.position;
        float x = diff.x;
        float y = diff.y;

        // 타원 방정식 검사: (x^2 / a^2) + (y^2 / b^2) <= 1
        bool isInside = ((x * x) / (radiusX * radiusX)) + ((y * y) / (radiusY * radiusY)) <= 1f;

        if (isInside)
        {
            if (!bIsCharacterInside)
            {
                bIsCharacterInside = true;
                character.SetStaminaDecrease(false);
            }

            // 초당 staminaRecoverAmount만큼 회복
            float recoverAmount = character.statComponent.staminaRecoverAmount * Time.deltaTime;
            if (recoverAmount > 0f)
            {
                pHealthComponent.StaminaRecover(recoverAmount);
            }
        }
        else
        {
            if (bIsCharacterInside)
            {
                bIsCharacterInside = false;
                character.SetStaminaDecrease(true);
            }
        }
    }

    private void OnDisable()
    {
        // 컴포넌트가 꺼질 때 캐릭터가 안에 있었다면 원상 복구
        if (bIsCharacterInside && character != null)
        {
            bIsCharacterInside = false;
            character.SetStaminaDecrease(true);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        DrawEllipse(transform.position, radiusX, radiusY, 32);
    }

    private void DrawEllipse(Vector3 center, float rx, float ry, int segments)
    {
        if (rx <= 0 || ry <= 0) return;

        float angle = 0f;
        float step = 360f / segments;

        Vector3 prevPos = center + new Vector3(Mathf.Cos(0) * rx, Mathf.Sin(0) * ry, 0);

        for (int i = 1; i <= segments; i++)
        {
            angle += step;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 nextPos = center + new Vector3(Mathf.Cos(rad) * rx, Mathf.Sin(rad) * ry, 0);
            Gizmos.DrawLine(prevPos, nextPos);
            prevPos = nextPos;
        }
    }
}
