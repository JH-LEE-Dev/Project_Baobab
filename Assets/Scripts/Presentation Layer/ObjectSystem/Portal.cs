using System;
using UnityEngine;

public class Portal : MonoBehaviour
{
    //이벤트
    public event Action<PortalType> PortalActivated;

    //내부 의존성
    private int characterLayer;
    [SerializeField] private PortalType type;

    //퍼블릭 초기화 및 제어 메서드
    public void Initialize(PortalType _type)
    {
        type = _type;
        characterLayer = LayerMask.NameToLayer("Character");
    }

    //유니티 이벤트 함수
    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (_other.gameObject.layer == characterLayer)
        {
            PortalActivated?.Invoke(type);
        }
    }
}
