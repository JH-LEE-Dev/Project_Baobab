using UnityEngine;

/// <summary>
/// 깎인 잿불 나무의 숯불 무늬를 미리 구워 둔 작은 텍스처(4스테이지 전용, 렌더링 전용).
/// 돌처럼 생긴 나무의 금은 나무 그림마다 따로 굽는다(TreeCrackMaskTexture).
/// 나무 셰이더(TreeDamageLook.hlsl)가 픽셀마다 무늬를 계산하지 않고 이 텍스처를 한 번 읽는다.
/// 그림 파일 없이 처음 쓰일 때 한 번 만들어 셰이더 전역값(_TreeDamagePatternTex)으로 올려 두고, 그 뒤로는 그대로 쓴다.
/// 가로세로로 이어 붙어도 무늬가 끊기지 않게 칸 번호를 텍스처 크기에서 감아 만든다.
///
/// 채널 (TreeDamageLook.hlsl의 설명과 같아야 한다)
/// R·G: 쓰지 않는다(0)
/// B: 숯불 덩이 무늬(뭉글뭉글한 값)
/// A: 숯불 깜빡임 박자(두 픽셀 묶음마다 다르다)
/// </summary>
public static class TreeDamagePatternTexture
{
    // 한 변의 크기(픽셀). 셰이더의 TreeDamagePatternSize와 같아야 하고, 아래 두 칸 크기로 모두 나누어떨어져야 한다.
    public const int Size = 144;

    // 숯불 덩이 하나의 크기(픽셀)
    private const int EmberBlobSize = 3;
    // 숯불 깜빡임 박자를 나누는 묶음 크기(픽셀)
    private const int EmberFlickerSize = 2;

    private static readonly int PatternTexID = Shader.PropertyToID("_TreeDamagePatternTex");

    private static Texture2D texture;

    /// <summary>
    /// 무늬 텍스처가 셰이더 전역값으로 올라가 있게 한다. 이미 있으면 아무것도 하지 않는다.
    /// 깎인 모습을 그리는 맵(본편 4스테이지, 로그라이크 용암 숲)에서 나무를 심을 때 부른다(TreeObj.SetDamageLookEnabled).
    /// </summary>
    public static void EnsureGlobal()
    {
        if (null != texture)
        {
            return;
        }

        texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
        texture.name = "TreeDamagePattern";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Repeat;
        // 씬이 바뀌어도 남고 저장되지 않는다(전역값이 가리키는 동안 사라지면 안 된다).
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixels32(Build());
        texture.Apply(false, true);

        Shader.SetGlobalTexture(PatternTexID, texture);
    }

    private static Color32[] Build()
    {
        int blobCells = Size / EmberBlobSize;
        int flickerCells = Size / EmberFlickerSize;

        Color32[] pixels = new Color32[Size * Size];

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                // ── 숯불 덩이: 격자점 값 사이를 부드럽게 이은 값 ──
                int blobX = x / EmberBlobSize;
                int blobY = y / EmberBlobSize;
                float fx = (x % EmberBlobSize) / (float)EmberBlobSize;
                float fy = (y % EmberBlobSize) / (float)EmberBlobSize;
                int blobX1 = (blobX + 1) % blobCells;
                int blobY1 = (blobY + 1) % blobCells;
                float blob = Mathf.Lerp(
                    Mathf.Lerp(Hash01(blobX, blobY, 5), Hash01(blobX1, blobY, 5), fx),
                    Mathf.Lerp(Hash01(blobX, blobY1, 5), Hash01(blobX1, blobY1, 5), fx),
                    fy);

                float flicker = Hash01((x / EmberFlickerSize) % flickerCells, (y / EmberFlickerSize) % flickerCells, 6);

                int pixelIndex = y * Size + x;
                pixels[pixelIndex] = new Color32(
                    0,
                    0,
                    (byte)Mathf.RoundToInt(blob * 255f),
                    (byte)Mathf.RoundToInt(flicker * 255f));
            }
        }

        return pixels;
    }

    // 정수 둘과 갈래 번호로 0~1 값을 만든다(같은 입력이면 늘 같은 값).
    private static float Hash01(int _x, int _y, int _channel)
    {
        unchecked
        {
            uint h = (uint)_x * 374761393u + (uint)_y * 668265263u + (uint)_channel * 2246822519u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / 16777216f;
        }
    }
}
