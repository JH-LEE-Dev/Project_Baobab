Shader "Custom/PixelArtDropShadowURP"
{
    Properties
    {
        [MainColor] _BaseColor("Shadow Color", Color) = (0, 0, 0, 0.5)
        [MainTexture] _BaseMap("Base Map (Caster Texture)", 2D) = "white" {}
        _ShadowAngle("Shadow Angle (0-360)", Range(0, 360)) = 270
        _MaxDistance("Max Shadow Offset Distance", Range(0, 1)) = 0.2
        _Expansion("Mesh Expansion", Range(1, 10)) = 1.5
        _UvRect("UV Rect (minX, minY, maxX, maxY)", Vector) = (0,0,1,1)
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
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 worldPos    : TEXCOORD1;
                float2 posOS       : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float _ShadowAngle;
                float _MaxDistance;
                float _Expansion;
                float4 _UvRect;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.posOS = IN.positionOS.xy;
                
                float3 pos = IN.positionOS.xyz;
                pos.xy *= _Expansion;
                
                OUT.positionHCS = TransformObjectToHClip(pos);
                OUT.worldPos = TransformObjectToWorld(pos);
                OUT.uv = IN.uv;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // --- 1. 월드 공간 픽셀 스냅 (Jitter 및 고해상도 방지) ---
                const float PPU = 32.0;
                float2 worldPos = IN.worldPos.xy;

                // 텍셀 중앙(+0.5)으로 스냅하여 계단 현상 강제 적용
                float2 snappedWorldPos = (floor(worldPos * PPU) + 0.5) / PPU;
                float2 worldDelta = snappedWorldPos - worldPos;

                // 역행렬 계산을 위한 그라디언트 산출
                float2 dx_wp = ddx(worldPos);
                float2 dy_wp = ddy(worldPos);
                float2 dx_uv = ddx(IN.uv);
                float2 dy_uv = ddy(IN.uv);
                float2 dx_pos = ddx(IN.posOS);
                float2 dy_pos = ddy(IN.posOS);

                // 결정자(Determinant) 계산 및 스냅 보정
                float det = dx_wp.x * dy_wp.y - dy_wp.x * dx_wp.y;
                float2 snappedUV = IN.uv;
                float2 snappedPosOS = IN.posOS;

                if (abs(det) > 1e-7)
                {
                    float invDet = 1.0 / det;
                    // UV 스냅
                    snappedUV.x += (worldDelta.x * (dy_wp.y * dx_uv.x - dy_wp.x * dy_uv.x) + 
                                    worldDelta.y * (dx_wp.x * dy_uv.x - dx_wp.y * dx_uv.x)) * invDet;
                    snappedUV.y += (worldDelta.x * (dy_wp.y * dx_uv.y - dy_wp.x * dy_uv.y) + 
                                    worldDelta.y * (dx_wp.x * dy_uv.y - dx_wp.y * dx_uv.y)) * invDet;
                    
                    // posOS 스냅 (저해상도 효과 유지의 핵심)
                    snappedPosOS.x += (worldDelta.x * (dy_wp.y * dx_pos.x - dy_wp.x * dy_pos.x) + 
                                       worldDelta.y * (dx_wp.x * dy_pos.x - dx_wp.y * dx_pos.x)) * invDet;
                    snappedPosOS.y += (worldDelta.x * (dy_wp.y * dx_pos.y - dy_wp.x * dy_pos.y) + 
                                       worldDelta.y * (dx_wp.x * dy_pos.y - dx_wp.y * dx_pos.y)) * invDet;
                }

                // --- 2. 메시 확장에 따른 UV 보정 ---
                float2 spriteUV = snappedUV;
                if (_Expansion > 1.001) 
                {
                    float2 posGrad = float2(length(dx_pos), length(dy_pos));
                    float2 uvGrad = float2(length(dx_uv), length(dy_uv));
                    float2 uvScale = uvGrad / max(posGrad, 0.0001);

                    // 스냅된 posOS를 사용하여 보정함으로써 픽셀 단위 변화를 유지
                    spriteUV = snappedUV + snappedPosOS * uvScale * (_Expansion - 1.0);
                }

                // --- 3. 레이마칭 그림자 생성 ---
                float rad = radians(_ShadowAngle);
                float2 shadowDir = float2(cos(rad), sin(rad));

                [unroll(50)]
                for (int i = 0; i < 50; i++) 
                {
                    float progress = (float)i / 50.0;
                    float dist = progress * _MaxDistance;
                    float2 sampleUV = spriteUV + shadowDir * dist; 

                    // 아틀라스 UV Rect 경계 체크
                    bool isOutOfBounds = (sampleUV.x < _UvRect.x || sampleUV.x > _UvRect.z || 
                                          sampleUV.y < _UvRect.y || sampleUV.y > _UvRect.w);
                    
                    if (isOutOfBounds) continue;

                    half4 sampled = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, sampleUV, 0);

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
