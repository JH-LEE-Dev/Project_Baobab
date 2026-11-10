// 2스테이지 블룸 데코 타일맵 전용. Custom-Sprite-Default와 같고, 장식들이 숲을 따라 차례로 잠깐 밝아졌다 꺼지는 반짝임만 더했다.
// 공용 Custom-Sprite-Default 머티리얼은 그대로 두고, 반짝임을 켠 스테이지에서만 TileMapGenerator가 이 머티리얼로 갈아 끼운다.
Shader "Custom/2D/BloomDecoTwinkle"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        [Header(HDR)]
        _HDRIntensity("HDR Intensity", Float) = 1

        [Header(Twinkle)]
        _TwinkleAmount("가장 밝을 때 더하는 밝기 비율", Float) = 0.6
        _TwinkleSpeed("박자 속도(라디안/초)", Float) = 0.9
        _TwinkleScale("물결 크기(월드)", Float) = 3
        _TwinkleSharpness("반짝이는 순간 좁히기(클수록 짧다)", Float) = 10

        // Legacy properties. They're here so that materials using this shader can gracefully fallback to the legacy sprite shader.
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0

        _FlashAmount("Flash Amount", Range(0,1)) = 0
        _FlashColor("Flash Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Stencil
        {
            Ref 0
            WriteMask 32
            Comp Always
            Pass Replace
        }

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
                float3 worldPos    : TEXCOORD7;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"
            #include "Include/BloomDecoTwinkle.hlsl"

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _HDRIntensity)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlashAmount)
                UNITY_DEFINE_INSTANCED_PROP(half4, _FlashColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _TwinkleAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _TwinkleSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _TwinkleScale)
                UNITY_DEFINE_INSTANCED_PROP(float, _TwinkleSharpness)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            Varyings LitVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonLitVertex(input);
                o.worldPos = TransformObjectToWorld(input.positionOS);
                o.color = input.color * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Color) * unity_SpriteColor;

                return o;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                float ppu = 32.0;
                float2 worldPos = input.worldPos.xy;

                float2 snappedWorldPos = (floor(worldPos * ppu) + 0.5) / ppu;
                float2 worldDelta = snappedWorldPos - worldPos;

                float2 dx_wp = ddx(worldPos);
                float2 dy_wp = ddy(worldPos);
                float2 dx_uv = ddx(input.uv);
                float2 dy_uv = ddy(input.uv);

                float det = dx_wp.x * dy_wp.y - dx_wp.y * dy_wp.x;
                if (abs(det) > 1e-8)
                {
                    float2 uvDelta = (worldDelta.x * (dy_wp.y * dx_uv - dy_wp.x * dy_uv) +
                                      worldDelta.y * (dx_wp.x * dy_uv - dx_wp.y * dx_uv)) / det;
                    input.uv += uvDelta;
                }

                half4 color = CommonLitFragment(input, input.color);
                clip(color.a - 0.01);
                color.rgb *= UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HDRIntensity);
                // 장식들이 차례로 잠깐 밝아졌다 꺼진다
                color.rgb = ApplyBloomDecoTwinkle(color.rgb, snappedWorldPos,
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _TwinkleAmount),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _TwinkleSpeed),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _TwinkleScale),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _TwinkleSharpness));
                color.rgb = lerp(color.rgb, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FlashColor).rgb, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FlashAmount) * color.a);
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
                float3  worldPos        : TEXCOORD7;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _HDRIntensity)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlashAmount)
                UNITY_DEFINE_INSTANCED_PROP(half4, _FlashColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _TwinkleAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _TwinkleSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _TwinkleScale)
                UNITY_DEFINE_INSTANCED_PROP(float, _TwinkleSharpness)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonNormalsVertex(input);
                o.worldPos = TransformObjectToWorld(input.positionOS);
                o.color = input.color * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Color) * unity_SpriteColor;

                return o;
            }

            half4 NormalsRenderingFragment(Varyings input) : SV_Target
            {
                float ppu = 32.0;
                float2 worldPos = input.worldPos.xy;

                float2 snappedWorldPos = (floor(worldPos * ppu) + 0.5) / ppu;
                float2 worldDelta = snappedWorldPos - worldPos;

                float2 dx_wp = ddx(worldPos);
                float2 dy_wp = ddy(worldPos);
                float2 dx_uv = ddx(input.uv);
                float2 dy_uv = ddy(input.uv);

                float det = dx_wp.x * dy_wp.y - dx_wp.y * dy_wp.x;
                if (abs(det) > 1e-8)
                {
                    float2 uvDelta = (worldDelta.x * (dy_wp.y * dx_uv - dy_wp.x * dy_uv) +
                                      worldDelta.y * (dx_wp.x * dy_uv - dx_wp.y * dx_uv)) / det;
                    input.uv += uvDelta;
                }

                half4 color = CommonNormalsFragment(input, input.color);
                clip(color.a - 0.01);
                return color;
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
                float3 worldPos : TEXCOORD7;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            #include "Include/BloomDecoTwinkle.hlsl"
          
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _HDRIntensity)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlashAmount)
                UNITY_DEFINE_INSTANCED_PROP(half4, _FlashColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _TwinkleAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _TwinkleSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _TwinkleScale)
                UNITY_DEFINE_INSTANCED_PROP(float, _TwinkleSharpness)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.worldPos = TransformObjectToWorld(input.positionOS);
                o.color = input.color * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Color) * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                float ppu = 32.0;
                float2 worldPos = input.worldPos.xy;

                float2 snappedWorldPos = (floor(worldPos * ppu) + 0.5) / ppu;
                float2 worldDelta = snappedWorldPos - worldPos;

                float2 dx_wp = ddx(worldPos);
                float2 dy_wp = ddy(worldPos);
                float2 dx_uv = ddx(input.uv);
                float2 dy_uv = ddy(input.uv);

                float det = dx_wp.x * dy_wp.y - dx_wp.y * dy_wp.x;
                if (abs(det) > 1e-8)
                {
                    float2 uvDelta = (worldDelta.x * (dy_wp.y * dx_uv - dy_wp.x * dy_uv) +
                                      worldDelta.y * (dx_wp.x * dy_uv - dx_wp.y * dx_uv)) / det;
                    input.uv += uvDelta;
                }

                half4 color = CommonUnlitFragment(input, input.color);
                color.rgb *= UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HDRIntensity);
                // 장식들이 차례로 잠깐 밝아졌다 꺼진다
                color.rgb = ApplyBloomDecoTwinkle(color.rgb, snappedWorldPos,
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _TwinkleAmount),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _TwinkleSpeed),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _TwinkleScale),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _TwinkleSharpness));
                color.rgb = lerp(color.rgb, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FlashColor).rgb, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FlashAmount) * color.a);
                return color;
            }
            ENDHLSL
        }
    }
}