using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class LogInBelt : MonoBehaviour
{
    public event Action<LogItem, ILogItemData> LogOutEvent;
    private LogItemData logItemData = new LogItemData();
    [SerializeField] Tilemap tilemap;


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

    // 외부 의존성
    [SerializeField] private List<Transform> checkPoints = new List<Transform>(5);
    [SerializeField] private float beltSpeed = 1.5f;


    // 내부 상태
    private List<BeltItem> activeItems = new List<BeltItem>(10);
    private bool isMoving = false;

    public void Initialize()
    {
        activeItems.Clear();
        isMoving = false;
        if (tilemap != null) tilemap.animationFrameRate = 0f;
    }

    public void IncreaseSpeed(float _percentage)
    {
        // 0.1(10%) 증가 시 기존 속도에 1.1을 곱함
        beltSpeed *= (1f + _percentage);
        
        if (isMoving && tilemap != null)
        {
            tilemap.animationFrameRate = beltSpeed * 3.33f;
        }
    }

    public void LogIn(LogItem _item)
    {
        if (_item == null || checkPoints.Count == 0) return;

        // 아이템을 첫 번째 체크포인트 위치로 즉시 이동
        _item.transform.position = checkPoints[0].position;

        // 다음 목표 인덱스 설정 (체크포인트가 1개보다 많으면 1번부터, 아니면 0번 도달 처리 대기)
        int nextTarget = checkPoints.Count > 1 ? 1 : 0;
        activeItems.Add(new BeltItem(_item, nextTarget));

        StartBelt();
    }

    private void Update()
    {
        if (!isMoving || activeItems.Count == 0) return;

        // 역순 순회하여 리스트 수정 시의 안정성 확보 및 GC 최소화
        for (int i = activeItems.Count - 1; i >= 0; i--)
        {
            BeltItem beltItem = activeItems[i];

            if (beltItem.item == null)
            {
                activeItems.RemoveAt(i);
                continue;
            }

            Transform target = checkPoints[beltItem.targetIndex];
            float step = beltSpeed * Time.deltaTime;

            // 이동 처리
            beltItem.item.transform.position = Vector3.MoveTowards(
                beltItem.item.transform.position,
                target.position,
                step
            );

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

    private void LogOut(LogItem _item)
    {
        isMoving = false;
        if (tilemap != null) tilemap.animationFrameRate = 0f;
        tilemap.RefreshAllTiles();
        
        logItemData.itemType = _item.itemType;
        logItemData.sprite = _item.sprite;
        logItemData.color = _item.color;
        logItemData.logState = _item.logState;
        logItemData.treeType = _item.treeType;

        LogOutEvent?.Invoke(_item, logItemData);
    }

    public void StartBelt()
    {
        if (activeItems.Count == 0)
            return;

        isMoving = true;
        if (tilemap != null) tilemap.animationFrameRate = beltSpeed * 3.33f;

        tilemap.RefreshAllTiles();
    }

    public void PopulateSaveData(ref BeltSaveData _saveData)
    {
        _saveData.isMoving = isMoving;
        _saveData.beltSpeed = beltSpeed;
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
    }

    public void LoadSaveData(BeltSaveData _data, LogItemPoolingManager _poolingManager)
    {
        activeItems.Clear();
        isMoving = _data.isMoving;
        beltSpeed = _data.beltSpeed;

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
                    activeItems.Add(new BeltItem(newItem, itemData.targetIndex));
                }
            }
        }

        if (isMoving) StartBelt();
        else if (tilemap != null) tilemap.animationFrameRate = 0f;
    }
}
