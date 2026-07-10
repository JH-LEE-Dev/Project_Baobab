Shader "Custom/Lightning2D"
{
    Properties
    {
        [HDR] _Color ("Lightning Color", Color) = (0.5, 0.8, 1, 1)
        _Thickness ("Thickness (선 두께)", Range(0.01, 0.5)) = 0.1
        _NoiseScale ("Noise Scale (꺾임 빈도)", Float) = 10.0
        _NoiseSpeed ("Noise Speed (이동 속도)", Float) = 30.0
        _Distortion ("Distortion (상하 요동치는 폭)", Range(0.0, 0.5)) = 0.3
        
        [Header(Pixel Art Settings)]
        _FPS ("Animation FPS (프레임 끊김)", Float) = 12.0
        _PixelResX ("Pixel Resolution X (가로 픽셀 쪼개기)", Float) = 64.0
        _PixelResY ("Pixel Resolution Y (세로 픽셀 쪼개기)", Float) = 32.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "PreviewType"="Plane" }
        Blend SrcAlpha OneMinusSrcAlpha 
        Cull Off ZWrite Off Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            float4 _Color;
            float _Thickness;
            float _NoiseScale;
            float _NoiseSpeed;
            float _Distortion;
            float _FPS;
            float _PixelResX;
            float _PixelResY;

            // 1D 난수 생성
            float random(float x)
            {
                return frac(sin(x) * 43758.5453123);
            }

            // 날카로운 선형 노이즈
            float noise(float x)
            {
                float i = floor(x);
                float f = frac(x);
                return lerp(random(i), random(i + 1.0), f);
            }

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. 도트 감성을 위한 프레임 끊기 (Stepped Time)
                float steppedTime = floor(_Time.y * _FPS) / _FPS;

                // 2. 부드러운 좌표를 네모 반듯한 픽셀 격자로 강제 변환 (Pixelated UV)
                float2 pixelatedUV = i.uv;
                pixelatedUV.x = floor(pixelatedUV.x * _PixelResX) / _PixelResX;
                pixelatedUV.y = floor(pixelatedUV.y * _PixelResY) / _PixelResY;

                // 픽셀화된 좌표와 끊기는 시간을 기반으로 노이즈 계산
                float n = noise(pixelatedUV.x * _NoiseScale - steppedTime * _NoiseSpeed);
                n = (n - 0.5) * 2.0;

                // Y축을 왜곡시켜 지그재그 생성
                float2 distortedUV = pixelatedUV;
                distortedUV.y += n * _Distortion;

                // 중심으로부터의 거리 계산
                float dist = abs(distortedUV.y - 0.5);

                // 캔버스 밖으로 나가면 투명하게
                if (distortedUV.y < 0.0 || distortedUV.y > 1.0) 
                    return fixed4(0, 0, 0, 0);

                // 3. 안티앨리어싱(부드러운 테두리) 완전 제거 (Hard Edge)
                // 정해진 두께보다 멀면 아예 투명하게 깎아버림
                if (dist > _Thickness) 
                    return fixed4(0, 0, 0, 0);

                // 기본 색상 적용
                fixed4 col = i.color * _Color;
                
                // 도트 특유의 투톤 컬러 (가운데 얇은 심지는 완전한 하얀색으로 덮기)
                if (dist < _Thickness * 0.4) 
                {
                    col = fixed4(1.0, 1.0, 1.0, col.a);
                }
                
                return col;
            }
            ENDCG
        }
    }
}
