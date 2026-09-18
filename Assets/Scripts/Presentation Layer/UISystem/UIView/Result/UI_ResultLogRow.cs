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

    [Serializable]
    private struct GemOreSpriteMapping
    {
        public GemOreType gemOreType;
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

    [Header("Log Sprite Mapping")]
    [SerializeField] private List<LogSpriteMapping> logSpriteMappings = new List<LogSpriteMapping>();
    [SerializeField] private List<GemOreSpriteMapping> gemOreSpriteMappings = new List<GemOreSpriteMapping>();
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

    public void SetGemOreDataVisible(GemOreType gemOreType, long amount)
    {
        gameObject.SetActive(true);
        SetItemData(GetGemOreSprite(gemOreType), amount);
    }

    public void SetLootDataVisible(LootType lootType)
    {
        gameObject.SetActive(true);
        SetItemData(GetLootSprite(lootType), 0L, false);
    }

    private void SetDataInternal(TreeType treeType, int count)
    {
        SetItemData(GetSprite(treeType), count);
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

    private Sprite GetSprite(TreeType treeType)
    {
        for (int i = 0; i < logSpriteMappings.Count; i++)
        {
            if (logSpriteMappings[i].treeType == treeType && logSpriteMappings[i].sprite != null)
                return logSpriteMappings[i].sprite;
        }

        return null;
    }

    private Sprite GetGemOreSprite(GemOreType gemOreType)
    {
        for (int i = 0; i < gemOreSpriteMappings.Count; i++)
        {
            if (gemOreSpriteMappings[i].gemOreType == gemOreType)
                return gemOreSpriteMappings[i].sprite;
        }

        return null;
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
