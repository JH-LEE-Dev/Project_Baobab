using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 카드 한 장에 붙는 작은 픽셀 입자 이펙트(착지 먼지·금빛 반짝). 프로젝트의 다른 이펙트(전리품 진화·별 비)와 같은 규칙을 따른다.
///   - 반투명이 없다. 입자는 항상 불투명이고, 나이가 들면 밝은 톤 → 중간 톤 → 어두운 톤으로 한 단계씩 내려가다 깜빡이며 사라진다.
///   - 한 색 계열은 3톤(먼지 / 금빛). 모양 자체에 명암(중심 100% · 팔 78%)이 있다.
///   - 위치는 적분하지 않고 시간에서 바로 계산한다(감속하며 뻗는 EaseOut + 중력 + 위로 뜸 + 좌우 흔들림). 깜빡임·톤 단계는 24Hz 틱으로 끊는다.
///   - 움직이는 입자는 지나온 자리에 한두 칸 어두운 잔상이 따라붙는다. 반짝은 1px → 3px 십자 → 5px 십자 → 3px → 1px 로 커졌다 줄어드는 팝이다.
///   - 한 번의 호출 안에서도 개수·방향·거리·시작 시각·수명이 비대칭으로 흩어진다(거리는 소수만 멀리 가는 분포, 멀리 가는 입자는 크고 밝고 일찍 사라진다).
///   - 무작위는 난수 상태 없이 호출 번호와 입자 번호의 해시로 정한다. 풀(particleImages)을 돌려 쓰고 힙 할당이 없으며, 살아 있는 입자가 없으면 Update가 곧바로 끝난다.
/// 입자 하나는 이미지 3장(머리 · 잔상 둘)을 쓴다. 좌표는 이 오브젝트의 RectTransform(= 카드 왼쪽 아래 기준, 앵커·피벗 왼쪽 아래) 기준 픽셀이다.
/// </summary>
public class UI_RoguelikePixelFx : MonoBehaviour
{
    private enum EShape : byte
    {
        Dot1,
        Dot2,
        Bar2,
        PlusSmall,
        PlusLarge,
    }

    private struct Particle
    {
        public bool bActive;
        public float delay;
        public float age;
        public float life;
        public Vector2 origin;
        public Vector2 direction;
        public float reach;
        public float drag;
        public float gravity;
        public float rise;
        public float swayAmplitude;
        public float swayPhase;
        public EShape shape;
        public bool bGold;
        public bool bPop;
        public bool bTrail;
        public byte toneBias;
    }

    // 먼지 3톤 / 금빛 3톤(밝음 → 어두움). 문양의 금색(217,166,64 · 243,204,89)과 같은 계열이다.
    private static readonly Color[] DustTones =
    {
        new Color(0.91f, 0.85f, 0.72f, 1f),
        new Color(0.79f, 0.71f, 0.54f, 1f),
        new Color(0.56f, 0.48f, 0.35f, 1f),
    };

    private static readonly Color[] GoldTones =
    {
        new Color(1f, 0.93f, 0.62f, 1f),
        new Color(0.95f, 0.8f, 0.35f, 1f),
        new Color(0.85f, 0.65f, 0.25f, 1f),
    };

    private const int ImagesPerParticle = 3;
    private const float TicksPerSecond = 24f;
    private const float TrailStep = 0.07f;

    //외부 의존성 (프리팹에서 연결)
    [SerializeField] private Image[] particleImages = new Image[0];
    [SerializeField] private Sprite dot1Sprite;
    [SerializeField] private Sprite dot2Sprite;
    [SerializeField] private Sprite bar2Sprite;
    [SerializeField] private Sprite plusSmallSprite;
    [SerializeField] private Sprite plusLargeSprite;

    //상태 변수
    private Particle[] particles;
    private int emitCounter;
    private int activeCount;

    /// <summary>
    /// 착지. 그림자 자리에서 양옆으로 먼지가 튀고(대부분 가깝고 소수만 멀리), 바닥을 따라 미끄러지는 줄기와 천천히 떠오르는 먼지, 위로 오르는 금빛 반짝이 따른다.
    /// _groundY는 그림자가 놓인 바닥 높이, _centerX는 카드 가운데.
    /// </summary>
    public void EmitLanding(float _centerX, float _groundY)
    {
        EnsureBuffers();
        int seed = ++emitCounter;

        for (int side = -1; side <= 1; side += 2)
        {
            // 한 쪽이 더 세게 튄다(0.75~1.25배).
            float sideBias = 0.75f + 0.5f * Hash01(seed, 900 + side);
            int count = 3 + (int)(Hash01(seed, 910 + side) * 3f);

            for (int i = 0; i < count; i++)
            {
                int key = seed * 131 + (side + 1) * 29 + i;
                float far = Hash01(key, 1);
                float reach = (5f + 34f * Mathf.Pow(far, 2.2f)) * sideBias;
                float angle = (8f + 42f * Hash01(key, 2)) * Mathf.Deg2Rad;

                Particle p = default;
                p.origin = new Vector2(_centerX + side * (16f + Hash01(key, 3) * 9f), _groundY + 1f + Hash01(key, 4) * 2f);
                p.direction = new Vector2(side * Mathf.Cos(angle), Mathf.Sin(angle));
                p.reach = reach;
                p.drag = 2.4f;
                p.gravity = 4f + 10f * Hash01(key, 5);
                p.life = Mathf.Clamp(0.56f - 0.26f * (reach / 40f), 0.26f, 0.56f) + Hash01(key, 6) * 0.06f;
                p.delay = Hash01(key, 7) * 0.14f;
                p.shape = reach > 22f ? EShape.Dot2 : (reach > 10f && Hash01(key, 8) < 0.5f ? EShape.Dot2 : EShape.Dot1);
                p.toneBias = (byte)(reach > 14f ? 0 : 1);
                p.bTrail = reach > 18f;
                Spawn(in p);
            }

            // 바닥을 따라 미끄러지는 낮은 줄기.
            {
                int key = seed * 131 + 60 + side;
                Particle p = default;
                p.origin = new Vector2(_centerX + side * (18f + Hash01(key, 1) * 6f), _groundY + 1f);
                p.direction = new Vector2(side, 0.05f);
                p.reach = 16f + Hash01(key, 2) * 12f;
                p.drag = 2.8f;
                p.life = 0.3f + Hash01(key, 3) * 0.08f;
                p.delay = 0.02f + Hash01(key, 4) * 0.05f;
                p.shape = EShape.Bar2;
                p.toneBias = 1;
                Spawn(in p);
            }
        }

        // 천천히 떠오르는 먼지(좌우로 흔들린다).
        for (int i = 0; i < 2; i++)
        {
            int key = seed * 131 + 70 + i;
            Particle p = default;
            p.origin = new Vector2(_centerX + (Hash01(key, 1) - 0.5f) * 40f, _groundY + 2f);
            p.direction = new Vector2(0f, 1f);
            p.rise = 8f + Hash01(key, 2) * 10f;
            p.drag = 1.6f;
            p.swayAmplitude = 1.5f + Hash01(key, 3) * 1.5f;
            p.swayPhase = Hash01(key, 4) * 6.28f;
            p.life = 0.5f + Hash01(key, 5) * 0.2f;
            p.delay = 0.05f + Hash01(key, 6) * 0.1f;
            p.shape = EShape.Dot1;
            p.toneBias = 1;
            Spawn(in p);
        }

        // 금빛 반짝: 3~4개가 서로 다른 시각에 팝으로 켜졌다 꺼지며 천천히 오른다.
        int sparks = 3 + (Hash01(seed, 920) < 0.5f ? 1 : 0);
        for (int i = 0; i < sparks; i++)
        {
            int key = seed * 131 + 97 + i;
            Particle p = default;
            p.origin = new Vector2(_centerX + (Hash01(key, 1) - 0.5f) * 46f, _groundY + 6f + Hash01(key, 2) * 22f);
            p.direction = new Vector2(0f, 1f);
            p.rise = 6f + Hash01(key, 3) * 8f;
            p.drag = 2f;
            p.swayAmplitude = 1.5f;
            p.swayPhase = Hash01(key, 4) * 6.28f;
            p.life = 0.3f + Hash01(key, 5) * 0.2f;
            p.delay = 0.04f + Hash01(key, 6) * 0.22f;
            p.bGold = true;
            p.bPop = true;
            Spawn(in p);
        }
    }

    /// <summary>살아 있는 입자를 모두 지운다(카드가 새로 채워질 때·꺼질 때·다른 연출이 끼어들 때).</summary>
    public void Clear()
    {
        if (null != particles)
        {
            for (int i = 0; i < particles.Length; i++) particles[i].bActive = false;
        }

        for (int i = 0; i < particleImages.Length; i++)
        {
            if (null != particleImages[i]) particleImages[i].enabled = false;
        }

        activeCount = 0;
    }

    private void EnsureBuffers()
    {
        if (null == particles) particles = new Particle[particleImages.Length / ImagesPerParticle];
    }

    /// <summary>비어 있는(죽은) 칸에 입자를 넣는다. 풀이 꽉 차면 이번 입자를 버린다(풀 크기가 상한).</summary>
    private void Spawn(in Particle _particle)
    {
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i].bActive) continue;

            particles[i] = _particle;
            particles[i].bActive = true;
            particles[i].age = -_particle.delay;
            activeCount++;
            return;
        }
    }

    private void Step(float _dt)
    {
        int alive = 0;

        for (int i = 0; i < particles.Length; i++)
        {
            if (false == particles[i].bActive) continue;

            ref Particle p = ref particles[i];
            p.age += _dt;

            if (p.age >= p.life)
            {
                p.bActive = false;
                HideSlot(i);
                continue;
            }

            alive++;
            if (p.age < 0f)
            {
                HideSlot(i);
                continue;
            }

            Apply(i, in p);
        }

        activeCount = alive;
    }

    private void Apply(int _slot, in Particle _p)
    {
        float u = _p.age / _p.life;
        int tick = (int)(_p.age * TicksPerSecond);

        // 수명 끝 무렵에는 3틱 중 1틱씩 꺼지며 깜빡이다 사라진다.
        if (0.7f < u && 0 == (tick + _slot) % 3)
        {
            HideSlot(_slot);
            return;
        }

        int stage = u < 0.4f ? 0 : (u < 0.75f ? 1 : 2);
        int toneIndex = Mathf.Min(2, stage + _p.toneBias);
        Color[] palette = _p.bGold ? GoldTones : DustTones;

        EShape shape = _p.shape;
        if (_p.bPop) shape = u < 0.12f ? EShape.Dot1 : (u < 0.28f ? EShape.PlusSmall : (u < 0.6f ? EShape.PlusLarge : (u < 0.8f ? EShape.PlusSmall : EShape.Dot1)));

        PlaceImage(particleImages[_slot * ImagesPerParticle], GetSprite(shape), Position(in _p, u), palette[toneIndex]);

        // 잔상: 조금 전 위치에 한 칸씩 더 어두운 톤. 움직임이 아직 큰 동안에만 보인다.
        for (int k = 1; k < ImagesPerParticle; k++)
        {
            Image trail = particleImages[_slot * ImagesPerParticle + k];
            float tu = u - k * TrailStep;
            if (false == _p.bTrail || tu <= 0.02f || 0.8f < u)
            {
                trail.enabled = false;
                continue;
            }

            PlaceImage(trail, dot1Sprite, Position(in _p, tu), palette[Mathf.Min(2, toneIndex + k)]);
        }
    }

    /// <summary>시간(u = 0~1)에서 바로 위치를 계산한다: 감속하며 뻗기 + 중력 + 위로 뜸 + 좌우 흔들림.</summary>
    private static Vector2 Position(in Particle _p, float _u)
    {
        float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(_u), _p.drag);
        float x = _p.origin.x + _p.direction.x * _p.reach * e + Mathf.Sin(_u * 6.2832f * 1.3f + _p.swayPhase) * _p.swayAmplitude * _u;
        float y = _p.origin.y + _p.direction.y * _p.reach * e - _p.gravity * _u * _u + _p.rise * e;
        return new Vector2(x, y);
    }

    private static void PlaceImage(Image _image, Sprite _sprite, Vector2 _position, Color _color)
    {
        if (null == _sprite)
        {
            _image.enabled = false;
            return;
        }

        Vector2 size = _sprite.rect.size;
        _image.enabled = true;
        _image.sprite = _sprite;
        _image.rectTransform.sizeDelta = size;
        _image.rectTransform.anchoredPosition = new Vector2(Mathf.Round(_position.x) - Mathf.Floor(size.x * 0.5f), Mathf.Round(_position.y) - Mathf.Floor(size.y * 0.5f));
        _image.color = _color;
    }

    private void HideSlot(int _slot)
    {
        for (int k = 0; k < ImagesPerParticle; k++) particleImages[_slot * ImagesPerParticle + k].enabled = false;
    }

    private Sprite GetSprite(EShape _shape)
    {
        switch (_shape)
        {
            case EShape.Dot1: return dot1Sprite;
            case EShape.Dot2: return dot2Sprite;
            case EShape.Bar2: return bar2Sprite;
            case EShape.PlusSmall: return plusSmallSprite;
            default: return plusLargeSprite;
        }
    }

    /// <summary>두 정수의 해시를 0~1로. 연속한 번호가 겹치지 않도록 섞는 마무리 단계가 있다.</summary>
    private static float Hash01(int _a, int _b)
    {
        unchecked
        {
            uint h = 2166136261u;
            h = (h ^ (uint)_a) * 16777619u;
            h = (h ^ (uint)_b) * 16777619u;
            h ^= h >> 15;
            h *= 0x2c1b3c6du;
            h ^= h >> 12;
            h *= 0x297a2d39u;
            h ^= h >> 15;
            return (h & 0xFFFFFFu) / 16777216f;
        }
    }

    private void Update()
    {
        if (0 >= activeCount || null == particles) return;

        Step(Mathf.Min(Time.unscaledDeltaTime, 1f / 20f));
    }

    private void OnDisable()
    {
        Clear();
    }
}
