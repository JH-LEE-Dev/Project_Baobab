using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 픽셀 타원 윤곽 도우미. 모든 바닥 타원은 이 한 가지 midpoint 알고리즘으로만 그려서 곡선이 흔들리지 않는다(JAGGIES).
    /// 윤곽은 L자 모서리 픽셀을 제거해 1px 굵기를 유지한다(DOUBLE PIXEL).
    ///
    /// Build는 중심 기준 정수 오프셋을 정적 배열(OutX/OutY)에 각도 순서(동쪽에서 시작해 반시계, y가 위)로 채운다.
    /// 호출이 끝나기 전에 다음 Build를 부르면 앞선 결과가 덮어써지므로, 호출 측은 Build 직후 바로 읽는다(메인 스레드 단일 실행).
    /// 정적 배열만 써서 재생 중 힙 할당이 없다.
    /// </summary>
    public static class PixelEllipse
    {
        public const int MaxRadius = 480;

        private const int QuadrantCapacity = MaxRadius * 2 + 8;
        private const int RingCapacity = QuadrantCapacity * 4;

        public static readonly int[] OutX = new int[RingCapacity];
        public static readonly int[] OutY = new int[RingCapacity];

        private static readonly int[] quadrantX = new int[QuadrantCapacity];
        private static readonly int[] quadrantY = new int[QuadrantCapacity];
        private static readonly int[] rawX = new int[RingCapacity];
        private static readonly int[] rawY = new int[RingCapacity];

        /// <summary>
        /// 가로 반지름 _rx, 세로 반지름 _ry인 타원 윤곽을 OutX/OutY에 만들고 점 개수를 돌려준다.
        /// </summary>
        public static int Build(int _rx, int _ry)
        {
            if (_rx < 1)
            {
                OutX[0] = 0;
                OutY[0] = 0;
                return 1;
            }

            int rx = Mathf.Min(_rx, MaxRadius);
            int ry = Mathf.Clamp(_ry, 1, MaxRadius);

            // 1사분면: (0, ry) -> (rx, 0) 순서의 midpoint 타원
            int n = 0;
            double rx2 = (double)rx * rx;
            double ry2 = (double)ry * ry;
            int x = 0;
            int y = ry;
            double dx = 0.0;
            double dy = 2.0 * rx2 * y;
            double p = ry2 - rx2 * ry + 0.25 * rx2;

            while (dx < dy && n < QuadrantCapacity)
            {
                quadrantX[n] = x;
                quadrantY[n] = y;
                n++;
                x++;
                dx += 2.0 * ry2;
                if (p < 0.0)
                {
                    p += ry2 + dx;
                }
                else
                {
                    y--;
                    dy -= 2.0 * rx2;
                    p += ry2 + dx - dy;
                }
            }

            p = ry2 * (x + 0.5) * (x + 0.5) + rx2 * (y - 1.0) * (y - 1.0) - rx2 * ry2;
            while (0 <= y && n < QuadrantCapacity)
            {
                quadrantX[n] = x;
                quadrantY[n] = y;
                n++;
                y--;
                dy -= 2.0 * rx2;
                if (0.0 < p)
                {
                    p += rx2 - dy;
                }
                else
                {
                    x++;
                    dx += 2.0 * ry2;
                    p += rx2 - dy + dx;
                }
            }

            // 네 사분면을 동쪽에서 반시계로 이어 붙인다(축 위의 점은 한 번만)
            int m = 0;
            for (int i = n - 1; i >= 0; i--)
            {
                rawX[m] = quadrantX[i];
                rawY[m] = quadrantY[i];
                m++;
            }

            for (int i = 1; i < n; i++)
            {
                rawX[m] = -quadrantX[i];
                rawY[m] = quadrantY[i];
                m++;
            }

            for (int i = n - 2; i >= 0; i--)
            {
                rawX[m] = -quadrantX[i];
                rawY[m] = -quadrantY[i];
                m++;
            }

            for (int i = 1; i < n - 1; i++)
            {
                rawX[m] = quadrantX[i];
                rawY[m] = -quadrantY[i];
                m++;
            }

            // 앞뒤 픽셀이 이미 이어져 있으면 가운데(L자 모서리)를 제거한다
            int w = 0;
            for (int i = 0; i < m; i++)
            {
                if (0 < w)
                {
                    int next = i + 1 < m ? i + 1 : 0;
                    if (Mathf.Abs(rawX[next] - OutX[w - 1]) <= 1 && Mathf.Abs(rawY[next] - OutY[w - 1]) <= 1) continue;
                }

                OutX[w] = rawX[i];
                OutY[w] = rawY[i];
                w++;
            }

            return w;
        }
    }
}
