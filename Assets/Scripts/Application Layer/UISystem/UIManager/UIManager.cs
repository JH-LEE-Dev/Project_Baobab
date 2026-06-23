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

    protected Transform overlayPopupLayerRoot;
    protected Transform overlayOverlayLayerRoot;
    protected Transform overlayTooltipLayerRoot;

    protected Transform screenSpacePopupLayerRoot;
    protected Transform screenSpaceOverlayLayerRoot;
    protected Transform screenSpaceTooltipLayerRoot;

    protected Canvas ppCanvas;

    [Header("UIView Prefab")]
    [SerializeField] private List<UIView> viewPrefabs = new List<UIView>();

    private Dictionary<Type, UIView> prefabByType = new Dictionary<Type, UIView>();

    private Dictionary<Type, UIView> instanceByType = new Dictionary<Type, UIView>();

    public void SceneChanged(CanvasRoot canvasRoot, CanvasRoot worldCanvasRoot, CanvasRoot overlayCanvasRoot, CanvasRoot screenSpaceCanvasRoot)
    {
        CloseAll();

        popupLayerRoot = canvasRoot.popupLayerRoot;
        overlayLayerRoot = canvasRoot.overlayLayerRoot;
        tooltipLayerRoot = canvasRoot.tooltipLayerRoot;

        worldPopupLayerRoot = worldCanvasRoot.popupLayerRoot;
        worldOverlayLayerRoot = worldCanvasRoot.overlayLayerRoot;
        worldTooltipLayerRoot = worldCanvasRoot.tooltipLayerRoot;

        overlayPopupLayerRoot = overlayCanvasRoot.popupLayerRoot;
        overlayOverlayLayerRoot = overlayCanvasRoot.overlayLayerRoot;
        overlayTooltipLayerRoot = overlayCanvasRoot.tooltipLayerRoot;

        screenSpacePopupLayerRoot = screenSpaceCanvasRoot.popupLayerRoot;
        screenSpaceOverlayLayerRoot = screenSpaceCanvasRoot.overlayLayerRoot;
        screenSpaceTooltipLayerRoot = screenSpaceCanvasRoot.tooltipLayerRoot;
    }

    public void DI(Canvas _ppCanvas)
    {
        ppCanvas = _ppCanvas;
        viewCtx.DI(ppCanvas);
    }

    public void Initialize(InputManager _inputManager, LocalizationManager _localizeManager, UIDepthController _depthController)
    {
        viewCtx = new UIViewContext();
        viewCtx.Initialize(_inputManager, _localizeManager, _depthController);
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

        Transform parent = GetLayerRoot(prefab.Layer, prefab.bWorld, prefab.bScreenSpace, prefab.bOverlay);

        UIView instance = Instantiate(prefab, parent);
        instance.gameObject.name = $"{prefab.gameObject.name}_Instance";

        instance.Initialize(viewCtx);
        DataInjection(instance);

        return (T)instance;
    }

    private Transform GetLayerRoot(UILayer _layer, bool _bWorld, bool _bScreenSpace, bool _bOverlay)
    {
        if (_bOverlay)
        {
            switch (_layer)
            {
                case UILayer.Popup: return overlayPopupLayerRoot;
                case UILayer.Overlay: return overlayOverlayLayerRoot;
                case UILayer.Tooltip: return overlayTooltipLayerRoot;
                default: return default;
            }
        }

        if (_bScreenSpace)
        {
            switch (_layer)
            {
                case UILayer.Popup: return screenSpacePopupLayerRoot;
                case UILayer.Overlay: return screenSpaceOverlayLayerRoot;
                case UILayer.Tooltip: return screenSpaceTooltipLayerRoot;
                default: return default;
            }
        }

        if (_bWorld == false)
            switch (_layer)
            {
                case UILayer.Popup: return popupLayerRoot;
                case UILayer.Overlay: return overlayLayerRoot;
                case UILayer.Tooltip: return tooltipLayerRoot;
                default: return default;
            }
        else
            switch (_layer)
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

    public void ReleaseAllUIView()
    {
        foreach (var kv in instanceByType)
        {
            UIView view = kv.Value;
            if (view != null)
            {
                view.Release();
            }
        }
    }
}