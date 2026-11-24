/// <summary>
/// 카드 도감 대상 카드의 정본 목록(기획서 v3.3 8-4, 부록 C-3 G-10). 도감 창(UIView_RoguelikeCollection.collectionCardIds)의 직렬화 목록과 같은 순서다.
/// 도감 완성 도전과제(CollectionAll)의 목표 수 · 기록 소급(RoguelikeRecordData.BackfillCollection)이 이 목록을 기준으로 한다.
/// 카드를 추가 · 삭제하면 이 목록과 도감 창 프리팹의 목록을 함께 고친다.
/// </summary>
public static class RoguelikeCollection
{
    // 분류 순서: 액티브 7 → 공통 패시브 10 → 프리즘 6 → 계약 3.
    public static readonly string[] CardIds =
    {
        "active_offroad", "active_spin", "active_shockwave", "active_drone", "active_boomerang", "active_stella", "active_sporebomb",
        "passive_range", "passive_damage", "passive_attackspeed", "passive_duration", "passive_dot", "passive_dot_speed",
        "passive_movespeed", "passive_stamina_recover", "passive_max_stamina", "passive_grace",
        "combo_spin_shockwave", "combo_spin_boomerang", "combo_spore_drone", "combo_spore_offroad", "combo_spore_shockwave", "combo_stella_drone",
        "ruby_glass_cannon", "ruby_more_the_merrier", "ruby_patience",
    };
}
