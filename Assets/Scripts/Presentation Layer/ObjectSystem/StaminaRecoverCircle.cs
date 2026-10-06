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

    // [기획 확정] 휴식 구역 안에서는 어떤 스태미나 피해도 들어오지 않고 회복만 돈다.
    //   일반 소모뿐 아니라 용암 지속 피해와 나무 열기(환경 피해)까지 전부 멈춘다.
    //   PHealthComponent의 세 경로(DecreaseStamina / ApplyEnvironmentalStaminaDrain / DecreaseStaminaFlat)가
    //   모두 휴식 구역 전용 플래그(SetInRestArea)를 함께 보므로, 그 값 하나가 셋을 모두 멈춘다.
    //
    // [예전에 막혀 있던 것들 - 전부 해소됨. 다시 같은 함정에 빠지지 않도록 남긴다]
    //  1) 이 컴포넌트는 한동안 한 줄도 실행되지 않았다. OffroadVehicleObj가 넘겨주는 _charTransform은
    //     캐릭터 루트가 아니라 character.centerTransform(자식 피벗)인데
    //     (InDungeonObjectManager.ReadyPortal -> OffroadVehicleObj.Initialize),
    //     자식에서 GetComponent<Character>()를 하면 null이라 Update 첫 줄 가드에서 매 프레임 즉시
    //     반환했다. 지금은 RepairBox.Initialize와 같이 부모에서 찾는다.
    //  2) 참조를 고치면 특성과 무관하게 소모 정지가 켜져 차량 주변이 안전지대가 되어버린다.
    //     그래서 Update에 [개방 게이트]를 두어 staminaRecoverAmount > 0 일 때만 관여한다.
    //  3) 한동안 공용 소모 스위치(SetStaminaDecrease)를 같이 썼다. 그 스위치는 원정 시작
    //     (StartDecreaseStamina) · 경고창(GameEnd/AbortGameEnd) · 귀환 확정(HandleGameEnd)도 쓰므로,
    //     처음엔 경계에서만 쓰다가 남이 덮어쓰는 문제가, 그 다음엔 "들어오기 직전 값"을 저장했다가
    //     되돌리는 방식이 입장 직후 false(소모 시작 전)를 저장해 버리는 문제가 생겼다. 스폰 지점이
    //     원 한가운데라, 조작이 풀리고 1.4초 안에 원을 벗어나지 않으면 소모 시작 신호를 원이 덮고
    //     나갈 때 false로 되돌려 그 원정 내내 스태미나가 전혀 닳지 않았다.
    //     지금은 휴식 구역 전용 플래그만 쓰고 공용 스위치는 건드리지 않는다.

    public void Initialize(Transform _charTransform)
    {
        charTransform = _charTransform;

        // 넘어오는 것은 캐릭터 루트가 아니라 character.centerTransform(자식 피벗)이다.
        // Character 컴포넌트는 그 부모에 붙어 있으므로 부모에서 찾아야 한다. (RepairBox.Initialize와 동일)
        if (charTransform != null && charTransform.parent != null)
        {
            character = charTransform.parent.GetComponent<Character>();
            if (character != null)
            {
                pHealthComponent = character.pHealthComponent as PHealthComponent;
            }
        }
    }

    private void Update()
    {
        if (charTransform == null || character == null || pHealthComponent == null) return;

        // 사망 중에는 관여하지 않는다. 사망 처리가 공용 스위치로 소모를 끄므로 휴식 상태만 풀어 둔다.
        if (character.bDead)
        {
            ExitRestArea();
            return;
        }

        // [개방 게이트] "휴식" 특성을 찍기 전에는 차량 주변이 안전지대가 되면 안 된다.
        // staminaRecoverAmount는 특성으로만 0보다 커지므로 이 값이 곧 개방 여부다.
        // (RepairBox가 repairBoxCount > 0으로 같은 판단을 하는 것과 같은 방식)
        if (character.statComponent == null || character.statComponent.staminaRecoverAmount <= 0f)
        {
            ExitRestArea();
            return;
        }

        Vector3 diff = charTransform.position - transform.position;
        float x = diff.x;
        float y = diff.y;

        // 타원 방정식 검사: (x^2 / a^2) + (y^2 / b^2) <= 1
        bool isInside = ((x * x) / (radiusX * radiusX)) + ((y * y) / (radiusY * radiusY)) <= 1f;

        if (isInside)
        {
            if (!bIsCharacterInside)
            {
                // 전용 플래그는 이 컴포넌트만 쓰므로 경계를 넘는 순간에만 써도 남이 덮어쓰지 않는다.
                bIsCharacterInside = true;
                pHealthComponent.SetInRestArea(true);
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
            ExitRestArea();
        }
    }

    // 원 밖으로 나갔거나, 사망/미개방 등으로 더 이상 관여하지 않을 때 휴식 상태를 푼다.
    // 들어온 적이 없으면 아무 일도 하지 않으므로 매 프레임 불러도 안전하다.
    private void ExitRestArea()
    {
        if (!bIsCharacterInside) return;

        bIsCharacterInside = false;

        if (pHealthComponent != null)
        {
            pHealthComponent.SetInRestArea(false);
        }
    }

    private void OnDisable()
    {
        // 컴포넌트가 꺼질 때 캐릭터가 안에 있었다면 원상 복구
        ExitRestArea();
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
