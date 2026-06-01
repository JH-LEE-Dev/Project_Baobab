Shader "Custom/OutlineShader"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth("Outline Width", Float) = 1
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        
        Stencil
        {
            Ref 32
            Comp Equal
            Pass Keep
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
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
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv = IN.uv; // 2D SRP Batcher 호환을 위해 TRANSFORM_TEX 제거
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float ppu = 32.0;
                float2 worldPos = IN.worldPos.xy;

                // 1. 월드 좌표 스냅 (IsometricShadowURP 방식)
                float2 snappedWorldPos = (floor(worldPos * ppu) + 0.5) / ppu;
                float2 worldDelta = snappedWorldPos - worldPos;

                float2 dx_wp = ddx(worldPos);
                float2 dy_wp = ddy(worldPos);
                float2 dx_uv = ddx(IN.uv);
                float2 dy_uv = ddy(IN.uv);

                float det = dx_wp.x * dy_wp.y - dx_wp.y * dy_wp.x;
                float2 snappedUV = IN.uv;

                // 2. 결정자(det)를 이용한 UV 보정
                if (abs(det) > 1e-8)
                {
                    float2 uvDelta = (worldDelta.x * (dy_wp.y * dx_uv - dy_wp.x * dy_uv) +
                                      worldDelta.y * (dx_wp.x * dy_uv - dx_wp.y * dx_uv)) / det;
                    snappedUV += uvDelta;
                }

                // 3. 메인 컬러 샘플링
                half4 mainColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV) * _BaseColor;
                
                // 4. 아웃라인 로직 (PPU 단위의 인접 픽셀 샘플링)
                float2 uvOffset_X = 0;
                float2 uvOffset_Y = 0;

                if (abs(det) > 1e-8)
                {
                    float one_over_ppu = 1.0 / ppu;
                    // 월드 공간에서 1픽셀(1/ppu) 이동에 해당하는 UV 오프셋 계산
                    uvOffset_X = (one_over_ppu * (dy_wp.y * dx_uv - dy_wp.x * dy_uv)) / det;
                    uvOffset_Y = (one_over_ppu * (dx_wp.x * dy_uv - dx_wp.y * dx_uv)) / det;
                }

                float2 finalOffset_X = uvOffset_X * _OutlineWidth;
                float2 finalOffset_Y = uvOffset_Y * _OutlineWidth;

                half alphaUp = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV + finalOffset_Y).a;
                half alphaDown = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV - finalOffset_Y).a;
                half alphaLeft = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV - finalOffset_X).a;
                half alphaRight = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV + finalOffset_X).a;
                half alphaUpLeft = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV + finalOffset_Y - finalOffset_X).a;
                half alphaUpRight = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV + finalOffset_Y + finalOffset_X).a;
                half alphaDownLeft = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV - finalOffset_Y - finalOffset_X).a;
                half alphaDownRight = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV - finalOffset_Y + finalOffset_X).a;
                
                half outlineAlpha = max(max(max(alphaUp, alphaDown), max(alphaLeft, alphaRight)), 
                                        max(max(alphaUpLeft, alphaUpRight), max(alphaDownLeft, alphaDownRight)));

                if (mainColor.a < 0.1 && outlineAlpha > 0.1)
                {
                    return _OutlineColor;
                }

                if (mainColor.a < 0.1) discard;

                return mainColor;
            }
            ENDHLSL
        }
    }
}
