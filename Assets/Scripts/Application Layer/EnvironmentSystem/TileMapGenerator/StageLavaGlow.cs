using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 용암 빛 번짐. 맵을 만들 때 칸마다 "용암에서 받는 빛"(0~1)을 담은 작은 텍스처를 한 장 만들어 전역으로 넘긴다.
/// 바닥 셰이더(GroundLavaGlowLit)가 월드 위치를 칸 좌표로 바꿔 이 텍스처를 읽고, 용암 둘레 바닥을 주황으로 물들인다.
/// 용암 칸은 1, 용암 가장자리 칸에서 radius(월드) 안쪽 바닥 칸은 거리만큼 줄어든 값, 나머지는 0이다.
/// 가장자리 용암 칸에서만 둘레로 퍼뜨리므로 맵을 만들 때 한 번 몇 밀리초면 끝나고, 매 프레임 하는 일은 없다.
/// 렌더링만 바꾸고 게임 동작에는 영향이 없다. 꺼진 스테이지·로그라이크는 전역값을 0으로 비워 아무것도 그리지 않는다.
/// </summary>
public static class StageLavaGlow
{
    private static readonly int TextureID = Shader.PropertyToID("_LavaGlowTex");
    // 켜짐(0/1), 더하는 세기, 곱하는 세기, 진하기 단계 수
    private static readonly int AID = Shader.PropertyToID("_LavaGlowA");
    // 감쇠 곡선, 일렁임 정도, 일렁임 속도, 일렁임 무늬 크기
    private static readonly int BID = Shader.PropertyToID("_LavaGlowB");
    private static readonly int ColorID = Shader.PropertyToID("_LavaGlowColor");
    // 월드 → 칸 좌표 변환(2x2 역행렬: m00, m01, m10, m11)
    private static readonly int MapAID = Shader.PropertyToID("_LavaGlowMapA");
    // xy = 칸 (0,0) 가운데의 월드 위치, zw = 텍스처 왼쪽 아래 칸 번호
    private static readonly int MapBID = Shader.PropertyToID("_LavaGlowMapB");
    // xy = 1 / 텍스처 크기
    private static readonly int MapCID = Shader.PropertyToID("_LavaGlowMapC");
    // 서 있는 그림(나무·캐릭터): x = 빛 배율(0이면 끔), y = 발밑에서 빛이 사라지는 높이(월드)
    private static readonly int ObjectID = Shader.PropertyToID("_LavaGlowObject");

    private static Texture2D glowTexture;
    private static byte[] glowBytes;
    private static float[] glowValues;

    // // 퍼블릭 제어 메서드
    /// <summary>
    /// _lavaTiles는 _bounds 범위의 타일 배열(x 먼저, 인덱스 = x + y * 너비)이고, 비어 있지 않은 칸을 용암으로 본다.
    /// </summary>
    public static void Apply(LavaGlowSettings _settings, TileBase[] _lavaTiles, BoundsInt _bounds, Tilemap _grid)
    {
        int width = _bounds.size.x;
        int height = _bounds.size.y;
        if (null == _settings || false == _settings.enabled || null == _settings.groundMaterial || null == _lavaTiles || null == _grid
            || GameModeContext.IsRoguelike || width <= 0 || height <= 0 || _lavaTiles.Length < width * height)
        {
            Clear();
            return;
        }

        Vector3 origin = _grid.GetCellCenterWorld(Vector3Int.zero);
        Vector2 axisX = _grid.GetCellCenterWorld(new Vector3Int(1, 0, 0)) - origin;
        Vector2 axisY = _grid.GetCellCenterWorld(new Vector3Int(0, 1, 0)) - origin;
        float determinant = axisX.x * axisY.y - axisY.x * axisX.y;
        if (Mathf.Abs(determinant) < 0.000001f)
        {
            Clear();
            return;
        }

        BuildValues(_settings, _lavaTiles, width, height, axisX, axisY);
        UploadTexture(width, height);

        Shader.SetGlobalTexture(TextureID, glowTexture);
        Shader.SetGlobalVector(MapAID, new Vector4(axisY.y / determinant, -axisY.x / determinant, -axisX.y / determinant, axisX.x / determinant));
        Shader.SetGlobalVector(MapBID, new Vector4(origin.x, origin.y, _bounds.xMin, _bounds.yMin));
        Shader.SetGlobalVector(MapCID, new Vector4(1f / width, 1f / height, 0f, 0f));
        // 바닥 색은 셰이더가 선형으로 받으므로 인스펙터(감마) 색을 선형으로 바꿔 넘긴다.
        Color color = QualitySettings.activeColorSpace == ColorSpace.Linear ? _settings.color.linear : _settings.color;
        Shader.SetGlobalVector(ColorID, new Vector4(color.r, color.g, color.b, 1f));
        Shader.SetGlobalVector(AID, new Vector4(1f, Mathf.Max(0f, _settings.intensity), Mathf.Max(0f, _settings.warmMultiply), Mathf.Max(1f, _settings.steps)));
        Shader.SetGlobalVector(BID, new Vector4(Mathf.Max(0.1f, _settings.falloffPower), Mathf.Clamp01(_settings.flickerAmount), Mathf.Max(0f, _settings.flickerSpeed), Mathf.Max(0.1f, _settings.flickerScale)));
        Shader.SetGlobalVector(ObjectID, new Vector4(Mathf.Max(0f, _settings.objectStrength), Mathf.Max(0.1f, _settings.objectFadeHeight), 0f, 0f));
    }

    public static void Clear()
    {
        Shader.SetGlobalVector(AID, Vector4.zero);
        Shader.SetGlobalVector(ObjectID, Vector4.zero);
    }

    private static void BuildValues(LavaGlowSettings _settings, TileBase[] _lavaTiles, int _width, int _height, Vector2 _axisX, Vector2 _axisY)
    {
        int size = _width * _height;
        if (null == glowValues || glowValues.Length != size)
        {
            glowValues = new float[size];
        }

        for (int i = 0; i < size; i++)
        {
            glowValues[i] = null != _lavaTiles[i] ? 1f : 0f;
        }

        float radius = Mathf.Max(0.3f, _settings.radius);
        // 한 칸 이동의 가장 짧은 월드 거리(아이소메트릭은 대각선 한 칸이 가장 짧다)로 훑을 칸 범위를 정한다.
        float shortestStep = Mathf.Min(Mathf.Min(_axisX.magnitude, _axisY.magnitude), Mathf.Min((_axisX + _axisY).magnitude, (_axisX - _axisY).magnitude));
        int reach = Mathf.CeilToInt(radius / Mathf.Max(0.01f, shortestStep));

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                int index = x + y * _width;
                if (null == _lavaTiles[index] || false == IsLavaEdge(_lavaTiles, x, y, _width, _height))
                {
                    continue;
                }

                // 가장자리 용암 칸에서 둘레 바닥 칸으로 빛을 퍼뜨린다(가까운 용암 칸 값이 남는다).
                int yMin = Mathf.Max(0, y - reach);
                int yMax = Mathf.Min(_height - 1, y + reach);
                int xMin = Mathf.Max(0, x - reach);
                int xMax = Mathf.Min(_width - 1, x + reach);
                for (int ny = yMin; ny <= yMax; ny++)
                {
                    for (int nx = xMin; nx <= xMax; nx++)
                    {
                        int neighbor = nx + ny * _width;
                        if (null != _lavaTiles[neighbor])
                        {
                            continue;
                        }

                        Vector2 offset = _axisX * (nx - x) + _axisY * (ny - y);
                        float glow = 1f - offset.magnitude / radius;
                        if (glow > glowValues[neighbor])
                        {
                            glowValues[neighbor] = glow;
                        }
                    }
                }
            }
        }
    }

    private static bool IsLavaEdge(TileBase[] _lavaTiles, int _x, int _y, int _width, int _height)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = _x + dx;
                int ny = _y + dy;
                if (nx < 0 || ny < 0 || nx >= _width || ny >= _height)
                {
                    continue;
                }

                if (null == _lavaTiles[nx + ny * _width])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void UploadTexture(int _width, int _height)
    {
        int size = _width * _height;
        if (null == glowTexture || glowTexture.width != _width || glowTexture.height != _height)
        {
            if (null != glowTexture)
            {
                Object.Destroy(glowTexture);
            }

            glowTexture = new Texture2D(_width, _height, TextureFormat.R8, false, true);
            glowTexture.name = "LavaGlowMap";
            glowTexture.filterMode = FilterMode.Bilinear;
            glowTexture.wrapMode = TextureWrapMode.Clamp;
            glowTexture.hideFlags = HideFlags.DontSave;
            glowBytes = new byte[size];
        }

        for (int i = 0; i < size; i++)
        {
            glowBytes[i] = (byte)Mathf.RoundToInt(Mathf.Clamp01(glowValues[i]) * 255f);
        }

        glowTexture.SetPixelData(glowBytes, 0);
        glowTexture.Apply(false, false);
    }
}
