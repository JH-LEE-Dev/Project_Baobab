using System;
using UnityEngine;

/// <summary>
/// 로비에서 캐릭터가 "쳐서" 실행하는 오브젝트(기획서 4-2 "버튼을 누르지 않고, 캐릭터로 친다").
///
/// 나무와 같은 충돌 레이어에 등록되어 도끼 판정(AttackComponent)이 그대로 맞힌다. IDamageable이므로
/// 도끼가 TakeDamage를 부르는 순간을 "타격"으로 받는다. 체력은 없고 부서지지 않는다.
/// 부메랑·드론은 ITreeObj만 노리므로 이 오브젝트를 겨냥하지 않는다.
///
/// 스프라이트(UI)와 번쩍임 같은 이펙트는 각 담당이 프리팹과 HitReactionEvent로 입힌다.
/// 맞을 때의 움직임(눌렸다 튀는 스프링)·타격음·진동은 여기서 낸다. 동작이 실행되는 타격은 더 크게 출렁인다.
/// </summary>
public class RoguelikeLobbyInteractable : MonoBehaviour, IStaticCollidable, IDamageable
{
    /// <summary>타격으로 동작이 실행됐다.</summary>
    public event Action<ERoguelikeLobbyAction> ActionTriggeredEvent;
    /// <summary>타격을 받았다(쿨다운 중 타격 포함). 이펙트 담당이 번쩍임 같은 이펙트를 붙이는 지점.</summary>
    public event Action HitReactionEvent;

    [SerializeField] private ERoguelikeLobbyAction action = ERoguelikeLobbyAction.StartRun;
    [SerializeField] private float collisionRadius = 0.5f;
    [SerializeField] private Vector2 collisionOffset = new Vector2(0f, 0.3f);
    [Tooltip("연속 타격으로 동작이 여러 번 실행되지 않게 막는 시간(초).")]
    [SerializeField] private float triggerCooldown = 1f;

    [Header("타격 반응")]
    [Tooltip("출렁일 그림 루트. 비우면 자식 'Visual'을 쓴다.")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("타격 때 가로로 퍼지고 세로로 눌리는 정도. 동작이 실행되는 타격은 두 배.")]
    [SerializeField] private float hitSquash = 0.12f;
    [SerializeField] private float hitReactionSeconds = 0.25f;
    [SerializeField] private float hitSpringFrequency = 22f;

    private Vector3 visualBaseScale = Vector3.one;
    private bool bReacting;
    private float hitReactionTime = -100f;
    private float hitReactionStrength;

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

        bool bTriggers = Time.time - lastTriggerTime >= triggerCooldown;
        PlayHitReaction(bTriggers ? 2f : 1f);

        if (false == bTriggers) return;
        lastTriggerTime = Time.time;

        ActionTriggeredEvent?.Invoke(action);
    }

    private void PlayHitReaction(float _strength)
    {
        if (null == visualRoot) visualRoot = transform.Find("Visual");

        // 반응이 없는 동안의 스케일을 기준으로 잡는다(반응 도중 다시 맞으면 이미 잡아 둔 기준을 그대로 쓴다).
        if (null != visualRoot && false == bReacting) visualBaseScale = visualRoot.localScale;

        bReacting = null != visualRoot;
        hitReactionTime = Time.time;
        hitReactionStrength = _strength;
        Sound.Play(SoundID.TreeHit, transform.position);
        Rumble.Play(EHapticEvent.TreeImpact);
    }

    /// <summary>
    /// 반응이 도는 동안에만 그림 스케일을 쓴다. 반응이 끝나는 프레임에 한 번 원래 스케일로 되돌리고 그 뒤로는 손대지 않는다
    /// (다른 담당이 그림에 거는 애니메이션을 매 프레임 덮어쓰지 않게).
    /// </summary>
    private void LateUpdate()
    {
        if (false == bReacting || null == visualRoot) return;

        float t = (Time.time - hitReactionTime) / Mathf.Max(0.01f, hitReactionSeconds);
        if (t >= 1f)
        {
            visualRoot.localScale = visualBaseScale;
            bReacting = false;
            return;
        }

        // 감쇠 스프링: 가로로 퍼지고 세로로 눌렸다가 반대로 튀며 잦아든다.
        float spring = Mathf.Exp(-5f * t) * Mathf.Cos(hitSpringFrequency * t * hitReactionSeconds);
        float k = hitSquash * hitReactionStrength * spring;
        visualRoot.localScale = new Vector3(visualBaseScale.x * (1f + k), visualBaseScale.y * (1f - k), visualBaseScale.z);
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
