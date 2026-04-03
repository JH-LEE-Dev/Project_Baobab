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
    [SerializeField] private float beltSpeed = 3f;


    // 내부 상태
    private List<BeltItem> activeItems = new List<BeltItem>(10);
    private bool isMoving = false;

    public void Initialize()
    {
        activeItems.Clear();
        isMoving = false;
        if (tilemap != null) tilemap.animationFrameRate = 0f;
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
        if (tilemap != null) tilemap.animationFrameRate = 3f;

        tilemap.RefreshAllTiles();
    }
}
