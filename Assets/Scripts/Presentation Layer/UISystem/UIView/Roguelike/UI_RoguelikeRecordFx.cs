using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기록 창의 작은 입자(톱밥 · 반짝임 · 불티 · 먼지). UI Image 풀(최대 PoolSize개)을 돌려 쓴다.
///   모양은 x + . ● ◆ 다섯 가지(흰 단색 그림, Graphics/Roguelike/records/Fx_*). 단색이라 Image.color를 곱해도 중간색이 생기지 않는다.
///   움직임은 30fps 단계로만 갱신하고 위치는 정수 픽셀로 반올림한다(서브 픽셀 방지). 시간은 unscaled.
/// 좌표는 이 컴포넌트의 RectTransform(왼쪽 위 기준) 안에서 x 오른쪽 + , y 아래쪽 + 로 받는다.
/// </summary>
public class UI_RoguelikeRecordFx : MonoBehaviour
{
    public const int PoolSize = 24;
    private const float StepSeconds = 1f / 30f;
    private const float LateColorRatio = 0.6f;
    private const float VelocityDamping = 0.9f;

    public enum EShape
    {
        Dot = 0,
        Dot2 = 1,
        Plus = 2,
        Cross = 3,
        Diamond = 4,
    }

    /// <summary>입자 한 번 뿌리기 설정. 값 형식이라 할당이 없다.</summary>
    public struct EmitSettings
    {
        public float angle;
        public float spread;
        public float speed;
        public float gravity;
        public float life;
        public EShape shapeA;
        public EShape shapeB;
        public EShape shapeC;
        public Color colorA;
        public Color colorB;
        public Color lateColor;
    }

    private struct Particle
    {
        public bool bAlive;
        public Vector2 position;
        public Vector2 velocity;
        public float life;
        public float maxLife;
        public float gravity;
        public Color color;
        public Color lateColor;
        public int shape;
    }

    //외부 의존성 (프리팹에서 연결)
    [SerializeField] private Image[] images = new Image[PoolSize];
    [SerializeField, Tooltip("EShape 순서의 흰 단색 입자 그림.")] private Sprite[] shapeSprites = new Sprite[5];

    //상태 변수
    private readonly Particle[] particles = new Particle[PoolSize];
    private float accumulator;

    public void Emit(Vector2 _position, int _count, in EmitSettings _settings)
    {
        for (int n = 0; _count > n; n++)
        {
            int slot = FindFreeSlot();
            if (0 > slot) return;

            float angle = _settings.angle + (Random.value - 0.5f) * _settings.spread;
            float speed = _settings.speed * (0.5f + Random.value);
            int pick = n % 3;
            EShape shape = 0 == pick ? _settings.shapeA : (1 == pick ? _settings.shapeB : _settings.shapeC);

            particles[slot] = new Particle
            {
                bAlive = true,
                position = _position,
                velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed),
                life = 0f,
                maxLife = _settings.life * (0.7f + Random.value * 0.6f),
                gravity = _settings.gravity,
                color = 0 == n % 2 ? _settings.colorA : _settings.colorB,
                lateColor = _settings.lateColor,
                shape = (int)shape,
            };

            Image image = images[slot];
            if (null == image) continue;

            Sprite sprite = shapeSprites.Length > particles[slot].shape ? shapeSprites[particles[slot].shape] : null;
            image.sprite = sprite;
            if (null != sprite) image.rectTransform.sizeDelta = sprite.rect.size;
            image.color = particles[slot].color;
            image.gameObject.SetActive(true);
            Place(slot);
        }
    }

    /// <summary>모든 입자를 끈다(창을 닫거나 탭을 넘길 때).</summary>
    public void Clear()
    {
        accumulator = 0f;
        for (int i = 0; PoolSize > i; i++)
        {
            particles[i].bAlive = false;
            if (null != images[i] && true == images[i].gameObject.activeSelf) images[i].gameObject.SetActive(false);
        }
    }

    private int FindFreeSlot()
    {
        for (int i = 0; PoolSize > i; i++)
        {
            if (false == particles[i].bAlive) return i;
        }
        return -1;
    }

    /// <summary>입자 중심을 정수 픽셀로 반올림하고 그림의 왼쪽 위를 맞춘다(홀수 크기는 내림).</summary>
    private void Place(int _slot)
    {
        Image image = images[_slot];
        if (null == image) return;

        Vector2 size = image.rectTransform.sizeDelta;
        float x = Mathf.Round(particles[_slot].position.x) - Mathf.Floor(size.x * 0.5f);
        float y = Mathf.Round(particles[_slot].position.y) - Mathf.Floor(size.y * 0.5f);
        image.rectTransform.anchoredPosition = new Vector2(x, -y);
    }

    private void Step()
    {
        for (int i = 0; PoolSize > i; i++)
        {
            if (false == particles[i].bAlive) continue;

            Particle p = particles[i];
            p.life += StepSeconds;
            if (p.life >= p.maxLife)
            {
                p.bAlive = false;
                particles[i] = p;
                if (null != images[i]) images[i].gameObject.SetActive(false);
                continue;
            }

            p.position += p.velocity * StepSeconds;
            p.velocity.y += p.gravity * StepSeconds;
            p.velocity.x *= VelocityDamping;
            particles[i] = p;

            if (null == images[i]) continue;
            Color color = LateColorRatio < p.life / p.maxLife ? p.lateColor : p.color;
            if (images[i].color != color) images[i].color = color;
            Place(i);
        }
    }

    private void OnDisable()
    {
        Clear();
    }

    private void Update()
    {
        // 멈췄다 돌아온 프레임(창을 다시 연 직후 등)에서 한꺼번에 몰아 돌지 않게 최대 6단계만 따라잡는다.
        accumulator = Mathf.Min(accumulator + Time.unscaledDeltaTime, StepSeconds * 6f);
        while (StepSeconds <= accumulator)
        {
            accumulator -= StepSeconds;
            Step();
        }
    }
}
