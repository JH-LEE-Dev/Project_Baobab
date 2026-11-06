using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 4스테이지 불씨 이펙트의 입자 묶음. 바닥 불씨 본체(VFX_OverheatEmber), 획득 뒤 남은 꼬리(OverheatEmberResidue),
    /// 단발 연출(VFX_OverheatEmberOneShot)이 같은 규칙으로 움직이고 그린다.
    ///
    /// 종류
    /// - 불티(Spark): 흰 노랑 -> 노랑 -> 주황 -> 빨강으로 식는다. `+` `x`는 수명 절반부터 `.`로 준다. 꼬리를 켜면 진행 반대쪽에 2px을 남긴다.
    /// - 조각(Chunk): `x` `+` `.` 불길 조각. 가운데 칸이 밝고 팔 4칸은 한 단계 어둡다. 수명 후반에 `.`로 줄고 끝 무렵 검붉게 식는다.
    /// - 그을음(Smoke): 회갈색 `.`.
    /// - 떨어지는 불티(Shed): 드랍 비행 꼬리에서 떨어져 나가 중력(60px/초²)으로 처지며 식는다. 흰 노랑 -> 노랑 -> 주황 -> 빨강,
    ///   `+` -> `x` -> `.`로 줄고 수명 끝 무렵 깜빡인다.
    ///
    /// 좌표는 월드 픽셀(실수, y 위쪽), 속도는 px/초. 고정 용량 구조체 배열이라 힙 할당이 없다. 가득 차면 새 입자는 버린다.
    /// </summary>
    public sealed class OverheatEmberParticles
    {
        private const int KindSpark = 0;
        private const int KindChunk = 1;
        private const int KindSmoke = 2;
        private const int KindShed = 3;

        private const float Gravity = 20.0f;
        private const float ShedGravity = 60.0f;

        private struct Particle
        {
            public int kind;
            public int shape;
            public bool bHot;
            public bool bTrail;
            public float x;
            public float y;
            public float vx;
            public float vy;
            public float age;
            public float life;
            public float drag;
            public float buoy;
            public float sway;
            // 떨어지는 불티의 깜빡임 위상(입자마다 다르게)
            public int tag;
        }

        // 내부 의존성
        private readonly Particle[] items;

        // 상태 변수
        private int count;

        public int Count => count;

        // 퍼블릭 초기화 및 제어 메서드

        public OverheatEmberParticles(int _capacity)
        {
            items = new Particle[_capacity];
        }

        public void Clear()
        {
            count = 0;
        }

        /// <summary>불티. _delay초 뒤 나타난다. _drag가 0보다 크면 감속하며 아래로 조금 처진다.</summary>
        public void SpawnSpark(float _x, float _y, float _vx, float _vy, float _life, int _shape, bool _bTrail, float _drag, float _delay, float _sway)
        {
            if (count >= items.Length) return;

            items[count] = new Particle
            {
                kind = KindSpark,
                shape = _shape,
                bTrail = _bTrail,
                x = _x,
                y = _y,
                vx = _vx,
                vy = _vy,
                age = -_delay,
                life = _life,
                drag = _drag,
                sway = _sway,
            };
            count++;
        }

        /// <summary>불길 조각. _bHot이면 주황에서 시작하고 아니면 빨강에서 시작한다. _buoy(px/초²)만큼 위로 떠오른다.</summary>
        public void SpawnChunk(float _x, float _y, float _vx, float _vy, float _life, int _shape, bool _bHot, float _drag, float _buoy, float _startAge, float _sway)
        {
            if (count >= items.Length) return;

            items[count] = new Particle
            {
                kind = KindChunk,
                shape = _shape,
                bHot = _bHot,
                x = _x,
                y = _y,
                vx = _vx,
                vy = _vy,
                age = _startAge,
                life = _life,
                drag = _drag,
                buoy = _buoy,
                sway = _sway,
            };
            count++;
        }

        /// <summary>떨어지는 불티. 중력으로 처지며 식는다. _tag는 깜빡임 위상이다.</summary>
        public void SpawnShed(float _x, float _y, float _vx, float _vy, float _life, int _shape, int _tag)
        {
            if (count >= items.Length) return;

            items[count] = new Particle
            {
                kind = KindShed,
                shape = _shape,
                x = _x,
                y = _y,
                vx = _vx,
                vy = _vy,
                life = _life,
                tag = _tag,
            };
            count++;
        }

        public void SpawnSmoke(float _x, float _y, float _vy, float _life, float _sway)
        {
            if (count >= items.Length) return;

            items[count] = new Particle
            {
                kind = KindSmoke,
                shape = PixelPainter.ShapeDot,
                x = _x,
                y = _y,
                vy = _vy,
                life = _life,
                sway = _sway,
            };
            count++;
        }

        /// <summary>살아 있는 입자를 _target으로 옮기고 이쪽은 비운다(불씨가 꺼져도 꼬리를 끝까지 그리기 위해).</summary>
        public void TransferTo(OverheatEmberParticles _target)
        {
            for (int i = 0; i < count; i++)
            {
                if (_target.count >= _target.items.Length) break;

                _target.items[_target.count] = items[i];
                _target.count++;
            }

            count = 0;
        }

        // 일반 비즈니스 로직 및 내부 메서드

        public void Step(float _dt, float _time)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                ref Particle p = ref items[i];
                p.age += _dt;

                if (p.age >= p.life)
                {
                    count--;
                    items[i] = items[count];
                    continue;
                }

                if (0.0f > p.age) continue;

                if (0.0f < p.drag)
                {
                    float damp = Mathf.Exp(-p.drag * _dt);
                    p.vx *= damp;
                    p.vy = p.vy * damp - Gravity * _dt;
                }

                if (KindShed == p.kind) p.vy -= ShedGravity * _dt;

                p.vy += p.buoy * _dt;
                // 좌우 흔들림은 30fps 기준 스텝당 0.12px을 시간 비례로 더한다(프레임 수와 무관하게 같은 폭)
                p.x += p.vx * _dt + Mathf.Sin(_time * 7.0f + p.sway) * 0.12f * _dt * 30.0f;
                p.y += p.vy * _dt;
            }
        }

        public void Draw(PixelPainter _painter, Color32[] _tones, Color32[] _darks, Color32 _smoke)
        {
            for (int i = 0; i < count; i++)
            {
                Particle p = items[i];
                if (0.0f > p.age) continue;

                float r = p.age / p.life;
                int x = Mathf.RoundToInt(p.x);
                int y = Mathf.RoundToInt(p.y);

                switch (p.kind)
                {
                    case KindSmoke:
                        _painter.Dot(x, y, _smoke);
                        break;

                    case KindChunk:
                        DrawChunk(_painter, p, r, x, y, _tones, _darks);
                        break;

                    case KindShed:
                        DrawShed(_painter, p, r, x, y, _tones);
                        break;

                    default:
                        DrawSpark(_painter, p, r, x, y, _tones);
                        break;
                }
            }
        }

        private static void DrawSpark(PixelPainter _painter, in Particle _p, float _r, int _x, int _y, Color32[] _tones)
        {
            int tone = _r < 0.25f ? 5 : (_r < 0.55f ? 4 : (_r < 0.8f ? 3 : 2));

            if (true == _p.bTrail && 8.0f < Mathf.Abs(_p.vx) + Mathf.Abs(_p.vy))
            {
                _painter.Dot(Mathf.RoundToInt(_p.x - _p.vx * 0.035f), Mathf.RoundToInt(_p.y - _p.vy * 0.035f), _tones[Mathf.Max(0, tone - 1)]);
                _painter.Dot(Mathf.RoundToInt(_p.x - _p.vx * 0.07f), Mathf.RoundToInt(_p.y - _p.vy * 0.07f), _tones[Mathf.Max(0, tone - 3)]);
            }

            int shape = _r < 0.5f ? _p.shape : PixelPainter.ShapeDot;
            DrawTwoTone(_painter, shape, _x, _y, _tones[tone], _tones[Mathf.Max(1, tone - 2)]);
        }

        private static void DrawChunk(PixelPainter _painter, in Particle _p, float _r, int _x, int _y, Color32[] _tones, Color32[] _darks)
        {
            int tone = true == _p.bHot
                ? (_r < 0.2f ? 3 : (_r < 0.45f ? 2 : (_r < 0.7f ? 1 : 0)))
                : (_r < 0.35f ? 2 : (_r < 0.7f ? 1 : 0));

            Color32 center = 0.86f < _r ? _darks[2] : _tones[tone];
            bool bBig = _r < (true == _p.bHot ? 0.5f : 0.35f);
            int shape = true == bBig ? _p.shape : PixelPainter.ShapeDot;

            DrawTwoTone(_painter, shape, _x, _y, center, _tones[Mathf.Max(0, tone - 1)]);
        }

        private static void DrawShed(PixelPainter _painter, in Particle _p, float _r, int _x, int _y, Color32[] _tones)
        {
            int tone = _r < 0.2f ? 5 : (_r < 0.45f ? 4 : (_r < 0.7f ? 3 : 2));

            int shape = _p.shape;
            if (PixelPainter.ShapePlus == shape && 0.5f < _r) shape = PixelPainter.ShapeCross;
            if (0.72f < _r) shape = PixelPainter.ShapeDot;
            // 수명 끝 무렵 30fps 간격으로 깜빡인다
            if (0.82f < _r && 0 != ((Mathf.FloorToInt(_p.age * 30.0f) + _p.tag) & 1)) return;

            DrawTwoTone(_painter, shape, _x, _y, _tones[tone], _tones[Mathf.Max(1, tone - 2)]);
        }

        // 가운데 칸은 _center, `+`/`x`의 팔 4칸은 _arm 색
        private static void DrawTwoTone(PixelPainter _painter, int _shape, int _x, int _y, Color32 _center, Color32 _arm)
        {
            if (PixelPainter.ShapePlus == _shape)
            {
                _painter.Dot(_x, _y - 1, _arm);
                _painter.Dot(_x - 1, _y, _arm);
                _painter.Dot(_x, _y, _center);
                _painter.Dot(_x + 1, _y, _arm);
                _painter.Dot(_x, _y + 1, _arm);
                return;
            }

            if (PixelPainter.ShapeCross == _shape)
            {
                _painter.Dot(_x - 1, _y - 1, _arm);
                _painter.Dot(_x + 1, _y - 1, _arm);
                _painter.Dot(_x, _y, _center);
                _painter.Dot(_x - 1, _y + 1, _arm);
                _painter.Dot(_x + 1, _y + 1, _arm);
                return;
            }

            _painter.Dot(_x, _y, _center);
        }
    }
}
