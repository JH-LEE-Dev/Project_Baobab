using UnityEngine;

/// <summary>
/// 부메랑 잔상 1장. 찍히는 순간의 Boomerang_Base 프레임을 그 자리에 그대로 남겨두고 알파만 서서히
/// 줄이다가 꺼진다. 부메랑 본체가 풀로 돌아가(비활성화) 멈춰도 잔상은 끝까지 자연스럽게 사라져야
/// 하므로, 본체의 자식이 아니라 독립된 루트 오브젝트로 두고 스스로 Update에서 페이드한다.
/// 매번 새로 만들지 않고 Boomerang이 Awake에서 몇 장 만들어 링 버퍼로 돌려 쓴다(무할당).
/// </summary>
public class BoomerangAfterimage : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Color baseColor;
    private float startAlpha;
    private float fadeDuration;
    private float timer;
    private bool isPaused;

    /// <summary>
    /// _source(부메랑 본체 렌더러)와 같은 머티리얼/소팅 레이어를 쓰는 비활성 잔상 오브젝트를 만든다.
    /// </summary>
    public static BoomerangAfterimage Create(SpriteRenderer _source, string _name)
    {
        GameObject go = new GameObject(_name);
        go.SetActive(false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        if (_source != null)
        {
            sr.sharedMaterial = _source.sharedMaterial;
            sr.sortingLayerID = _source.sortingLayerID;
        }

        BoomerangAfterimage afterimage = go.AddComponent<BoomerangAfterimage>();
        afterimage.spriteRenderer = sr;
        return afterimage;
    }

    public void Show(Sprite _sprite, Color _color, Vector3 _position, Vector3 _scale, int _sortingOrder, float _startAlpha, float _fadeDuration)
    {
        transform.position = _position;
        transform.localScale = _scale;

        spriteRenderer.sprite = _sprite;
        spriteRenderer.sortingOrder = _sortingOrder;

        baseColor = _color;
        startAlpha = _startAlpha;
        fadeDuration = Mathf.Max(_fadeDuration, 0.01f);
        timer = 0f;
        isPaused = false;
        ApplyAlpha(1f);

        gameObject.SetActive(true);
    }

    public void SetPaused(bool _paused)
    {
        isPaused = _paused;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (isPaused) return;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / fadeDuration);
        ApplyAlpha(1f - t);

        if (t >= 1f)
        {
            Hide();
        }
    }

    private void ApplyAlpha(float _ratio)
    {
        Color c = baseColor;
        c.a = baseColor.a * startAlpha * _ratio;
        spriteRenderer.color = c;
    }
}
