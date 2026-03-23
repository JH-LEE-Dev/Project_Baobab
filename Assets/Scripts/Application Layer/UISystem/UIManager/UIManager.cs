using System;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    protected UIViewContext viewCtx;

    protected Transform popupLayerRoot;
    protected Transform overlayLayerRoot;
    protected Transform tooltipLayerRoot;

    protected Transform worldPopupLayerRoot;
    protected Transform worldOverlayLayerRoot;
    protected Transform worldTooltipLayerRoot;

    [Header("UIView Prefab")]
    [SerializeField] private List<UIView> viewPrefabs = new List<UIView>();

    private Dictionary<Type, UIView> prefabByType = new Dictionary<Type, UIView>();

    private Dictionary<Type, UIView> instanceByType = new Dictionary<Type, UIView>();

    public void SceneChanged(CanvasRoot canvasRoot,CanvasRoot worldCanvasRoot)
    {
        CloseAll();

        popupLayerRoot = canvasRoot.popupLayerRoot;
        overlayLayerRoot = canvasRoot.overlayLayerRoot;
        tooltipLayerRoot = canvasRoot.tooltipLayerRoot;

        worldPopupLayerRoot = worldCanvasRoot.popupLayerRoot;
        worldOverlayLayerRoot = worldCanvasRoot.overlayLayerRoot;
        worldTooltipLayerRoot = worldCanvasRoot.tooltipLayerRoot;
    }

    public void Initialize(InputManager _inputManager)
    {
        viewCtx = new UIViewContext();
        viewCtx.Initialize(_inputManager);
    }

    protected void Awake()
    {
        foreach (var view in viewPrefabs)
        {
            if (view == null)
                continue;

            var type = view.GetType();

            if (!prefabByType.ContainsKey(type))
            {
                prefabByType.Add(type, view);
            }
        }
    }

    public T Open<T>() where T : UIView
    {
        var type = typeof(T);

        if (!instanceByType.TryGetValue(type, out UIView instance) || instance == null)
        {
            instance = CreateViewInstance<T>();
            instanceByType[type] = instance;
        }

        instance.Show();

        return (T)instance;
    }

    public void Close<T>() where T : UIView
    {
        var type = typeof(T);

        if (instanceByType.TryGetValue(type, out UIView instance) && instance != null)
        {
            instance.Hide();
        }
    }

    public void CloseAll()
    {
        foreach (var kv in instanceByType)
        {
            UIView view = kv.Value;
            if (view != null)
            {
                view.Hide();
            }
        }
    }

    public T GetView<T>() where T : UIView
    {
        var type = typeof(T);
        if (instanceByType.TryGetValue(type, out UIView instance))
        {
            return instance as T;
        }
        return null;
    }

    private T CreateViewInstance<T>() where T : UIView
    {
        var type = typeof(T);

        if (!prefabByType.TryGetValue(type, out UIView prefab) || prefab == null)
        {
            return null;
        }

        Transform parent = GetLayerRoot(prefab.Layer,prefab.bWorld);

        UIView instance = Instantiate(prefab, parent);
        instance.gameObject.name = $"{prefab.gameObject.name}_Instance";

        instance.Initialize(viewCtx);
        DataInjection(instance);

        return (T)instance;
    }

    private Transform GetLayerRoot(UILayer layer,bool bWorld)
    {
        if(bWorld == false)
        switch (layer)
        {
            case UILayer.Popup: return popupLayerRoot;
            case UILayer.Overlay: return overlayLayerRoot;
            case UILayer.Tooltip: return tooltipLayerRoot;
            default: return default;
        }
        else
        switch (layer)
        {
            case UILayer.Popup: return worldPopupLayerRoot;
            case UILayer.Overlay: return worldOverlayLayerRoot;
            case UILayer.Tooltip: return worldTooltipLayerRoot;
            default: return default;
        }
    }

    public void ReleaseDependency()
    {
        viewCtx.ReleaseDependency();
    }

    protected virtual void DataInjection(UIView view)
    {
        
    }
}