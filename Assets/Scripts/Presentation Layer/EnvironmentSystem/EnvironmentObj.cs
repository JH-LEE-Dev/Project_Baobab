using UnityEngine;

public class EnvironmentObj : MonoBehaviour
{
    // 관리용 인덱스
    public int PoolIndex { get; set; } = -1;
    public int UpdateIndex { get; set; } = -1;
    public bool bActivated { get; protected set; } = true;

    [SerializeField] public EnvironmentObjType envObjType;

    private Transform _cachedTransform;
    public Transform cachedTransform 
    { 
        get 
        { 
            if (ReferenceEquals(_cachedTransform, null)) 
            {
                _cachedTransform = transform;
            }
            return _cachedTransform; 
        } 
        protected set => _cachedTransform = value; 
    }

    public virtual void Initialize()
    {
        // 초기화 로직 (상속받아 사용)
    }

    public virtual void ManualUpdate()
    {
        // 매 프레임 Manager에 의해 호출됨
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
