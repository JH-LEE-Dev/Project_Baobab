using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 일정 주기로 자신의 위치를 중심으로 반경 내 IStaticCollidable(아이템 등)을 스캔하는 재사용 헬퍼.
/// Character/LumberjackNPC처럼 "찾은 아이템으로 무엇을 할지"는 서로 다르지만
/// "얼마나 자주, 어떤 반경으로 스캔할지"는 동일한 패턴을 공유하는 대상들이 사용한다.
/// </summary>
public class ItemDetector
{
    private readonly Transform sensorTransform;
    private readonly LayerMask itemLayer;
    private readonly List<IStaticCollidable> results = new List<IStaticCollidable>(16);
    private float timer;

    public ItemDetector(Transform _sensorTransform, LayerMask _itemLayer)
    {
        sensorTransform = _sensorTransform;
        itemLayer = _itemLayer;
    }

    /// <summary>
    /// 매 프레임(혹은 FixedUpdate)마다 호출하세요. _interval마다 실제로 스캔을 수행해
    /// 발견된 각 콜라이더블을 _onFound에 전달합니다.
    /// </summary>
    public void Tick(float _deltaTime, float _interval, float _radius, Action<IStaticCollidable> _onFound)
    {
        if (CollisionSystem.Instance == null) return;

        timer += _deltaTime;
        if (timer < _interval) return;
        timer = 0f;

        CollisionSystem.Instance.GetCollidablesInRadius(sensorTransform.position, _radius, itemLayer.value, results);
        for (int i = 0; i < results.Count; i++)
        {
            _onFound(results[i]);
        }
    }
}
