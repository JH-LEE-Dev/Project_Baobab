Shader "Custom/VFX/URP2D_HeatBurstDistortion"
{
    // 나무가 열기를 뿜을 때의 화면 왜곡. URP 2D 카메라 소팅 레이어 텍스처(_CameraSortingLayerTexture, Renderer2D가 Objects 레이어까지 캡처)를 샘플링해서
    // 이 쿼드 영역의 화면을 일그러뜨린다. 왜곡이 없는 곳은 알파 0이라 원래 화면이 그대로 보인다.
    //
    // 두 가지가 겹친다.
    // 1. 충격파 링: 중심에서 바깥으로 퍼지는 고리 모양 왜곡(_Progress 0 -> 1, 퍼지며 약해진다)
    // 2. 아지랑이: 중심 위쪽으로 피어오르는 세로 물결 왜곡(_Intensity로 켜고 끈다)
    // 진행도(_Progress)와 세기(_Intensity)는 VFX_TreeHeatBurst가 MaterialPropertyBlock으로 매 프레임 넘긴다.
    Properties
    {
        _Progress ("Ring Progress (0~1)", Range(0, 1)) = 0.0
        _Intensity ("Haze Intensity (0~1)", Range(0, 1)) = 0.0
        _TimeSeed ("Time Seed", Float) = 0.0

        [Header(Layout)]
        _Aspect ("Quad Aspect (Width / Height)", Float) = 1.0
        _CenterY ("Center Y (UV)", Range(0, 1)) = 0.25

        [Header(Ring)]
        _RingMaxRadius ("Ring Max Radius (UV height units)", Float) = 0.5
        _RingWidth ("Ring Width", Float) = 0.07
        _RingStrength ("Ring Strength (screen UV)", Float) = 0.008

        [Header(Haze)]
        _HazeWidth ("Haze Column Half Width (UV height units)", Float) = 0.3
        _HazeFrequency ("Haze Wave Frequency", Float) = 22.0
        _HazeSpeed ("Haze Rise Speed", Float) = 6.0
        _HazeStrength ("Haze Strength (screen UV)", Float) = 0.004

        [Header(Pixel Art Style)]
        [Toggle] _PixelSnap ("Snap Offset To Screen Pixels", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 screenPos   : TEXCOORD1;
            };

            TEXTURE2D(_CameraSortingLayerTexture);
            SAMPLER(sampler_CameraSortingLayerTexture);

            CBUFFER_START(UnityPerMaterial)
                float _Progress;
                float _Intensity;
                float _TimeSeed;
                float _Aspect;
                float _CenterY;
                float _RingMaxRadius;
                float _RingWidth;
                float _RingStrength;
                float _HazeWidth;
                float _HazeFrequency;
                float _HazeSpeed;
                float _HazeStrength;
                float _PixelSnap;
            CBUFFER_END

            float hash21(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.positionHCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float time = _Time.y + _TimeSeed;

                // 중심 기준 좌표(세로 길이를 1로 맞춘 단위)
                float2 p = i.uv - float2(0.5, _CenterY);
                p.x *= _Aspect;
                float dist = length(p);

                // 1. 충격파 링: 가운데가 가장 센 가우시안 고리가 퍼지며 약해진다.
                // 링이 다 퍼지면(_Progress >= 1) (1 - _Progress) = 0이라 링 값이 0이므로 계산을 건너뛴다(쿼드 전체가 같은 분기).
                float ring = 0.0;
                float2 offset = float2(0.0, 0.0);
                if (_Progress < 1.0)
                {
                    float ringRadius = _Progress * _RingMaxRadius;
                    float ringOffsetT = (dist - ringRadius) / max(_RingWidth, 0.0001);
                    ring = exp(-ringOffsetT * ringOffsetT) * (1.0 - _Progress);
                    float2 radialDir = p / max(dist, 0.0001);
                    radialDir.x /= max(_Aspect, 0.0001);
                    offset = radialDir * ring * _RingStrength;
                }

                // 2. 아지랑이 마스크: 중심 위쪽 기둥 안에서만 일렁이고 위로 갈수록 옅어진다.
                float columnT = p.x / max(_HazeWidth, 0.0001);
                float column = exp(-columnT * columnT);
                float rise = smoothstep(_CenterY, _CenterY + 0.12, i.uv.y) * (1.0 - smoothstep(0.55, 1.0, i.uv.y));
                float hazeMask = column * rise * _Intensity;

                // 왜곡이 사실상 없는 곳(알파가 8비트 한 단계의 절반 미만)은 노이즈와 화면 샘플 없이 버린다. 알파는 노이즈와 무관하게 링/마스크로만 정해진다.
                float alpha = saturate(ring * 2.5 + hazeMask * 1.5);
                if (alpha < 0.00196) discard;

                // 아지랑이 물결(노이즈로 위상을 흔든다)
                float phaseNoise = valueNoise(float2(i.uv.x * 7.0, i.uv.y * 4.0 - time * 1.4)) * 6.2831;
                float wave = sin(i.uv.y * _HazeFrequency - time * _HazeSpeed + phaseNoise);
                float lift = valueNoise(float2(i.uv.x * 5.0 + 17.0, i.uv.y * 3.0 - time * 1.1)) - 0.5;
                offset.x += wave * hazeMask * _HazeStrength;
                offset.y += lift * hazeMask * _HazeStrength * 0.6;

                // 3. 화면 픽셀 단위로 오프셋을 반올림해서 도트가 깨지듯 일렁이게 한다
                if (_PixelSnap > 0.5)
                {
                    offset = round(offset * _ScreenParams.xy) / _ScreenParams.xy;
                }

                float2 screenUV = i.screenPos.xy / i.screenPos.w + offset;
                half4 bg = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, screenUV);

                // 왜곡이 일어나는 곳만 불투명하게 해서 나머지는 원래 화면이 보이게 한다
                return half4(bg.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
