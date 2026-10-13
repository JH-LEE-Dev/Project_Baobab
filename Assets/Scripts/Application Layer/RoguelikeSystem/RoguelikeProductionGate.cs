using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 시스템이 연출 완료를 기다리는 단일 창구. 같은 연출에 핸들러가 여럿이면 전부 끝날 때까지 기다린다.
///
///   핸들러 없음  : fallbackSeconds(종류별) 기다린 뒤 완료.
///   핸들러 있음  : 모든 핸들러가 _onComplete를 부르면 완료. maxWaitSeconds가 지나면 경고를 남기고 강제 완료.
///
/// 대기는 스케일 시간(WaitForSeconds)이다. ESC 일시정지 동안에는 같이 멈춘다.
/// </summary>
public sealed class RoguelikeProductionGate
{
    private readonly List<IRoguelikeProductionHandler> handlers = new List<IRoguelikeProductionHandler>(8);
    private readonly List<IRoguelikeProductionHandler> matched = new List<IRoguelikeProductionHandler>(4);

    private Func<ERoguelikeProduction, float> getFallbackSeconds;
    private float maxWaitSeconds = 15f;

    // 연출이 끝나기 전에 다른 상태로 넘어가면(포기, 메인메뉴 이탈) 이전 연출의 완료 콜백을 무시하기 위한 번호.
    private int requestVersion;

    public void Initialize(Func<ERoguelikeProduction, float> _getFallbackSeconds, float _maxWaitSeconds)
    {
        getFallbackSeconds = _getFallbackSeconds;
        maxWaitSeconds = Mathf.Max(0.1f, _maxWaitSeconds);
    }

    public void Register(IRoguelikeProductionHandler _handler)
    {
        if (null != _handler && false == handlers.Contains(_handler)) handlers.Add(_handler);
    }

    public void Unregister(IRoguelikeProductionHandler _handler)
    {
        handlers.Remove(_handler);
    }

    /// <summary>진행 중인 대기를 모두 무효로 만든다. 이후 도착하는 완료 콜백은 무시된다.</summary>
    public void CancelAll()
    {
        requestVersion++;
    }

    /// <summary>연출을 요청하고 끝날 때까지 기다린다. 코루틴 안에서 yield return으로 쓴다.</summary>
    public IEnumerator Play(RoguelikeProductionRequest _request)
    {
        int version = ++requestVersion;

        matched.Clear();
        for (int i = 0; i < handlers.Count; i++)
        {
            if (null != handlers[i] && handlers[i].CanHandle(_request.type)) matched.Add(handlers[i]);
        }

        if (0 == matched.Count)
        {
            float fallback = null != getFallbackSeconds ? getFallbackSeconds(_request.type) : 0f;
            if (fallback > 0f) yield return new WaitForSeconds(fallback);
            yield break;
        }

        int pending = matched.Count;
        Action onComplete = () =>
        {
            if (version == requestVersion) pending--;
        };

        // matched는 다음 요청이 재사용하므로 핸들러 호출 중에 다른 Play가 끼어들어도 안전하도록 개수만 들고 간다.
        int count = matched.Count;
        for (int i = 0; i < count; i++)
        {
            try
            {
                matched[i].Play(in _request, onComplete);
            }
            catch (Exception e)
            {
                Debug.LogError($"[RoguelikeProductionGate] '{_request.type}' 연출 핸들러에서 예외가 발생했습니다. 이 핸들러는 완료된 것으로 칩니다.");
                Debug.LogException(e);
                if (version == requestVersion) pending--;
            }
        }

        float elapsed = 0f;
        while (pending > 0 && version == requestVersion)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= maxWaitSeconds)
            {
                Debug.LogWarning($"[RoguelikeProductionGate] '{_request.type}' 연출이 {maxWaitSeconds}초 안에 끝나지 않아 강제로 진행합니다. " +
                    "핸들러가 _onComplete를 부르는지 확인하십시오.");
                break;
            }

            yield return null;
        }
    }
}
