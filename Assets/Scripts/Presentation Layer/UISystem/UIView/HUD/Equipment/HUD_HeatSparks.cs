using UnityEngine;
using UnityEngine.UI;

namespace PresentationLayer.UISystem.UIView.HUD.Equipment
{
    /// <summary>
    /// 과열 게이지 바의 불티('.' '+'). 채움 꼭대기에서 튀어 오른다.
    /// 1~3단계는 바 안쪽 빈칸에서만 날고 사라지며, 4단계와 단계가 오를 때의 버스트는 바 밖(숫자 위)까지 튄다.
    /// 21x80 CPU 텍스처(RawImage)에 직접 찍는다. 바(9x50)는 텍스처의 (6, 0)부터 놓인다고 보고 좌표를 계산한다.
    /// 30fps 고정 스텝, unscaled 시간. 입자가 없고 직전 그림도 비었으면 업로드하지 않는다.
    /// </summary>
    public class HUD_HeatSparks : MonoBehaviour
    {
        private const int TexWidth = 21;
        private const int TexHeight = 80;
        private const int BarOffsetX = 6;
        private const int Capacity = 64;
        private const float StepSeconds = 1f / 30f;
        private const float MaxCatchUpSeconds = 0.25f;

        // 바 안쪽(채움 칸) 범위. 바 로컬 px 기준.
        private const float InnerMinX = 2f;
        private const float InnerMaxX = 7f;
        private const float InnerTopY = 47f;
        private const float InnerBottomY = 2f;

        private struct Spark
        {
            public float x;
            public float y;
            public float vx;
            public float vy;
            public float life;
            public float maxLife;
            public bool bPlus;
            public bool bInside;
        }

        // //외부 의존성
        [SerializeField] private RawImage image;
        [Tooltip("수명 비율 0~35% / 35~70% / 70~100%의 색")]
        [SerializeField] private Color32 sparkHotColor = new Color32(255, 243, 166, 255);
        [SerializeField] private Color32 sparkWarmColor = new Color32(255, 201, 60, 255);
        [SerializeField] private Color32 sparkCoolColor = new Color32(255, 138, 30, 255);

        // //내부 의존성
        private readonly Spark[] sparks = new Spark[Capacity];
        private Texture2D texture;
        private Color32[] buffer;

        // //상태 변수
        private bool bReady = false;
        private int activeCount = 0;
        private int stage = 0;
        private float spawnPerSecond = 0f;
        private float spawnAccumulator = 0f;
        private float stepAccumulator = 0f;
        private int fillPixels = 0;
        private bool bUploadedEmpty = true;
        private uint randomState = 0x9E3779B9u;

        // //퍼블릭 초기화 및 제어 메서드

        public void Initialize()
        {
            if (true == bReady || null == image)
                return;

            texture = new Texture2D(TexWidth, TexHeight, TextureFormat.RGBA32, false);
            texture.name = "HUD_HeatSparks";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            buffer = new Color32[TexWidth * TexHeight];
            texture.SetPixelData(buffer, 0);
            texture.Apply(false);

            image.texture = texture;
            image.raycastTarget = false;
            bReady = true;
        }

        /// <summary>게이지 단계와 초당 생성 수. 0단계거나 생성 수가 0이면 새로 생기지 않는다(날던 불티는 끝까지 난다).</summary>
        public void SetStage(int _stage, float _spawnPerSecond)
        {
            stage = _stage;
            spawnPerSecond = 0 < _stage ? Mathf.Max(0f, _spawnPerSecond) : 0f;

            if (0f >= spawnPerSecond)
                spawnAccumulator = 0f;
        }

        /// <summary>지금 채움 높이(px). 불티는 이 꼭대기에서 생긴다.</summary>
        public void SetFillPixels(int _fillPixels)
        {
            fillPixels = _fillPixels;
        }

        /// <summary>단계가 오를 때 한꺼번에 터뜨린다. 버스트 불티는 바 밖까지 튄다.</summary>
        public void Burst(int _count)
        {
            for (int i = 0; i < _count; i++)
            {
                Spawn(true);
            }
        }

        /// <summary>모든 불티를 지운다(숨김 · 값 초기화).</summary>
        public void Clear()
        {
            activeCount = 0;
            spawnAccumulator = 0f;
            stepAccumulator = 0f;
            Redraw();
        }

        // //일반 비즈니스 로직 및 내부 메서드

        private float NextRandom()
        {
            // xorshift32 (힙 할당 없는 난수)
            randomState ^= randomState << 13;
            randomState ^= randomState >> 17;
            randomState ^= randomState << 5;
            return (randomState & 0xFFFFFF) / 16777216f;
        }

        private void Spawn(bool _bBurst)
        {
            if (Capacity <= activeCount)
                return;

            bool _bEscape = 4 <= stage || true == _bBurst;

            Spark _s = new Spark();
            _s.x = InnerMinX + Mathf.Floor(NextRandom() * 5f);
            _s.y = InnerBottomY + fillPixels;
            _s.vy = (true == _bEscape ? 18f : 6f) + NextRandom() * (true == _bEscape ? 22f : 8f) + stage * 2f;
            _s.vx = (NextRandom() - 0.5f) * (true == _bEscape ? 10f : 2f);
            _s.life = 0f;
            _s.maxLife = true == _bEscape ? 0.5f + NextRandom() * 0.5f : 0.25f + NextRandom() * 0.35f;
            _s.bPlus = true == _bBurst ? 0.5f > NextRandom() : (3 <= stage && 0.35f > NextRandom());
            _s.bInside = false == _bEscape;

            sparks[activeCount++] = _s;
        }

        private void StepSimulation(float _dt)
        {
            if (0f < spawnPerSecond)
            {
                spawnAccumulator += _dt * spawnPerSecond;

                while (1f <= spawnAccumulator)
                {
                    spawnAccumulator -= 1f;
                    Spawn(false);
                }
            }

            for (int i = activeCount - 1; i >= 0; i--)
            {
                Spark _s = sparks[i];
                _s.life += _dt;
                _s.y += _s.vy * _dt;
                _s.x += _s.vx * _dt;
                _s.vy *= 0.96f;

                bool _bDead = _s.life >= _s.maxLife || (true == _s.bInside && InnerTopY < _s.y);

                if (true == _bDead)
                {
                    // 마지막 원소로 덮어 지운다(순서 무관).
                    activeCount--;
                    sparks[i] = sparks[activeCount];
                    continue;
                }

                sparks[i] = _s;
            }
        }

        private void Redraw()
        {
            if (false == bReady)
                return;

            if (0 == activeCount && true == bUploadedEmpty)
                return;

            System.Array.Clear(buffer, 0, buffer.Length);

            for (int i = 0; i < activeCount; i++)
            {
                Spark _s = sparks[i];

                if (true == _s.bInside && (InnerTopY < _s.y || InnerMinX > _s.x || InnerMaxX <= _s.x))
                    continue;

                int _x = Mathf.RoundToInt(_s.x) + BarOffsetX;
                int _y = Mathf.RoundToInt(_s.y);
                float _k = _s.life / _s.maxLife;
                Color32 _c = 0.35f > _k ? sparkHotColor : (0.7f > _k ? sparkWarmColor : sparkCoolColor);

                Plot(_x, _y, _c);

                if (true == _s.bPlus)
                {
                    Plot(_x - 1, _y, _c);
                    Plot(_x + 1, _y, _c);
                    Plot(_x, _y - 1, _c);
                    Plot(_x, _y + 1, _c);
                }
            }

            texture.SetPixelData(buffer, 0);
            texture.Apply(false);
            bUploadedEmpty = 0 == activeCount;
        }

        private void Plot(int _x, int _y, Color32 _c)
        {
            if (0 > _x || TexWidth <= _x || 0 > _y || TexHeight <= _y)
                return;

            buffer[_y * TexWidth + _x] = _c;
        }

        // //유니티 라이프사이클 이벤트 함수

        private void Update()
        {
            if (false == bReady)
                return;

            stepAccumulator = Mathf.Min(stepAccumulator + Time.unscaledDeltaTime, MaxCatchUpSeconds);

            if (StepSeconds > stepAccumulator)
                return;

            while (StepSeconds <= stepAccumulator)
            {
                stepAccumulator -= StepSeconds;
                StepSimulation(StepSeconds);
            }

            Redraw();
        }

        private void OnDisable()
        {
            // 숨겨질 때 날던 불티를 남기지 않는다(다시 켜지면 빈 상태에서 시작).
            activeCount = 0;
            spawnAccumulator = 0f;
            stepAccumulator = 0f;
            Redraw();
        }

        private void OnDestroy()
        {
            if (null != texture)
                Destroy(texture);
        }
    }
}
