using UnityEngine;

/// <summary>
/// 로그라이크 신규 스킬(오프로드·스텔라·포자폭탄)과 조합 효과의 기본 수치. 기획서 9장 "밸런스 조정 목록"의 항목들이다.
/// 카드 효과(피해%·범위%·지속%·공격속도%·발동 수·확률)는 이 기본값 위에 곱하거나 더한다.
///
/// 회전베기·충격파·부메랑·드론은 본편 스탯을 그대로 쓰므로 여기 없다(RunEffectApplier).
/// 모든 값은 임시이며 밸런스 단계에서 조정한다.
/// </summary>
[CreateAssetMenu(fileName = "RoguelikeSkillTuning", menuName = "Baobab/Roguelike/Skill Tuning")]
public class RoguelikeSkillTuningSO : ScriptableObject
{
    [Header("오프로드")]
    [Tooltip("소환 주기(초). 공격속도%로 빨라진다.")]
    public float offroadInterval = 6f;
    [Tooltip("돌진 속도(월드/초).")]
    public float offroadSpeed = 14f;
    [Tooltip("돌진 판정 폭(반경).")]
    public float offroadHitRadius = 0.9f;
    public float offroadDamage = 60f;
    [Tooltip("소환 전 플래시 시간(초). 이 동안은 피해가 없다.")]
    public float offroadWarmup = 0.4f;

    [Header("스텔라")]
    [Tooltip("나무를 벨 때 별이 생길 확률(%). 카드 ChancePercent가 더해진다. 처음에는 낮게.")]
    public float stellaBaseChance = 5f;
    [Tooltip("필드에 동시에 있을 수 있는 별 수. 발동 수(CountAdd)가 더해진다.")]
    public int stellaBaseMaxStars = 3;
    [Tooltip("별이 사라지기까지의 시간(초). 지속%로 늘어난다.")]
    public float stellaStarLifetime = 12f;
    [Tooltip("별을 쳤을 때의 단발 공격 반경.")]
    public float stellaBlastRadius = 2.2f;
    public float stellaBlastDamage = 80f;
    [Tooltip("별 표식(받는 피해 증가) 지속 시간(초). 지속%로 늘어난다.")]
    public float stellaMarkDuration = 6f;

    [Header("포자폭탄")]
    [Tooltip("투하 주기(초). 공격속도%로 빨라진다.")]
    public float sporeInterval = 4f;
    [Tooltip("투하 대상 탐색 반경(캐릭터 기준).")]
    public float sporeTargetRange = 7f;
    public float sporeRadius = 1.6f;
    [Tooltip("연막 지속(초). 지속%로 늘어난다.")]
    public float sporeDuration = 4f;
    public float sporeTickInterval = 0.5f;
    [Tooltip("틱당 피해. 도트%로 늘어난다.")]
    public float sporeTickDamage = 12f;

    [Header("조합: 회전베기 + 충격파 (360도 충격파)")]
    [Tooltip("이 휘두름 수마다 360도 충격파. 회전베기 발동 간격 카드 효과가 함께 적용된다.")]
    public int spinShockwaveSwingInterval = 3;

    [Header("조합: 부메랑 + 충격파")]
    [Tooltip("같은 자리에 연달아 터지지 않게 하는 최소 간격(초).")]
    public float boomerangShockwaveCooldown = 0.25f;

    [Header("조합: 회전베기 + 부메랑")]
    public float spinBoomerangRadius = 2.2f;
    [Tooltip("도끼 피해 대비 배율.")]
    public float spinBoomerangDamageMultiplier = 1f;
    public float spinBoomerangCooldown = 0.2f;

    [Header("조합: 스텔라 + 포자폭탄")]
    [Tooltip("별 표식에 붙는 도트(틱당 피해·간격).")]
    public float stellaSporeDotDamage = 10f;
    public float stellaSporeDotInterval = 0.5f;
    [Tooltip("도트 중인 나무가 죽을 때 터지는 포자 폭발.")]
    public float stellaSporeExplosionRadius = 1.6f;
    public float stellaSporeExplosionDamage = 40f;
    [Tooltip("연쇄 폭발 상한(한 프레임에 터지는 수). 기획서 9장 '연쇄 폭발의 상한'.")]
    public int stellaSporeMaxExplosionsPerFrame = 8;

    [Header("조합: 스텔라 + 드론")]
    [Tooltip("이음선 빔 주기(초). 공격속도%로 빨라진다.")]
    public float stellaDroneBeamInterval = 1.2f;
    public float stellaDroneBeamWidth = 0.6f;
    public float stellaDroneBeamDamage = 40f;
    [Tooltip("이 수만큼 별이 이어지면 노바(기획서 9개).")]
    public int stellaDroneNovaStars = 9;
    public float stellaDroneNovaRadius = 6f;
    public float stellaDroneNovaDamage = 600f;
    [Tooltip("홀로 남은 별의 표식 공격 주기(초)와 반경.")]
    public float stellaDroneLoneStarInterval = 1.5f;
    public float stellaDroneLoneStarRadius = 1.5f;
}
