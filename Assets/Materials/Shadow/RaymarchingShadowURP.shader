Shader "Custom/PixelArtDropShadowURP"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map (Caster Texture)", 2D) = "white" {}
        _ShadowColor("Shadow Color", Color) = (0, 0, 0, 0.5)
        _ShadowAngle("Shadow Angle (0-360)", Range(0, 360)) = 270 // 아래쪽
        _MaxDistance("Max Shadow Offset Distance", Range(0, 1)) = 0.2
        _Expansion("Mesh Expansion", Range(1, 10)) = 1.5
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
                float _MaxDistance;
                float _Expansion;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                
                // 메시 확장
                float3 pos = IN.positionOS.xyz;
                pos.xy *= _Expansion;
                OUT.positionHCS = TransformObjectToHClip(pos);
                
                OUT.uv = IN.uv;
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 현재 픽셀을 확장된 스프라이트의 좌표계(0~1)로 변환
                float2 spriteUV = (IN.uv - 0.5) * _Expansion + 0.5;

                // 그림자 오프셋 방향 계산
                float rad = radians(_ShadowAngle);
                float2 shadowDir = float2(cos(rad), sin(rad));

                // 레이마칭: 현재 위치에서 광원 반대 방향(그림자가 올 방향)으로 역추적
                for (int i = 0; i < 50; i++) 
                {
                    float dist = (float(i) / 50.0) * _MaxDistance;
                    
                    // 현재 픽셀 위치에서 shadowDir * dist 만큼 *위로* 올라간 위치 샘플링
                    float2 sampleUV = spriteUV + shadowDir * dist; 

                    // 샘플링 위치가 텍스처 범위를 벗어나면 중단
                    if (sampleUV.x < 0.0 || sampleUV.x > 1.0 || sampleUV.y < 0.0 || sampleUV.y > 1.0) continue;

                    // 원본 텍스처 샘플링
                    half4 sampled = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, sampleUV);
                    
                    // 불투명한 픽셀 실루엣을 발견하면 그림자로 확정
                    if (sampled.a > 0.1)
                    {
                        // 원본 픽셀 위치는 그림자에서 제외하여 차를 가리지 않게 함.
                        if (i == 0) return half4(0, 0, 0, 0); 

                        return _ShadowColor;
                    }
                }

                // 루프를 돌았으나 오프셋된 실루엣을 찾지 못한 경우
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}