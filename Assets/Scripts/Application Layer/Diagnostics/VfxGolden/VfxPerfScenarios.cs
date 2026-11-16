// ─────────────────────────────────────────────────────────────────────────────
// VFX 성능 측정 시나리오 목록(VfxGoldenRunner.Perf.cs). 에디터 전용이라 빌드에는 컴파일되지 않습니다.
// 게임에서 실제로 몰리는 수(광역 타격·연쇄 등)를 기준으로 동시 개수를 잡습니다.
// 골든 케이스와 달리 순서·추가 위치 제약은 없습니다(성능 값은 시작 프레임과 무관).
// 케이스는 골든 목록에서 이름으로 가져옵니다(같은 시나리오를 그대로 겹쳐 띄움).
// ─────────────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR

using System.Collections.Generic;

namespace Diagnostics.VfxGolden
{
    public static class VfxPerfScenarios
    {
        public const string FloorName = "Perf_Floor_Empty";

        /// <summary>아무것도 만들지 않는 케이스(측정 바닥값).</summary>
        private sealed class EmptyCase : VfxGoldenCase
        {
            public override string Name => FloorName;
            public override int Frames => 120;
            public override void Setup(VfxGoldenContext _context) { }
        }

        /// <summary>화면 밖 비용 측정용: 원래 케이스를 그대로 재생하되 카메라만 멀리 떨어진 곳을 보게 합니다(케이스 안에서 카메라를 옮기지 않는 케이스만).</summary>
        private sealed class OffscreenCase : VfxGoldenCase
        {
            private const float OffsetX = 40.0f;
            private readonly VfxGoldenCase inner;

            public OffscreenCase(VfxGoldenCase _inner)
            {
                inner = _inner;
            }

            public override string Name => inner.Name + "_Offscreen";
            public override int Frames => inner.Frames;
            public override UnityEngine.Vector2 ViewCenter => inner.ViewCenter + new UnityEngine.Vector2(OffsetX, 0.0f);
            public override UnityEngine.Vector2 ViewSize => inner.ViewSize;
            public override void Setup(VfxGoldenContext _context) { inner.Setup(_context); }
            public override void OnFrame(VfxGoldenContext _context, int _frame) { inner.OnFrame(_context, _frame); }
        }

        public static List<VfxPerfScenario> Create()
        {
            return new List<VfxPerfScenario>
            {
                // 바닥값: 아무 이펙트도 없는 구간(필터와 무관하게 항상 잰다). 첫 번째는 예열, 두 번째 값을 바닥으로 쓴다
                new VfxPerfScenario(FloorName, () => new EmptyCase(), 1),
                new VfxPerfScenario(FloorName, () => new EmptyCase(), 1),

                // 던전 타격·처치(광역 타격 한 번에 몰리는 수)
                ByName("Pool_TreeHitEffect_Top", 30),
                ByName("Pool_TreeDeadEffect_Bottom", 30),
                ByName("Dungeon_TreeHit_Shared", 30),
                ByName("Dungeon_TreeDead_Leaf_Shared", 30),
                ByName("CriticalImpact", 12),
                ByName("Dungeon_BrandStampBurst", 20),
                ByName("Dungeon_TreeBurn_Loop", 15),
                ByName("Dungeon_TreeHeatBurst", 10),
                ByName("Dungeon_GemLogTrail_T3", 20),
                ByName("Dungeon_LaserHit_Overheat", 10),
                ByName("Dungeon_DroneLaser_Chain", 4),
                ByName("Dungeon_TreeBarrier_BreakWithShatter", 10),
                ByName("Dungeon_SporeExplosionVFX_Burst", 8),

                // 스킬(보통 한 번에 하나지만 비용 차이가 보이도록 몇 개 겹침)
                ByName("SporeBigExplosion_T1", 8),
                ByName("Skill_SporeBigExplosion_T3_R5", 4),
                ByName("Skill_StarRain_T3", 2),
                ByName("Skill_ConstellationNova_R4", 2),
                ByName("Skill_ConstellationLaser_Loop5", 4),
                ByName("Skill_Whirlwind_Hits14", 4),
                ByName("Skill_Whirlwind_BoomerangLoop", 4),
                ByName("Skill_OverheatAura_BlueFlame", 4),
                ByName("Skill_StellaBurst_R4", 3),
                ByName("StellaRange", 6),
                ByName("Skill_OffroadLane_TwoLanes", 2),

                // 마을·원목·전리품(화면에 많이 깔리는 것)
                ByName("TownLoot_ItemAura_Perfect", 30),
                ByName("TownLoot_Pool_Shiny_LogItem", 30),
                ByName("TownLoot_CuttingEffect_Tree", 8),
                ByName("TownLoot_SawmillCombo_Overdrive", 2),
                ByName("TownLoot_TownMoney_ShopTwinkleHigh", 2),

                // 화면 밖(카메라가 40유닛 떨어진 곳을 볼 때)
                Offscreen("Dungeon_BrandStampBurst", 20),
                Offscreen("Dungeon_GemLogTrail_T3", 20),
                Offscreen("Dungeon_LaserHit_Overheat", 10),
            };
        }

        private static VfxPerfScenario Offscreen(string _name, int _count)
        {
            return new VfxPerfScenario("Perf_" + _name + "_Offscreen", () => new OffscreenCase(Find(_name)), _count);
        }

        /// <summary>골든 목록에서 이름이 _name과 같은(없으면 _name으로 끝나는) 케이스를 시나리오로 씁니다.</summary>
        private static VfxPerfScenario ByName(string _name, int _count)
        {
            return new VfxPerfScenario("Perf_" + _name, () => Find(_name), _count);
        }

        private static VfxGoldenCase Find(string _name)
        {
            List<VfxGoldenCase> cases = VfxGoldenCases.Create();
            VfxGoldenCase suffixMatch = null;
            for (int i = 0; i < cases.Count; i++)
            {
                string caseName = cases[i].Name;
                if (_name == caseName) return cases[i];
                if (null == suffixMatch && caseName.EndsWith(_name, System.StringComparison.Ordinal)) suffixMatch = cases[i];
            }
            if (null == suffixMatch)
            {
                throw new System.ArgumentException("[VfxGolden] 성능 시나리오에 쓸 케이스가 없습니다: " + _name);
            }
            return suffixMatch;
        }
    }
}

#endif
