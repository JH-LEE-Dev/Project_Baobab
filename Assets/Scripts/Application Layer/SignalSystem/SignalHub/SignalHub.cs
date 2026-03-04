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
    }

    public delegate void SpanHandler<TContext, TData>(TContext context, ReadOnlySpan<TData> data);
    private class SpanSignalHandlerList<TContext, TData> : ISignalHandlerList
    {
        public readonly List<SpanHandler<TContext, TData>> Handlers = new(16);
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

    public void Publish<T>(T signal) where T : struct
    {
        if (_storage.TryGetValue(typeof(T), out var listObj))
        {
            var handlers = ((SignalHandlerList<T>)listObj).Handlers;
            for (int i = handlers.Count - 1; i >= 0; i--) handlers[i]?.Invoke(signal);
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

    public void Publish<TContext, TData>(TContext context, ReadOnlySpan<TData> data) where TContext : struct
    {
        var key = (typeof(TContext), typeof(TData));
        if (_spanStorage.TryGetValue(key, out var listObj))
        {
            var handlers = ((SpanSignalHandlerList<TContext, TData>)listObj).Handlers;
            for (int i = handlers.Count - 1; i >= 0; i--) handlers[i]?.Invoke(context, data);
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