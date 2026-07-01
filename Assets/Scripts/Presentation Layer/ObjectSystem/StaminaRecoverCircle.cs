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
