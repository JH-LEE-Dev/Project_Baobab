using System;
using UnityEngine;

public enum UILayer
{ 
    None,
    Popup,      
    Overlay,    
    Tooltip,     
}

[Serializable]
public struct CanvasRoot
{
    public Transform popupLayerRoot;
    public Transform overlayLayerRoot;
    public Transform tooltipLayerRoot;
}