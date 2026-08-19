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

    // 외부 의존성
    [SerializeField] private List<Transform> checkPoints = new List<Transform>(5);
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
        if (_item == null || checkPoints.Count == 0) return;

        Sound.Play(SoundID.ConvayerPut, checkPoints[0].position, GetSoundVolume());

        _item.SetHeight(0.425f);
        // 아이템을 첫 번째 체크포인트 위치로 즉시 이동
        _item.transform.position = checkPoints[0].position;

        // 진입 연출 (스프링 댐퍼 효과)
        _item.transform.DOKill();
        _item.transform.localScale = Vector3.zero;
        var targetScale = new Vector3(1f,1f,1f);
        _item.transform.DOScale(targetScale, 0.5f).SetEase(Ease.OutElastic, 1.7f, 0.3f);

        // 다음 목표 인덱스 설정 (체크포인트가 1개보다 많으면 1번부터, 아니면 0번 도달 처리 대기)
        int nextTarget = checkPoints.Count > 1 ? 1 : 0;
        activeItems.Add(new BeltItem(_item, nextTarget));

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
        if (activeItems.Count == 0) return;

        float step = currentSpeed * globalSpeedMultiplier * deltaTime;
        for (int i = activeItems.Count - 1; i >= 0; i--)
        {
            BeltItem beltItem = activeItems[i];

            if (beltItem.item == null)
            {
                activeItems.RemoveAt(i);
                continue;
            }

            Transform target = checkPoints[beltItem.targetIndex];

            // 이동 처리
            beltItem.item.transform.position = Vector3.MoveTowards(
                beltItem.item.transform.position,
                target.position,
                step
            );

            beltItem.item.UpdateSortingOrder();

            // 도달 확인
            if (Vector3.Distance(beltItem.item.transform.position, target.position) < 0.01f)
            {
                beltItem.targetIndex++;

                // 모든 체크포인트를 통과했는지 확인
                if (beltItem.targetIndex >= checkPoints.Count)
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

        if (stopsOnLogOut)
        {
            // 커터는 한 번에 하나만 가공하므로, 아이템이 하나 나갈 때마다(뒤에 남은 아이템이 있어도)
            // 무조건 벨트를 멈춘다. 그렇지 않으면 뒤따르는 아이템이 커터가 비기 전에 끝까지 도달해
            // LogCutter.StartCutting의 bIsCutting 가드에 막혀 조용히 유실된다.
            // 벨트는 CuttingDone -> LogProcessLine.CuttingDone()의 inBelt.StartBelt() 호출로 재개된다.
            isMoving = false;
            BeltStopEvent?.Invoke();
        }

        // 퇴출 연출: 스케일이 작아지는 동안 마지막 이동 방향으로 계속 전진
        _item.transform.DOKill();

        Vector3 moveDir = Vector3.right; // 기본값
        if (checkPoints.Count >= 2)
        {
            // 마지막 이동 방향 계산 (마지막 체크포인트 - 이전 체크포인트)
            moveDir = (checkPoints[checkPoints.Count - 1].position - checkPoints[checkPoints.Count - 2].position).normalized;
        }

        float duration = 0.1f;
        // 현재 벨트 속도를 반영하여 미끄러지는 거리 산출
        float moveDist = slideSpeed * duration * 3;
        Vector3 targetPos = _item.transform.position + (moveDir * moveDist);

        _item.transform.DOMove(targetPos, duration).SetEase(Ease.Linear);
        _item.transform.DOScale(Vector3.zero, duration).SetEase(Ease.InBack);

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
                color = item.item.color // 컬러 저장
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
                color = dItem.item.color
            };

            _saveData.deactivatingItems.Add(dSaveData);
        }
    }

    public void LoadSaveData(BeltSaveData _data, LogItemPoolingManager _poolingManager)
    {
        activeItems.Clear();
        deactivatingItems.Clear();
        isMoving = _data.isMoving;

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
                    newItem.transform.position = itemData.position;
                    newItem.durability = itemData.itemData.durability;
                    newItem.UpdateSortingOrder();
                    activeItems.Add(new BeltItem(newItem, itemData.targetIndex));
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
                    newItem.transform.position = dItemData.position;
                    newItem.durability = dItemData.itemData.durability;
                    newItem.UpdateSortingOrder();
                    deactivatingItems.Add(new DeactivatingItem(newItem, dItemData.remainingTime));
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
}
