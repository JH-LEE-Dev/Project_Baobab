Shader "Custom/PixelArtDropShadowURP"
{
    Properties
    {
        [MainColor] _BaseColor("Shadow Color", Color) = (0, 0, 0, 0.5)
        [MainTexture] _BaseMap("Base Map (Caster Texture)", 2D) = "white" {}
        _ShadowAngle("Shadow Angle (0-360)", Range(0, 360)) = 270
        _MaxDistance("Max Shadow Offset Distance", Range(0, 1)) = 0.2
        _Expansion("Mesh Expansion", Range(1, 10)) = 1.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+1"
            "RenderPipeline" = "UniversalPipeline" 
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            Stencil
            {
                Ref 0
                Comp Equal
                ReadMask 7
                WriteMask 7
                Pass IncrSat
            }

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
                float3 worldPos : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float _ShadowAngle;
                float _MaxDistance;
                float _Expansion;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // 메시 확장 적용
                float3 pos = IN.positionOS.xyz;
                pos.xy *= _Expansion;
                
                OUT.positionHCS = TransformObjectToHClip(pos);
                // 스냅 계산을 위해 확장된 위치의 월드 좌표를 넘김
                OUT.worldPos = TransformObjectToWorld(pos);
                OUT.uv = IN.uv;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // --- 안정화된 월드 공간 32 PPU 픽셀 스냅 로직 (IsometricShadowURP와 동일하게 유지) ---
                float ppu = 32.0;
                float2 worldPos = IN.worldPos.xy;

                // 1. 경계면 진동 방지를 위해 아주 작은 offset(0.001)을 더해 스냅 안정화
                float2 snappedWorldPos = floor(worldPos * ppu + 0.001) / ppu;
                float2 worldDelta = snappedWorldPos - worldPos;

                // 2. UV 변화량(ddx, ddy)과 월드 좌표 변화량 사이의 관계를 행렬로 계산
                float2 dx_wp = ddx(worldPos);
                float2 dy_wp = ddy(worldPos);
                float2 dx_uv = ddx(IN.uv);
                float2 dy_uv = ddy(IN.uv);

                // 행렬의 결정자(Determinant) 계산 (원본의 식과 완전히 동일하게 유지)
                float det = dx_wp.x * dy_wp.y - dx_wp.y * dx_wp.x;
                float2 snappedUV = IN.uv;

                if (abs(det) > 0.0000001)
                {
                    // 월드 좌표 변화량(worldDelta)에 대응하는 정확한 UV 변화량을 역행렬로 산출
                    float2 uvDelta = (worldDelta.x * (dy_wp.y * dx_uv - dy_wp.x * dy_uv) + 
                                      worldDelta.y * (dx_wp.x * dy_uv - dx_wp.y * dx_uv)) / det;
                    snappedUV += uvDelta;
                }

                // 3. 보정된 UV를 사용하여 레이마칭 수행
                float2 spriteUV = (snappedUV - 0.5) * _Expansion + 0.5;

                float rad = radians(_ShadowAngle);
                float2 shadowDir = float2(cos(rad), sin(rad));

                for (int i = 0; i < 50; i++) 
                {
                    float dist = (float(i) / 50.0) * _MaxDistance;
                    float2 sampleUV = spriteUV + shadowDir * dist; 

                    if (sampleUV.x < 0.0 || sampleUV.x > 1.0 || sampleUV.y < 0.0 || sampleUV.y > 1.0) continue;

                    half4 sampled = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, sampleUV);

                    if (sampled.a > 0.1)
                    {
                        return _BaseColor;
                    }
                }

                clip(-1);
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}