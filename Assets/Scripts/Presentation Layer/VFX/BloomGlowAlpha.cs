using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 정점 알파 = HDR 발광 세기(BrandWrapGlow 셰이더: glow = lerp(_GlowMin, _GlowMax, a))로 그리는 픽셀의 블룸을 일정 비율로 줄이는 알파 환산표.
    ///
    /// URP 블룸 프리필터는 밝기 b = max(r, g, b)에 임계 T(볼륨 값을 GammaToLinearSpace로 바꾼 것), 무릎 K = T / 2를 써서
    /// 블룸 = rgb / b × f(b), f(b) = b - T (b ≥ T + K), (b - T + K)² / 4K (T - K &lt; b &lt; T + K), 0 (그 아래)로 뽑는다.
    /// 색 비율은 그대로이므로 f(b') = 비율 × f(b)가 되는 b'를 풀어 알파로 되돌린다. 블룸이 없던 픽셀(b ≤ T - K)은 그대로 둔다.
    /// </summary>
    public static class BloomGlowAlpha
    {
        // 카메라 볼륨(Global Post Process Volume Profile 2)의 블룸 임계 1.345
        private const float VolumeThreshold = 1.345f;

        /// <summary>
        /// 색 _color, 재질 발광 범위 _glowMin~_glowMax에서 원래 알파(0~255) -> 블룸이 _scale배가 되는 알파 표(256칸)를 만든다.
        /// </summary>
        public static byte[] Build(Color32 _color, float _glowMin, float _glowMax, float _scale)
        {
            byte[] table = new byte[256];
            float threshold = Mathf.GammaToLinearSpace(VolumeThreshold);
            float knee = threshold * 0.5f;
            float maxChannel = Mathf.Max(_color.r, Mathf.Max(_color.g, _color.b)) / 255.0f;
            float range = _glowMax - _glowMin;

            for (int a = 0; a < 256; a++)
            {
                table[a] = (byte)a;
                if (maxChannel <= 0.0f || range <= 0.0f) continue;

                float brightness = maxChannel * (_glowMin + range * (a / 255.0f));
                float bloom = BloomAmount(brightness, threshold, knee);
                if (bloom <= 0.0f) continue;

                float target = bloom * _scale;
                float newBrightness = target >= knee
                    ? threshold + target
                    : threshold - knee + Mathf.Sqrt(4.0f * knee * target);
                float newAlpha = (newBrightness / maxChannel - _glowMin) / range;
                table[a] = (byte)Mathf.RoundToInt(Mathf.Clamp01(newAlpha) * 255.0f);
            }
            return table;
        }

        private static float BloomAmount(float _brightness, float _threshold, float _knee)
        {
            if (_brightness >= _threshold + _knee) return _brightness - _threshold;
            float soft = _brightness - _threshold + _knee;
            if (soft <= 0.0f) return 0.0f;
            return soft * soft / (4.0f * _knee);
        }
    }
}
