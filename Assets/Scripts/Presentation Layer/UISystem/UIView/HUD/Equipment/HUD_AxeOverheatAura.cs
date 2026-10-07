using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PresentationLayer.UISystem.UIView.HUD.Equipment
{
    /// <summary>
    /// 도끼 HUD의 과열 아우라. 캐릭터의 VFX_OverheatAura와 같은 알고리즘(몸을 감싸는 얇은 파랑 링 + 어깨선 위로 솟는 푸른 불꽃 껍질,
    /// 파랑/보라/청록 계단 그라데이션, 실루엣 가장자리 불씨와 반짝임, 점화 충격파, 소화)를 HUD 캔버스에 옮긴 것이다.
    /// 아우라 본체는 도끼 이미지 뒤, 불씨와 반짝임과 충격파 앞쪽은 도끼 이미지 앞에 그린다.
    ///
    /// 성능/최적화
    /// - 실루엣은 도끼 스프라이트에서 한 번만 읽어(스프라이트당 한 번, 캐시) 거리장과 가장자리 목록을 만든다. 단계별 스프라이트 5장은
    ///   Initialize에서 전부 만들어 둔다 - 읽기(ReadPixels)가 GPU 동기화를 일으키므로 과열 시작 프레임이 아니라 로딩 때 치른다.
    ///   파손으로 스프라이트가 바뀌는 순간(HUD_EquipmentAxe.UpdateAxeImage -> OnAxeSpriteChanged)에는 캐시된 실루엣으로 바꾸기만 한다.
    /// - 과열 중에만 컴포넌트가 켜져 LateUpdate가 돈다. 메쉬는 30fps로만 다시 만든다(UI 리빌드 최소화).
    /// 캐릭터 아우라는 HDR 발광(셰이더)으로 밝아지는데, HUD 캔버스에는 블룸이 없어서 같은 팔레트를 밝기 배율로 키우고 채널을 255에서 자른다.
    /// </summary>
    public class HUD_AxeOverheatAura : MonoBehaviour
    {
        [Header("Graphics")]
        [SerializeField, Tooltip("도끼 이미지 뒤쪽 층(아우라 본체 + 뒤쪽 불씨)")] private HUD_AxeAuraGraphic backGraphic;
        [SerializeField, Tooltip("도끼 이미지 앞쪽 층(앞쪽 불씨, 반짝임, 점화 충격파)")] private HUD_AxeAuraGraphic frontGraphic;
        [SerializeField, Tooltip("도끼 스프라이트 둘레로 아우라가 뻗을 수 있는 여백(px)")] private int marginPx = 14;

        [Header("Body Ring / Flame (px)")]
        [SerializeField] private float ringThicknessPx = 2.0f;
        [SerializeField, Range(0.0f, 1.0f)] private float ringGlow = 0.3f;
        [SerializeField, Tooltip("링 위로 솟는 불꽃의 최대 추가 두께(px)")] private float crownExtraThicknessPx = 3.5f;
        [SerializeField, Range(0.0f, 1.0f), Tooltip("불꽃이 위쪽 최대치로 자라기 시작하는 높이(스프라이트 높이 대비)")] private float crownStartRatio = 0.3f;
        [SerializeField, Range(0.05f, 1.0f), Tooltip("불꽃이 아래쪽 비중에서 최대 두께까지 자라는 구간(스프라이트 높이 대비)")] private float crownRampRatio = 0.5f;
        [SerializeField, Range(0.0f, 1.0f), Tooltip("가장 낮은 곳(자루 끝)의 불꽃 비중. 1이면 위아래가 같다")] private float crownBaseWeight = 0.6f;
        [SerializeField] private float noiseScrollSpeed = 2.0f;
        [SerializeField, Tooltip("불꽃이 일렁이는 프레임 속도(fps)")] private float flickerFps = 15.0f;
        [SerializeField, Tooltip("메쉬를 다시 만드는 속도(fps). 불씨/밝은 띠/충격파가 이 속도로 움직인다")] private float meshFps = 30.0f;
        [SerializeField, Range(0.0f, 1.0f)] private float dissolveStart = 0.8f;

        [Header("Gradient")]
        [SerializeField] private float gradientSpeed = 0.25f;
        [SerializeField] private float gradientCycles = 1.5f;
        [SerializeField, Range(0.0f, 0.6f)] private float gradientNoise = 0.2f;
        [SerializeField] private int gradientSteps = 12;

        [Header("Sparks")]
        [SerializeField] private int sparkleCount = 6;
        [SerializeField] private float ambientEmberInterval = 0.04f;
        [SerializeField] private float emberSwayAmplitude = 9.0f;
        [SerializeField] private float igniteRingScale = 1.8f;
        [SerializeField] private int igniteEmberCount = 30;

        [Header("Extinguish / Brightness")]
        [SerializeField] private float extinguishDuration = 0.45f;
        [SerializeField, Tooltip("HDR 발광 배율의 최댓값. 캐릭터 셰이더(BrandWrapGlow)의 _GlowMax와 같다")] private float glowMax = 2.8f;
        [SerializeField, Range(0.0f, 2.0f)] private float glowScale = 1.0f;

        private const float TwoPi = Mathf.PI * 2.0f;
        private const int BackLayer = 0;
        private const int FrontLayer = 1;
        private const int AuraLayer = 2; // 아우라 본체(뒤쪽 그래픽에 그린다)
        private const int EmberCapacity = 160;
        private const int DistanceInfinity = 100000;
        private const int DistanceUnitsPerPixel = 3; // 3-4 챔퍼 거리(직교 3, 대각 4)
        private const float RingNoiseAmplitude = 0.6f;
        private const float BodyBleedPx = 0.3f;
        private const float MaxIntensity = 1.6f;
        private const byte SolidAlphaThreshold = 76;
        private const int MaxReadbackSize = 512;

        private enum AuraPhase
        {
            Burning,
            Extinguishing,
        }

        private struct Ember
        {
            public bool bActive;
            public int layer;
            public float x;
            public float y;
            public float vx;
            public float vy;
            public float swayPhase;
            public float swaySpeed;
            public float age;
            public float life;
            public int kind; // 0: `.`, 1: `+`/`x`(번갈아 회전)
            public int seed;
            public Color32 color;
        }

        private struct AuraFrame
        {
            public float flickerTime;
            public int flickerFrame;
            public float intensity;
            public float dissolve;
            public float ringThickness;
            public float colorTime;
            public float maxReachPx;
        }

        // 도끼 스프라이트 한 장의 실루엣 격자(여백 포함). 스프라이트마다 한 번만 만들어 캐시한다.
        private sealed class SilhouetteField
        {
            public int width;
            public int height;
            public bool[] solid;
            public int[] distance;
            public int[] edgeCells;
            public int edgeCellCount;
        }

        // 팔레트: 캐릭터 아우라와 같은 파랑 계열 3색 + 흰색, 포인트는 보라와 청록
        private static readonly Color32 ColorWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 ColorIce = new Color32(120, 196, 255, 255);
        private static readonly Color32 ColorBlue = new Color32(14, 64, 200, 255);
        private static readonly Color32 ColorDeepBlue = new Color32(20, 52, 190, 255);
        private static readonly Color32 ColorViolet = new Color32(51, 23, 158, 255);
        private static readonly Color32 ColorCyan = new Color32(30, 235, 215, 255);
        private static readonly Color32[] GradientAnchors = { ColorBlue, ColorViolet, ColorCyan };

        // //외부 의존성
        private Image axeImage;
        private List<Sprite> axeSprites;

        // //내부 의존성
        private readonly Dictionary<Sprite, SilhouetteField> fields = new Dictionary<Sprite, SilhouetteField>(8);
        private readonly Ember[] embers = new Ember[EmberCapacity];
        private SilhouetteField field;
        private Texture2D readbackTexture;

        // //상태 변수
        private bool bInitialized = false;
        private bool bOverheat = false;
        private Sprite currentAxeSprite;
        private AuraPhase phase;
        private float elapsed;
        private float extinguishElapsed;
        private bool bIgniteEmbersSpawned;
        private float emberSpawnAccumulator;
        private int lastFlickerFrame;
        private int emberCursor;
        private uint randomState;
        private float spriteHeightPx;
        private float bodyCenterYPx;
        private float crownStartYPx;
        private float crownRampPx;

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 도끼 이미지와 단계별 스프라이트 목록(파손 순서: 100%, 75%, 50%, 25%, 0%)을 받아 의존성을 구성한다.
        /// </summary>
        public void Initialize(Image _axeImage, List<Sprite> _axeSprites)
        {
            axeImage = _axeImage;
            axeSprites = _axeSprites;
            randomState = unchecked((uint)(GetInstanceID() * 2654435761u));
            if (0u == randomState) randomState = 1u;

            if (null != backGraphic) backGraphic.raycastTarget = false;
            if (null != frontGraphic) frontGraphic.raycastTarget = false;

            PrebuildAllFields();

            enabled = false;
            bInitialized = true;
        }

        // 단계별 스프라이트의 실루엣을 전부 미리 만든다. 다 만들고 나면 읽기용 텍스처는 더 쓸 일이 없으므로 바로 놓는다
        // (목록에 없는 스프라이트가 들어오는 예외 상황에서만 ApplySilhouette이 다시 만든다).
        private void PrebuildAllFields()
        {
            if (null == axeSprites)
                return;

            for (int i = 0; i < axeSprites.Count; i++)
            {
                if (null != axeSprites[i])
                    GetOrBuildField(axeSprites[i]);
            }

            if (null != readbackTexture)
            {
                Destroy(readbackTexture);
                readbackTexture = null;
            }
        }

        /// <summary>
        /// 과열 시작/종료. 꺼질 때는 소화 연출이 끝난 뒤에 스스로 멈춘다.
        /// </summary>
        public void SetOverheat(bool _bActive)
        {
            if (false == bInitialized || bOverheat == _bActive)
                return;

            bOverheat = _bActive;

            if (true == _bActive)
            {
                ApplySilhouette(currentAxeSprite);
                BeginBurning();
                enabled = true;
            }
            else
            {
                phase = AuraPhase.Extinguishing;
                extinguishElapsed = 0.0f;
            }
        }

        /// <summary>
        /// 도끼 스프라이트가 바뀐 순간(파손/수리 단계 전환)에 한 번만 호출한다. 과열 중이면 캐시된 실루엣으로 바로 교체한다.
        /// </summary>
        public void OnAxeSpriteChanged(Sprite _sprite)
        {
            bool bSame = ReferenceEquals(currentAxeSprite, _sprite);
            currentAxeSprite = _sprite;

            if (true == bSame || false == bInitialized || false == bOverheat)
                return;

            ApplySilhouette(_sprite);
            lastFlickerFrame = -1;
        }

        // //일반 비즈니스 로직 및 내부 메서드

        private void BeginBurning()
        {
            phase = AuraPhase.Burning;
            elapsed = 0.0f;
            extinguishElapsed = 0.0f;
            bIgniteEmbersSpawned = false;
            emberSpawnAccumulator = 0.0f;
            emberCursor = 0;
            lastFlickerFrame = -1;
            for (int i = 0; i < EmberCapacity; i++) embers[i].bActive = false;
        }

        private void ApplySilhouette(Sprite _axeSprite)
        {
            if (null == _axeSprite)
                return;

            SilhouetteField built = GetOrBuildField(_axeSprite);
            if (null == built)
                return;

            field = built;
            spriteHeightPx = _axeSprite.rect.height;
            bodyCenterYPx = field.height * 0.5f;
            crownStartYPx = marginPx + spriteHeightPx * crownStartRatio;
            crownRampPx = Mathf.Max(1.0f, spriteHeightPx * crownRampRatio);

            // 도끼 이미지와 같은 위치/배율에 놓는다(그래픽의 원점은 이미지 중심).
            RectTransform axeRect = axeImage.rectTransform;
            Rect spriteRect = _axeSprite.rect;
            float scale = 0.0f < spriteRect.width ? axeRect.rect.width / spriteRect.width : 1.0f;
            Vector2 center = axeRect.anchoredPosition + new Vector2((0.5f - axeRect.pivot.x) * axeRect.rect.width, (0.5f - axeRect.pivot.y) * axeRect.rect.height);

            ConfigureGraphic(backGraphic, axeRect, center, scale);
            ConfigureGraphic(frontGraphic, axeRect, center, scale);
        }

        private static void ConfigureGraphic(HUD_AxeAuraGraphic _graphic, RectTransform _axeRect, Vector2 _center, float _scale)
        {
            if (null == _graphic)
                return;

            RectTransform rect = _graphic.rectTransform;
            rect.anchorMin = _axeRect.anchorMin;
            rect.anchorMax = _axeRect.anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = _center;
            rect.sizeDelta = Vector2.zero;
            _graphic.SetCellScale(_scale);
        }

        private SilhouetteField GetOrBuildField(Sprite _axeSprite)
        {
            SilhouetteField cached;
            if (true == fields.TryGetValue(_axeSprite, out cached))
                return cached;

            SilhouetteField built = BuildField(_axeSprite);
            fields[_axeSprite] = built;
            return built;
        }

        // 스프라이트의 불투명 픽셀(알파 > 임계값)을 읽어 실루엣 격자와 3-4 챔퍼 거리, 가장자리 목록을 만든다.
        // 텍스처가 읽기 가능(Read/Write)하지 않아도 되도록 GPU에서 작은 RenderTexture로 복사해서 읽는다.
        private SilhouetteField BuildField(Sprite _axeSprite)
        {
            Rect rect = _axeSprite.textureRect;
            int spriteWidth = Mathf.RoundToInt(rect.width);
            int spriteHeight = Mathf.RoundToInt(rect.height);
            Texture source = _axeSprite.texture;

            if (null == source || 0 >= spriteWidth || 0 >= spriteHeight || MaxReadbackSize < spriteWidth || MaxReadbackSize < spriteHeight)
                return null;

            int width = spriteWidth + marginPx * 2;
            int height = spriteHeight + marginPx * 2;

            RenderTexture temp = RenderTexture.GetTemporary(spriteWidth, spriteHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Vector2 scale = new Vector2(rect.width / source.width, rect.height / source.height);
            Vector2 offset = new Vector2(rect.x / source.width, rect.y / source.height);
            Graphics.Blit(source, temp, scale, offset);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = temp;
            if (null == readbackTexture || readbackTexture.width != spriteWidth || readbackTexture.height != spriteHeight)
            {
                if (null != readbackTexture) Destroy(readbackTexture);
                readbackTexture = new Texture2D(spriteWidth, spriteHeight, TextureFormat.RGBA32, false, true);
            }

            readbackTexture.ReadPixels(new Rect(0, 0, spriteWidth, spriteHeight), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temp);

            Color32[] pixels = readbackTexture.GetPixels32();

            SilhouetteField built = new SilhouetteField();
            built.width = width;
            built.height = height;
            built.solid = new bool[width * height];
            built.distance = new int[width * height];
            built.edgeCells = new int[width * height];

            for (int y = 0; y < spriteHeight; y++)
            {
                for (int x = 0; x < spriteWidth; x++)
                {
                    if (SolidAlphaThreshold < pixels[y * spriteWidth + x].a)
                        built.solid[(y + marginPx) * width + (x + marginPx)] = true;
                }
            }

            BuildChamferDistance(built);
            BuildEdgeList(built);
            return built;
        }

        // 3-4 챔퍼 거리 변환: 실루엣에서 각 칸까지의 거리(직교 3, 대각 4). OverheatSilhouette.BuildDistanceField와 같은 방식이다.
        private static void BuildChamferDistance(SilhouetteField _field)
        {
            int w = _field.width;
            int h = _field.height;
            int[] d = _field.distance;

            for (int i = 0; i < d.Length; i++)
                d[i] = true == _field.solid[i] ? 0 : DistanceInfinity;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int index = y * w + x;
                    int best = d[index];
                    if (0 < x) best = Mathf.Min(best, d[index - 1] + 3);
                    if (0 < y) best = Mathf.Min(best, d[index - w] + 3);
                    if (0 < x && 0 < y) best = Mathf.Min(best, d[index - w - 1] + 4);
                    if (x < w - 1 && 0 < y) best = Mathf.Min(best, d[index - w + 1] + 4);
                    d[index] = best;
                }
            }

            for (int y = h - 1; y >= 0; y--)
            {
                for (int x = w - 1; x >= 0; x--)
                {
                    int index = y * w + x;
                    int best = d[index];
                    if (x < w - 1) best = Mathf.Min(best, d[index + 1] + 3);
                    if (y < h - 1) best = Mathf.Min(best, d[index + w] + 3);
                    if (x < w - 1 && y < h - 1) best = Mathf.Min(best, d[index + w + 1] + 4);
                    if (0 < x && y < h - 1) best = Mathf.Min(best, d[index + w - 1] + 4);
                    d[index] = best;
                }
            }
        }

        // 실루엣 가장자리(불투명 칸 중 상하좌우에 빈 칸이 있는 것) 목록
        private static void BuildEdgeList(SilhouetteField _field)
        {
            int w = _field.width;
            int h = _field.height;
            _field.edgeCellCount = 0;

            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    int index = y * w + x;
                    if (false == _field.solid[index]) continue;

                    if (false == _field.solid[index - 1] || false == _field.solid[index + 1] || false == _field.solid[index - w] || false == _field.solid[index + w])
                    {
                        _field.edgeCells[_field.edgeCellCount] = index;
                        _field.edgeCellCount++;
                    }
                }
            }
        }

        // ---------- 난수 ----------

        private float Rand01()
        {
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return ((randomState >> 8) & 0xFFFF) / 65535.0f;
        }

        private static float Hash01(int _a, int _b)
        {
            uint h = unchecked((uint)(_a * 73856093) ^ (uint)(_b * 19349663));
            h ^= h >> 13;
            h = unchecked(h * 0x5bd1e995u);
            h ^= h >> 15;
            return (h & 0xFFFF) / 65535.0f;
        }

        private static float ValueNoise(float _x, float _y)
        {
            int ix = Mathf.FloorToInt(_x);
            int iy = Mathf.FloorToInt(_y);
            float fx = _x - ix;
            float fy = _y - iy;
            fx = fx * fx * (3.0f - 2.0f * fx);
            fy = fy * fy * (3.0f - 2.0f * fy);

            float a = Hash01(ix, iy);
            float b = Hash01(ix + 1, iy);
            float c = Hash01(ix, iy + 1);
            float d = Hash01(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        // ---------- 갱신 ----------

        private static float EaseOut(float _t)
        {
            return 1.0f - (1.0f - _t) * (1.0f - _t);
        }

        // 불꽃 껍질의 전체 세기(0~1+). 점화 때는 잠깐 솟았다(플레어) 안착하고, 소화 때는 줄어든다. 1/12 단위 계단식이다.
        private float GetIntensity()
        {
            float rise = EaseOut(Mathf.Clamp01(elapsed / 0.35f));
            float flare = 0.6f * Mathf.Max(0.0f, 1.0f - Mathf.Abs(elapsed - 0.14f) / 0.14f);
            float intensity = rise + flare;

            if (AuraPhase.Extinguishing == phase)
            {
                float u = Mathf.Clamp01(extinguishElapsed / Mathf.Max(0.01f, extinguishDuration));
                intensity *= Mathf.Pow(1.0f - u, 1.4f);
            }

            return Mathf.Floor(intensity * 12.0f) / 12.0f;
        }

        private void SpawnEmber(float _x, float _y, float _vx, float _vy, float _life, int _layer, Color32 _color)
        {
            Ember ember = new Ember();
            ember.bActive = true;
            ember.layer = _layer;
            ember.x = _x;
            ember.y = _y;
            ember.vx = _vx;
            ember.vy = _vy;
            ember.swayPhase = Rand01() * TwoPi;
            ember.swaySpeed = Mathf.Lerp(5.0f, 9.0f, Rand01());
            ember.age = 0.0f;
            ember.life = _life;
            ember.kind = Rand01() < 0.45f ? 1 : 0;
            ember.seed = (int)(Rand01() * 1000.0f);
            ember.color = _color;

            embers[emberCursor] = ember;
            emberCursor = (emberCursor + 1) % EmberCapacity;
        }

        private Color32 RandomEmberColor()
        {
            float roll = Rand01();
            if (roll < 0.28f) return ColorCyan;
            if (roll < 0.5f) return ColorViolet;
            if (roll < 0.82f) return ColorIce;
            return ColorWhite;
        }

        // 불씨는 위로 올라가면서 좌우로 아지랑이처럼 흔들린다(사인 흔들림). 위로 갈수록 느려진다.
        private void UpdateEmbers(float _dt)
        {
            for (int i = 0; i < EmberCapacity; i++)
            {
                if (false == embers[i].bActive) continue;

                embers[i].age += _dt;
                if (embers[i].age >= embers[i].life)
                {
                    embers[i].bActive = false;
                    continue;
                }

                float sway = Mathf.Sin(embers[i].age * embers[i].swaySpeed + embers[i].swayPhase) * emberSwayAmplitude;
                embers[i].vy *= Mathf.Max(0.0f, 1.0f - 0.9f * _dt);
                embers[i].x += (embers[i].vx + sway) * _dt;
                embers[i].y += embers[i].vy * _dt;
            }
        }

        private void SpawnEmbers(float _dt)
        {
            // 점화 직후 몸에서 사방으로 터지는 불씨
            if (false == bIgniteEmbersSpawned && 0.05f <= elapsed)
            {
                bIgniteEmbersSpawned = true;
                int igniteCount = Mathf.Max(0, igniteEmberCount);
                for (int i = 0; i < igniteCount; i++)
                {
                    float angle = TwoPi * (i + Rand01()) / igniteCount;
                    float speed = Mathf.Lerp(20.0f, 56.0f, Rand01());
                    SpawnEmber(Mathf.Cos(angle) * 6.0f, bodyCenterYPx + Mathf.Sin(angle) * 8.0f, Mathf.Cos(angle) * speed, 10.0f + Mathf.Abs(Mathf.Sin(angle)) * speed, Mathf.Lerp(0.4f, 0.75f, Rand01()), FrontLayer, RandomEmberColor());
                }
            }

            int edgeCellCount = field.edgeCellCount;
            if (AuraPhase.Extinguishing == phase || 0 >= edgeCellCount) return;
            int[] edgeCells = field.edgeCells;

            // 실루엣 가장자리에서 피어오르는 불씨(위쪽 가장자리일수록 잘 나오게 한다)
            float interval = Mathf.Max(0.02f, ambientEmberInterval);
            emberSpawnAccumulator += _dt / interval;
            int groups = Mathf.Min(6, (int)emberSpawnAccumulator);
            emberSpawnAccumulator -= (int)emberSpawnAccumulator;

            for (int g = 0; g < groups; g++)
            {
                for (int n = 0; n < 2; n++)
                {
                    int cell = edgeCells[(int)(Rand01() * edgeCellCount) % edgeCellCount];
                    int cx = cell % field.width - field.width / 2;
                    int cy = cell / field.width;

                    float upward = Mathf.Clamp01((cy - crownStartYPx) / Mathf.Max(1.0f, spriteHeightPx * 0.4f));
                    if (Rand01() > Mathf.Lerp(0.35f, 1.0f, upward)) continue;

                    SpawnEmber(cx, cy + 1.0f, (Rand01() - 0.5f) * 6.0f, Mathf.Lerp(12.0f, 26.0f, Rand01()), Mathf.Lerp(0.5f, 0.95f, Rand01()), Rand01() < 0.5f ? FrontLayer : BackLayer, RandomEmberColor());
                }
            }
        }

        // ---------- 그리기 ----------

        private void Rebuild(int _meshFrame)
        {
            if (null != backGraphic) backGraphic.Clear();
            if (null != frontGraphic) frontGraphic.Clear();

            // 불꽃 일렁임(노이즈)은 flickerFps로, 불씨/반짝임/밝은 띠/충격파는 메쉬 프레임(meshFps)으로 움직인다.
            float fps = Mathf.Max(1.0f, flickerFps);
            int flickerFrame = (int)(elapsed * fps);
            float flickerTime = flickerFrame / fps;

            DrawIgniteRing();
            DrawSparkles(_meshFrame);
            DrawEmbers();
            DrawAura(flickerTime, flickerFrame, GetIntensity());

            if (null != backGraphic) backGraphic.Flush();
            if (null != frontGraphic) frontGraphic.Flush();
        }

        // 캐릭터 셰이더의 HDR 발광(정점 알파 0~1 -> 1~glowMax 배율)을 흉내 낸다: 팔레트를 밝기 배율로 키우고 채널을 255에서 자른다.
        private Color32 Tint(Color32 _color, float _glow, int _layer)
        {
            float g = Mathf.Clamp01(_glow * glowScale);
            float factor = Mathf.Lerp(1.0f, glowMax, g);
            return new Color32(
                (byte)Mathf.Min(255, Mathf.RoundToInt(_color.r * factor)),
                (byte)Mathf.Min(255, Mathf.RoundToInt(_color.g * factor)),
                (byte)Mathf.Min(255, Mathf.RoundToInt(_color.b * factor)),
                255);
        }

        private Color32 GetGradientColor(float _phase, float _shade)
        {
            float steps = Mathf.Max(2, gradientSteps);
            float wrapped = _phase - Mathf.Floor(_phase);
            float stepped = (Mathf.Floor(wrapped * steps) + 0.5f) / steps * GradientAnchors.Length;

            int index = Mathf.FloorToInt(stepped) % GradientAnchors.Length;
            float blend = stepped - Mathf.Floor(stepped);
            Color32 mixed = Color32.Lerp(GradientAnchors[index], GradientAnchors[(index + 1) % GradientAnchors.Length], blend);

            return new Color32((byte)(mixed.r * _shade), (byte)(mixed.g * _shade), (byte)(mixed.b * _shade), 255);
        }

        private void DrawAura(float _flickerTime, int _flickerFrame, float _intensity)
        {
            if (_intensity <= 0.0f || null == field) return;

            AuraFrame frame = CreateAuraFrame(_flickerTime, _flickerFrame, _intensity);
            int halfWidth = field.width / 2;

            for (int gy = 0; gy < field.height; gy++)
            {
                float crown = Mathf.Lerp(crownBaseWeight, 1.0f, Mathf.Clamp01((gy - crownStartYPx) / crownRampPx));

                int runStart = -1;
                Color32 runColor = default(Color32);
                bool bRunHasColor = false;

                for (int gx = 0; gx <= field.width; gx++)
                {
                    Color32 cellColor = default(Color32);
                    bool bDraw = gx < field.width && TryGetAuraCellColor(in frame, gx, gy, crown, halfWidth, out cellColor);

                    bool bSameAsRun = true == bDraw && true == bRunHasColor && runColor.r == cellColor.r && runColor.g == cellColor.g && runColor.b == cellColor.b;
                    if (true == bRunHasColor && false == bSameAsRun)
                    {
                        AddRect(AuraLayer, runStart - halfWidth, gy, gx - halfWidth, gy + 1, runColor);
                        bRunHasColor = false;
                    }

                    if (true == bDraw && false == bRunHasColor)
                    {
                        runStart = gx;
                        runColor = cellColor;
                        bRunHasColor = true;
                    }
                }
            }
        }

        private AuraFrame CreateAuraFrame(float _flickerTime, int _flickerFrame, float _intensity)
        {
            AuraFrame frame = new AuraFrame();
            frame.flickerTime = _flickerTime;
            frame.flickerFrame = _flickerFrame;
            frame.intensity = _intensity;
            frame.dissolve = Mathf.Clamp(dissolveStart, 0.05f, 0.95f);
            frame.ringThickness = Mathf.Max(1.0f, ringThicknessPx);

            frame.colorTime = elapsed * gradientSpeed;

            frame.maxReachPx = (frame.ringThickness + BodyBleedPx + crownExtraThicknessPx) * _intensity;
            return frame;
        }

        private bool TryGetAuraCellColor(in AuraFrame _frame, int _gx, int _gy, float _crown, int _halfWidth, out Color32 _color)
        {
            _color = default(Color32);

            int index = _gy * field.width + _gx;
            int dist = field.distance[index];

            if (0 == dist && true == IsEdgeCell(index))
            {
                // 실루엣 가장자리 안쪽 칸에도 링의 파랑을 깐다(도끼 이미지가 위에서 덮는다). 파손으로 스프라이트가 비는 칸의 빈틈을 메운다.
                _color = Tint(ColorBlue, ringGlow, AuraLayer);
                return true;
            }

            if (0 >= dist || dist > Mathf.CeilToInt(_frame.maxReachPx * DistanceUnitsPerPixel)) return false;

            float d = dist / (float)DistanceUnitsPerPixel;
            int cx = _gx - _halfWidth;
            int cy = _gy;

            float n1 = ValueNoise(cx * 0.34f, cy * 0.21f - _frame.flickerTime * noiseScrollSpeed);
            float n2 = ValueNoise(cx * 0.75f + 31.0f, cy * 0.46f - _frame.flickerTime * noiseScrollSpeed * 2.0f);

            float ring = (_frame.ringThickness + (n2 - 0.5f) * RingNoiseAmplitude) * _frame.intensity;
            float flame = _crown * crownExtraThicknessPx * (0.55f * n1 + 0.45f * n2) * _frame.intensity;
            float thick = ring + flame;
            if (d > thick || 0.0f >= thick) return false;

            if (d <= ring) return TryGetRingColor(d, ring, _gx, _gy, out _color);
            return TryGetFlameColor(in _frame, d, ring, thick, index, cx, cy, n2, out _color);
        }

        private bool IsEdgeCell(int _index)
        {
            int w = field.width;
            int x = _index % w;
            int y = _index / w;
            if (1 > x || x >= w - 1 || 1 > y || y >= field.height - 1) return false;
            if (false == field.solid[_index]) return false;

            return false == field.solid[_index - 1] || false == field.solid[_index + 1] || false == field.solid[_index - w] || false == field.solid[_index + w];
        }

        // 링: 한 가지 깊은 파랑. 안쪽 1px는 파랑, 그 바깥은 진파랑, 가장 바깥 절반은 체크무늬 디더로 옅어진다.
        private bool TryGetRingColor(float _d, float _ring, int _gx, int _gy, out Color32 _color)
        {
            if (_d <= 1.0f)
            {
                _color = Tint(ColorBlue, ringGlow, AuraLayer);
                return true;
            }

            if (_d <= _ring - 0.5f || 0 == ((_gx + _gy) & 1))
            {
                _color = Tint(ColorDeepBlue, ringGlow * 0.5f, AuraLayer);
                return true;
            }

            _color = default(Color32);
            return false;
        }

        // 불꽃: 링 바깥의 남은 두께에 대한 비율(r2)로 코어/중간/끝을 나눈다. 끝은 점이 무작위로 빠져 흩어진다(반투명 없음).
        private bool TryGetFlameColor(in AuraFrame _frame, float _d, float _ring, float _thick, int _index, int _cx, int _cy, float _n2, out Color32 _color)
        {
            _color = default(Color32);

            float accent = Hash01(_index + 977, _frame.flickerFrame / 2);
            float r2 = (_d - _ring) / Mathf.Max(0.01f, _thick - _ring);
            float dissolve = _frame.dissolve;
            bool bDissolved = r2 > dissolve && Hash01(_index, _frame.flickerFrame) < (r2 - dissolve) / (1.0f - dissolve) * 1.1f;
            if (true == bDissolved) return false;

            float angle01 = Mathf.Atan2(_cy - bodyCenterYPx, _cx) / TwoPi + 0.5f;
            float phaseValue = angle01 * gradientCycles + _frame.colorTime + (_n2 - 0.5f) * gradientNoise * 2.0f;

            float glow;
            Color32 color;
            if (r2 < 0.3f)
            {
                color = GetGradientColor(phaseValue, 1.0f);
                glow = 0.7f;
            }
            else if (r2 < 0.7f)
            {
                color = GetGradientColor(phaseValue + 0.33f, 0.85f);
                glow = 0.3f;
            }
            else
            {
                bool bPoint = accent < 0.25f;
                color = GetGradientColor(phaseValue + 0.66f, true == bPoint ? 1.0f : 0.7f);
                glow = true == bPoint ? 0.75f : 0.15f;
            }

            _color = Tint(color, glow, AuraLayer);
            return true;
        }

        // 실루엣 가장자리에서 프레임마다 튀는 밝은 불씨(몸 표면에서 에너지가 튀는 느낌). 앞 층에 그려서 도끼 위에 얹는다.
        private void DrawSparkles(int _flickerFrame)
        {
            int edgeCellCount = field.edgeCellCount;
            if (0 >= edgeCellCount || AuraPhase.Extinguishing == phase || 0 >= sparkleCount) return;
            int[] edgeCells = field.edgeCells;

            float ramp = Mathf.Clamp01((elapsed - 0.1f) / 0.3f);
            int count = Mathf.RoundToInt(sparkleCount * ramp);
            for (int i = 0; i < count; i++)
            {
                int cell = edgeCells[(int)(Hash01(i * 13 + 7, _flickerFrame) * edgeCellCount) % edgeCellCount];
                int cx = cell % field.width - field.width / 2;
                int cy = cell / field.width;

                bool bCyan = Hash01(i, _flickerFrame + 91) < 0.4f;
                AddRect(FrontLayer, cx, cy, cx + 1, cy + 1, Tint(true == bCyan ? ColorCyan : ColorWhite, 0.9f, FrontLayer));
            }
        }

        // 점화 순간의 점선 충격파: 도끼 중심의 타원으로 계단식으로 퍼지는 도트 고리
        private void DrawIgniteRing()
        {
            float t = elapsed - 0.02f;
            if (0.0f > t || 0.36f <= t) return;

            int step = (int)(t / 0.06f);
            float radius = (7.0f + step * 6.0f) * igniteRingScale;
            Color32 baseColor = step < 2 ? ColorWhite : (step < 4 ? ColorIce : ColorBlue);
            float glow = step < 2 ? 0.9f : (step < 4 ? 0.65f : 0.35f);
            int size = step < 3 ? 2 : 1;
            int dotCount = 20;
            float phaseShift = 0 == (step & 1) ? 0.0f : TwoPi / dotCount * 0.5f;

            for (int i = 0; i < dotCount; i++)
            {
                float angle = phaseShift + TwoPi * i / dotCount;
                float sin = Mathf.Sin(angle);
                int layer = sin < 0.0f ? FrontLayer : BackLayer;
                int px = Mathf.RoundToInt(Mathf.Cos(angle) * radius);
                int py = Mathf.RoundToInt(bodyCenterYPx + sin * radius * 0.6f);
                AddRect(layer, px, py, px + size, py + size, Tint(baseColor, glow, layer));
            }
        }

        // 불씨: 위로 올라가며 좌우로 흔들리고, `+`와 `x`는 번갈아 바뀌며 회전하는 것처럼 보인다. 후반에는 `.`로 작아졌다가 점멸하며 소멸한다.
        private void DrawEmbers()
        {
            for (int i = 0; i < EmberCapacity; i++)
            {
                if (false == embers[i].bActive) continue;

                float u = embers[i].age / embers[i].life;
                if (0.75f < u && 0 != ((int)(embers[i].age * 30.0f) & 1)) continue;

                int px = Mathf.RoundToInt(embers[i].x);
                int py = Mathf.RoundToInt(embers[i].y);

                int twinkle = ((int)(embers[i].age * 14.0f) + embers[i].seed) % 3;
                float glow = (0 == twinkle ? 0.5f : (1 == twinkle ? 0.7f : 0.9f)) * (1.0f - u * 0.4f);
                Color32 baseColor = 0.8f < u ? ColorBlue : embers[i].color;
                Color32 color = Tint(baseColor, glow, embers[i].layer);

                if (1 == embers[i].kind && u < 0.55f)
                {
                    bool bPlus = 0 == (((int)(embers[i].age / 0.09f)) + embers[i].seed & 1);
                    if (true == bPlus)
                    {
                        AddRect(embers[i].layer, px - 1, py, px + 2, py + 1, color);
                        AddRect(embers[i].layer, px, py - 1, px + 1, py + 2, color);
                    }
                    else
                    {
                        AddRect(embers[i].layer, px, py, px + 1, py + 1, color);
                        AddRect(embers[i].layer, px - 1, py - 1, px, py, color);
                        AddRect(embers[i].layer, px + 1, py - 1, px + 2, py, color);
                        AddRect(embers[i].layer, px - 1, py + 1, px, py + 2, color);
                        AddRect(embers[i].layer, px + 1, py + 1, px + 2, py + 2, color);
                    }
                }
                else
                {
                    AddRect(embers[i].layer, px, py, px + 1, py + 1, color);
                }
            }
        }

        // 격자 중심 기준 칸 좌표 직사각형 [x0,x1) x [y0,y1). 그래픽의 원점은 이미지 중심이라 y에서 격자 높이의 절반을 뺀다.
        private void AddRect(int _layer, int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            int halfHeight = field.height / 2;
            HUD_AxeAuraGraphic target = FrontLayer == _layer ? frontGraphic : backGraphic;
            if (null != target)
                target.AddRect(_x0, _y0 - halfHeight, _x1, _y1 - halfHeight, _color);
        }

        private bool HasActiveEmbers()
        {
            for (int i = 0; i < EmberCapacity; i++)
            {
                if (true == embers[i].bActive) return true;
            }

            return false;
        }

        private void StopAndClear()
        {
            if (null != backGraphic) { backGraphic.Clear(); backGraphic.Flush(); }
            if (null != frontGraphic) { frontGraphic.Clear(); frontGraphic.Flush(); }
            enabled = false;
        }

        // //유니티 이벤트 함수

        private void LateUpdate()
        {
            if (null == field)
                return;

            float dt = Time.deltaTime;
            elapsed += dt;

            if (AuraPhase.Extinguishing == phase)
            {
                extinguishElapsed += dt;
                if (extinguishElapsed >= extinguishDuration && false == HasActiveEmbers())
                {
                    StopAndClear();
                    return;
                }
            }

            // meshFps(30)로만 다시 만든다(UI 메쉬 리빌드 최소화). 불씨도 이 간격으로 움직인다.
            float fps = Mathf.Max(1.0f, meshFps);
            int meshFrame = (int)(elapsed * fps);
            if (meshFrame == lastFlickerFrame)
                return;

            // 게임 프레임이 meshFps보다 느리면 메쉬 프레임이 여러 개 건너뛰어진다. 그만큼의 시간을 한 번에 흘려야 불씨가 느려지지 않는다.
            // (-1은 점화/실루엣 교체 직후라 한 걸음만 간다. 긴 멈춤 뒤에는 상한을 두어 한 프레임에 몰아서 계산하지 않는다)
            int steps = 0 > lastFlickerFrame ? 1 : Mathf.Clamp(meshFrame - lastFlickerFrame, 1, 8);
            float stepDt = steps / fps;
            lastFlickerFrame = meshFrame;
            SpawnEmbers(stepDt);
            UpdateEmbers(stepDt);
            Rebuild(meshFrame);
        }

        private void OnDisable()
        {
            if (null != backGraphic) backGraphic.Clear();
            if (null != frontGraphic) frontGraphic.Clear();
        }

        private void OnDestroy()
        {
            if (null != readbackTexture)
                Destroy(readbackTexture);
        }
    }
}
