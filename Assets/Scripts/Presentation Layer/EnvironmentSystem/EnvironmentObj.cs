using UnityEngine;

public class EnvironmentObj : MonoBehaviour
{
    // 관리용 인덱스
    public int PoolIndex { get; set; } = -1;
    public int UpdateIndex { get; set; } = -1;
    public bool bActivated { get; protected set; } = true;

    [SerializeField] public EnvironmentObjType envObjType;

    public virtual void Initialize()
    {
        // 초기화 로직 (상속받아 사용)
    }

    public virtual Vector3 GetCurrentPosition()
    {
        return transform.position;
    }

    public virtual void Show()
    {
        gameObject.SetActive(true);
        bActivated = true;
    }

    public virtual void Hide()
    {
        gameObject.SetActive(false);
        bActivated = false;
    }

    public virtual void ResetObj()
    {
        bActivated = true;
        PoolIndex = -1;
        UpdateIndex = -1;
    }

    public virtual void DeActivate()
    {
        // 비활성화 로직 (상속받아 사용)
    }
}
