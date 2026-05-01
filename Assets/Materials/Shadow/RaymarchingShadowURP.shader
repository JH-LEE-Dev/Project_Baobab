Shader "Custom/PixelArtDropShadowURP"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map (Caster Texture)", 2D) = "white" {}
        _ShadowColor("Shadow Color", Color) = (0, 0, 0, 0.5)
        _ShadowAngle("Shadow Angle (0-360)", Range(0, 360)) = 270 // 270이면 아래로 향함
        
        // 그림자가 길어지는 배율 (기존 _MaxDistance 역할을 대체)
        _ShadowLengthMultiplier("Shadow Length Multiplier", Range(0, 5)) = 2.0 
        
        // 차체가 바닥에 닿는 기준점 (보통 바퀴 밑단 0.1 ~ 0.2)
        _ShadowPivotY("Shadow Pivot Y (Ground)", Range(0, 0.5)) = 0.15 
        
        _Expansion("Mesh Expansion", Range(1, 10)) = 3.0 // 그림자가 잘리지 않게 넉넉히
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
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                half4 _ShadowColor;
                float _ShadowAngle;
                float _ShadowLengthMultiplier;
                float _ShadowPivotY;
                float _Expansion;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 pos = IN.positionOS.xyz;
                pos.xy *= _Expansion;
                OUT.positionHCS = TransformObjectToHClip(pos);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 spriteUV = (IN.uv - 0.5) * _Expansion + 0.5;

                // 1. 원본 차체 영역은 그림자로 덮지 않도록 예외 처리
                // (그림자가 차체 위로 타고 올라오는 것을 방지)
                if (spriteUV.x >= 0.0 && spriteUV.x <= 1.0 && spriteUV.y >= 0.0 && spriteUV.y <= 1.0)
                {
                    if (SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, spriteUV).a > 0.1) 
                        return half4(0, 0, 0, 0); 
                }

                // 그림자가 뻗어나가는 방향
                float rad = radians(_ShadowAngle);
                float2 shadowDir = float2(cos(rad), sin(rad));
                
                // 역추적(Gather) 방향은 빛이 오는 곳(그림자의 반대 방향)
                float2 gatherDir = -shadowDir;

                // 2. 가변 압출 레이마칭 (질문자님의 아이디어 적용)
                for (int i = 0; i < 50; i++) 
                {
                    // 최대로 확인할 거리를 정해두고 50등분하여 훑어 올라감
                    // 여기서 1.0은 탐색할 최대 반경(UV 기준)입니다.
                    float dist = (float(i) / 50.0) * 1.0; 
                    
                    float2 sampleUV = spriteUV + gatherDir * dist;

                    // 텍스처 범위를 벗어나면 확인 생략
                    if (sampleUV.x < 0.0 || sampleUV.x > 1.0 || sampleUV.y < 0.0 || sampleUV.y > 1.0) continue;

                    half4 sampled = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, sampleUV);
                    
                    // 불투명한 픽셀(차체)을 발견했다면?
                    if (sampled.a > 0.1)
                    {
                        // [핵심] 발견된 차체 픽셀이 바닥(Pivot)으로부터 얼마나 높은 곳에 있는지 계산
                        float heightFromPivot = max(0, sampleUV.y - _ShadowPivotY);
                        
                        // 높이에 비례하여 이 픽셀이 만들 수 있는 '허용 그림자 길이'를 계산
                        float allowedDist = heightFromPivot * _ShadowLengthMultiplier;

                        // 만약 현재 빈 공간에서 차체까지 역추적한 거리(dist)가
                        // 해당 차체 부위가 만들 수 있는 그림자 길이(allowedDist) 이내라면 그림자로 칠함!
                        if (dist <= allowedDist)
                        {
                            return _ShadowColor;
                        }
                    }
                }

                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}