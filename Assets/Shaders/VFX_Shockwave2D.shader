Shader "Custom/VFX/URP2D_SlashEnergy"
{
    Properties
    {
        [MainTexture] _MainTex ("Base Image", 2D) = "white" {}
        [HDR] _Color ("Tint Color (For White Part)", Color) = (1,1,1,1)
        
        [Header(Direction Settings)]
        _TailDirectionX ("Direction X (Left -1, Right 1)", Range(-1, 1)) = 0
        _TailDirectionY ("Direction Y (Down -1, Up 1)", Range(-1, 1)) = 1
        _TailStart ("Effect Start Point", Range(0, 1)) = 0.2
        
        [Header(Wobble Settings)]
        _WobbleSpeed ("Wobble Speed", Float) = 10.0
        _WobbleFrequency ("Wobble Frequency", Float) = 15.0
        _WobbleAmount ("Wobble Amount", Range(0, 0.2)) = 0.05

        [Header(Distortion Settings)]
        _DistortionStrength ("Background Distortion", Range(0, 2)) = 0.5
        
        [Header(Dissolve Settings)]
        _DissolveAmount ("Dissolve Amount", Range(0, 1.2)) = 0.0
        _WipeDirection ("Wipe Direction (0=HeadToTail, 1=TailToHead)", Range(0, 1)) = 1.0
        _DissolveNoiseWeight ("Noise Weight (0=Line, 1=Full Noise)", Range(0, 1)) = 0.2
        _DissolveScale ("Dissolve Noise Scale", Float) = 50.0
        [HDR] _DissolveEdgeColor ("Dissolve Edge Color", Color) = (2, 1, 0, 1)
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0.01, 0.5)) = 0.05
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline"="UniversalPipeline"
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
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                float4 screenPos    : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            // URP 2D Camera Sorting Layer Texture (배경 캡처용)
            TEXTURE2D(_CameraSortingLayerTexture); 
            SAMPLER(sampler_CameraSortingLayerTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _TailDirectionX;
                float _TailDirectionY;
                float _TailStart;
                float _WobbleSpeed;
                float _WobbleFrequency;
                float _WobbleAmount;
                float _DistortionStrength;
                float _DissolveAmount;
                float _WipeDirection;
                float _DissolveNoiseWeight;
                float _DissolveScale;
                float4 _DissolveEdgeColor;
                float _DissolveEdgeWidth;
            CBUFFER_END
            
            // 2D Noise 생성 함수 (텍스처 없이 계산으로 노이즈 생성)
            float random(float2 p) {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }
            float noise(float2 p) {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = random(i);
                float b = random(i + float2(1.0, 0.0));
                float c = random(i + float2(0.0, 1.0));
                float d = random(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a)* u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color * _Color;
                o.screenPos = ComputeScreenPos(o.positionHCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // 1. 방향 벡터 세팅
                float2 tailDir = float2(_TailDirectionX, _TailDirectionY);
                float dirLength = length(tailDir);
                tailDir = dirLength > 0.001 ? tailDir / dirLength : float2(0, 1);
                
                float2 perpendicularDir = float2(-tailDir.y, tailDir.x);

                // 2. 꼬리 방향 진행도
                float2 centerUV = i.uv - 0.5;
                float tailFactor = dot(centerUV, tailDir) + 0.5;
                
                // 3. 마스크 (머리 보호)
                float effectMask = smoothstep(_TailStart, 1.0, tailFactor);

                // 4. Wobble & Surge 오프셋 계산
                float wTime = _Time.y * _WobbleSpeed;
                float wave1 = sin(tailFactor * _WobbleFrequency - wTime);
                float wave2 = sin(tailFactor * (_WobbleFrequency * 1.7) - wTime * 1.3);
                float organicWave = (wave1 + wave2) * 0.5; 
                float2 wobbleOffset = perpendicularDir * organicWave * _WobbleAmount * effectMask;
                
                // 5. 최종 왜곡 오프셋
                float2 finalOffset = wobbleOffset;
                float2 wobbledUV = i.uv + finalOffset;

                // 6. 메인 텍스처 샘플링 (흰색/검은색 판단용)
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, wobbledUV);
                
                // UV가 0~1 영역을 벗어나면 텍스처의 가장자리 픽셀이 쭈욱 밀려보이는 현상 방지
                if (wobbledUV.x < 0.0 || wobbledUV.x > 1.0 || wobbledUV.y < 0.0 || wobbledUV.y > 1.0)
                {
                    texColor = half4(0.0, 0.0, 0.0, 0.0);
                }

                // 7. 배경 화면 샘플링 (디스토션 적용)
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                // 왜곡 오프셋을 스크린 UV에도 더해서 배경을 일렁이게 만듭니다.
                screenUV += finalOffset * _DistortionStrength;
                half4 bg = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, screenUV);

                // 8. 영역별 컬러 합성
                // 흰색 부분(texColor.r 가 1에 가까움) -> 틴트 컬러(_Color) 적용
                // 검은색 부분(texColor.r 가 0에 가까움) -> 왜곡된 배경(bg) 적용
                half3 edgeColor = _Color.rgb; 
                half3 finalRGB = lerp(bg.rgb, edgeColor, texColor.r);

                // 9. 프로시저럴 노이즈 기반 디졸브 (Dissolve)
                if (_DissolveAmount > 0.0)
                {
                    float n = noise(i.uv * _DissolveScale);
                    
                    // 방향에 따른 기준선 계산 (1.0 = 꼬리에서 머리로 삭제, 0.0 = 머리에서 꼬리로 삭제)
                    float wipeLine = lerp(tailFactor, 1.0 - tailFactor, _WipeDirection);
                    
                    // 일자로 지워지는 선(wipeLine)과 노이즈(n)를 혼합하여 지워지는 형태 결정
                    float dissolveValue = lerp(wipeLine, n, _DissolveNoiseWeight);
                    
                    // 디졸브 수치보다 작으면 픽셀 완전 삭제 (투명도 0, 왜곡도 사라짐)
                    if (dissolveValue < _DissolveAmount)
                    {
                        return half4(0.0, 0.0, 0.0, 0.0);
                    }
                    
                    // 디졸브 경계선 타들어가는 효과 (Edge Burn)
                    // texColor.r 값이 1에 가까운 흰색(에너지) 영역에서만 Edge 색상이 나타나도록 lerp 처리
                    if (dissolveValue < _DissolveAmount + _DissolveEdgeWidth)
                    {
                        finalRGB = lerp(finalRGB, _DissolveEdgeColor.rgb, texColor.r);
                    }
                }

                // 최종 알파는 이미지의 알파 채널과 스프라이트 렌더러의 알파 값을 따름
                return half4(finalRGB, texColor.a * i.color.a);
            }
            ENDHLSL
        }
    }
}
