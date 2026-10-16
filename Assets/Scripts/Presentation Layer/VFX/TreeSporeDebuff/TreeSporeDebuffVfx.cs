using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 나무 포자 디버프 이펙트의 진입점. 호출하는 쪽(TreeObj 등)은 이 정적 메서드 세 개만 알면 된다.
    ///  - Apply: 포자 대폭발에 맞아 피해 증폭이 걸렸을 때(ApplySporeDamageAmplify). 이미 걸려 있으면 더 큰 단계로만 올라간다.
    ///  - Clear: 증폭이 풀릴 때(ResetTree, 쓰러짐, 풀 반환). 안 불러도 비활성화된 나무는 다음 틱에 스스로 정리된다.
    ///  - NotifyHit: 증폭 나무가 맞았을 때(균열 번쩍임, 파편, 표식 내리꽂힘).
    /// 단계는 증폭률로 정한다: 0.25 미만 1단계(+20%), 0.4 미만 2단계(+30%), 그 이상 3단계(+50%).
    /// </summary>
    public static class TreeSporeDebuffVfx
    {
        public const float Tier2Threshold = 0.25f;
        public const float Tier3Threshold = 0.4f;

        public static int TierFromAmplify(float _amplify)
        {
            if (_amplify < Tier2Threshold) return 1;
            if (_amplify < Tier3Threshold) return 2;
            return 3;
        }

        public static void Apply(TreeObj _tree, float _amplify)
        {
            if (null == _tree || _amplify <= 0.0f) return;

            TreeSporeDebuffVfxManager manager = TreeSporeDebuffVfxManager.GetOrCreate();
            if (null == manager) return;

            manager.Register(_tree, TierFromAmplify(_amplify));
        }

        public static void Clear(TreeObj _tree)
        {
            TreeSporeDebuffVfxManager manager = TreeSporeDebuffVfxManager.Instance;
            if (null != manager) manager.Unregister(_tree);
        }

        public static void NotifyHit(TreeObj _tree)
        {
            TreeSporeDebuffVfxManager manager = TreeSporeDebuffVfxManager.Instance;
            if (null != manager) manager.NotifyHit(_tree);
        }
    }
}
