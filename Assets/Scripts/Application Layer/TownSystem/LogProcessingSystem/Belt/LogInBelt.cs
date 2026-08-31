using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class LogInBelt : MonoBehaviour
{
    public event Action BeltStopEvent;
    public event Action<LogItem, ILogItemData> LogOutEvent;
    private LogItemData logItemData = new LogItemData();
    [SerializeField] List<BeltObj> belts;


    private struct BeltItem
    {
        public LogItem item;
        public int targetIndex;

        public BeltItem(LogItem _item, int _targetIndex)
        {
            item = _item;
            targetIndex = _targetIndex;
        }
    }

    private struct DeactivatingItem
    {
        public LogItem item;
        public float remainingTime;

        public DeactivatingItem(LogItem _item, float _time)
        {
            item = _item;
            remainingTime = _time;
        }
    }

    // 벨트 위 원목의 정렬 높이. 원목은 벨트 타일 원점보다 위(벨트 상판)에 얹혀 보이므로, 정렬 기준이 되는
    // 지면 Y를 이 값만큼 내려줘야 자기가 올라탄 벨트 타일보다 앞에 그려진다. 이 보정이 빠지면 원목이
    // 레일에 가려진다. LogIn(신규 투입)과 LoadSaveData(세이브 복원) 두 경로가 반드시 같은 값을 써야 한다.
    private const float LogOnBeltSortHeight = 0.425f;

    // 원목이 지나갈 경로. 손으로 찍은 체크포인트 몇 개만 잇던 방식은 그 직선이 벨트 타일이 놓인
    // 아이소메트릭 대각선(타일 간격 0.5, 0.25 = 기울기 0.5)과 미세하게 어긋나서, 구간 중간으로
    // 갈수록 원목이 레일 밖으로 밀려 보였다. 지금은 벨트 타일 트랜스폼에서 경로를 그대로 만들어
    // 쓰므로 원목이 항상 타일 중심을 지난다.
    // - 중간 지점: belts[i] 위치 + beltSurfaceOffset (타일 상판 중심)
    // - 마지막 지점: checkPoints의 마지막 항목. 벨트를 벗어나 커터/평가기로 넘어가는 투입구라
    //   타일 중심으로 대체할 수 없어 손으로 찍은 값을 그대로 쓴다.
    // 그래서 checkPoints의 중간 항목들은(있어도) 더 이상 경로에 쓰이지 않는다.
    private struct PathPoint
    {
        public Transform anchor;
        public Vector3 offset;

        public PathPoint(Transform _anchor, Vector3 _offset)
        {
            anchor = _anchor;
            offset = _offset;
        }

        // 상점 오브젝트가 통째로 이동할 수 있으므로(DisableShopObj/EnableShopObj) 월드 좌표를
        // 캐싱하지 않고 매번 트랜스폼에서 읽는다.
        public Vector3 Position => anchor.position + offset;
    }

    private List<PathPoint> path = new List<PathPoint>(8);

    // 외부 의존성
    [SerializeField] private List<Transform> checkPoints = new List<Transform>(5);

    [Tooltip("벨트 타일 트랜스폼 원점에서 상판(원목이 실제로 얹혀 보이는 면) 중심까지의 오프셋. " +
             "벨트 스프라이트 피벗이 상판 중심보다 4px(=0.125유닛) 아래에 있어 그만큼 올려준다.")]
    [SerializeField] private Vector2 beltSurfaceOffset = new Vector2(0f, 0.12f);

    [Tooltip("배출 지점(마지막 체크포인트)의 방향을 벨트 그리드 축에 맞춰 보정한다. 손으로 찍은 " +
             "배출 지점은 축에서 40~49도로 벗어나 있어, 원목이 커터로 빨려들어갈 때 벨트와 다른 " +
             "각도로 튀어나가 어색해진다. 마지막 타일에서 떨어진 거리는 그대로 두고 방향만 맞춘다.")]
    [SerializeField] private bool snapExitToBeltAxis = true;

    [Tooltip("아이소메트릭 타일 한 칸 간격. 벨트 타일이 2장 이상이면 실제 배치 간격을 쓰고, " +
             "1장뿐이라 간격을 알아낼 수 없을 때만 이 값을 쓴다.")]
    [SerializeField] private Vector2 beltGridStep = new Vector2(0.5f, 0.25f);

    [SerializeField] private float beltSpeed = 0.1f;
    [SerializeField] private float acceleration = 2.5f;
    [SerializeField] private float beltAnimationSpeedMultiplier = 1f;

    [Header("Loop Sound")]
    [Tooltip("벨트가 완전히 멈췄을 때의 피치(반음/세미톤 단위, 음수). 실제 재생 피치 = 2^(세미톤/12).")]
    [SerializeField] private float loopStopPitchSemitones = -5f;
    [Tooltip("벨트가 기본 속도로 돌 때 도달하는 목표 볼륨 배율(0~1). AudioDatabase의 ConvayerLoop " +
             "defaultVolume에 곱해진다. 아주 작게 잡아둔 기본값이며 추후 직접 튜닝 예정.")]
    [SerializeField] private float loopIntendedVolume = 0.4f;
    [Tooltip("컨베이어 가속 특성으로 beltSpeed가 최초 속도 대비 이 배율까지 올라갔을 때 피치가 loopMaxSpeedPitch에 도달한다.")]
    [SerializeField] private float loopMaxSpeedMultiplier = 2f;
    [Tooltip("최고 속도에서 도달하는 최대 피치")]
    [SerializeField] private float loopMaxSpeedPitch = 1.6f;

    private AudioHandle loopSoundHandle = AudioHandle.Invalid;
    private float baseBeltSpeed = -1f;

    // 내부 상태
    private List<BeltItem> activeItems = new List<BeltItem>(10);
    private List<DeactivatingItem> deactivatingItems = new List<DeactivatingItem>(10);
    private bool isMoving = false;
    private float currentSpeed = 0f;
    private float slideSpeed = 1f;
    private float globalSpeedMultiplier = 1f;

    // 커터 투입용 벨트(inBelt)는 커터가 한 번에 하나만 받을 수 있어 아이템 배출 시 벨트를 멈춰야 하지만,
    // 평가기로 향하는 벨트(outBelt)는 평가기가 용량 제약 없이 즉시 아이템을 받으므로 멈출 필요가 없다.
    // activeItems가 비면 Update()의 속도 계산에서 자연히 멈추므로 별도 강제 정지가 없어도 된다.
    private bool stopsOnLogOut = true;
    private MapType mapType;

    public void SetGlobalSpeedMultiplier(float _mul)
    {
        globalSpeedMultiplier = _mul;
    }

    // LogCutter.GetSoundVolume()과 동일한 규칙: 마을이 아니면(=던전에 있는 동안 배경에서 계속 도는
    // 상태) 벨트 사운드도 재생하지 않는다. ratio 기반 볼륨이 매 프레임 다시 계산되므로(UpdateLoopSound),
    // Cutter처럼 맵 전환 시 별도로 사운드를 끊었다 재시작할 필요 없이 다음 프레임에 자동으로 반영된다.
    public void SetMapType(MapType _mapType)
    {
        mapType = _mapType;
    }

    private float GetSoundVolume()
    {
        return mapType == MapType.Town ? 1f : 0f;
    }

    public void SetStopsOnLogOut(bool _value)
    {
        stopsOnLogOut = _value;
    }

    public void Initialize()
    {
        activeItems.Clear();
        deactivatingItems.Clear();
        isMoving = false;
        currentSpeed = 0f;

        // 가속 특성(IncreaseSpeed)으로 beltSpeed가 이미 오른 상태에서 재초기화될 수 있으므로,
        // "기본 속도 대비 몇 배 빨라졌는지"의 기준점은 최초 1회만 캐싱한다.
        if (baseBeltSpeed < 0f)
        {
            baseBeltSpeed = beltSpeed;
        }

        for (int i = 0; i < belts.Count; ++i)
        {
            belts[i].Initialize();
        }
        SetBeltsAnimationSpeed(0f);

        BuildPath();
    }

    private void BuildPath()
    {
        path.Clear();

        Vector3 surfaceOffset = new Vector3(beltSurfaceOffset.x, beltSurfaceOffset.y, 0f);
        for (int i = 0; i < belts.Count; ++i)
        {
            if (belts[i] == null) continue;
            path.Add(new PathPoint(belts[i].transform, surfaceOffset));
        }

        if (path.Count > 0)
        {
            // 벨트 밖 투입구(커터/평가기)로 넘어가는 마지막 한 지점만 체크포인트에서 가져온다.
            if (checkPoints.Count > 0 && checkPoints[checkPoints.Count - 1] != null)
                path.Add(BuildExitPoint(checkPoints[checkPoints.Count - 1], path[path.Count - 1]));

            return;
        }

        // 벨트 타일이 하나도 배선되지 않은 예외 상황에서는 기존처럼 체크포인트만으로 움직인다.
        for (int i = 0; i < checkPoints.Count; ++i)
        {
            if (checkPoints[i] == null) continue;
            path.Add(new PathPoint(checkPoints[i], Vector3.zero));
        }
    }

    // 배출 지점. 원목은 여기 도달한 뒤 같은 방향으로 한 번 더 미끄러지며(PlayBeltExitAnimation)
    // 커터/평가기로 빨려들어가므로, 이 마지막 구간의 방향이 곧 "빨려들어가는 방향"이 된다.
    // 손으로 찍은 체크포인트는 벨트 축(26.57도)에서 40~49도로 벗어나 있어 원목이 벨트와 다른
    // 각도로 튀어나갔다. 마지막 타일에서 떨어진 거리는 그대로 두고 방향만 축에 맞춘다.
    // 마지막 벨트 타일을 기준(anchor)으로 삼기 때문에 상점이 통째로 움직여도 축이 유지된다.
    private PathPoint BuildExitPoint(Transform _exitCheckPoint, PathPoint _lastBelt)
    {
        if (!snapExitToBeltAxis)
            return new PathPoint(_exitCheckPoint, Vector3.zero);

        Vector3 raw = _exitCheckPoint.position - _lastBelt.Position;
        raw.z = 0f;

        if (raw.sqrMagnitude <= Mathf.Epsilon)
            return new PathPoint(_exitCheckPoint, Vector3.zero);

        Vector3 axis = SnapToBeltAxis(raw);
        return new PathPoint(_lastBelt.anchor, _lastBelt.offset + axis * raw.magnitude);
    }

    // 벨트 타일 간격이 곧 아이소메트릭 축이다. 간격 (dx, dy)의 부호를 뒤집어 만든 네 방향 중
    // 원래 배출 방향과 가장 가까운 축을 고른다.
    private Vector3 SnapToBeltAxis(Vector3 _rawDir)
    {
        Vector3 step = GetBeltGridStep();
        float ax = Mathf.Abs(step.x);
        float ay = Mathf.Abs(step.y);

        if (Mathf.Approximately(ax, 0f) && Mathf.Approximately(ay, 0f))
            return _rawDir.normalized;

        Vector3 rawDir = _rawDir.normalized;
        Vector3 best = rawDir;
        float bestDot = float.MinValue;

        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sy = -1; sy <= 1; sy += 2)
            {
                Vector3 axis = new Vector3(ax * sx, ay * sy, 0f).normalized;
                float dot = Vector3.Dot(rawDir, axis);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = axis;
                }
            }
        }

        return best;
    }

    private Vector3 GetBeltGridStep()
    {
        // 타일이 2장 이상이면 실제 배치 간격을 그대로 쓴다(그리드가 바뀌어도 따라간다).
        if (belts != null && belts.Count >= 2 && belts[0] != null && belts[1] != null)
        {
            Vector3 step = belts[1].transform.position - belts[0].transform.position;
            step.z = 0f;
            if (step.sqrMagnitude > 0.0001f) return step;
        }

        return new Vector3(beltGridStep.x, beltGridStep.y, 0f);
    }

    // Initialize()를 거치지 않은 경로(에디터에서 라인만 켜 두고 테스트하는 경우 등)에서도
    // 경로가 비어 있으면 그때 만들어 둔다.
    private void EnsurePath()
    {
        if (path.Count == 0) BuildPath();
    }

    // 저장된 위치에서 다음 목표 지점을 다시 계산한다. 경로가 체크포인트 2~3개에서 벨트 타일 수만큼으로
    // 늘어났기 때문에, 예전 세이브의 targetIndex를 그대로 쓰면 엉뚱한 지점을 향해 되돌아간다.
    // 경로는 한 방향으로만 뻗어 자기교차가 없으므로, 위치에서 가장 가까운 구간의 끝점이 곧 다음 목표다.
    private int ResolveTargetIndex(Vector3 _position)
    {
        if (path.Count <= 1) return 0;

        int best = 1;
        float bestSqrDist = float.MaxValue;

        for (int i = 1; i < path.Count; ++i)
        {
            Vector3 from = path[i - 1].Position;
            Vector3 to = path[i].Position;
            Vector3 seg = to - from;

            float sqrLen = seg.sqrMagnitude;
            float t = sqrLen > 0f ? Mathf.Clamp01(Vector3.Dot(_position - from, seg) / sqrLen) : 0f;

            float sqrDist = (_position - (from + seg * t)).sqrMagnitude;
            if (sqrDist < bestSqrDist)
            {
                bestSqrDist = sqrDist;
                best = i;
            }
        }

        return best;
    }

    private void SetBeltsAnimationSpeed(float _speed)
    {
        for (int i = 0; i < belts.Count; i++)
        {
            if (belts[i].animator != null)
            {
                belts[i].animator.speed = _speed * beltAnimationSpeedMultiplier * globalSpeedMultiplier;
            }
        }
    }

    public void IncreaseSpeed(float _percentage)
    {
        _percentage *= 0.01f;
        Debug.Log(_percentage);
        // 0.1(10%) 증가 시 기존 속도에 1.1을 곱함
        beltSpeed *= (1f + _percentage);
    }

    public void LogIn(LogItem _item)
    {
        if (null == _item) return;

        EnsurePath();
        if (0 == path.Count) return;

        Vector3 entryPos = path[0].Position;

        Sound.Play(SoundID.ConvayerPut, entryPos, GetSoundVolume());

        _item.SetHeight(LogOnBeltSortHeight);
        // 아이템을 첫 번째 벨트 타일 중심으로 즉시 이동
        _item.transform.position = entryPos;

        // 진입 연출 (스프링 댐퍼 효과)
        _item.PlayBeltEnterAnimation();

        // 다음 목표 인덱스 설정 (경로 지점이 1개보다 많으면 1번부터, 아니면 0번 도달 처리 대기)
        int nextTarget = path.Count > 1 ? 1 : 0;
        activeItems.Add(new BeltItem(_item, nextTarget));

        // 보석 등급이면 벨트 위에서도 반짝임을 붙인다(제재목 포함).
        _item.PlayGemShiny();

        StartBelt();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        // 비활성화 예정 아이템 업데이트 (람다 대신 수동 관리)
        UpdateDeactivatingItems(deltaTime);

        // 1. 목표 속도 결정 (움직임 명령이 있고 아이템이 있는 경우에만 목표 속도 유지)
        float targetSpeedValue = (isMoving && activeItems.Count > 0) ? beltSpeed : 0f;

        // 2. 현재 속도를 목표 속도로 부드럽게 이동 및 애니메이션 적용
        if (!Mathf.Approximately(currentSpeed, targetSpeedValue))
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeedValue, acceleration * deltaTime);
            SetBeltsAnimationSpeed(currentSpeed);
        }

        UpdateLoopSound();

        // 3. 실행 조건 확인 (속도가 0이고 목표 속도도 0이면 중단)
        if (currentSpeed <= 0f && targetSpeedValue <= 0f) return;

        // 4. 아이템 이동 처리
        if (activeItems.Count == 0 || path.Count == 0) return;

        float step = currentSpeed * globalSpeedMultiplier * deltaTime;
        for (int i = activeItems.Count - 1; i >= 0; i--)
        {
            BeltItem beltItem = activeItems[i];

            if (beltItem.item == null)
            {
                activeItems.RemoveAt(i);
                continue;
            }

            Vector3 targetPos = path[beltItem.targetIndex].Position;

            // 이동 처리
            beltItem.item.transform.position = Vector3.MoveTowards(
                beltItem.item.transform.position,
                targetPos,
                step
            );

            beltItem.item.UpdateSortingOrder();

            // 도달 확인
            if (Vector3.Distance(beltItem.item.transform.position, targetPos) < 0.01f)
            {
                beltItem.targetIndex++;

                // 모든 경로 지점을 통과했는지 확인
                if (beltItem.targetIndex >= path.Count)
                {
                    LogOut(beltItem.item);
                    activeItems.RemoveAt(i);
                }
                else
                {
                    // 인덱스 갱신 후 리스트에 다시 저장 (구조체 복사)
                    activeItems[i] = beltItem;
                }
            }
        }
    }

    // ConvayerLoop는 Start/End 구간이 따로 없는 순수 루프 클립이라, 코드에서 currentSpeed(가감속 곡선)를
    // 그대로 따라가는 볼륨/피치로 매 프레임 직접 밀어준다. 별도의 페이드 타이머 없이 이 방식만으로
    // 벨트가 멈춰있을 때(볼륨 0, 피치 -5세미톤) -> 가속(정상 볼륨/피치로 상승) -> 감속(다시 0/-5세미톤으로
    // 하강)이 실제 컨베이어 속도와 항상 정확히 연동된다.
    private void UpdateLoopSound()
    {
        // IsValid만으로는 부족하다 - 씬 전환 시 AudioManager.StopAll3DSounds()가 핸들은 그대로 둔 채
        // AudioSource만 직접 Stop()시키는 경로가 있어서, 핸들은 여전히 "유효"하지만 실제로는 재생이
        // 멈춰있는 상태가 될 수 있다(예: 던전에서 마을로 돌아온 직후). 그 경우도 걸러서 다시 재생한다.
        if (!loopSoundHandle.IsValid || !Sound.IsTrackedPlaying(loopSoundHandle))
        {
            loopSoundHandle = Sound.PlayTracked(SoundID.ConvayerLoop, transform.position, 0f);
        }

        float ratio = beltSpeed > 0f ? Mathf.Clamp01(currentSpeed / beltSpeed) : 0f;

        // 가속 특성으로 beltSpeed가 기본 속도 대비 올라간 만큼, 정상 주행 시 도달하는 피치도
        // 1.0에서 loopMaxSpeedPitch(기본 1.6)까지 함께 올라간다.
        float speedMultiplier = baseBeltSpeed > 0f ? beltSpeed / baseBeltSpeed : 1f;
        float runningPitch = loopMaxSpeedMultiplier > 1f
            ? Mathf.Lerp(1f, loopMaxSpeedPitch, Mathf.InverseLerp(1f, loopMaxSpeedMultiplier, speedMultiplier))
            : 1f;

        float stopPitch = Mathf.Pow(2f, loopStopPitchSemitones / 12f);

        Sound.SetTrackedVolume(loopSoundHandle, Mathf.Lerp(0f, loopIntendedVolume, ratio) * GetSoundVolume());
        Sound.SetTrackedPitch(loopSoundHandle, Mathf.Lerp(stopPitch, runningPitch, ratio));
        Sound.UpdateTrackedPosition(loopSoundHandle, transform.position);
    }

    // 루프 사운드는 AudioManager의 소스 풀이 소유하므로, 이 오브젝트가 꺼져도(제재소 라인 축소,
    // 세이브 로드로 라인 수가 줄어드는 경우 등) 저절로 멈추지 않는다. Update()가 돌지 않아
    // 볼륨 갱신도 끊기므로, 마지막 볼륨 그대로 영영 남는다. 여기서 확실히 끊는다.
    private void OnDisable()
    {
        Sound.StopTracked(loopSoundHandle);
        loopSoundHandle = AudioHandle.Invalid;
    }

    private void UpdateDeactivatingItems(float _deltaTime)
    {
        for (int i = deactivatingItems.Count - 1; i >= 0; i--)
        {
            DeactivatingItem dItem = deactivatingItems[i];
            dItem.remainingTime -= _deltaTime;

            if (dItem.item != null)
            {
                dItem.item.UpdateSortingOrder();
            }

            if (dItem.remainingTime <= 0f)
            {
                if (dItem.item != null)
                {
                    // 데이터 동기화 및 이벤트 호출 (연출 종료 시점)
                    logItemData.itemType = dItem.item.itemType;
                    logItemData.sprite = dItem.item.sprite;
                    logItemData.color = dItem.item.color;
                    logItemData.logState = dItem.item.logState;
                    logItemData.treeType = dItem.item.treeType;

                    LogOutEvent?.Invoke(dItem.item, logItemData);

                    // 파티클이 자식으로 붙은 채 꺼지면 파티클의 activeSelf는 true로 남아
                    // VFX 풀이 "사용 중"으로 오인하고, 그 인스턴스는 영영 재사용되지 못한다.
                    dItem.item.StopGemShiny();
                    dItem.item.gameObject.SetActive(false);
                }
                deactivatingItems.RemoveAt(i);
            }
            else
            {
                deactivatingItems[i] = dItem;
            }
        }
    }

    private void LogOut(LogItem _item)
    {
        // _item.gameObject.SetActive(false); // 지연 비활성화를 위해 제거

        if (true == stopsOnLogOut)
        {
            // 커터는 한 번에 하나만 가공하므로, 아이템이 하나 나갈 때마다(뒤에 남은 아이템이 있어도)
            // 무조건 벨트를 멈춘다. 그렇지 않으면 뒤따르는 아이템이 커터가 비기 전에 끝까지 도달해
            // LogCutter.StartCutting의 bIsCutting 가드에 막혀 조용히 유실된다.
            // 벨트는 CuttingDone -> LogProcessLine.CuttingDone()의 inBelt.StartBelt() 호출로 재개된다.
            isMoving = false;
            BeltStopEvent?.Invoke();
        }

        Vector3 moveDir = Vector3.right; // 기본값
        if (2 <= path.Count)
        {
            // 마지막 이동 방향 계산 (마지막 경로 지점 - 이전 경로 지점)
            moveDir = (path[path.Count - 1].Position - path[path.Count - 2].Position).normalized;
        }

        float duration = 0.1f;
        // 현재 벨트 속도를 반영하여 미끄러지는 거리 산출
        float moveDist = slideSpeed * duration * 3f;
        Vector3 targetPos = _item.transform.position + (moveDir * moveDist);

        _item.PlayBeltExitAnimation(targetPos, duration);

        // 람다 대신 비활성화 대기 리스트에 추가
        deactivatingItems.Add(new DeactivatingItem(_item, duration));
    }

    public void StartBelt()
    {
        if (activeItems.Count == 0)
            return;

        isMoving = true;
    }

    public void ShiftItems(Vector3 _offset)
    {
        for (int i = 0; i < activeItems.Count; i++)
        {
            if (activeItems[i].item != null)
            {
                activeItems[i].item.transform.position += _offset;
            }
        }

        if (deactivatingItems.Count > 0)
        {
            for (int i = 0; i < deactivatingItems.Count; i++)
            {
                DeactivatingItem dItem = deactivatingItems[i];
                if (dItem.item != null)
                {
                    dItem.item.transform.DOKill();

                    logItemData.itemType = dItem.item.itemType;
                    logItemData.sprite = dItem.item.sprite;
                    logItemData.color = dItem.item.color;
                    logItemData.logState = dItem.item.logState;
                    logItemData.treeType = dItem.item.treeType;

                    LogOutEvent?.Invoke(dItem.item, logItemData);

                    dItem.item.StopGemShiny();
                    dItem.item.gameObject.SetActive(false);
                }
            }
            deactivatingItems.Clear();
        }
    }

    public void PopulateSaveData(ref BeltSaveData _saveData)
    {
        _saveData.isMoving = isMoving;
        _saveData.activeItems.Clear();

        for (int i = 0; i < activeItems.Count; i++)
        {
            BeltItem item = activeItems[i];
            if (item.item == null) continue;

            BeltItemSaveData itemSaveData = new BeltItemSaveData();
            itemSaveData.targetIndex = item.targetIndex;
            itemSaveData.position = item.item.transform.position;

            itemSaveData.itemData = new ItemSaveData
            {
                itemType = item.item.itemType,
                treeType = item.item.treeType,
                logState = item.item.logState,
                durability = item.item.durability,
                color = item.item.color, // 컬러 저장
                bIsTimber = item.item.BIsTimber // 가공 여부 저장
            };

            _saveData.activeItems.Add(itemSaveData);
        }

        // 퇴출 연출 대기 중인 아이템도 저장 (이 구간에 걸린 아이템 유실 방지)
        if (_saveData.deactivatingItems == null)
            _saveData.deactivatingItems = new List<DeactivatingItemSaveData>(deactivatingItems.Count);
        else
            _saveData.deactivatingItems.Clear();

        for (int i = 0; i < deactivatingItems.Count; i++)
        {
            DeactivatingItem dItem = deactivatingItems[i];
            if (dItem.item == null) continue;

            DeactivatingItemSaveData dSaveData = new DeactivatingItemSaveData();
            dSaveData.position = dItem.item.transform.position;
            dSaveData.remainingTime = dItem.remainingTime;
            dSaveData.itemData = new ItemSaveData
            {
                itemType = dItem.item.itemType,
                treeType = dItem.item.treeType,
                logState = dItem.item.logState,
                durability = dItem.item.durability,
                color = dItem.item.color,
                bIsTimber = dItem.item.BIsTimber // 가공 여부 저장
            };

            _saveData.deactivatingItems.Add(dSaveData);
        }
    }

    public void LoadSaveData(BeltSaveData _data, LogItemPoolingManager _poolingManager)
    {
        activeItems.Clear();
        deactivatingItems.Clear();
        isMoving = _data.isMoving;

        EnsurePath();

        if (_data.activeItems != null)
        {
            foreach (var itemData in _data.activeItems)
            {
                LogItemData data = new LogItemData
                {
                    itemType = itemData.itemData.itemType,
                    treeType = itemData.itemData.treeType,
                    logState = itemData.itemData.logState,
                    color = itemData.itemData.color // 컬러 복구
                };

                LogItem newItem = _poolingManager.GetLogItem(data);
                if (newItem != null)
                {
                    newItem.SetHeight(LogOnBeltSortHeight);
                    newItem.transform.position = itemData.position;
                    newItem.durability = itemData.itemData.durability;
                    newItem.UpdateSortingOrder();
                    // targetIndex는 저장 당시 경로 기준이라 그대로 믿을 수 없다(경로 지점 수가 바뀐
                    // 세이브 호환). 저장된 위치에서 다음 목표를 다시 찾는다.
                    activeItems.Add(new BeltItem(newItem, ResolveTargetIndex(itemData.position)));

                    // 가공이 끝난 제재목이면 외형을 되돌린다(스프라이트만 바뀌므로 별도 복원이 필요하다).
                    if (itemData.itemData.bIsTimber) newItem.SetTimberSprite();

                    newItem.PlayGemShiny();
                }
            }
        }

        // 퇴출 연출 대기 아이템 복원 - 남은 시간이 지나면 저장 당시와 동일하게 LogOutEvent를 발생시켜
        // 다음 단계(커터 투입 / 평가)로 이어진다.
        if (_data.deactivatingItems != null)
        {
            foreach (var dItemData in _data.deactivatingItems)
            {
                LogItemData data = new LogItemData
                {
                    itemType = dItemData.itemData.itemType,
                    treeType = dItemData.itemData.treeType,
                    logState = dItemData.itemData.logState,
                    color = dItemData.itemData.color
                };

                LogItem newItem = _poolingManager.GetLogItem(data);
                if (newItem != null)
                {
                    newItem.SetHeight(LogOnBeltSortHeight);
                    newItem.transform.position = dItemData.position;
                    newItem.durability = dItemData.itemData.durability;
                    newItem.UpdateSortingOrder();
                    deactivatingItems.Add(new DeactivatingItem(newItem, dItemData.remainingTime));

                    if (dItemData.itemData.bIsTimber) newItem.SetTimberSprite();

                    newItem.PlayGemShiny();
                }
            }
        }

        if (isMoving)
        {
            StartBelt();
            currentSpeed = beltSpeed;
        }
        else
        {
            currentSpeed = 0f;
            SetBeltsAnimationSpeed(0f);
        }
    }

#if UNITY_EDITOR
    // 원목이 실제로 지나갈 경로를 씬 뷰에 그려서, beltSurfaceOffset이 벨트 상판 중심과 맞는지
    // 눈으로 바로 확인할 수 있게 한다.
    private void OnDrawGizmosSelected()
    {
        if (belts == null || checkPoints == null) return;

        Vector3 surfaceOffset = new Vector3(beltSurfaceOffset.x, beltSurfaceOffset.y, 0f);

        Vector3 prev = Vector3.zero;
        bool hasPrev = false;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < belts.Count; ++i)
        {
            if (belts[i] == null) continue;

            Vector3 p = belts[i].transform.position + surfaceOffset;
            Gizmos.DrawWireSphere(p, 0.04f);
            if (hasPrev) Gizmos.DrawLine(prev, p);
            prev = p;
            hasPrev = true;
        }

        if (hasPrev && checkPoints.Count > 0 && checkPoints[checkPoints.Count - 1] != null)
        {
            Transform exitCheckPoint = checkPoints[checkPoints.Count - 1];
            Vector3 exit = exitCheckPoint.position;

            if (snapExitToBeltAxis)
            {
                Vector3 raw = exit - prev;
                raw.z = 0f;
                if (raw.sqrMagnitude > Mathf.Epsilon) exit = prev + SnapToBeltAxis(raw) * raw.magnitude;

                // 보정 전 위치도 같이 보여줘야 얼마나 틀어져 있었는지 바로 보인다.
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(exitCheckPoint.position, exit);
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(exit, 0.04f);
            Gizmos.DrawLine(prev, exit);

            // 커터/평가기로 빨려들어가는 방향 (LogOut의 슬라이드 연출과 같은 방향)
            Vector3 slideDir = (exit - prev).normalized;
            Gizmos.DrawLine(exit, exit + slideDir * 0.3f);
        }
    }
#endif
}
