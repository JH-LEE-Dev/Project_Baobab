using UnityEngine;

/// <summary>
/// 던전 내 모든 VFX를 중앙에서 관리하는 매니저입니다.
/// 나무 프리팹이 아닌 씬 레벨의 독립 오브젝트에서 VFXComponent를 소유하므로,
/// 나무가 비활성화되어도 파티클이 끊기지 않고 끝까지 재생됩니다.
/// </summary>
public class InDungeonVFXManager : MonoBehaviour
{
    // 외부 의존성
    [SerializeField] private VFXComponent vfxComponent;

    public void Initialize()
    {
        if (vfxComponent != null)
            vfxComponent.Initialize();
    }

    /// <summary>
    /// 나무 피격 VFX를 재생합니다. parent는 null로 고정하여 나무 오브젝트와 완전히 분리합니다.
    /// Top/Bottom 이펙트는 각각 설정된 컬러를 공유합니다.
    /// </summary>
    public void PlayTreeHitVFX(TreeVisualComponent _visual)
    {
        if (vfxComponent == null || _visual == null) return;

        ParticleColorSet topColor = _visual.GetTopVfxColor();
        vfxComponent.Play(new VFXPlaySettings(
            "TreeHitEffect_Top",
            _visual.GetTopRootPosition(),
            _visual.GetTopRootRotation(),
            topColor.startColor,
            topColor.overrideChildrenColor,
            null
        ));

        ParticleColorSet bottomColor = _visual.GetBottomVfxColor();
        vfxComponent.Play(new VFXPlaySettings(
            "TreeHitEffect_Bottom",
            _visual.GetBottomRootPosition(),
            _visual.GetBottomRootRotation(),
            bottomColor.startColor,
            bottomColor.overrideChildrenColor,
            null
        ));
    }

    /// <summary>
    /// 나무 사망 VFX를 재생합니다. parent는 null로 고정하여 나무 오브젝트와 완전히 분리합니다.
    /// Top/Bottom 이펙트는 각각 설정된 컬러를 공유합니다.
    /// </summary>
    public void PlayTreeDeadVFX(TreeVisualComponent _visual)
    {
        if (vfxComponent == null || _visual == null) return;

        ParticleColorSet topColor = _visual.GetTopVfxColor();
        vfxComponent.Play(new VFXPlaySettings(
            "TreeDeadEffect_Top",
            _visual.GetTopRootPosition(),
            _visual.GetTopRootRotation(),
            topColor.startColor,
            topColor.overrideChildrenColor,
            null
        ));

        ParticleColorSet bottomColor = _visual.GetBottomVfxColor();
        vfxComponent.Play(new VFXPlaySettings(
            "TreeDeadEffect_Bottom",
            _visual.GetBottomRootPosition(),
            _visual.GetBottomRootRotation(),
            bottomColor.startColor,
            bottomColor.overrideChildrenColor,
            null
        ));
    }
}
