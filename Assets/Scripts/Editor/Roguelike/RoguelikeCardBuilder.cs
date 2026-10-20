#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 카드 UI 프리팹 구성기. 여러 번 실행해도 같은 결과로 덮어쓴다.
///
/// 계층(모든 자식은 앵커·피벗 왼쪽 아래, 위치는 프레임 왼쪽 아래 기준 정수 픽셀, 크기는 스프라이트 원본 픽셀):
///   UI_RoguelikeCard (50x81, 프레임 크기)
///    ├ HitBox   50x87  투명, 마우스 판정. 호버로 올라간 그림 범위(+6px)까지 덮고 움직이지 않는다
///    └ Visual   50x81  호버 때 위로 올라가는 부분. 아래 그림은 모두 raycastTarget 끔
///       └ FlipRoot 50x81  뒤집힘 연출 때 폭이 줄었다 느는 부분(피벗 가운데 아래)
///          ├ Frame   CardFrame_0   (0, 0)
///          ├ Profile CardProfile_0 (8, 38)  - 같은 64x96 캔버스에서 잘린 스프라이트라 캔버스 좌표 (15,47)-(7,9)
///          ├ Icon    스킬 아이콘 (스크립트가 프로필 칸 중앙에 배치)
///          └ Level   I~VI, MAX (스크립트가 하단 중앙에 배치)
///
/// 프레임과 프로필은 같은 캔버스(64x96)에서 잘려 나온 스프라이트라 서로의 위치가 잘린 사각형 좌표 차이로 정해진다.
/// </summary>
public static class RoguelikeCardBuilder
{
    private const string CardDir = "Assets/Graphics/Roguelike/card/";
    public const string PrefabPath = "Assets/Prefabs/Roguelike/UI/UI_RoguelikeCard.prefab";

    public const int FrameWidth = 50;
    public const int FrameHeight = 81;
    private const int HoverLift = UI_RoguelikeCard.HoverLiftPixels;
    private static readonly Vector2Int ProfilePosition = new Vector2Int(8, 38);

    [MenuItem("LumberBoy/UI/Build Roguelike Card")]
    public static void Build()
    {
        Sprite frame = LoadSprite(CardDir + "CardFrame.png");
        Sprite profile = LoadSprite(CardDir + "CardProfile.png");
        Sprite[] levels =
        {
            LoadSprite(CardDir + "I.png"), LoadSprite(CardDir + "II.png"), LoadSprite(CardDir + "III.png"),
            LoadSprite(CardDir + "IV.png"), LoadSprite(CardDir + "V.png"), LoadOptionalSprite(CardDir + "VI.png"),
        };
        Sprite maxLevel = LoadOptionalSprite(CardDir + "MAX.png");

        // I~V와 프레임·프로필은 필수. VI와 MAX는 리소스가 아직 없을 수 있어(null이면 슬롯만 비워 둔다) 필수에서 뺀다.
        if (null == frame || null == profile || System.Array.IndexOf(levels, null, 0, 5) >= 0)
        {
            Debug.LogError("[RoguelikeCardBuilder] 스프라이트를 찾지 못했습니다(CardFrame, CardProfile, I~V).");
            return;
        }

        GameObject root = new GameObject("UI_RoguelikeCard", typeof(RectTransform));
        root.layer = LayerMask.NameToLayer("UI");
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.sizeDelta = new Vector2(FrameWidth, FrameHeight);

        Image hitBox = CreateImage("HitBox", rootRect, null, Vector2Int.zero, new Vector2(FrameWidth, FrameHeight + HoverLift));
        hitBox.color = new Color(1f, 1f, 1f, 0f);
        hitBox.raycastTarget = true;

        RectTransform visual = CreateRect("Visual", rootRect, Vector2Int.zero, new Vector2(FrameWidth, FrameHeight));

        // 피벗이 가운데 아래라 폭 스케일이 가운데를 기준으로 줄어든다. 앵커는 왼쪽 아래, 위치는 폭의 절반.
        RectTransform flipRoot = CreateRect("FlipRoot", visual, new Vector2Int(FrameWidth / 2, 0), new Vector2(FrameWidth, FrameHeight));
        flipRoot.pivot = new Vector2(0.5f, 0f);

        Image frameImage = CreateImage("Frame", flipRoot, frame, Vector2Int.zero, frame.rect.size);
        Image profileImage = CreateImage("Profile", flipRoot, profile, ProfilePosition, profile.rect.size);
        Image iconImage = CreateImage("Icon", flipRoot, null, Vector2Int.zero, Vector2.zero);
        iconImage.enabled = false;
        Image levelImage = CreateImage("Level", flipRoot, levels[0], Vector2Int.zero, levels[0].rect.size);

        UI_RoguelikeCard card = root.AddComponent<UI_RoguelikeCard>();
        SerializedObject so = new SerializedObject(card);
        so.FindProperty("visual").objectReferenceValue = visual;
        so.FindProperty("flipRoot").objectReferenceValue = flipRoot;
        so.FindProperty("frameImage").objectReferenceValue = frameImage;
        so.FindProperty("profileImage").objectReferenceValue = profileImage;
        so.FindProperty("iconImage").objectReferenceValue = iconImage;
        so.FindProperty("levelImage").objectReferenceValue = levelImage;

        SerializedProperty levelSprites = so.FindProperty("levelSprites");
        levelSprites.arraySize = levels.Length;
        for (int i = 0; i < levels.Length; i++) levelSprites.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];
        so.FindProperty("maxLevelSprite").objectReferenceValue = maxLevel;

        so.ApplyModifiedPropertiesWithoutUndo();

        // 미리보기용: 레벨 I 위치를 스크립트와 같은 규칙으로 맞춰 둔다.
        card.SetCard(null, ECardKind.Active, ECardDisplayGrade.Bronze, 1, 7, false);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[RoguelikeCardBuilder] 완료: " + PrefabPath);
    }

    /// <summary>없을 수 있는 리소스. 파일이 없으면 null.</summary>
    private static Sprite LoadOptionalSprite(string _path)
    {
        return System.IO.File.Exists(_path) ? LoadSprite(_path) : null;
    }

    /// <summary>경로의 첫 번째 스프라이트(Multiple이면 잘린 첫 조각, Single이면 본체).</summary>
    public static Sprite LoadSprite(string _path)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(_path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite) return sprite;
        }

        return null;
    }

    /// <summary>이름이 _name인 스프라이트(Multiple 시트의 조각).</summary>
    public static Sprite LoadSprite(string _path, string _name)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(_path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite && sprite.name == _name) return sprite;
        }

        return null;
    }

    /// <summary>앵커·피벗이 왼쪽 아래(0, 0)인 자식 RectTransform. 위치는 부모 왼쪽 아래 기준 정수 픽셀.</summary>
    public static RectTransform CreateRect(string _name, RectTransform _parent, Vector2Int _position, Vector2 _size)
    {
        GameObject go = new GameObject(_name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");

        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(_parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.sizeDelta = _size;
        rect.anchoredPosition = _position;
        return rect;
    }

    public static Image CreateImage(string _name, RectTransform _parent, Sprite _sprite, Vector2Int _position, Vector2 _size)
    {
        RectTransform rect = CreateRect(_name, _parent, _position, _size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = _sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = false;
        return image;
    }
}
#endif
