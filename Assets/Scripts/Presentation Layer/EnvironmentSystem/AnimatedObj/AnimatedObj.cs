using UnityEngine;
using System.Collections.Generic;

public class AnimatedObj : MonoBehaviour
{
    // // 외부 의존성
    [SerializeField] private float hdrIntensity = 1f; 
    [SerializeField] private List<Sprite> sprites;
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private float frameRate = 10f;

    // // 내부 의존성 및 상태 필드
    private static readonly int HDRIntensityID = Shader.PropertyToID("_HDRIntensity");
    private CustomSortable customSortable;
    private int currentFrameIndex;
    private float timer;
    private float frameDuration;

    // // 퍼블릭 초기화 및 제어 메서드

    public void Initialize()
    {
        customSortable = GetComponent<CustomSortable>();
        if (customSortable != null)
        {
            customSortable.Initialize(transform);
            customSortable.AddSpriteRenderer(sr);
        }

        var mpb = new MaterialPropertyBlock();
        sr.GetPropertyBlock(mpb);
        mpb.SetFloat(HDRIntensityID, hdrIntensity);
        sr.SetPropertyBlock(mpb);

        frameDuration = frameRate > 0f ? 1f / frameRate : 0.1f;
        ResetAnimationToRandomFrame();
    }

    public void SetSortingOrder()
    {
        if (customSortable != null)
            customSortable.ManualLateUpdate();
    }

    public void ResetAnimationToRandomFrame()
    {
        if (sprites == null || sprites.Count == 0) return;

        currentFrameIndex = Random.Range(0, sprites.Count);
        if (sr != null)
        {
            sr.sprite = sprites[currentFrameIndex];
        }
        timer = Random.Range(0f, frameDuration);
    }

    // // 유니티 이벤트 함수

    private void Update()
    {
        if (sprites == null || sprites.Count <= 1) return;

        timer += Time.deltaTime;
        if (timer >= frameDuration)
        {
            timer -= frameDuration;
            currentFrameIndex = (currentFrameIndex + 1) % sprites.Count;
            if (sr != null)
            {
                sr.sprite = sprites[currentFrameIndex];
            }
        }
    }
}
