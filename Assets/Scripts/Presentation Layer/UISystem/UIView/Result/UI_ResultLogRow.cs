using System;
using System.Collections.Generic;
using PresentationLayer.UISystem.CustomNumber;
using UnityEngine;
using UnityEngine.UI;

public class UI_ResultLogRow : MonoBehaviour
{
    [Serializable]
    private struct LogSpriteMapping
    {
        public TreeType treeType;
        public LogState logState;
        public Sprite sprite;
    }

    [Header("UI References")]
    [SerializeField] private Image logImage;
    [SerializeField] private CurrencyFontHUD countFont;

    [Header("Log Sprite Mapping")]
    [SerializeField] private List<LogSpriteMapping> logSpriteMappings = new List<LogSpriteMapping>();

    public void Initialize()
    {
        if (logImage == null)
            logImage = GetComponentInChildren<Image>(true);

        if (countFont == null)
            countFont = GetComponentInChildren<CurrencyFontHUD>(true);

        if (countFont != null)
        {
            countFont.Initialize();
            countFont.SetMode(CurrencyFontAlignmentMode.Center);
        }

        gameObject.SetActive(false);
    }

    public void SetData(TreeType treeType, LogState logState, int count)
    {
        gameObject.SetActive(0 < count);
        SetDataInternal(treeType, logState, count);
    }

    public void SetDataVisible(TreeType treeType, LogState logState, int count)
    {
        gameObject.SetActive(true);
        SetDataInternal(treeType, logState, count);
    }

    private void SetDataInternal(TreeType treeType, LogState logState, int count)
    {
        if (logImage != null)
            logImage.sprite = GetSprite(treeType, logState);

        if (countFont != null)
            countFont.SetNumber(count);
    }

    private Sprite GetSprite(TreeType treeType, LogState logState)
    {
        Sprite normalSprite = null;

        for (int i = 0; i < logSpriteMappings.Count; i++)
        {
            if (logSpriteMappings[i].treeType != treeType)
                continue;

            if (logSpriteMappings[i].logState == logState && logSpriteMappings[i].sprite != null)
                return logSpriteMappings[i].sprite;

            if (logSpriteMappings[i].logState == LogState.Normal)
                normalSprite = logSpriteMappings[i].sprite;
        }

        // 희귀 상태의 매핑을 아직 연결하지 않은 경우에도 기존 일반 원목 이미지는 유지한다.
        if (normalSprite != null)
            return normalSprite;

        return logImage != null ? logImage.sprite : null;
    }
}
