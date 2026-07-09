using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resources/Spore/SporeExplosion 스프라이트 시트를 프레임 단위로 재생하는 1회성 VFX 오브젝트.
/// 별도 프리팹 없이 코드에서 직접 GameObject를 생성해 재생하고, 끝나면 풀로 반환된다.
/// </summary>
public class SporeExplosionVFX : MonoBehaviour
{
    private const string ResourcePath = "Spore/SporeExplosion";
    private const float FrameRate = 24f;
    private const int SortingPrecision = 100;

    // 새로 생성되는 GameObject는 기본적으로 "Default" 정렬 레이어를 쓰는데, 이 프로젝트는
    // Default를 정렬 레이어 목록 맨 뒤(가장 안쪽)에만 두고 있어 다른 오브젝트에 전부 가려진다.
    // 나무 본체와 동일한 레이어를 써야 화면에 정상적으로 보인다.
    private const string SortingLayerName = "Objects";

    private static Sprite[] cachedFrames;
    // 폭발마다 4~5개씩 생성/파괴가 반복되면 GC 부담이 커지므로 풀링해서 재사용한다.
    private static readonly Stack<SporeExplosionVFX> pool = new Stack<SporeExplosionVFX>();

    private SpriteRenderer spriteRenderer;
    private float frameTimer;
    private int currentFrame;

    public static void Spawn(Vector3 _position, int _sortingOrderOffset = 100)
    {
        EnsureFramesLoaded();
        if (cachedFrames == null || cachedFrames.Length == 0) return;

        SporeExplosionVFX instance = pool.Count > 0 ? pool.Pop() : CreateInstance();

        instance.gameObject.SetActive(true);
        instance.transform.position = _position;
        instance.Play(_sortingOrderOffset);
    }

    private static SporeExplosionVFX CreateInstance()
    {
        GameObject go = new GameObject("SporeExplosionVFX");
        SporeExplosionVFX instance = go.AddComponent<SporeExplosionVFX>();
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

    private void Play(int _sortingOrderOffset)
    {
        currentFrame = 0;
        frameTimer = 0f;
        spriteRenderer.sprite = cachedFrames[0];
        spriteRenderer.sortingLayerName = SortingLayerName;

        // CustomSortable과 동일한 공식으로 1회성 정렬 순서를 계산한다 (매 프레임 갱신할 필요는 없음).
        spriteRenderer.sortingOrder = -Mathf.RoundToInt(transform.position.y * SortingPrecision) + _sortingOrderOffset;
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
