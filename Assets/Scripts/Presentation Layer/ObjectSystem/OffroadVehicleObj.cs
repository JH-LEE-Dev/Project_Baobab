using System;
using UnityEngine;

public class OffroadVehicleObj : MonoBehaviour
{
    private IEnvironmentProvider environmentProvider;

    //이벤트
    public event Action PortalActivated;

    //내부 의존성
    private int characterLayer;
    [SerializeField] private PortalType type;
    [SerializeField] private float cooldownTime = 2.0f; // 쿨타임 설정
    private float lastActivatedTime = -10.0f; // 마지막 활성화 시간 (초기값은 충분히 과거로 설정)

    private bool bCanJump = false;

    [SerializeField] private RaymarchingShadow baseShadow;
    [SerializeField] private RaymarchingShadow wheelShadow;

    //퍼블릭 초기화 및 제어 메서드
    public void Initialize(PortalType _type, IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;
        type = _type;
        characterLayer = LayerMask.NameToLayer("Character");

        lastActivatedTime = Time.time;
    }

    private void Update()
    {
        UpdateShadow(baseShadow);
        UpdateShadow(wheelShadow);
    }

    private void UpdateShadow(RaymarchingShadow shadow)
    {
        if (shadow == null)
        {
            return;
        }

        shadow.ManualUpdate(
            environmentProvider.shadowDataProvider.CurrentShadowRotation,
            environmentProvider.shadowDataProvider.CurrentShadowScaleY,
            environmentProvider.shadowDataProvider.IsShadowActive
        );
    }

    public void ResetPortal()
    {
        lastActivatedTime = Time.time;
    }

    //유니티 이벤트 함수
    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (bCanJump == false)
            return;
            
        // 캐릭터 레이어인지 확인 및 쿨타임 체크
        if (_other.gameObject.layer == characterLayer && Time.time >= lastActivatedTime + cooldownTime)
        {
            lastActivatedTime = Time.time;

            PortalActivated?.Invoke();
        }
    }

    public void SetCanTravel(bool _canJump)
    {
        bCanJump = _canJump;
    }

}
