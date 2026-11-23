using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 4스테이지 돌처럼 생긴 나무(수정 기둥이 모인 모양)가 깎일 때 그리는 금의 모양을 나무 그림마다 미리 구워 둔 텍스처(렌더링 전용).
/// 예전에는 그림과 상관없는 격자 무늬를 나무 위에 깔아 금이 조각 모양·앞뒤 가림을 무시하고 아무 데나 지나갔다.
/// 이제는 그림을 한 번 읽어 외곽선(가장 어두운 색)으로 나뉜 조각을 찾고, 도끼가 닿는 밑동 쪽 한 자리에서 금 몇 줄이 갈라져 나가게 만든다.
/// - 금은 조각 안에서만 그린다. 외곽선 위는 건너뛰므로 금이 앞 조각 뒤로 숨었다가 다음 조각에서 1픽셀 어긋나 다시 나온다.
/// - 길쭉한 조각 안에서는 조각의 결(긴 방향)을 따라 조금씩 휜다. 조각 테두리 픽셀은 되도록 피해 금이 외곽선 빛처럼 보이지 않게 한다.
/// - 다른 금에 닿으면 거기서 합쳐지고 끝난다(금끼리 나란히 붙어 덩어리지지 않는다). 외곽선 사이에 1픽셀만 남는 토막은 버린다.
/// - 많이 깎이면 이미 난 금에서 짧은 금이 더 갈라진다.
/// - 밑동 그림은 수관에서 내려온 금이 이어지듯 짧은 한 줄만, 늦게 나고 아래로 갈수록 식어 사그라든다.
/// 같은 그림을 쓰는 나무끼리 금이 겹치지 않게 모양을 몇 가지(VariantCount) 만들어 가로로 이어 붙이고, 나무마다 하나를 고른다(셰이더가 무늬 위치 값으로 고른다).
///
/// 채널 (TreeDamageLook.hlsl의 설명과 같아야 한다)
/// R: 아래 2비트 = 종류(0 = 금 아님, 1 = 금의 심, 2 = 심 옆의 달궈진 가장자리 - 조각 안쪽 픽셀만),
///    위 6비트 = 열기(0~63 -> 0~1, 수관 금은 1, 밑동 금은 아래로 갈수록 낮다)
/// G: 이 픽셀이 보이기 시작하는 깎임 정도(0~1)
/// B: 금을 따라 시작 자리에서 잰 거리(픽셀) - 빛이 바깥으로 흐르는 박자
/// A: 금 한 줄마다 고유한 값(0~1) - 빛이 흐르는 박자를 줄마다 다르게
/// </summary>
public static class TreeCrackMaskTexture
{
    // 그림 하나에 만드는 금 모양 수. 셰이더의 TreeCrackVariantCount와 같아야 한다.
    public const int VariantCount = 4;
    // 모양마다 쓰는 씨앗. 씨앗 20개를 실제 나무 그림(라바샙·흑요석 수관 + 밑동)에 그려 보고, 금이 한쪽에 몰리거나 일찍 끊기지 않고
    // 수관 전체로 고르게 퍼지는 것 중 서로 다른 모양 넷을 골랐다.
    private static readonly int[] VariantSeeds = { 1, 12, 14, 18 };
    // 그림이 바뀌어 첫 금이 일찍 끊기면 금이 거의 없는 모양이 나올 수 있다. 금의 심이 조각 픽셀의 이 비율보다 적으면
    // 씨앗을 바꿔(RetrySeedStep씩) 몇 번 더 만들어 보고, 그래도 모자라면 그중 가장 많은 것을 쓴다.
    private const double MinCoreRatio = 0.06;
    private const int RetryCount = 4;
    private const int RetrySeedStep = 1000;

    // 금이 시작하는 자리(그림 안 비율, 수관 아래쪽 가운데 - 도끼가 닿는 쪽)
    private const double OriginX = 0.5;
    private const double OriginY = 0.3;
    // 시작 자리에서 이 거리(픽셀)만큼 금을 따라가면 Spread만큼 더 깎여야 보인다(깎일수록 금이 뻗어 나간다).
    private const double Reach = 36.0;
    private const double Spread = 0.62;
    // 처음 나는 금 줄 수의 상한(절반 확률로 하나 더)과, 무게중심 방향을 기준으로 퍼지는 각(라디안, 한쪽).
    // 줄 수와 나중에 더 갈라지는 금 수는 조각 픽셀 PixelsPerCrack개마다 하나씩이다(수관 450픽셀쯤이면 상한 그대로).
    private const int RootCount = 4;
    private const double PixelsPerCrack = 110.0;
    private const double FanAngle = 1.1;
    // 다른 줄은 첫 금의 시작 자리에서 이 픽셀 수 안쪽에서 갈라진다.
    private const int SplitNear = 5;
    // 다음 조각으로 넘어갈 때 금이 거기서 멈출 확률, 외곽선을 이 픽셀 수보다 길게 지나면 멈춘다.
    private const double StopAtShard = 0.06;
    private const int MaxOutlineRun = 3;
    // 조각의 결 쪽으로 휘는 정도와, 결을 따르기 시작하는 걸음 수. 이보다 길쭉한(긴 축 분산 / 짧은 축 분산) 조각만 결이 있다고 본다.
    private const double GrainPull = 0.2;
    private const int GrainAfterSteps = 4;
    private const double GrainElongation = 3.0;
    private const int GrainMinPixels = 12;
    // 걸음마다 방향이 꺾일 확률과 꺾이는 폭(라디안)
    private const double JagChance = 0.3;
    private const double JagAmount = 0.9;
    // 걸음마다 곁가지가 날 확률(곁가지의 곁가지는 절반)
    private const double BranchChance = 0.12;
    // 많이 깎였을 때 이미 난 금에서 더 갈라지는 금 수의 상한과, 그 금이 보이기 시작하는 깎임 정도의 바닥값
    private const int LateCount = 4;
    private const double LateFrom = 0.42;
    // 금이 보이기 시작하는 깎임 정도의 상한(쓰러지기 전에 다 보이게)
    private const double MaxAppear = 0.95;

    // 밑동(무게중심이 시작 자리 아래쪽인 그림). 수관에서 내려온 금이 이어지는 느낌만 나게, 시작 자리에서 짧은 금 한 줄만
    // 수관보다 늦게 나고, 아래로 갈수록 식어(어둡고 붉게) 사그라든다. 결은 따르지 않고 더 자주 꺾여 나무껍질 틈처럼 보인다.
    // 무게중심 방향의 sin이 이보다 작으면(아래쪽) 밑동으로 본다.
    private const double TrunkDirection = -0.5;
    private const double TrunkLength = 13.0;
    private const double TrunkSpread = 1.3;
    private const double TrunkStart = 0.14;
    private const double TrunkBranchChance = 0.05;
    private const double TrunkJagChance = 0.7;
    private const double TrunkJagAmount = 1.4;
    // 밑동 금의 열기: 시작 자리에서 TrunkHeatTop, 끝에서 TrunkHeatEnd(수관 금은 1). 열기가 RimMinHeat보다 낮은 곳은 가장자리가 없다.
    private const double TrunkHeatTop = 0.6;
    private const double TrunkHeatEnd = 0.12;
    private const double RimMinHeat = 0.6;
    // 텍스처 R에는 종류(아래 2비트)와 열기(위 6비트, 0~HeatLevels)를 함께 넣는다. 셰이더 TreeCrackHeatLevels와 같아야 한다.
    private const int HeatLevels = 63;

    private const byte KindCore = 1;
    private const byte KindRim = 2;

    private static readonly Dictionary<Texture, Texture2D> cache = new Dictionary<Texture, Texture2D>();

    /// <summary>
    /// 이 그림의 금 텍스처(그림 크기의 가로 VariantCount배). 처음 부를 때 한 번 만들고 그 뒤로는 그대로 돌려준다.
    /// 그림 텍스처가 읽기 금지여도 GPU에서 한 번 복사해 읽는다. 그림이 없으면 null.
    /// </summary>
    public static Texture2D Get(Sprite _sprite)
    {
        if (null == _sprite) return null;

        Texture2D source = _sprite.texture;
        if (null == source) return null;

        if (cache.TryGetValue(source, out Texture2D cached) && null != cached)
        {
            return cached;
        }

        int width = source.width;
        int height = source.height;
        Color32[] spritePixels = ReadPixels(source);

        Color32[] pixels = new Color32[width * VariantCount * height];
        for (int variant = 0; variant < VariantCount; variant++)
        {
            Generator best = null;
            for (int attempt = 0; attempt < RetryCount; attempt++)
            {
                Generator generator = new Generator(spritePixels, width, height, VariantSeeds[variant] + attempt * RetrySeedStep);
                generator.Run();
                if (null == best || generator.CoreCount > best.CoreCount) best = generator;
                if (generator.CoreCount >= generator.FacePixelCount * MinCoreRatio) break;
            }
            best.WriteTo(pixels, width * VariantCount, variant * width);
        }

        Texture2D texture = new Texture2D(width * VariantCount, height, TextureFormat.RGBA32, false, true);
        texture.name = "TreeCrackMask_" + source.name;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        // 씬이 바뀌어도 남고 저장되지 않는다(깎인 나무의 재질 사본이 가리키는 동안 사라지면 안 된다).
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        cache[source] = texture;
        return texture;
    }

    // 그림 픽셀(아래 줄부터). 읽기 금지 텍스처는 같은 크기의 렌더 텍스처에 복사해 읽는다(sRGB로 되돌려 원래 값과 같다).
    private static Color32[] ReadPixels(Texture2D _source)
    {
        if (_source.isReadable)
        {
            return _source.GetPixels32();
        }

        int width = _source.width;
        int height = _source.height;
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rt.filterMode = FilterMode.Point;
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(_source, rt);
        RenderTexture.active = rt;

        Texture2D readable = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        readable.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
        readable.Apply(false, false);

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        Color32[] pixels = readable.GetPixels32();
        Object.DestroyImmediate(readable);
        return pixels;
    }

    private struct CrackPoint
    {
        public int X;
        public int Y;
        public double Appear;
        public double Flow;
    }

    private struct BranchRequest
    {
        public double X;
        public double Y;
        public double Angle;
        public double Length;
        public double Appear;
        public double Flow;
        public double Phase;
        public int Depth;
    }

    private sealed class Generator
    {
        private readonly int width;
        private readonly int height;
        private readonly bool[] opaque;
        // 조각 번호(0 = 외곽선·투명, 1부터 조각)
        private readonly int[] shard;
        // 조각마다의 결(단위 벡터). 결이 없으면 hasAxis가 false.
        private readonly double[] axisX;
        private readonly double[] axisY;
        private readonly bool[] hasAxis;

        private readonly byte[] kind;
        private readonly double[] appear;
        private readonly double[] flow;
        private readonly double[] phase;
        private readonly double[] heat;
        private readonly int[] owner;

        // 그림에 따라 정해지는 금 성질(밑동이면 Trunk 값으로 바뀐다)
        private bool bTrunk;
        private double spread = Spread;
        private double branchChance = BranchChance;
        private double jagChance = JagChance;
        private double jagAmount = JagAmount;
        private double grainPull = GrainPull;

        // 조각(외곽선이 아닌 그림) 픽셀 수와, 다 만든 뒤 금의 심 픽셀 수
        public int FacePixelCount { get; private set; }
        public int CoreCount { get; private set; }

        private readonly int seed;
        private int rngCount;
        private int nextFissureId;
        private readonly double originX;
        private readonly double originY;

        public Generator(Color32[] _pixels, int _width, int _height, int _seed)
        {
            width = _width;
            height = _height;
            seed = _seed * 7919 + 13;
            originX = width * OriginX;
            originY = height * OriginY;

            int count = width * height;
            opaque = new bool[count];
            shard = new int[count];
            kind = new byte[count];
            appear = new double[count];
            flow = new double[count];
            phase = new double[count];
            heat = new double[count];
            owner = new int[count];
            for (int i = 0; i < count; i++)
            {
                appear[i] = 1.0;
                heat[i] = 1.0;
                owner[i] = -1;
            }

            int shardCount = FindShards(_pixels);
            axisX = new double[shardCount + 1];
            axisY = new double[shardCount + 1];
            hasAxis = new bool[shardCount + 1];
            FindGrains(shardCount);
        }

        private double NextRandom()
        {
            rngCount++;
            return Hash01(seed, rngCount, 7);
        }

        // 외곽선(그림에서 많이 쓰인 색 중 가장 어두운 색과 그다음 색의 중간보다 어두운 픽셀)으로 나뉜 조각에 번호를 붙인다.
        private int FindShards(Color32[] _pixels)
        {
            int count = width * height;
            float[] luminance = new float[count];
            Dictionary<int, int> colorCounts = new Dictionary<int, int>();
            int total = 0;

            for (int i = 0; i < count; i++)
            {
                Color32 c = _pixels[i];
                opaque[i] = c.a / 255f > 0.5f;
                luminance[i] = 0.299f * (c.r / 255f) + 0.587f * (c.g / 255f) + 0.114f * (c.b / 255f);
                if (false == opaque[i]) continue;

                int key = (c.r << 16) | (c.g << 8) | c.b;
                colorCounts.TryGetValue(key, out int n);
                colorCounts[key] = n + 1;
                total++;
            }

            List<int> major = new List<int>();
            foreach (KeyValuePair<int, int> pair in colorCounts)
            {
                if (pair.Value >= total * 0.05) major.Add(pair.Key);
            }
            // 색 값 순으로 놓고 밝기 순으로 다시 늘어놓는다(밝기가 같으면 색 값 순 그대로, 늘 같은 순서).
            major.Sort();
            for (int i = 1; i < major.Count; i++)
            {
                int key = major[i];
                double keyLuminance = KeyLuminance(key);
                int j = i - 1;
                while (j >= 0 && KeyLuminance(major[j]) > keyLuminance)
                {
                    major[j + 1] = major[j];
                    j--;
                }
                major[j + 1] = key;
            }

            // 많이 쓰인 색이 둘보다 적으면 외곽선을 가리지 못하니 그림 전체를 한 조각으로 본다.
            double threshold = major.Count >= 2 ? (KeyLuminance(major[0]) + KeyLuminance(major[1])) * 0.5 : -1.0;
            float faceLimit = (float)(threshold + 1e-6);

            bool[] face = new bool[count];
            for (int i = 0; i < count; i++)
            {
                face[i] = opaque[i] && luminance[i] > faceLimit;
                if (face[i]) FacePixelCount++;
            }

            int shardCount = 0;
            Stack<int> stack = new Stack<int>();
            for (int i = 0; i < count; i++)
            {
                if (false == face[i] || 0 != shard[i]) continue;

                shardCount++;
                shard[i] = shardCount;
                stack.Push(i);
                while (stack.Count > 0)
                {
                    int p = stack.Pop();
                    int x = p % width;
                    int y = p / width;
                    TryFill(x + 1, y, face, shardCount, stack);
                    TryFill(x - 1, y, face, shardCount, stack);
                    TryFill(x, y + 1, face, shardCount, stack);
                    TryFill(x, y - 1, face, shardCount, stack);
                }
            }

            return shardCount;
        }

        private void TryFill(int _x, int _y, bool[] _face, int _id, Stack<int> _stack)
        {
            if (_x < 0 || _x >= width || _y < 0 || _y >= height) return;
            int i = _y * width + _x;
            if (false == _face[i] || 0 != shard[i]) return;
            shard[i] = _id;
            _stack.Push(i);
        }

        private static double KeyLuminance(int _key)
        {
            return (0.299 * ((_key >> 16) & 255) + 0.587 * ((_key >> 8) & 255) + 0.114 * (_key & 255)) / 255.0;
        }

        // 조각마다 픽셀 분포의 긴 축을 구해, 충분히 길쭉하면 그 방향을 결로 삼는다.
        private void FindGrains(int _shardCount)
        {
            double[] sumX = new double[_shardCount + 1];
            double[] sumY = new double[_shardCount + 1];
            int[] counts = new int[_shardCount + 1];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int id = shard[y * width + x];
                    if (0 == id) continue;
                    sumX[id] += x + 0.5;
                    sumY[id] += y + 0.5;
                    counts[id]++;
                }
            }

            double[] cxx = new double[_shardCount + 1];
            double[] cyy = new double[_shardCount + 1];
            double[] cxy = new double[_shardCount + 1];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int id = shard[y * width + x];
                    if (0 == id) continue;
                    double dx = x + 0.5 - sumX[id] / counts[id];
                    double dy = y + 0.5 - sumY[id] / counts[id];
                    cxx[id] += dx * dx;
                    cyy[id] += dy * dy;
                    cxy[id] += dx * dy;
                }
            }

            for (int id = 1; id <= _shardCount; id++)
            {
                if (counts[id] < GrainMinPixels) continue;

                double xx = cxx[id] / counts[id];
                double yy = cyy[id] / counts[id];
                double xy = cxy[id] / counts[id];
                double trace = xx + yy;
                double det = xx * yy - xy * xy;
                double disc = System.Math.Sqrt(System.Math.Max(trace * trace / 4.0 - det, 0.0));
                double major = trace / 2.0 + disc;
                double minor = trace / 2.0 - disc;
                if (major <= minor * GrainElongation) continue;

                double ex;
                double ey;
                if (System.Math.Abs(xy) > 1e-6)
                {
                    ex = major - yy;
                    ey = xy;
                }
                else if (xx >= yy)
                {
                    ex = 1.0;
                    ey = 0.0;
                }
                else
                {
                    ex = 0.0;
                    ey = 1.0;
                }

                double length = System.Math.Sqrt(ex * ex + ey * ey);
                axisX[id] = ex / length;
                axisY[id] = ey / length;
                hasAxis[id] = true;
            }
        }

        private int ShardAt(int _x, int _y)
        {
            return _x >= 0 && _x < width && _y >= 0 && _y < height ? shard[_y * width + _x] : 0;
        }

        private bool OpaqueAt(int _x, int _y)
        {
            return _x >= 0 && _x < width && _y >= 0 && _y < height && opaque[_y * width + _x];
        }

        // 네 이웃이 모두 같은 조각인 픽셀(조각 테두리가 아닌 안쪽)
        private bool IsInterior(int _x, int _y, int _id)
        {
            return ShardAt(_x + 1, _y) == _id && ShardAt(_x - 1, _y) == _id && ShardAt(_x, _y + 1) == _id && ShardAt(_x, _y - 1) == _id;
        }

        private static int Floor(double _v)
        {
            return (int)System.Math.Floor(_v);
        }

        public void Run()
        {
            double sumX = 0.0;
            double sumY = 0.0;
            int opaqueCount = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (false == opaque[y * width + x]) continue;
                    sumX += x;
                    sumY += y;
                    opaqueCount++;
                }
            }
            if (0 == opaqueCount) return;

            // 시작 자리에서 그림의 무게중심 쪽(수관이면 위, 밑동이면 아래)으로 퍼진다.
            double mainAngle = System.Math.Atan2(sumY / opaqueCount + 0.5 - originY, sumX / opaqueCount + 0.5 - originX);

            bTrunk = System.Math.Sin(mainAngle) < TrunkDirection;
            if (bTrunk)
            {
                spread = TrunkSpread;
                branchChance = TrunkBranchChance;
                jagChance = TrunkJagChance;
                jagAmount = TrunkJagAmount;
                grainPull = 0.0;
            }

            // 한 줄이 먼저 시작 자리에서 나고(도끼 자국), 나머지는 그 금의 시작 부분에서 갈라진다. 밑동은 한 줄뿐이다.
            int crackBudget = bTrunk ? 1 : System.Math.Max(1, System.Math.Min(RootCount, (int)System.Math.Round(FacePixelCount / PixelsPerCrack)));
            int rootCount = bTrunk ? 1 : crackBudget + (NextRandom() < 0.5 ? 1 : 0);
            int first = (int)(NextRandom() * rootCount);
            List<CrackPoint> rootPoints = null;
            int rootId = -1;
            for (int n = 0; n < rootCount; n++)
            {
                int index = 0 == n ? first : (n <= first ? n - 1 : n);
                double t = (index + 0.5) / rootCount;
                double angle = mainAngle + (t - 0.5) * 2.0 * FanAngle + (NextRandom() - 0.5) * (2.0 * FanAngle / rootCount) * 0.7;
                double length = Reach * (0.6 + NextRandom() * 0.45);
                if (bTrunk) length = TrunkLength * (0.7 + 0.3 * NextRandom());

                if (null == rootPoints)
                {
                    rootId = nextFissureId;
                    double rootAppear = (bTrunk ? TrunkStart : 0.03) + NextRandom() * 0.03;
                    rootPoints = Fissure(originX, originY, angle, length, rootAppear, 0.0, NextRandom(), 0, -1);
                    continue;
                }

                if (0 == rootPoints.Count) break;

                int j = System.Math.Min((int)(SplitNear * NextRandom()) + 1, rootPoints.Count - 1);
                CrackPoint from = rootPoints[j];
                double splitAppear = from.Appear + 0.02 + NextRandom() * 0.08;
                Fissure(from.X + 0.5, from.Y + 0.5, angle, length, splitAppear, from.Flow, NextRandom(), 0, rootId);
            }

            // 많이 깎이면 이미 난 금에서 바깥쪽으로 짧은 금이 더 갈라진다.
            List<int> corePixels = new List<int>();
            int lateCount = bTrunk ? 0 : System.Math.Min(LateCount, crackBudget);
            for (int late = 0; late < lateCount; late++)
            {
                corePixels.Clear();
                for (int i = 0; i < kind.Length; i++)
                {
                    if (KindCore == kind[i]) corePixels.Add(i);
                }
                if (0 == corePixels.Count) break;

                int start = corePixels[(int)(NextRandom() * corePixels.Count)];
                int x0 = start % width;
                int y0 = start / width;
                double side = NextRandom() < 0.5 ? 1.0 : -1.0;
                double angle = System.Math.Atan2(y0 + 0.5 - originY, x0 + 0.5 - originX) + side * (0.4 + NextRandom() * 0.5);
                double lateAppear = System.Math.Max(appear[start], LateFrom) + 0.04 + NextRandom() * 0.12;
                double lateLength = 6.0 + NextRandom() * 10.0;
                Fissure(x0 + 0.5, y0 + 0.5, angle, lateLength, lateAppear, flow[start], NextRandom(), 1, owner[start]);
            }

            for (int i = 0; i < kind.Length; i++)
            {
                if (KindCore == kind[i]) CoreCount++;
            }

            if (bTrunk) CoolTrunk();
            AddRims();
        }

        // 금 한 줄을 한 픽셀씩 그어 나간다. 다 그은 뒤에 기록하고, 곁가지는 그다음에 긋는다(곁가지가 부모 금에 닿아 멈추지 않게).
        private List<CrackPoint> Fissure(double _x, double _y, double _angle, double _maxLength, double _appear, double _flow,
            double _phase, int _depth, int _parent)
        {
            int id = nextFissureId++;
            double vx = System.Math.Cos(_angle);
            double vy = System.Math.Sin(_angle);
            int currentShard = -1;
            int outlineRun = 0;
            double distance = _flow;
            double jx = 0.0;
            double jy = 0.0;
            int steps = 0;
            bool bEntered = _depth > 0;
            List<CrackPoint> points = new List<CrackPoint>();
            int segmentStart = 0;
            List<BranchRequest> branches = new List<BranchRequest>();
            double px = _x;
            double py = _y;

            while (steps < _maxLength)
            {
                double m = System.Math.Max(System.Math.Abs(vx), System.Math.Abs(vy));
                px += vx / m;
                py += vy / m;
                steps++;
                distance += 1.0 / m;
                int ix = Floor(px + jx);
                int iy = Floor(py + jy);

                // 시작 자리(그림 아래 투명한 곳)에서 그림에 닿을 때까지는 건너뛰고, 그림에 들어온 뒤 그림 밖으로 나가면 끝난다.
                if (false == OpaqueAt(ix, iy))
                {
                    if (bEntered || steps > 8) break;
                    continue;
                }
                bEntered = true;

                int id2 = ShardAt(ix, iy);
                // 외곽선 위는 그리지 않고 지나간다(앞 조각 뒤로 숨는다).
                if (0 == id2)
                {
                    outlineRun++;
                    if (outlineRun > MaxOutlineRun) break;
                    continue;
                }
                outlineRun = 0;

                if (id2 != currentShard)
                {
                    if (-1 != currentShard)
                    {
                        CloseSegment(points, segmentStart);
                        segmentStart = points.Count;
                        if (NextRandom() < StopAtShard) break;

                        // 다음 조각에서는 1픽셀 어긋나 나온다(조각마다 앞뒤 깊이가 다르다).
                        double nx = -vy;
                        double ny = vx;
                        double sign = NextRandom() < 0.5 ? 1.0 : -1.0;
                        if (ShardAt(Floor(px + jx + nx * sign), Floor(py + jy + ny * sign)) == id2)
                        {
                            jx += nx * sign;
                            jy += ny * sign;
                            ix = Floor(px + jx);
                            iy = Floor(py + jy);
                        }
                    }
                    currentShard = id2;
                }

                // 조각 테두리 픽셀이면 바로 옆 안쪽 픽셀로 옮긴다(외곽선 빛처럼 보이지 않게).
                if (false == IsInterior(ix, iy, id2))
                {
                    double nx = -vy;
                    double ny = vx;
                    for (int s = 0; s < 2; s++)
                    {
                        double sign = 0 == s ? 1.0 : -1.0;
                        int tx = Floor(px + jx + nx * sign);
                        int ty = Floor(py + jy + ny * sign);
                        if (ShardAt(tx, ty) == id2 && IsInterior(tx, ty, id2))
                        {
                            jx += nx * sign;
                            jy += ny * sign;
                            ix = tx;
                            iy = ty;
                            break;
                        }
                    }
                }

                // 다른 금에 닿으면 합쳐지고 끝난다.
                bool bFree = distance - _flow > 2.0;
                int pixel = iy * width + ix;
                if (bFree && KindCore == kind[pixel] && owner[pixel] != _parent) break;

                double appearAt = System.Math.Min(_appear + (distance - _flow) / Reach * spread, MaxAppear);
                if (0 == points.Count || points[points.Count - 1].X != ix || points[points.Count - 1].Y != iy)
                {
                    points.Add(new CrackPoint { X = ix, Y = iy, Appear = appearAt, Flow = distance });
                }

                if (bFree && (TouchesOtherCrack(ix, iy, _parent) || TouchesItself(points, ix, iy))) break;

                // 길쭉한 조각 안에서는 결 쪽으로 조금씩 휜다.
                if (hasAxis[id2] && steps > GrainAfterSteps && grainPull > 0.0)
                {
                    double dx = axisX[id2];
                    double dy = axisY[id2];
                    if (dx * vx + dy * vy < 0.0)
                    {
                        dx = -dx;
                        dy = -dy;
                    }
                    vx = vx * (1.0 - grainPull) + dx * grainPull;
                    vy = vy * (1.0 - grainPull) + dy * grainPull;
                }

                if (NextRandom() < jagChance)
                {
                    double turn = (NextRandom() - 0.5) * jagAmount;
                    double c = System.Math.Cos(turn);
                    double sn = System.Math.Sin(turn);
                    double rx = vx * c - vy * sn;
                    double ry = vx * sn + vy * c;
                    vx = rx;
                    vy = ry;
                }

                double len = System.Math.Sqrt(vx * vx + vy * vy);
                vx /= len;
                vy /= len;

                if (_depth < 2 && steps > 3 && NextRandom() < branchChance / (1 + _depth))
                {
                    double side = NextRandom() < 0.5 ? 1.0 : -1.0;
                    BranchRequest branch = new BranchRequest();
                    branch.X = px + jx;
                    branch.Y = py + jy;
                    branch.Angle = System.Math.Atan2(vy, vx) + side * (0.55 + NextRandom() * 0.5);
                    branch.Length = 3.0 + NextRandom() * 6.0;
                    branch.Appear = appearAt + 0.07;
                    branch.Flow = distance;
                    branch.Phase = NextRandom();
                    branch.Depth = _depth + 1;
                    branches.Add(branch);
                }
            }

            CloseSegment(points, segmentStart);

            for (int i = 0; i < points.Count; i++)
            {
                CrackPoint point = points[i];
                int pixel = point.Y * width + point.X;
                if (KindCore != kind[pixel] || appear[pixel] > point.Appear)
                {
                    kind[pixel] = KindCore;
                    appear[pixel] = point.Appear;
                    flow[pixel] = point.Flow;
                    phase[pixel] = _phase;
                    owner[pixel] = id;
                }
            }

            for (int i = 0; i < branches.Count; i++)
            {
                BranchRequest b = branches[i];
                Fissure(b.X, b.Y, b.Angle, b.Length, b.Appear, b.Flow, b.Phase, b.Depth, id);
            }

            return points;
        }

        // 두 외곽선 사이에 1픽셀만 남은 토막은 금이 아니라 점으로 보여 버린다(첫 토막은 남긴다).
        private static void CloseSegment(List<CrackPoint> _points, int _segmentStart)
        {
            if (_segmentStart > 0 && _points.Count - _segmentStart == 1)
            {
                _points.RemoveAt(_points.Count - 1);
            }
        }

        private bool TouchesOtherCrack(int _x, int _y, int _parent)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (0 == dx && 0 == dy) continue;
                    int x = _x + dx;
                    int y = _y + dy;
                    if (x < 0 || x >= width || y < 0 || y >= height) continue;
                    int pixel = y * width + x;
                    if (KindCore == kind[pixel] && owner[pixel] != _parent) return true;
                }
            }
            return false;
        }

        // 제 몸(방금 그은 세 픽셀 앞까지) 옆으로 휘어 돌아오면 멈춘다.
        private static bool TouchesItself(List<CrackPoint> _points, int _x, int _y)
        {
            for (int i = 0; i < _points.Count - 3; i++)
            {
                if (System.Math.Abs(_points[i].X - _x) <= 1 && System.Math.Abs(_points[i].Y - _y) <= 1) return true;
            }
            return false;
        }

        // 밑동 금은 시작 자리(수관과 맞닿는 쪽)에서 멀어질수록 식는다.
        private void CoolTrunk()
        {
            double maxFlow = 0.0;
            for (int i = 0; i < kind.Length; i++)
            {
                if (KindCore == kind[i] && flow[i] > maxFlow) maxFlow = flow[i];
            }
            maxFlow = System.Math.Max(maxFlow, 1.0);

            for (int i = 0; i < kind.Length; i++)
            {
                if (KindCore != kind[i]) continue;
                double h = TrunkHeatTop - flow[i] / maxFlow * (TrunkHeatTop - TrunkHeatEnd);
                heat[i] = System.Math.Min(System.Math.Max(h, 0.0), 1.0);
            }
        }

        // 심 옆의 달궈진 가장자리. 심의 오른쪽·아래쪽 한 면에만 붙여(틈 안쪽 벽이 한쪽만 보이는 것처럼) 금이 2픽셀 줄로 파여 보이게 하고,
        // 같은 조각의 안쪽 픽셀에만 붙여 좁은 조각이 통째로 빛나지 않게 한다. 식은 심(RimMinHeat 아래)에는 붙이지 않고,
        // 붙인 가장자리는 심의 열기를 따른다.
        private void AddRims()
        {
            bool[] core = new bool[kind.Length];
            for (int i = 0; i < kind.Length; i++)
            {
                core[i] = KindCore == kind[i];
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int pixel = y * width + x;
                    if (false == core[pixel]) continue;

                    for (int side = 0; side < 2; side++)
                    {
                        int u = x + (0 == side ? 1 : 0);
                        int v = y + (1 == side ? -1 : 0);
                        if (u < 0 || u >= width || v < 0 || v >= height) continue;

                        int neighbor = v * width + u;
                        int id = shard[neighbor];
                        if (0 == id || id != shard[pixel] || core[neighbor] || false == IsInterior(u, v, id)) continue;

                        if (heat[pixel] < RimMinHeat) continue;

                        if (0 == kind[neighbor] || appear[neighbor] > appear[pixel])
                        {
                            kind[neighbor] = KindRim;
                            heat[neighbor] = heat[pixel];
                            appear[neighbor] = appear[pixel];
                            flow[neighbor] = flow[pixel];
                            phase[neighbor] = phase[pixel];
                        }
                    }
                }
            }
        }

        public void WriteTo(Color32[] _pixels, int _stride, int _offsetX)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int pixel = y * width + x;
                    if (0 == kind[pixel]) continue;

                    int heatLevel = (int)System.Math.Round(heat[pixel] * HeatLevels);
                    _pixels[y * _stride + _offsetX + x] = new Color32(
                        (byte)(kind[pixel] | (heatLevel << 2)),
                        (byte)System.Math.Round(System.Math.Min(appear[pixel], 1.0) * 255.0),
                        (byte)System.Math.Min(System.Math.Round(flow[pixel]), 255.0),
                        (byte)System.Math.Round(phase[pixel] * 255.0));
                }
            }
        }
    }

    // 정수 둘과 갈래 번호로 0~1 값을 만든다(같은 입력이면 늘 같은 값). TreeDamagePatternTexture와 같은 식.
    private static double Hash01(int _x, int _y, int _channel)
    {
        unchecked
        {
            uint h = (uint)_x * 374761393u + (uint)_y * 668265263u + (uint)_channel * 2246822519u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / 16777216.0;
        }
    }
}
