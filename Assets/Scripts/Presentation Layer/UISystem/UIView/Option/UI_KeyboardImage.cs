using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// 특정 키 바인딩 속성(ERebindableAction)을 지정하면 
/// 해당 키에 맞는 키보드 아이콘을 띄워주는 재사용 가능한 UI 컴포넌트입니다.
/// </summary>
public class UI_KeyboardImage : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField, Tooltip("키 아이콘 매핑 데이터베이스 (KeyIconDatabase)")] 
    private KeyIconDatabase keyIconDatabase;
    
    [SerializeField, Tooltip("현재 어떤 키 속성을 감지할지 선택")] 
    private ERebindableAction boundAction;
    
    [SerializeField, Tooltip("스프라이트를 노출할 이미지 컴포넌트")] 
    private Image targetImage;

    private InputManager inputManager;
    private Action cachedRefreshIcon; // GC 방지용 델리게이트 캐싱

    /// <summary>
    /// 부모 UI에서 이 컴포넌트를 초기화할 때 호출합니다.
    /// InputManager 참조를 전달받아 키 설정 변경 이벤트를 구독합니다.
    /// </summary>
    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;
        
        if (null == cachedRefreshIcon) cachedRefreshIcon = RefreshIcon;
        
        // 키 바인딩이 변경될 때마다 자동 갱신되도록 이벤트 구독
        if (null != inputManager && null != inputManager.inputReader)
        {
            inputManager.inputReader.KeyBindingsChangedEvent -= cachedRefreshIcon;
            inputManager.inputReader.KeyBindingsChangedEvent += cachedRefreshIcon;
        }

        RefreshIcon();
    }

    /// <summary>
    /// 현재 매핑된 키의 경로를 읽어와 아이콘을 갱신합니다.
    /// </summary>
    public void RefreshIcon()
    {
        if (null == inputManager || null == keyIconDatabase || null == targetImage) return;

        string _bindingPath = inputManager.GetBindingPath(boundAction);
        Sprite _icon = keyIconDatabase.GetIcon(_bindingPath);

        if (null != _icon)
        {
            targetImage.sprite = _icon;
            targetImage.enabled = true;
        }
        else
        {
            targetImage.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (null != inputManager && null != inputManager.inputReader && null != cachedRefreshIcon)
        {
            inputManager.inputReader.KeyBindingsChangedEvent -= cachedRefreshIcon;
        }
    }
}
