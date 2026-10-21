using UnityEngine;

/// <summary>
/// 로그라이크 신규 스킬(오프로드·스텔라·포자폭탄)과 조합 효과의 기본 수치. 기획서 9장 "밸런스 조정 목록"의 항목들이다.
/// 카드 효과(피해%·범위%·지속%·공격속도%·발동 수·확률)는 이 기본값 위에 곱하거나 더한다.
///
/// 회전베기·충격파·부메랑·드론은 본편 스탯을 쓰고, 그 기준값만 런 시작 때 아래 "전투 기준값"으로 바꿔 끼운다(RunEffectApplier).
/// 본편 캐릭터 프리팹의 값은 건드리지 않는다. 모든 값은 기획서 v2.4 부록 A 초안이며 밸런스 단계에서 조정한다.
/// </summary>
[CreateAssetMenu(fileName = "RoguelikeSkillTuning", menuName = "Baobab/Roguelike/Skill Tuning")]
public class RoguelikeSkillTuningSO : ScriptableObject
{
    [Header("전투 기준값 (런 시작 때 캐릭터에 주입, 부록 A-1)")]
    [Tooltip("도끼 공격력. 회전베기 피해도 이 값이다.")]
    public float axeDamage = 10f;
    [Tooltip("도끼 쿨타임(초).")]
    public float axeAttackCoolTime = 0.6f;
    [Tooltip("충격파 피해.")]
    public float shockwaveDamage = 8f;
    [Tooltip("부메랑 틱 피해.")]
    public float boomerangTickDamage = 4f;
    [Tooltip("부메랑 틱 간격(초).")]
    public float boomerangTickInterval = 0.3f;
    [Tooltip("부메랑이 돌아온 뒤 다시 던지기까지의 시간(초). 0이면 돌아오자마자 다시 던진다(상시 발사, 기획서 6-1).")]
    public float boomerangCooldown = 0f;
    [Tooltip("부메랑 사거리(화면 기준 반폭 대비 비율). 본편은 1(화면 끝까지)인데 로그라이크 섬은 한 화면에 꽉 차서, 그대로 두면 II·V의 사거리 증가가 " +
        "섬 밖 바다로 날아가 오히려 손해다. 그래서 기본을 줄여 V(+50%)에서 본편 사거리(1.0)에 닿게 한다.")]
    public float boomerangRangeRatio = 0.67f;
    [Tooltip("드론 레이저 피해.")]
    public float droneLaserDamage = 7f;
    [Tooltip("드론 레이저 발사 주기(초).")]
    public float droneLaserInterval = 1f;

    [Header("회전베기 (부록 A-4)")]
    [Tooltip("레벨 I의 필요 휘두름 수. n휘두름 뒤 n+1번째 공격에 발동한다(3 = 4번째 공격). 카드 효과 TriggerHitCountAdd로 3 → 2 → 1 → 0(상시)으로 줄어든다.")]
    public int spinRequiredSwings = 3;

    [Header("레벨 효과 반복 간격 (부록 A-4)")]
    [Tooltip("충격파 MAX 2중 발생의 두 번째 충격파까지 간격(초).")]
    public float shockwaveExtraDelay = 0.12f;
    [Tooltip("스텔라 MAX 별 폭발 2회 연속의 두 번째 폭발까지 간격(초).")]
    public float stellaBlastRepeatDelay = 0.2f;
    [Tooltip("계약 다다익선 도끼 연속 2회 타격의 두 번째 타격까지 간격(초).")]
    public float axeExtraStrikeDelay = 0.1f;

    [Header("오프로드")]
    [Tooltip("소환 주기(초). 공격속도%로 빨라진다.")]
    public float offroadInterval = 6f;
    [Tooltip("돌진 최고 속도(월드/초). 오프로드 IV의 돌진 속도 증가(MoveSpeedPercent, 오프로드 전용)가 곱해진다.")]
    public float offroadSpeed = 16f;
    [Tooltip("돌진 판정 폭(반경). 범위%로 넓어진다(오프로드 IV).")]
    public float offroadHitRadius = 0.9f;
    public float offroadDamage = 30f;
    [Tooltip("소환 전 예열 시간(초). 시동 떨림·플래시가 나오고, 이 동안은 피해가 없다.")]
    public float offroadWarmup = 0.45f;
    [Tooltip("출발 순간 속도(월드/초). 여기서 최고 속도까지 가속한다.")]
    public float offroadLaunchSpeed = 3f;
    [Tooltip("가속도(월드/초²). 클수록 확 튀어 나간다.")]
    public float offroadAcceleration = 70f;
    [Tooltip("여러 대가 나갈 때 한 대씩 출발이 늦어지는 간격(초). 연타감을 준다.")]
    public float offroadVolleyStagger = 0.15f;
    [Tooltip("차선을 고르는 폭(섬 반폭 대비 0~1). 1이면 섬 가장자리 차선까지 쓴다.")]
    [Range(0f, 1f)] public float offroadLaneSpread = 0.75f;
    [Tooltip("소환 지점과 멈추는 지점을 섬 가장자리에서 안쪽으로 들여오는 거리(월드). 섬 테두리는 계단 모양이라 경계선 그대로면 차가 물 위에 반쯤 걸친다.")]
    public float offroadEdgeInset = 0.8f;
    [Tooltip("섬 끝에 닿기 이 시간(초) 전부터 달리는 채로 하얗게 된 뒤, 이 시간이 지나면 사라진다(감속 없음). 프레임 수가 아닌 시간이라 어떤 프레임 속도에서도 같다(0.05초 = 60fps 3프레임).")]
    public float offroadExitSeconds = 0.05f;

    [Header("오프로드 손맛")]
    [Tooltip("나무를 들이받은 순간 차가 멈칫하는 시간(초).")]
    public float offroadHitStop = 0.035f;
    [Tooltip("같은 순간에 나무를 더 들이받을 때마다 늘어나는 멈칫 시간(초).")]
    public float offroadHitStopPerExtraTree = 0.01f;
    [Tooltip("한 번에 멈칫하는 시간의 상한(초).")]
    public float offroadHitStopMax = 0.08f;
    [Tooltip("들이받을 때 잃는 속도 비율(0~1). 다시 가속하며 '쿵쿵' 박는 느낌을 만든다.")]
    [Range(0f, 1f)] public float offroadHitSpeedLoss = 0.25f;
    [Tooltip("출발 순간 카메라 흔들림(세기, 시간).")]
    public Vector2 offroadLaunchShake = new Vector2(0.8f, 0.1f);
    [Tooltip("들이받을 때 카메라 흔들림(세기, 시간).")]
    public Vector2 offroadHitShake = new Vector2(0.5f, 0.06f);
    [Tooltip("들이받기 카메라 흔들림의 최소 간격(초). 여러 대가 연달아 박아도 화면이 계속 떨리지 않게 한다.")]
    public float offroadHitShakeInterval = 0.08f;
    [Tooltip("엔진음 볼륨. 한 번에 나가는 차량 중 첫 차만 엔진음을 낸다.")]
    [Range(0f, 1f)] public float offroadEngineVolume = 0.6f;

    [Header("스텔라")]
    [Tooltip("나무를 벨 때 별이 생길 확률(%). 카드 ChancePercent가 더해진다. 처음에는 낮게.")]
    public float stellaBaseChance = 5f;
    [Tooltip("필드에 동시에 있을 수 있는 별 수. 발동 수(CountAdd)가 더해진다.")]
    public int stellaBaseMaxStars = 3;
    [Tooltip("별이 사라지기까지의 시간(초). 지속%로 늘어난다.")]
    public float stellaStarLifetime = 12f;
    [Tooltip("별을 쳤을 때의 단발 공격 반경.")]
    public float stellaBlastRadius = 2.2f;
    public float stellaBlastDamage = 100f;
    [Tooltip("별 표식 유지 시간(초). 지속%로 늘어나고, 카드 효과 StarMarkDurationAdd(레벨 IV +3초)가 더해진다.")]
    public float stellaMarkDuration = 6f;
    [Tooltip("별 표식이 남은 나무가 받는 피해 증가율(0.3 = +30%). 도끼·다른 액티브·지속 피해 모두에 적용된다(기획서 6-1). 스텔라 V의 StarMarkDamageTakenAdd(+15%p)가 더해진다.")]
    public float stellaMarkDamageTakenBonus = 0.3f;
    [Tooltip("별 폭발 한 번이 나무에 주는 피해의 상한(나무 최대 HP 대비 비율, 표식 · 공격력 증가 포함). 기획서 v2.5 A-4 초안 25%. 0이면 상한 없음.")]
    [Range(0f, 1f)] public float stellaBlastMaxHpRatio = 0.25f;
    [Tooltip("별 폭발 피해 상한을 적용하는 첫 티어(0부터, 3 = 4티어). 언리밋도 4티어로 친다.")]
    public int stellaBlastCapFromTier = 3;

    [Header("포자폭탄 (포자 대폭발 + 오염, 기획서 v2.4 6-1)")]
    [Tooltip("투하 주기(초). 공격속도%로 빨라진다.")]
    public float sporeInterval = 5f;
    [Tooltip("투하 대상 탐색 반경(캐릭터 기준).")]
    public float sporeTargetRange = 7f;
    [Tooltip("포자 대폭발 범위(등각 타원 장반경, 월드). 본편 포자 대폭발과 같은 판정(세로는 절반)이다. 범위%로 넓어진다.")]
    public float sporeRadius = 2.5f;
    [Tooltip("폭탄이 떨어진 뒤 대폭발 판정까지의 시간(초). 본편 대폭발 연출이 응집한 뒤 터지는 순간에 맞춘다.")]
    public float sporeExplosionDelay = 0.2f;
    [Tooltip("대폭발 자체의 피해. 기획서는 오염 지속 피해만 정해 두었으므로 기본 0이다.")]
    public float sporeExplosionDamage = 0f;
    [Tooltip("오염 유지 시간(초). 지속%로 늘어난다(II 5초, VI 6초).")]
    public float sporeDuration = 4f;
    [Tooltip("오염 지속 피해 간격(초).")]
    public float sporeTickInterval = 0.5f;
    [Tooltip("오염 틱당 피해. 피해%·지속 피해%로 늘어난다. 중첩 수만큼 곱해진다.")]
    public float sporeTickDamage = 4f;

    [Header("조합: 회전베기 + 충격파 (360도 충격파)")]
    [Tooltip("이 휘두름 수마다 360도 충격파. 회전베기 발동 간격 카드 효과가 함께 적용된다.")]
    public int spinShockwaveSwingInterval = 3;

    [Header("조합: 부메랑 + 충격파")]
    [Tooltip("부메랑에 맞은 나무마다 충격파가 터진다(기획서). 0이면 타격마다, 값을 주면 그 간격(초) 안의 타격은 건너뛴다(밸런스용).")]
    public float boomerangShockwaveCooldown = 0f;
    [Tooltip("같은 프레임에 이 거리(월드) 안에서 터지는 충격파는 하나로 합친다. 붙어 선 나무를 한 번에 꿰뚫어도 같은 자리에 충격파가 겹쳐 쌓이지 않게 한다.")]
    public float boomerangShockwaveMergeRadius = 0.6f;

    [Header("조합: 회전베기 + 부메랑")]
    public float spinBoomerangRadius = 2.2f;
    [Tooltip("도끼 피해 대비 배율.")]
    public float spinBoomerangDamageMultiplier = 1f;
    [Tooltip("부메랑 타격 1회당 회전베기 1회(기획서). 회전베기는 캐릭터 중심이라 한 프레임에 여러 그루를 맞혀도 한 번만 친다(같은 공격이 겹쳐 쌓이지 않게). 값을 주면 그 간격(초) 안의 타격은 건너뛴다(밸런스용).")]
    public float spinBoomerangCooldown = 0f;

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
