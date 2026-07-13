using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Whirlwind 스프라이트 시트를 프레임 단위로 재생하는 1회성 VFX 오브젝트.
/// 프레임은 Resources.LoadAll 대신 호출부(AttackComponent)에서 인스펙터로 연결한 배열을 전달받는다.
/// ShockWave 이펙트와 동일한 Sorting Layer/Order(TagManager "ShockWave" 레이어, 오더 0)를 사용한다.
/// </summary>
public class WhirlwindVFX : MonoBehaviour
{
    private const float FrameRate = 24f;

    // ShockWave.prefab의 SpriteRenderer(m_SortingLayer: 11 = "ShockWave", m_SortingOrder: 0)와 동일하게 맞춘다.
    private const string SortingLayerName = "ShockWave";
    private const int SortingOrder = 0;

    // 판정 범위에 딱 맞춘 크기가 시각적으로 작아 보여서 추가로 곱해주는 배율
    private const float ExtraScaleMultiplier = 1.25f;

    // 3타마다 반복 발동되므로 SporeExplosionVFX와 동일하게 풀링해서 재사용한다.
    private static readonly Stack<WhirlwindVFX> pool = new Stack<WhirlwindVFX>();

    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private float frameTimer;
    private int currentFrame;

    public static void Spawn(Vector3 _position, float _attackRadius, Sprite[] _frames)
    {
        if (_frames == null || _frames.Length == 0) return;

        WhirlwindVFX instance = pool.Count > 0 ? pool.Pop() : CreateInstance();

        instance.gameObject.SetActive(true);
        instance.transform.position = _position;
        instance.Play(_attackRadius, _frames);
    }

    private static WhirlwindVFX CreateInstance()
    {
        GameObject go = new GameObject("WhirlwindVFX");
        WhirlwindVFX instance = go.AddComponent<WhirlwindVFX>();
        instance.spriteRenderer = go.AddComponent<SpriteRenderer>();
        return instance;
    }

    private void Play(float _attackRadius, Sprite[] _frames)
    {
        frames = _frames;
        currentFrame = 0;
        frameTimer = 0f;
        spriteRenderer.sprite = frames[0];
        spriteRenderer.sortingLayerName = SortingLayerName;
        spriteRenderer.sortingOrder = SortingOrder;

        // 공격 판정 타원(가로 지름 2*R, 세로 지름 R)과 스프라이트(128x64 = 가로:세로 2:1) 비율이 동일하므로,
        // 균일(x=y) 스케일만으로 스프라이트 가로폭을 판정 범위 가로 지름에 맞추면 세로도 함께 맞는다.
        float spriteWidthUnits = frames[0].bounds.size.x;
        float uniformScale = spriteWidthUnits > 0.0001f ? (_attackRadius * 2f) / spriteWidthUnits : 1f;
        uniformScale *= ExtraScaleMultiplier;
        transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / FrameRate;

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            currentFrame++;

            if (currentFrame >= frames.Length)
            {
                ReturnToPool();
                return;
            }

            spriteRenderer.sprite = frames[currentFrame];
        }
    }

    private void ReturnToPool()
    {
        gameObject.SetActive(false);
        pool.Push(this);
    }
}
