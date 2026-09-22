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

    [Serializable]
    private struct LootSpriteMapping
    {
        public LootType lootType;
        public Sprite sprite;
    }

    [Header("UI References")]
    [SerializeField] private Image logImage;
    [SerializeField] private CurrencyFontHUD countFont;

    [Header("Item Sprite Mapping")]
    [SerializeField] private List<LogSpriteMapping> logSpriteMappings = new List<LogSpriteMapping>();
    [SerializeField] private List<LootSpriteMapping> lootSpriteMappings = new List<LootSpriteMapping>();

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

    public void SetLootDataVisible(LootType lootType)
    {
        gameObject.SetActive(true);
        SetItemData(GetLootSprite(lootType), 0L, false);
    }

    private void SetDataInternal(TreeType treeType, LogState logState, int count)
    {
        SetItemData(GetSprite(treeType, logState), count);
    }

    private void SetItemData(Sprite sprite, long count, bool showCount = true)
    {
        if (logImage != null)
        {
            logImage.sprite = sprite;
            logImage.enabled = sprite != null;
        }

        if (countFont != null)
        {
            countFont.gameObject.SetActive(showCount);
            if (showCount)
                countFont.SetNumber(count);
        }
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

        return normalSprite;
    }

    private Sprite GetLootSprite(LootType lootType)
    {
        for (int i = 0; i < lootSpriteMappings.Count; i++)
        {
            if (lootSpriteMappings[i].lootType == lootType)
                return lootSpriteMappings[i].sprite;
        }

        return null;
    }
}
