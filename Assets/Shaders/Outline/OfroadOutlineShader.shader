Shader "Custom/OffroadOutlineShader"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth("Outline Width", Float) = 1
        _OutlineExpand("Outline Quad Expand (Pixels)", Float) = 1
        [HideInInspector] _Dummy("Dummy", Float) = 0
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

        Stencil
        {
            Ref 32
            Comp Equal
            Pass Keep
        }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Include/PixelOutline.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float2 baseWorldPos : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(half4, _OutlineColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _OutlineWidth)
                UNITY_DEFINE_INSTANCED_PROP(float, _OutlineExpand)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                // 아트가 스프라이트 경계에 닿아 있어도 아웃라인이 잘리지 않도록 쿼드를 바깥으로 넓힌다.
                float3 basePositionOS = IN.positionOS.xyz;
                float3 expandedPositionOS = ExpandOutlineQuad(
                    basePositionOS, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _OutlineExpand));

                OUT.positionHCS = TransformObjectToHClip(expandedPositionOS);
                OUT.worldPos = TransformObjectToWorld(expandedPositionOS);
                OUT.baseWorldPos = TransformObjectToWorld(basePositionOS).xy;
                OUT.uv = IN.uv; // 2D SRP Batcher 호환을 위해 TRANSFORM_TEX 제거
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float ppu = PIXEL_OUTLINE_PPU;
                float2 worldPos = IN.worldPos.xy;
                float2 baseWorldPos = IN.baseWorldPos;

                // 1. 월드 좌표 스냅 (IsometricShadowURP 방식)
                //    스냅은 실제 프래그먼트 위치 기준, UV 복원은 쿼드를 넓히기 전 좌표 기준이다.
                float2 snappedWorldPos = (floor(worldPos * ppu) + 0.5) / ppu;
                float2 worldDelta = snappedWorldPos - baseWorldPos;

                float2 dx_wp = ddx(baseWorldPos);
                float2 dy_wp = ddy(baseWorldPos);
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
                half4 mainColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, snappedUV) * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);

                // 넓혀서 생긴 여백은 텍스처 바깥이므로 비어 있는 것으로 취급한다.
                mainColor.a *= InsideSpriteUV(snappedUV);
                
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

                float outlineWidth = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _OutlineWidth);
                float2 finalOffset_X = uvOffset_X * outlineWidth;
                float2 finalOffset_Y = uvOffset_Y * outlineWidth;

                half alphaUp = SampleOutlineAlpha(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), snappedUV + finalOffset_Y);
                half alphaDown = SampleOutlineAlpha(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), snappedUV - finalOffset_Y);
                half alphaLeft = SampleOutlineAlpha(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), snappedUV - finalOffset_X);
                half alphaRight = SampleOutlineAlpha(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), snappedUV + finalOffset_X);
                half alphaUpLeft = SampleOutlineAlpha(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), snappedUV + finalOffset_Y - finalOffset_X);
                half alphaUpRight = SampleOutlineAlpha(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), snappedUV + finalOffset_Y + finalOffset_X);
                half alphaDownLeft = SampleOutlineAlpha(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), snappedUV - finalOffset_Y - finalOffset_X);
                half alphaDownRight = SampleOutlineAlpha(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), snappedUV - finalOffset_Y + finalOffset_X);
                
                half outlineAlpha = max(max(max(alphaUp, alphaDown), max(alphaLeft, alphaRight)), 
                                        max(max(alphaUpLeft, alphaUpRight), max(alphaDownLeft, alphaDownRight)));

                if (mainColor.a < 0.1 && outlineAlpha > 0.1)
                {
                    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _OutlineColor);
                }

                if (mainColor.a < 0.1) discard;

                return mainColor;
            }
            ENDHLSL
        }
    }
}
