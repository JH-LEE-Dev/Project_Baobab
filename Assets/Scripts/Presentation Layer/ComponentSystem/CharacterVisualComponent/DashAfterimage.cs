using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 대시 잔상 1장. 찍는 순간의 캐릭터 몸통·팔(도끼)·얼굴 스프라이트를 그 자리, 그 모양 그대로 고스트 색 실루엣으로 남기고
/// 알파만 서서히 줄이다가 꺼진다. 캐릭터가 계속 움직여도 제자리에 남아야 하므로 캐릭터의 자식이 아니라 독립된 루트 오브젝트로 두고
/// 스스로 Update에서 페이드한다(BoomerangAfterimage와 같은 방식).
/// 캐릭터는 SortingGroup(Base Visuals) 안에서 몸통·팔·얼굴의 앞뒤를 정하므로, 잔상도 루트에 SortingGroup을 두고 그 안의 순서를 그대로 옮긴다.
/// CharacterVisualComponent가 처음 대시할 때 몇 장 만들어 링 버퍼로 돌려 쓴다(재생 중 무할당).
/// </summary>
public class DashAfterimage : MonoBehaviour
{
    private SpriteRenderer[] parts;
    private SortingGroup sortingGroup;
    private int activePartCount;
    private Color ghostColor;
    private float startAlpha;
    private float fadeDuration;
    private float timer;

    /// <summary>
    /// 스프라이트 _partCount장을 담을 수 있는 비활성 잔상 오브젝트를 만든다. 모든 조각이 _material(고스트 실루엣 셰이더)로 그려진다.
    /// </summary>
    public static DashAfterimage Create(int _partCount, Material _material)
    {
        GameObject go = new GameObject("DashAfterimage");
        go.SetActive(false);

        DashAfterimage afterimage = go.AddComponent<DashAfterimage>();
        afterimage.sortingGroup = go.AddComponent<SortingGroup>();
        afterimage.parts = new SpriteRenderer[_partCount];

        for (int i = 0; i < _partCount; i++)
        {
            GameObject partObject = new GameObject("Part");
            partObject.transform.SetParent(go.transform, false);

            SpriteRenderer sr = partObject.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = _material;
            sr.enabled = false;
            afterimage.parts[i] = sr;
        }

        return afterimage;
    }

    /// <summary>
    /// _sources 중 지금 보이는 스프라이트를 그대로 옮겨 찍고 페이드를 시작한다. _offset만큼 옮긴 자리에 남긴다(대시 시작 자리에 찍을 때).
    /// _sortingLayerID/_sortingOrder는 잔상 전체(SortingGroup)의 정렬값이고, 조각끼리의 앞뒤는 원본 렌더러의 값을 그대로 쓴다.
    /// </summary>
    public void Show(SpriteRenderer[] _sources, Vector3 _offset, int _sortingLayerID, int _sortingOrder, Color _ghostColor, float _startAlpha, float _fadeDuration)
    {
        sortingGroup.sortingLayerID = _sortingLayerID;
        sortingGroup.sortingOrder = _sortingOrder;

        activePartCount = 0;
        for (int i = 0; null != _sources && i < _sources.Length && activePartCount < parts.Length; i++)
        {
            SpriteRenderer source = _sources[i];
            if (false == IsVisible(source)) continue;

            CopyPart(source, parts[activePartCount], _offset);
            activePartCount++;
        }

        for (int i = activePartCount; i < parts.Length; i++)
        {
            if (true == parts[i].enabled) parts[i].enabled = false;
        }

        if (0 == activePartCount)
        {
            Hide();
            return;
        }

        ghostColor = _ghostColor;
        startAlpha = _startAlpha;
        fadeDuration = Mathf.Max(_fadeDuration, 0.01f);
        timer = 0f;
        ApplyAlpha(1f);

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private static bool IsVisible(SpriteRenderer _source)
    {
        return null != _source && true == _source.enabled && true == _source.gameObject.activeInHierarchy
            && null != _source.sprite && 0.01f < _source.color.a;
    }

    // 원본의 월드 위치·회전·크기(좌우 반전 포함)를 옮긴다. 팔은 회전한 채로 부모(Arm)의 좌우 반전을 받으므로
    // lossyScale/rotation 대신 월드 행렬의 두 축에서 2D 회전·크기를 직접 꺼낸다(반전은 x 크기의 부호로 둔다).
    private static void CopyPart(SpriteRenderer _source, SpriteRenderer _part, Vector3 _offset)
    {
        Transform sourceTransform = _source.transform;
        Matrix4x4 m = sourceTransform.localToWorldMatrix;
        Vector2 axisX = new Vector2(m.m00, m.m10);
        Vector2 axisY = new Vector2(m.m01, m.m11);

        float scaleX = axisX.magnitude;
        float scaleY = axisY.magnitude;
        if (axisX.x * axisY.y - axisX.y * axisY.x < 0f) scaleX = -scaleX;

        float angle = Mathf.Atan2(-axisY.x, axisY.y) * Mathf.Rad2Deg;

        Transform partTransform = _part.transform;
        partTransform.SetPositionAndRotation(sourceTransform.position + _offset, Quaternion.Euler(0f, 0f, angle));
        partTransform.localScale = new Vector3(scaleX, scaleY, 1f);

        _part.sprite = _source.sprite;
        _part.flipX = _source.flipX;
        _part.flipY = _source.flipY;
        _part.sortingLayerID = _source.sortingLayerID;
        _part.sortingOrder = _source.sortingOrder;
        if (false == _part.enabled) _part.enabled = true;
    }

    private void Update()
    {
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
        Color c = ghostColor;
        c.a = ghostColor.a * startAlpha * _ratio;

        for (int i = 0; i < activePartCount; i++)
        {
            parts[i].color = c;
        }
    }
}
