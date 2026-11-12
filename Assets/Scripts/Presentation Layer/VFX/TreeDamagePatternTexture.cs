using UnityEngine;

/// <summary>
/// 깎인 나무의 금·숯불 무늬를 미리 구워 둔 작은 텍스처(4스테이지 전용, 렌더링 전용).
/// 나무 셰이더(TreeDamageLook.hlsl)가 픽셀마다 무늬를 계산하지 않고 이 텍스처를 한 번 읽는다.
/// 그림 파일 없이 처음 쓰일 때 한 번 만들어 셰이더 전역값(_TreeDamagePatternTex)으로 올려 두고, 그 뒤로는 그대로 쓴다.
/// 가로세로로 이어 붙어도 무늬가 끊기지 않게 칸 번호를 텍스처 크기에서 감아 만든다.
///
/// 채널 (TreeDamageLook.hlsl의 설명과 같아야 한다)
/// R: 0 = 금 아님, 그 밖 = 금의 심까지 거리 + 1 (심 = 1, 심 옆 = 2, 그 옆 = 3). 심은 어디서나 1픽셀 굵기로 이어진다.
/// G: 금 한 줄마다 고유한 값 - 뻗는 빠르기·빛이 흐르는 박자
/// B: 숯불 덩이 무늬(뭉글뭉글한 값)
/// A: 숯불 깜빡임 박자(두 픽셀 묶음마다 다르다)
/// </summary>
public static class TreeDamagePatternTexture
{
    // 한 변의 크기(픽셀). 셰이더의 TreeDamagePatternSize와 같아야 하고, 아래 세 칸 크기로 모두 나누어떨어져야 한다.
    public const int Size = 144;

    // 금 조각 한 칸의 크기(픽셀). 작을수록 금이 촘촘하다. 나무 그림(폭 30픽셀쯤)에 굵직한 금 몇 줄이 지나가는 크기다.
    private const int CrackCellSize = 12;
    // 조각 경계선 중 금으로 쓰지 않는 비율. 조금만 솎아야 금이 서로 이어진 줄기로 보인다.
    private const float CrackHiddenRatio = 0.15f;
    // 금의 심에서 이 거리(픽셀)까지 굽는다. 굵어진 금의 가장자리가 2다.
    private const int CrackMaxEdge = 2;
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
        int crackCells = Size / CrackCellSize;
        int blobCells = Size / EmberBlobSize;
        int flickerCells = Size / EmberFlickerSize;

        // 금 조각 중심(칸 안 좌표)
        int[] centerX = new int[crackCells * crackCells];
        int[] centerY = new int[crackCells * crackCells];
        for (int cy = 0; cy < crackCells; cy++)
        {
            for (int cx = 0; cx < crackCells; cx++)
            {
                int index = cy * crackCells + cx;
                centerX[index] = Mathf.FloorToInt((0.15f + 0.7f * Hash01(cx, cy, 1)) * CrackCellSize);
                centerY[index] = Mathf.FloorToInt((0.15f + 0.7f * Hash01(cx, cy, 2)) * CrackCellSize);
            }
        }

        // ── 금 1단계: 금의 심(조각 경계선 위의 1픽셀 줄)을 찾는다. ──
        // crackDistance: 금의 심까지 거리(심 = 0, 금이 아니면 -1), crackRandom: 그 금 한 줄의 고유한 값
        int[] crackDistance = new int[Size * Size];
        byte[] crackRandom = new byte[Size * Size];
        int[] queue = new int[Size * Size];
        int queueCount = 0;

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                // 가장 가까운 조각 중심 둘을 찾는다. 둘까지의 거리가 같은 곳이 조각 경계다.
                int cellX = x / CrackCellSize;
                int cellY = y / CrackCellSize;
                int nearest = int.MaxValue;
                int second = int.MaxValue;
                int nearestIndex = 0;
                int secondIndex = 0;

                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int idX = cellX + dx;
                        int idY = cellY + dy;
                        int wrappedX = (idX + crackCells) % crackCells;
                        int wrappedY = (idY + crackCells) % crackCells;
                        int index = wrappedY * crackCells + wrappedX;

                        // 가로세로 거리의 합으로 재서 경계가 계단 없이 대각선·직선으로 떨어지게 한다.
                        int distance = Mathf.Abs(x - (idX * CrackCellSize + centerX[index]))
                            + Mathf.Abs(y - (idY * CrackCellSize + centerY[index]));

                        if (distance < nearest)
                        {
                            second = nearest;
                            secondIndex = nearestIndex;
                            nearest = distance;
                            nearestIndex = index;
                        }
                        else if (distance < second)
                        {
                            second = distance;
                            secondIndex = index;
                        }
                    }
                }

                // 경계선마다 고유한 값(어느 쪽 조각에서 봐도 같다)
                int pairLow = Mathf.Min(nearestIndex, secondIndex);
                int pairHigh = Mathf.Max(nearestIndex, secondIndex);
                int edge = second - nearest;

                // 경계선이 픽셀 위를 지나면 그 픽셀이 심이고, 픽셀 사이를 지나면(양쪽 다 거리 차 1) 번호가 앞선 조각 쪽 픽셀만
                // 심으로 삼는다. 이렇게 해야 금이 어디서나 1픽셀 굵기로 끊기지 않고 이어진다.
                bool bCore = 0 == edge || (1 == edge && nearestIndex < secondIndex);
                int pixelIndex = y * Size + x;

                if (bCore && Hash01(pairLow, pairHigh, 3) >= CrackHiddenRatio)
                {
                    crackDistance[pixelIndex] = 0;
                    crackRandom[pixelIndex] = (byte)Mathf.RoundToInt(Hash01(pairLow, pairHigh, 4) * 255f);
                    queue[queueCount++] = pixelIndex;
                }
                else
                {
                    crackDistance[pixelIndex] = -1;
                }
            }
        }

        // ── 금 2단계: 심에서 한 픽셀씩 번져 가며 가장자리(심까지 거리)를 채운다. 가장자리는 가까운 심의 고유한 값을 물려받는다. ──
        for (int head = 0; head < queueCount; head++)
        {
            int pixelIndex = queue[head];
            int distance = crackDistance[pixelIndex];
            if (distance >= CrackMaxEdge)
            {
                continue;
            }

            int x = pixelIndex % Size;
            int y = pixelIndex / Size;
            for (int side = 0; side < 4; side++)
            {
                int nx = (x + (0 == side ? 1 : 1 == side ? Size - 1 : 0)) % Size;
                int ny = (y + (2 == side ? 1 : 3 == side ? Size - 1 : 0)) % Size;
                int neighborIndex = ny * Size + nx;
                if (crackDistance[neighborIndex] >= 0)
                {
                    continue;
                }

                crackDistance[neighborIndex] = distance + 1;
                crackRandom[neighborIndex] = crackRandom[pixelIndex];
                queue[queueCount++] = neighborIndex;
            }
        }

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
                    (byte)(crackDistance[pixelIndex] + 1),
                    crackRandom[pixelIndex],
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
