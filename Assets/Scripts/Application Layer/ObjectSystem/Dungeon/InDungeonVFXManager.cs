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

    // top 기준 위치가 나무 꼭대기보다 한참 위에서 생성되는 것을 보정하기 위한 하향 오프셋 (인스펙터에서 조정 가능)
    [SerializeField] private float shieldBrokenVfxYOffset = -0.5f;

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

    /// <summary>
    /// 포자막(Shield)이 파괴되었을 때의 VFX를 재생합니다. parent는 null로 고정하여 나무 오브젝트와 완전히 분리합니다.
    /// 나무 종류별로 이펙트가 다를 수 있어 TreeType에 따라 태그를 분기합니다.
    /// </summary>
    public void PlayShieldBrokenVFX(TreeVisualComponent _visual, TreeType _treeType)
    {
        if (vfxComponent == null || _visual == null) return;

        // BellpineTree는 전용 이펙트가 아직 제작되지 않아 빈 슬롯(SporeShieldBrokenEffect_Bellpine)만
        // 만들어둔 상태입니다. 이펙트가 준비되면 vfxPoolDataList에 프리팹만 연결하면 됩니다.
        string tag = _treeType == TreeType.BellpineTree
            ? "SporeShieldBrokenEffect_Bellpine"
            : "SporeShieldBrokenEffect";

        // 실드가 깨지는 순간의 이펙트이므로, 나무 top이 아니라 실드 스프라이트보다 한 단계 앞에 그려져야 한다.
        int sortingOrder = _visual.GetTopShieldSortingOrder() + 1;

        // 위치만 밑둥 쪽으로 내리고(정렬 순서는 그대로 top 기준 유지)
        Vector3 position = _visual.GetTopRootPosition() + new Vector3(0f, shieldBrokenVfxYOffset, 0f);

        vfxComponent.Play(new VFXPlaySettings(
            tag,
            position,
            _visual.GetTopRootRotation(),
            sortingOrder,
            null
        ));
    }
}
