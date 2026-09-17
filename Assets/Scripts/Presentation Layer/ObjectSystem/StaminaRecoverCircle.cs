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

    // 휴식 구역에 들어가기 직전의 소모 상태. 나갈 때 true를 박는 대신 이 값으로 되돌린다.
    // bStaminaDecrease는 소유자 구분이 없는 단일 bool이라, 남의 잠금 위에 겹쳐 잠그는 쪽이
    // 무조건 true로 풀어버리면 그 잠금까지 함께 열린다.
    // (선례: KnockBackState.bMovePausedBeforeKnockBack, GameplayUICoordinator.bMovePausedBeforeEsc)
    private bool bStaminaDecreaseBeforeRest = true;

    // [기획 확정] 휴식 구역 안에서는 어떤 스태미나 피해도 들어오지 않고 회복만 돈다.
    //   일반 소모뿐 아니라 용암 지속 피해와 나무 열기(환경 피해)까지 전부 멈춘다.
    //   셋을 따로 끄지 않아도 되는 이유는 PHealthComponent의 환경 피해 두 경로
    //   (ApplyEnvironmentalStaminaDrain / DecreaseStaminaFlat)가 일반 소모와 같은 bStaminaDecrease를
    //   따르도록 맞춰 두었기 때문이다. SetStaminaDecrease(false) 한 번이 셋 모두를 멈춘다.
    //   (경위는 PHealthComponent.DecreaseStaminaFlat의 [소모 정지와 환경 피해] 주석 참고)
    //
    // [예전에 막혀 있던 것들 - 전부 해소됨. 다시 같은 함정에 빠지지 않도록 남긴다]
    //  1) 이 컴포넌트는 한동안 한 줄도 실행되지 않았다. OffroadVehicleObj가 넘겨주는 _charTransform은
    //     캐릭터 루트가 아니라 character.centerTransform(자식 피벗)인데
    //     (InDungeonObjectManager.ReadyPortal -> OffroadVehicleObj.Initialize),
    //     자식에서 GetComponent<Character>()를 하면 null이라 Update 첫 줄 가드에서 매 프레임 즉시
    //     반환했다. 지금은 RepairBox.Initialize와 같이 부모에서 찾는다.
    //  2) 참조를 고치면 특성과 무관하게 소모 정지가 켜져 차량 주변이 안전지대가 되어버린다.
    //     그래서 Update에 [개방 게이트]를 두어 staminaRecoverAmount > 0 일 때만 관여한다.
    //  3) bStaminaDecrease는 소유자 구분이 없는 단일 bool이라 InDungeonObjectManager.GameEnd()/
    //     AbortGameEnd(true)와 Character.StartDecreaseStamina()도 같은 값을 건드린다. 경계를 넘는
    //     순간에만 값을 쓰면 그 사이 누군가 true로 덮었을 때 원 안에 있는데도 소모가 다시 시작된다.
    //     그래서 원 안에서는 매 프레임 다시 꺼두고, 나갈 때는 true를 박는 대신 들어오기 직전 값
    //     (bStaminaDecreaseBeforeRest)으로 되돌린다.
    //     소유자별 잠금(InputReader의 escLockOwners)까지 가지 않은 것은, 이 플래그를 건드리는 쪽 중
    //     StartDecreaseStamina처럼 "누가 걸었든 전부 켠다"는 의미의 블랭킷 호출이 있어서다.
    //     소유자 집합으로 바꾸면 그 호출들의 의미가 달라진다. (InputReader.PauseMove 주석과 같은 판단)

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

        // 사망 중에는 관여하지 않는다. 사망 처리가 스스로 소모를 끄므로 여기서 덮어쓸 이유가 없다.
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
                bIsCharacterInside = true;
                bStaminaDecreaseBeforeRest = character.IsStaminaDecreasing;
            }

            // 매 프레임 다시 꺼둔다. 경계를 넘는 순간에만 쓰면, 그 사이 AbortGameEnd(true)나
            // StartDecreaseStamina()가 true로 덮었을 때 원 안에 있는데도 소모가 다시 시작되고
            // 밖으로 나갔다 들어오기 전까지 복구되지 않는다.
            // 소모 정지는 일반 소모뿐 아니라 용암 지속 피해와 나무 열기까지 함께 멈춘다
            // (PHealthComponent의 [소모 정지와 환경 피해] 참고) - 휴식 구역은 회복만 하고
            // 어떤 피해도 받지 않는 것이 기획 의도다.
            character.SetStaminaDecrease(false);

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

    // 원 밖으로 나갔거나, 사망/미개방 등으로 더 이상 관여하지 않을 때 원래 소모 상태로 되돌린다.
    // 들어온 적이 없으면 아무 일도 하지 않으므로 매 프레임 불러도 안전하다.
    private void ExitRestArea()
    {
        if (!bIsCharacterInside) return;

        bIsCharacterInside = false;

        if (character != null)
        {
            character.SetStaminaDecrease(bStaminaDecreaseBeforeRest);
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
