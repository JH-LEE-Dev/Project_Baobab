using System;
using UnityEngine;

/// <summary>
/// 로비에서 캐릭터가 "쳐서" 실행하는 오브젝트(기획서 4-2 "버튼을 누르지 않고, 캐릭터로 친다").
///
/// 나무와 같은 충돌 레이어에 등록되어 도끼 판정(AttackComponent)이 그대로 맞힌다. IDamageable이므로
/// 도끼가 TakeDamage를 부르는 순간을 "타격"으로 받는다. 체력은 없고 부서지지 않는다.
/// 부메랑·드론은 ITreeObj만 노리므로 이 오브젝트를 겨냥하지 않는다.
///
/// 외형(스프라이트·타격 반응)은 UI/이펙트 담당이 프리팹과 HitReactionEvent로 입힌다.
/// </summary>
public class RoguelikeLobbyInteractable : MonoBehaviour, IStaticCollidable, IDamageable
{
    /// <summary>타격으로 동작이 실행됐다.</summary>
    public event Action<ERoguelikeLobbyAction> ActionTriggeredEvent;
    /// <summary>타격을 받았다(쿨다운 중 타격 포함). 이펙트 담당이 흔들림·번쩍임을 붙이는 지점.</summary>
    public event Action HitReactionEvent;

    [SerializeField] private ERoguelikeLobbyAction action = ERoguelikeLobbyAction.StartRun;
    [SerializeField] private float collisionRadius = 0.5f;
    [SerializeField] private Vector2 collisionOffset = new Vector2(0f, 0.3f);
    [Tooltip("연속 타격으로 동작이 여러 번 실행되지 않게 막는 시간(초).")]
    [SerializeField] private float triggerCooldown = 1f;

    private float lastTriggerTime = -999f;
    private bool bInteractable = true;
    private bool bRegistered;

    public ERoguelikeLobbyAction Action => action;

    // // IStaticCollidable
    public Vector2 Position => transform.position;
    public Vector2 Offset => collisionOffset;
    public float Radius => collisionRadius;
    public int Layer => gameObject.layer;

    // // IDamageable
    public bool bCanApplyDamage => bInteractable;
    public IBaseHealthComponent health => null;

    public void Initialize(int _collisionLayer)
    {
        gameObject.layer = _collisionLayer;
    }

    public void SetInteractable(bool _bInteractable)
    {
        bInteractable = _bInteractable;
    }

    public void TakeDamage(float _damage)
    {
        if (false == bInteractable) return;

        HitReactionEvent?.Invoke();

        if (Time.time - lastTriggerTime < triggerCooldown) return;
        lastTriggerTime = Time.time;

        ActionTriggeredEvent?.Invoke(action);
    }

    public void KnockBack(Vector2 _knockBackDir, float _knockBackForce) { }

    /// <summary>_position에 세우고 충돌 판정에 등록한다. 정적 충돌체라 위치를 정한 뒤에 등록해야 맞는 칸에 들어간다.</summary>
    public void Place(Vector3 _position)
    {
        Unregister();

        transform.position = _position;
        gameObject.SetActive(true);

        if (null != CollisionSystem.Instance)
        {
            CollisionSystem.Instance.Register(this, true);
            bRegistered = true;
        }
    }

    /// <summary>판정에서 빼고 숨긴다.</summary>
    public void Remove()
    {
        Unregister();
        gameObject.SetActive(false);
    }

    private void Unregister()
    {
        if (false == bRegistered) return;

        CollisionSystem.Instance?.Unregister(this, true);
        bRegistered = false;
    }

    private void OnDestroy()
    {
        Unregister();
    }
}
