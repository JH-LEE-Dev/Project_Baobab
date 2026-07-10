using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resources/Whirlwind/Whirlwind 스프라이트 시트를 프레임 단위로 재생하는 1회성 VFX 오브젝트.
/// ShockWave 이펙트와 동일한 Sorting Layer/Order(TagManager "ShockWave" 레이어, 오더 0)를 사용한다.
/// </summary>
public class WhirlwindVFX : MonoBehaviour
{
    private const string ResourcePath = "Whirlwind/Whirlwind";
    private const float FrameRate = 24f;

    // ShockWave.prefab의 SpriteRenderer(m_SortingLayer: 11 = "ShockWave", m_SortingOrder: 0)와 동일하게 맞춘다.
    private const string SortingLayerName = "ShockWave";
    private const int SortingOrder = 0;

    // 판정 범위에 딱 맞춘 크기가 시각적으로 작아 보여서 추가로 곱해주는 배율
    private const float ExtraScaleMultiplier = 1.25f;

    private static Sprite[] cachedFrames;
    // 3타마다 반복 발동되므로 SporeExplosionVFX와 동일하게 풀링해서 재사용한다.
    private static readonly Stack<WhirlwindVFX> pool = new Stack<WhirlwindVFX>();

    private SpriteRenderer spriteRenderer;
    private float frameTimer;
    private int currentFrame;

    public static void Spawn(Vector3 _position, float _attackRadius)
    {
        EnsureFramesLoaded();
        if (cachedFrames == null || cachedFrames.Length == 0) return;

        WhirlwindVFX instance = pool.Count > 0 ? pool.Pop() : CreateInstance();

        instance.gameObject.SetActive(true);
        instance.transform.position = _position;
        instance.Play(_attackRadius);
    }

    private static WhirlwindVFX CreateInstance()
    {
        GameObject go = new GameObject("WhirlwindVFX");
        WhirlwindVFX instance = go.AddComponent<WhirlwindVFX>();
        instance.spriteRenderer = go.AddComponent<SpriteRenderer>();
        return instance;
    }

    private static void EnsureFramesLoaded()
    {
        if (cachedFrames != null) return;

        Sprite[] loaded = Resources.LoadAll<Sprite>(ResourcePath);
        Array.Sort(loaded, (a, b) => ExtractFrameIndex(a.name).CompareTo(ExtractFrameIndex(b.name)));
        cachedFrames = loaded;
    }

    private static int ExtractFrameIndex(string _spriteName)
    {
        int underscoreIdx = _spriteName.LastIndexOf('_');
        if (underscoreIdx >= 0 && int.TryParse(_spriteName.Substring(underscoreIdx + 1), out int idx))
        {
            return idx;
        }
        return 0;
    }

    private void Play(float _attackRadius)
    {
        currentFrame = 0;
        frameTimer = 0f;
        spriteRenderer.sprite = cachedFrames[0];
        spriteRenderer.sortingLayerName = SortingLayerName;
        spriteRenderer.sortingOrder = SortingOrder;

        // 공격 판정 타원(가로 지름 2*R, 세로 지름 R)과 스프라이트(128x64 = 가로:세로 2:1) 비율이 동일하므로,
        // 균일(x=y) 스케일만으로 스프라이트 가로폭을 판정 범위 가로 지름에 맞추면 세로도 함께 맞는다.
        float spriteWidthUnits = cachedFrames[0].bounds.size.x;
        float uniformScale = spriteWidthUnits > 0.0001f ? (_attackRadius * 2f) / spriteWidthUnits : 1f;
        uniformScale *= ExtraScaleMultiplier;
        transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
    }

    private void Update()
    {
        frameTimer += Time.deltaTime;
        float frameDuration = 1f / FrameRate;

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            currentFrame++;

            if (currentFrame >= cachedFrames.Length)
            {
                ReturnToPool();
                return;
            }

            spriteRenderer.sprite = cachedFrames[currentFrame];
        }
    }

    private void ReturnToPool()
    {
        gameObject.SetActive(false);
        pool.Push(this);
    }
}
