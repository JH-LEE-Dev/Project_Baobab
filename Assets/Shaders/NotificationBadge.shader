Shader "Custom/NotificationBadge"
{
    Properties
    {
        [MainTexture] _BaseMap("Badge Texture (B)", 2D) = "white" {}
        _MaskMap("Icon Texture (A)", 2D) = "white" {}
        
        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth("Outline Width", Float) = 1.5
        
        [Header(Shadow)]
        _ShadowColor("Shadow Color", Color) = (0, 0, 0, 0.5)
        _ShadowOffset("Shadow Offset", Vector) = (0.01, -0.01, 0, 0)

        [Header(Dynamic Masking)]
        _MaskRect("Mask Screen Rect (XY:Pos, ZW:Size) 0-1 range", Vector) = (0.5, 0.5, 0.1, 0.1)
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
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
                float4 screenPos : TEXCOORD1; // 정규화된 스크린 좌표 전송용
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_TexelSize;

            TEXTURE2D(_MaskMap);
            SAMPLER(sampler_MaskMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _OutlineColor;
                float _OutlineWidth;
                half4 _ShadowColor;
                float4 _ShadowOffset;
                float4 _MaskRect;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                // 화면상에서의 위치 계산 (0~1 범위의 좌표를 얻기 위함)
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            float GetBadgeOutlineAlpha(float2 uv)
            {
                float2 texelSize = _BaseMap_TexelSize.xy * _OutlineWidth;
                float alpha = 0;
                float2 offsets[8] = {
                    float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1),
                    float2(1, 1), float2(1, -1), float2(-1, 1), float2(-1, -1)
                };
                for (int i = 0; i < 8; i++)
                {
                    alpha = max(alpha, SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + offsets[i] * texelSize).a);
                }
                return alpha;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 1. 현재 픽셀의 정규화된 스크린 UV (0~1)
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                // 2. 배지의 픽셀 위치를 아이콘(A)의 로컬 UV로 변환
                // _MaskRect.xy는 아이콘의 화면 중심(0-1), .zw는 화면 대비 크기(0-1)
                float2 maskUV = (screenUV - _MaskRect.xy) / _MaskRect.zw + 0.5;
                
                // 3. 아이콘(A) 알파 샘플링
                float maskAlpha = 0;
                if (all(maskUV >= 0) && all(maskUV <= 1))
                {
                    maskAlpha = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, maskUV).a;
                }

                // 4. 배지(B) 본체 샘플링
                half4 badgeColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                // 5. 외곽선 계산 (아이콘 A가 없는 영역에만 노출)
                float rawOutline = GetBadgeOutlineAlpha(IN.uv);
                float outlineAlpha = saturate(rawOutline - badgeColor.a);
                float visibleOutlineAlpha = outlineAlpha * (1.0 - maskAlpha);
                
                // 6. 그림자 계산 (아이콘 A가 있는 영역에만 노출)
                float2 shadowSampleUV = IN.uv - _ShadowOffset.xy;
                float shadowSourceAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, shadowSampleUV).a;
                float visibleShadowAlpha = shadowSourceAlpha * maskAlpha * (1.0 - badgeColor.a);

                // 7. 최종 색상 합성
                half4 finalColor = half4(0, 0, 0, 0);

                // 그림자 (A 내부)
                finalColor = lerp(finalColor, _ShadowColor, visibleShadowAlpha * _ShadowColor.a);
                
                // 외곽선 (A 외부)
                finalColor = lerp(finalColor, _OutlineColor, visibleOutlineAlpha * _OutlineColor.a);
                
                // 배지 본체
                finalColor = lerp(finalColor, badgeColor, badgeColor.a);

                return finalColor;
            }

            ENDHLSL
        }
    }
}
