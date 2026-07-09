using System;
using UnityEngine;

/// <summary>
/// Resources/ShootingStar/ShootingStar 스프라이트 시트를 재생하며, 하늘 높은 곳에서
/// 22.5도 경사(좌우 랜덤)로 목표 지점까지 낙하하는 1회성 VFX. 착지 시 콜백을 호출한다.
/// </summary>
public class ShootingStarVFX : MonoBehaviour
{
    private const string ResourcePath = "ShootingStar/ShootingStar";
    private const float FallDuration = 0.6f;
    private const float FallAngleDegrees = 22.5f;
    private const float FallHeight = 6f;
    private const string SortingLayerName = "Objects";
    private const int SortingOrder = 10000; // 하늘에서 떨어지는 연출이라 항상 다른 오브젝트보다 앞에 그려지도록 고정

    private static Sprite[] cachedFrames;

    private SpriteRenderer spriteRenderer;
    private Vector3 startPos;
    private Vector3 endPos;
    private float elapsed;
    private Action onLanded;

    public static void Spawn(Vector3 _landingPos, Action _onLanded)
    {
        EnsureFramesLoaded();
        if (cachedFrames == null || cachedFrames.Length == 0)
        {
            _onLanded?.Invoke();
            return;
        }

        GameObject go = new GameObject("ShootingStarVFX");
        ShootingStarVFX instance = go.AddComponent<ShootingStarVFX>();
        instance.spriteRenderer = go.AddComponent<SpriteRenderer>();
        instance.Begin(_landingPos, _onLanded);
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

    private void Begin(Vector3 _landingPos, Action _onLanded)
    {
        onLanded = _onLanded;
        elapsed = 0f;

        // 좌우 랜덤 경사로 22.5도만큼 기울어진 방향에서 낙하 시작
        float sign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        float horizontalOffset = FallHeight * Mathf.Tan(FallAngleDegrees * Mathf.Deg2Rad) * sign;

        endPos = _landingPos;
        startPos = _landingPos + new Vector3(horizontalOffset, FallHeight, 0f);

        transform.position = startPos;

        Vector3 dir = (endPos - startPos).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

        spriteRenderer.sprite = cachedFrames[0];
        spriteRenderer.sortingLayerName = SortingLayerName;
        spriteRenderer.sortingOrder = SortingOrder;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / FallDuration);

        transform.position = Vector3.Lerp(startPos, endPos, t);

        int frameIdx = Mathf.Min(cachedFrames.Length - 1, Mathf.FloorToInt(t * cachedFrames.Length));
        spriteRenderer.sprite = cachedFrames[frameIdx];

        if (t >= 1f)
        {
            onLanded?.Invoke();
            Destroy(gameObject);
        }
    }
}
