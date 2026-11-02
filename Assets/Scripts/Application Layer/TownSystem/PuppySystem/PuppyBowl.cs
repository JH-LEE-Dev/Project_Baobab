using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 강아지 밥그릇. [비어있음(0), 적당히(1), 가득(2)] 세 단계가 있고, 강아지가 한 번 먹을 때마다 한 단계 내려간다.
/// 가득 차 있지 않을 때 플레이어가 상호작용하면 가득 찬다(흰색 + 뽀잉). 상호작용 가능 여부와 아웃라인은
/// PuppyManager가 강아지와 함께 경합해 정해준다.
/// </summary>
public class PuppyBowl : MonoBehaviour
{
    public const int EmptyLevel = 0;
    public const int FullLevel = 2;

    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SortingGroup sortingGroup;
    [SerializeField] private GameObject outlineObject;
    [SerializeField] private PuppyPopEffect popEffect;

    [Tooltip("단계별 그림. 인덱스가 곧 단계다(0 = 비어있음, 1 = 적당히, 2 = 가득).")]
    [SerializeField] private Sprite[] levelSprites = new Sprite[FullLevel + 1];

    // 아웃라인(스텐실 쓰기 + 테두리) 렌더러. 단계가 바뀔 때 본체와 같은 그림으로 맞춘다.
    private SpriteRenderer[] outlineRenderers;

    public int Level { get; private set; }
    public bool HasFood => EmptyLevel < Level;

    public void Initialize(int _level)
    {
        outlineRenderers = outlineObject.GetComponentsInChildren<SpriteRenderer>(true);
        popEffect.Initialize(new[] { bodyRenderer });
        SetOutline(false);
        SetLevel(_level);
    }

    /// <summary>제자리에 세운 뒤 한 번 호출한다. 밥그릇은 움직이지 않으므로 정렬 순서를 매 프레임 갱신하지 않는다.</summary>
    public void RefreshSortingOrder()
    {
        // 강아지가 밥그릇 동/서쪽에서 먹을 때는 밥그릇과 높이(y)가 같아 정렬 순서가 똑같아진다. 그러면 앞뒤가
        // 정해지지 않아 깜빡일 수 있으므로, 같은 높이에서는 밥그릇이 항상 앞에 오도록 1을 더한다.
        sortingGroup.sortingOrder = -Mathf.RoundToInt(transform.position.y * 100f) + 1;
    }

    public void Fill()
    {
        SetLevel(FullLevel);
        popEffect.Play();
        Sound.Play(SoundID.PuppyBowl, transform.position);
    }

    public void Consume()
    {
        SetLevel(Level - 1);
    }

    public void SetOutline(bool _bActive)
    {
        if (outlineObject.activeSelf != _bActive)
            outlineObject.SetActive(_bActive);
    }

    private void SetLevel(int _level)
    {
        Level = Mathf.Clamp(_level, EmptyLevel, FullLevel);

        Sprite _sprite = levelSprites[Level];
        bodyRenderer.sprite = _sprite;

        // 아웃라인 렌더러들도 같은 그림을 그려야 테두리가 맞는다.
        for (int i = 0; i < outlineRenderers.Length; i++)
        {
            outlineRenderers[i].sprite = _sprite;
        }
    }
}
