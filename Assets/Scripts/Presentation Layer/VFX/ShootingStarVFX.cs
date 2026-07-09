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
    private const float FrameRate = 24f; // 프레임 전환(애니메이션) 속도 - 낙하 시간과 별개로 계속 순환한다
    private const string SortingLayerName = "Objects";

    private static Sprite[] cachedFrames;

    private SpriteRenderer spriteRenderer;
    private Vector3 startPos;
    private Vector3 endPos;
    private float elapsed;
    private Action onLanded;

    public static void Spawn(Vector3 _landingPos, int _sortingOrder, Action _onLanded)
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
        instance.Begin(_landingPos, _sortingOrder, _onLanded);
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

    private void Begin(Vector3 _landingPos, int _sortingOrder, Action _onLanded)
    {
        onLanded = _onLanded;
        elapsed = 0f;

        // 좌우 랜덤 경사로 22.5도만큼 기울어진 방향에서 낙하 시작
        float sign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        float horizontalOffset = FallHeight * Mathf.Tan(FallAngleDegrees * Mathf.Deg2Rad) * sign;

        endPos = _landingPos;
        startPos = _landingPos + new Vector3(horizontalOffset, FallHeight, 0f);

        transform.position = startPos;

        // 스프라이트 기본 방향이 아래를 향하고 있어(위아래가 뒤집힌 채로 보였음) +90으로 보정한다.
        Vector3 dir = (endPos - startPos).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + 90f);

        spriteRenderer.sprite = cachedFrames[0];
        spriteRenderer.sortingLayerName = SortingLayerName;

        // 낙하 중 시각적 높이(하늘 위)는 연출일 뿐이므로 정렬 기준으로 쓰지 않는다.
        // 착지할 나무 기준으로 미리 계산된 값(topHighlight+1)을 그대로 고정해서 쓰면,
        // 착지 지점보다 화면 앞/뒤에 있는 다른 나무들과 자연스럽게 깊이가 맞물린다.
        spriteRenderer.sortingOrder = _sortingOrder;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / FallDuration);

        transform.position = Vector3.Lerp(startPos, endPos, t);

        int frameIdx = Mathf.FloorToInt(elapsed * FrameRate) % cachedFrames.Length;
        spriteRenderer.sprite = cachedFrames[frameIdx];

        if (t >= 1f)
        {
            onLanded?.Invoke();
            Destroy(gameObject);
        }
    }
}
