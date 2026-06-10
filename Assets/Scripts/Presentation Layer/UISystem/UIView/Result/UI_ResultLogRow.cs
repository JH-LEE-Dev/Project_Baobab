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

    public void SetData(TreeType treeType, int count)
    {
        gameObject.SetActive(0 < count);
        SetDataInternal(treeType, count);
    }

    public void SetDataVisible(TreeType treeType, int count)
    {
        gameObject.SetActive(true);
        SetDataInternal(treeType, count);
    }

    private void SetDataInternal(TreeType treeType, int count)
    {
        if (logImage != null)
            logImage.sprite = GetSprite(treeType);

        if (countFont != null)
            countFont.SetNumber(count);
    }

    private Sprite GetSprite(TreeType treeType)
    {
        for (int i = 0; i < logSpriteMappings.Count; i++)
        {
            if (logSpriteMappings[i].treeType == treeType)
                return logSpriteMappings[i].sprite;
        }

        return logImage != null ? logImage.sprite : null;
    }
}
