using System;
using System.Collections.Generic;

public struct ScopeSignal<T> where T : struct
{
    public bool IsBegin;
    public T Context;
}

public class SignalHub
{
    private interface ISignalHandlerList { }
    private class SignalHandlerList<T> : ISignalHandlerList
    {
        public readonly List<Action<T>> Handlers = new(16);

        // 발행 도중 핸들러가 구독/해제되어도 안전하도록, 순회는 원본이 아닌 스냅샷으로 한다.
        // 매 발행마다 배열을 새로 만들면 GC 부담이 크므로 버퍼를 재사용하고,
        // 같은 시그널이 중첩 발행된 경우(Depth > 0)에만 임시 배열을 쓴다.
        public Action<T>[] Buffer = new Action<T>[16];
        public int Depth;
    }

    public delegate void SpanHandler<TContext, TData>(TContext context, ReadOnlySpan<TData> data);
    private class SpanSignalHandlerList<TContext, TData> : ISignalHandlerList
    {
        public readonly List<SpanHandler<TContext, TData>> Handlers = new(16);

        public SpanHandler<TContext, TData>[] Buffer = new SpanHandler<TContext, TData>[16];
        public int Depth;
    }

    private readonly Dictionary<Type, ISignalHandlerList> _storage = new();
    private readonly Dictionary<(Type, Type), ISignalHandlerList> _spanStorage = new();

    public void Subscribe<T>(Action<T> handler) where T : struct
    {
        var list = GetOrCreateList<T>();
        if (!list.Handlers.Contains(handler)) list.Handlers.Add(handler);
    }

    public void UnSubscribe<T>(Action<T> handler) where T : struct
    {
        if (_storage.TryGetValue(typeof(T), out var listObj))
            ((SignalHandlerList<T>)listObj).Handlers.Remove(handler);
    }

    /// <summary>
    /// 핸들러 목록을 스냅샷으로 복사한 뒤 순회한다(호출 순서는 기존과 동일한 역순).
    /// 예전엔 살아있는 List를 그대로 인덱싱해서, 발행 도중 핸들러가 구독 해제되면
    /// (예: 시그널 처리 중 시스템이 통째로 Release되는 씬 전환 경로) Count가 줄어든 리스트를
    /// 예전 인덱스로 접근해 ArgumentOutOfRangeException이 나거나 일부 핸들러가 통째로 누락됐다.
    /// 이 예외는 DOTween 콜백 안에서 발행된 경우 세이프 모드에 삼켜져 원인조차 남지 않는다.
    /// </summary>
    public void Publish<T>(T signal) where T : struct
    {
        if (!_storage.TryGetValue(typeof(T), out var listObj)) return;

        var list = (SignalHandlerList<T>)listObj;
        var handlers = list.Handlers;

        int count = handlers.Count;
        if (count == 0) return;

        // 중첩 발행이 아니면 공용 버퍼를 재사용해 할당을 피한다.
        bool useSharedBuffer = list.Depth == 0;
        Action<T>[] snapshot;

        if (useSharedBuffer)
        {
            if (list.Buffer.Length < count) list.Buffer = new Action<T>[count];
            snapshot = list.Buffer;
        }
        else
        {
            snapshot = new Action<T>[count];
        }

        handlers.CopyTo(0, snapshot, 0, count);

        list.Depth++;
        try
        {
            for (int i = count - 1; i >= 0; i--) snapshot[i]?.Invoke(signal);
        }
        finally
        {
            list.Depth--;
            // 해제된 핸들러의 참조를 버퍼가 붙들고 있지 않도록 비운다.
            if (useSharedBuffer) Array.Clear(snapshot, 0, count);
        }
    }

    public void Subscribe<TContext, TData>(SpanHandler<TContext, TData> handler) where TContext : struct
    {
        var list = GetOrCreateSpanList<TContext, TData>();
        if (!list.Handlers.Contains(handler)) list.Handlers.Add(handler);
    }

    public void UnSubscribe<TContext, TData>(SpanHandler<TContext, TData> handler) where TContext : struct
    {
        var key = (typeof(TContext), typeof(TData));
        if (_spanStorage.TryGetValue(key, out var listObj))
            ((SpanSignalHandlerList<TContext, TData>)listObj).Handlers.Remove(handler);
    }

    // 위 Publish와 동일한 이유로 스냅샷 순회를 사용한다.
    public void Publish<TContext, TData>(TContext context, ReadOnlySpan<TData> data) where TContext : struct
    {
        var key = (typeof(TContext), typeof(TData));
        if (!_spanStorage.TryGetValue(key, out var listObj)) return;

        var list = (SpanSignalHandlerList<TContext, TData>)listObj;
        var handlers = list.Handlers;

        int count = handlers.Count;
        if (count == 0) return;

        bool useSharedBuffer = list.Depth == 0;
        SpanHandler<TContext, TData>[] snapshot;

        if (useSharedBuffer)
        {
            if (list.Buffer.Length < count) list.Buffer = new SpanHandler<TContext, TData>[count];
            snapshot = list.Buffer;
        }
        else
        {
            snapshot = new SpanHandler<TContext, TData>[count];
        }

        handlers.CopyTo(0, snapshot, 0, count);

        list.Depth++;
        try
        {
            for (int i = count - 1; i >= 0; i--) snapshot[i]?.Invoke(context, data);
        }
        finally
        {
            list.Depth--;
            if (useSharedBuffer) Array.Clear(snapshot, 0, count);
        }
    }

    public void BeginScope<T>(T context) where T : struct
    {
        Publish(new ScopeSignal<T> { IsBegin = true, Context = context });
    }

    public void EndScope<T>(T context) where T : struct
    {
        Publish(new ScopeSignal<T> { IsBegin = false, Context = context });
    }

    public void SubscribeScope<T>(Action<ScopeSignal<T>> handler) where T : struct
    {
        Subscribe(handler);
    }

    public void UnSubscribeScope<T>(Action<ScopeSignal<T>> handler) where T : struct
    {
        UnSubscribe(handler);
    }

    private SignalHandlerList<T> GetOrCreateList<T>() where T : struct
    {
        var type = typeof(T);
        if (!_storage.TryGetValue(type, out var list))
            _storage[type] = list = new SignalHandlerList<T>();
        return (SignalHandlerList<T>)list;
    }

    private SpanSignalHandlerList<TContext, TData> GetOrCreateSpanList<TContext, TData>() where TContext : struct
    {
        var key = (typeof(TContext), typeof(TData));
        if (!_spanStorage.TryGetValue(key, out var list))
            _spanStorage[key] = list = new SpanSignalHandlerList<TContext, TData>();
        return (SpanSignalHandlerList<TContext, TData>)list;
    }
}