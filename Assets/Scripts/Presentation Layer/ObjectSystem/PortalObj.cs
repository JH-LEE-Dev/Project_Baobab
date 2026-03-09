using System;
using UnityEngine;

public class PortalObj : MonoBehaviour
{
    //이벤트
    public event Action<PortalType> PortalActivated;

    //내부 의존성
    private int characterLayer;
    [SerializeField] private PortalType type;
    [SerializeField] private float cooldownTime = 5.0f; // 쿨타임 설정
    private float lastActivatedTime = -10.0f; // 마지막 활성화 시간 (초기값은 충분히 과거로 설정)

    //퍼블릭 초기화 및 제어 메서드
    public void Initialize(PortalType _type)
    {
        type = _type;
        characterLayer = LayerMask.NameToLayer("Character");

        lastActivatedTime = Time.time;
    }

    //유니티 이벤트 함수
    private void OnTriggerEnter2D(Collider2D _other)
    {
        // 캐릭터 레이어인지 확인 및 쿨타임 체크
        if (_other.gameObject.layer == characterLayer && Time.time >= lastActivatedTime + cooldownTime)
        {
            lastActivatedTime = Time.time;
            PortalActivated?.Invoke(type);
        }
    }
}
