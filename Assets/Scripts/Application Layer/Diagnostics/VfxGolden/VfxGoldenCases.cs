// ─────────────────────────────────────────────────────────────────────────────
// VFX 골든 캡처 케이스 목록. 에디터 전용이라 빌드에는 컴파일되지 않습니다.
//
// [규칙]
// - 새 케이스는 반드시 Create()의 맨 끝에 추가합니다(시작 프레임 번호가 목록 순서로 정해지기 때문, VfxGoldenRunner 머리 주석).
// - 케이스는 게임이 부르는 경로(InDungeonVFXManager 등)를 그대로 따라 합니다. 따라 한 원본 위치를 주석에 남깁니다.
// ─────────────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR

using System.Collections.Generic;
using PresentationLayer.VFX;
using UnityEngine;

namespace Diagnostics.VfxGolden
{
    public static class VfxGoldenCases
    {
        public static List<VfxGoldenCase> Create()
        {
            List<VfxGoldenCase> cases = new List<VfxGoldenCase>
            {
                new PooledParticleCase("Pool_TreeHitEffect_Top", "TreeHitEffect_Top", 60),
                new PooledParticleCase("Pool_TreeDeadEffect_Bottom", "TreeDeadEffect_Bottom", 90),
                new CriticalImpactCase(),
                new SporeBigExplosionCase(1),
                new StellaRangeCase(),
            };

            // 묶음별 파일(VfxGoldenCases_*.cs). 기준(base)을 기록한 뒤에는 이 순서를 바꾸지 말고 맨 끝에만 덧붙인다.
            VfxGoldenCasesDungeon.Append(cases);
            VfxGoldenCasesSkill.Append(cases);
            VfxGoldenCasesTownLoot.Append(cases);
            return cases;
        }
    }

    /// <summary>
    /// 풀 경로로 파티클 프리팹 하나를 원점에서 재생합니다(소팅·색 오버라이드 없음).
    /// 공유 이미터 대상 태그도 회전 폴백 때는 이 경로를 탑니다.
    /// </summary>
    public class PooledParticleCase : VfxGoldenCase
    {
        private readonly string caseName;
        private readonly string tag;
        private readonly int frames;

        public PooledParticleCase(string _caseName, string _tag, int _frames)
        {
            caseName = _caseName;
            tag = _tag;
            frames = _frames;
        }

        public override string Name => caseName;
        public override int Frames => frames;

        public override void Setup(VfxGoldenContext _context)
        {
            _context.PlayPooled(new VFXPlaySettings(tag, Vector3.zero, Quaternion.identity, "Objects", 3, null));
        }
    }

    /// <summary>InDungeonVFXManager.PlayCriticalImpactVFX: Create(매니저) → Play(밑동 위치, "Objects", 앞 렌더러 + 1).</summary>
    public class CriticalImpactCase : VfxGoldenCase
    {
        public override string Name => "CriticalImpact";
        public override int Frames => 70;

        public override void Setup(VfxGoldenContext _context)
        {
            VFX_CriticalImpact vfx = VFX_CriticalImpact.Create(_context.Root);
            vfx.Play(new Vector3(0.0f, -1.0f, 0.0f), "Objects", 10);
        }
    }

    /// <summary>InDungeonVFXManager.PlaySporeBigExplosionVFX: 풀 재생("FlyingItem", 10) → SetRange → SetTier.</summary>
    public class SporeBigExplosionCase : VfxGoldenCase
    {
        private readonly int tier;

        public SporeBigExplosionCase(int _tier)
        {
            tier = _tier;
        }

        public override string Name => "SporeBigExplosion_T" + tier;
        public override int Frames => 100;
        public override Vector2 ViewSize => new Vector2(20.0f, 14.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            ParticleSystem effect = _context.PlayPooled(new VFXPlaySettings("SporeBigExplosionEffect", Vector3.zero, Quaternion.identity, "FlyingItem", 10, null));
            VFX_SporeBigExplosion explosion = effect.GetComponentInChildren<VFX_SporeBigExplosion>(true);
            explosion.SetRange(4.0f);
            explosion.SetTier(tier);
        }
    }

    /// <summary>InDungeonVFXManager의 StellaRange 재생: 풀 재생("ShockWave", 0) → SetRadius.</summary>
    public class StellaRangeCase : VfxGoldenCase
    {
        public override string Name => "StellaRange";
        public override int Frames => 150;
        public override Vector2 ViewSize => new Vector2(14.0f, 10.0f);

        private VFX_StellaRange range;

        public override void Setup(VfxGoldenContext _context)
        {
            ParticleSystem effect = _context.PlayPooled(new VFXPlaySettings("StellaRangeEffect", Vector3.zero, Quaternion.identity, "ShockWave", 0, null));
            range = effect.GetComponentInChildren<VFX_StellaRange>(true);
            range.SetRadius(3.0f);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            // 유지 → 폭발 직전 강조(RoguelikeSkillVfxPlayer가 폭발 전에 Activate) → 퇴장(End)
            if (70 == _frame) range.Activate();
            if (100 == _frame) range.End();
        }
    }
}

#endif
