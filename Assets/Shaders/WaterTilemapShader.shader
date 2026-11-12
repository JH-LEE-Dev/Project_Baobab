Shader "Custom/WaterTilemapShader"
{
       Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        // Legacy properties. They're here so that materials using this shader can gracefully fallback to the legacy sprite shader.
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0

        [Header(HDR)]
        _HDRIntensity("HDR Intensity", Float) = 1

        // 셰이더 타일 프레임 애니메이션(StageTileFrameAnimation이 켠 머티리얼 사본만 x = 1). 켜지 않으면 UV를 그대로 쓴다.
        [HideInInspector] _TileFrameGrid("Tile Frame Grid", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex LitVertex
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                half4 color        : COLOR;
                float2 shimmerPositionWS : TEXCOORD5;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"
            #include "Include/WaterShimmer.hlsl"
            #include "Include/WaterSpores.hlsl"
            #include "Include/WaterBubbles.hlsl"
            #include "Include/WaterMagma.hlsl"
            #include "Include/WaterDepth.hlsl"
            #include "Include/TileFrameAnimation.hlsl"

            float4 _MainTex_TexelSize;

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _HDRIntensity)
                UNITY_DEFINE_INSTANCED_PROP(float4, _TileFrameGrid)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            Varyings LitVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonLitVertex(input);
                o.color = input.color * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Color) * unity_SpriteColor;
                o.shimmerPositionWS = TransformObjectToWorld(input.positionOS).xy;

                return o;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                // 셰이더 타일 프레임 애니메이션(켠 머티리얼만): 지금 프레임 그림 자리로 UV를 옮긴 뒤 모든 계산에 쓴다.
                input.uv = TileFrameAnimationUV(input.uv, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _TileFrameGrid));

                half4 color = CommonLitFragment(input, input.color);
                // 연출 판정에 쓰는 텍스처 색(정점색 곱하기 전). CommonLitFragment 안의 같은 읽기와 컴파일러가 하나로 합친다.
                float4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // 반짝임·포자·온도 무늬·거품은 모두 같은 텍스처 색(정점색 곱하기 전)으로 판정한다.
                bool shimmerOn = IsWaterShimmerEnabled();
                bool sporesOn = IsWaterSporesEnabled();
                bool magmaOn = IsWaterMagmaEnabled();
                bool bubblesOn = IsWaterBubblesEnabled();
                bool depthOn = IsWaterDepthEnabled();
                if (shimmerOn || sporesOn || magmaOn || bubblesOn || depthOn)
                {
                    float3 texColor = texel.rgb;
                    // 텍셀 중심 월드 위치와 텍셀 번호는 깊이·반짝임·포자가 같이 쓰므로 한 번만 구한다.
                    // 타일 텍셀 중심은 (정수 + 0.5) / ppu에 놓이므로 내림하면 텍셀 하나가 한 번호를 갖는다.
                    float2 centerWS = StageAmbienceTexelCenterWS(input.shimmerPositionWS, input.uv, _MainTex_TexelSize);
                    float2 texelId = floor(centerWS * StageAmbiencePixelsPerUnit());

                    // 물 깊이(꺼진 스테이지는 그대로). 반짝임·포자가 위에 그려지도록 먼저 칠한다.
                    if (depthOn)
                    {
                        color.rgb = ApplyWaterDepthAt(color.rgb, centerWS, texelId, texColor);
                    }

                    // 물 반짝임(꺼진 스테이지는 건너뛴다).
                    if (shimmerOn)
                    {
                        float4 shimmer = ComputeWaterShimmerAt(centerWS, texelId, texColor);
                        color.rgb = lerp(color.rgb, shimmer.rgb, shimmer.a);
                    }

                    // 물 위 포자(꺼진 스테이지는 그대로)
                    if (sporesOn)
                    {
                        float4 spores = ComputeWaterSporesAt(texelId, texColor);
                        color.rgb = lerp(color.rgb, spores.rgb, spores.a);
                    }

                    // 용암 속 온도 무늬(꺼진 스테이지는 그대로). 거품이 위에 그려지도록 거품보다 먼저 칠한다.
                    // 바다에 떠다니는 굳은 껍질 위에는 거품을 올리지 않는다.
                    bool onCrust = false;
                    if (magmaOn)
                    {
                        float4 magma = ComputeWaterMagma(input.uv, input.shimmerPositionWS, _MainTex_TexelSize, texColor, onCrust);
                        color.rgb = lerp(color.rgb, magma.rgb, magma.a);
                    }

                    // 물(용암) 위 거품(꺼진 스테이지는 그대로)
                    if (bubblesOn && false == onCrust)
                    {
                        float4 bubbles = ComputeWaterBubbles(input.uv, input.shimmerPositionWS, _MainTex_TexelSize, texColor);
                        color.rgb = lerp(color.rgb, bubbles.rgb, bubbles.a);
                    }
                }

                color.rgb *= UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HDRIntensity);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_NORMALS_INPUTS
                float4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_NORMALS_OUTPUTS
                half4   color           : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"
            #include "Include/TileFrameAnimation.hlsl"

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _HDRIntensity)
                UNITY_DEFINE_INSTANCED_PROP(float4, _TileFrameGrid)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonNormalsVertex(input);
                o.color = input.color * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Color) * unity_SpriteColor;

                return o;
            }

            half4 NormalsRenderingFragment(Varyings input) : SV_Target
            {
                // 셰이더 타일 프레임 애니메이션(켠 머티리얼만): 지금 프레임 그림 자리로 UV를 옮긴 뒤 모든 계산에 쓴다.
                input.uv = TileFrameAnimationUV(input.uv, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _TileFrameGrid));
                return CommonNormalsFragment(input, input.color);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            #include "Include/TileFrameAnimation.hlsl"
          
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _HDRIntensity)
                UNITY_DEFINE_INSTANCED_PROP(float4, _TileFrameGrid)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Color) * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                // 셰이더 타일 프레임 애니메이션(켠 머티리얼만): 지금 프레임 그림 자리로 UV를 옮긴 뒤 모든 계산에 쓴다.
                input.uv = TileFrameAnimationUV(input.uv, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _TileFrameGrid));
                half4 color = CommonUnlitFragment(input, input.color);
                color.rgb *= UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HDRIntensity);
                return color;
            }
            ENDHLSL
        }
    }
}
