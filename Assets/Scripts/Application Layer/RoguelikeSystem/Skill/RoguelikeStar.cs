using System;
using UnityEngine;

/// <summary>
/// 스텔라 카드의 별. 기획서 6-1 액티브 "스텔라" - 나무를 벤 자리에 생기고, 기본적으로 도끼로만 칠 수 있다.
///
/// 나무 충돌 레이어에 등록되어 도끼 판정에 걸린다. 회전베기 휘두름의 360도 판정에서는 빠진다(IBasicAxeOnlyTarget). 충격파는 TreeObj만, 부메랑·드론은 ITreeObj만 노리므로
/// 평소에는 이 별을 치지 않는다. 스텔라 III("다른 액티브 스킬도 별을 공격")가 켜지면 RunCombatOverrides.bSkillsHitRunTargets로
/// 회전베기 360도·충격파·부메랑이 IRunSkillHitTarget으로 알리고, 드론 레이저와 로그라이크 신규 스킬은 RoguelikeSkillController가 직접 판정한다.
/// 생성·소멸 이펙트는 이펙트 담당이 프리팹과 RoguelikeSkillVfxSignal로 입힌다.
/// </summary>
public class RoguelikeStar : MonoBehaviour, IStaticCollidable, IDamageable, IBasicAxeOnlyTarget, IRunSkillHitTarget
{
    public event Action<RoguelikeStar> HitEvent;

    [SerializeField] private float collisionRadius = 0.4f;
    [SerializeField] private Vector2 collisionOffset = new Vector2(0f, 0.2f);

    private bool bAttackable = true;
    private bool bRegistered;
    private SpriteRenderer[] visualRenderers;

    /// <summary>링크·연출 식별용 번호. 컨트롤러가 붙인다.</summary>
    public int StarId { get; private set; }
    public float ExpireTime { get; set; }

    // // IStaticCollidable
    public Vector2 Position => transform.position;
    public Vector2 Offset => collisionOffset;
    public float Radius => collisionRadius;
    public int Layer => gameObject.layer;

    // // IDamageable
    public bool bCanApplyDamage => bAttackable;
    public IBaseHealthComponent health => null;

    public void Place(int _starId, Vector3 _position, int _layer, bool _bAttackable)
    {
        Unregister();

        StarId = _starId;
        bAttackable = _bAttackable;
        gameObject.layer = _layer;
        transform.position = _position;
        gameObject.SetActive(true);

        if (null != CollisionSystem.Instance)
        {
            CollisionSystem.Instance.Register(this, true);
            bRegistered = true;
        }
    }

    /// <summary>별 자체 그림을 켜고 끈다. 본편 땅 별로 그릴 때는 끈다(RoguelikeSkillVfxPlayer).</summary>
    public void SetVisualVisible(bool _bVisible)
    {
        if (null == visualRenderers) visualRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < visualRenderers.Length; i++)
        {
            if (null != visualRenderers[i]) visualRenderers[i].enabled = _bVisible;
        }
    }

    public void SetAttackable(bool _bAttackable)
    {
        bAttackable = _bAttackable;
    }

    public void Remove()
    {
        Unregister();
        gameObject.SetActive(false);
    }

    public void TakeDamage(float _damage)
    {
        if (false == bAttackable) return;
        HitEvent?.Invoke(this);
    }

    public void KnockBack(Vector2 _knockBackDir, float _knockBackForce) { }

    // // IRunSkillHitTarget
    public bool bCanBeHitBySkill => bAttackable && bRegistered;

    public void NotifySkillHit()
    {
        if (false == bAttackable) return;
        HitEvent?.Invoke(this);
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
