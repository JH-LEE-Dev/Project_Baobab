// 4스테이지 블룸 데코 타일맵 전용. Custom-Sprite-Default와 같고(은은한 블룸 _HDRGlowOnly 포함), 불씨 데코 숨쉬기만 더했다:
// 장식마다 제 박자로 불빛이 은은하게 밝아졌다 돌아오고, 그 장식에서 증기가 뿜어질 때 잠깐 더 밝아진다(Include/BloomDecoBreath.hlsl).
// 공용 Custom-Sprite-Default 머티리얼은 그대로 두고, 숨쉬기를 켠 스테이지에서만 TileMapGenerator가 이 머티리얼로 갈아 끼운다.
// 숨쉬기 배율은 HDR 배율에만 곱하므로 은은한 블룸에서는 불빛 칸만 숨쉬고 어두운 바위는 그대로다.
Shader "Custom/2D/BloomDecoBreath"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        [Header(HDR)]
        _HDRIntensity("HDR Intensity", Float) = 1
        // 1이면 HDR 배율을 그림 전체가 아니라 밝은 칸(불빛)에만 준다. 밝은 칸은 조명과 상관없이 스스로 빛난다.
        // 0(기본)이면 예전처럼 그림 전체에 HDR 배율을 곱한다.
        _HDRGlowOnly("HDR Glow Only", Float) = 0
        // x = 빛나기 시작하는 밝기, y = 다 빛나는 밝기(텍스처 rgb 중 가장 큰 값, 선형 공간 0~1)
        _HDRGlowRange("HDR Glow Range", Vector) = (0.2, 0.6, 0, 0)

        [Header(Breath)]
        _BreathAmount("가장 밝을 때 더하는 비율", Float) = 0.35
        _BreathSpeed("박자 속도(라디안/초, 장식마다 0.75~1.25배)", Float) = 1.2
        _BreathSharpness("밝아져 있는 순간 좁히기(클수록 짧다)", Float) = 2
        _DecoSpriteWorldSize("장식 그림 크기(월드)", Vector) = (1, 1, 0, 0)
        _SteamFlashAmount("증기 뿜을 때 더하는 비율", Float) = 0.8
        _SteamFlashRadius("증기 자리와 같은 장식으로 볼 거리(월드)", Float) = 0.25

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
                // 불빛 HDR 배율에 곱할 숨쉬기 배율(장식 하나 안에서 같은 값)
                half glowBoost     : TEXCOORD6;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"
            #include "Include/BloomDecoBreath.hlsl"

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _HDRIntensity)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlashAmount)
                UNITY_DEFINE_INSTANCED_PROP(half4, _FlashColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _HDRGlowOnly)
                UNITY_DEFINE_INSTANCED_PROP(float4, _HDRGlowRange)
                UNITY_DEFINE_INSTANCED_PROP(float, _BreathAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _BreathSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _BreathSharpness)
                UNITY_DEFINE_INSTANCED_PROP(float4, _DecoSpriteWorldSize)
                UNITY_DEFINE_INSTANCED_PROP(float, _SteamFlashAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _SteamFlashRadius)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            // 밝은 칸만 빛나게 할 때의 빛 비율(0 = 그대로, 1 = 다 빛남)
            half HDRGlowWeight(half3 _albedo)
            {
                float4 range = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HDRGlowRange);
                half brightness = max(_albedo.r, max(_albedo.g, _albedo.b));
                return saturate((brightness - range.x) / max(range.y - range.x, 0.0001));
            }

            Varyings LitVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonLitVertex(input);
                o.worldPos = TransformObjectToWorld(input.positionOS);
                o.glowBoost = BloomDecoBreathBoost(input.uv, o.worldPos.xy,
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DecoSpriteWorldSize).xy,
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BreathAmount),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BreathSpeed),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BreathSharpness),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SteamFlashAmount),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SteamFlashRadius));
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
                float hdrIntensity = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HDRIntensity) * input.glowBoost;
                if (UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HDRGlowOnly) > 0.5)
                {
                    // 밝은 칸은 조명을 받기 전 그림 색에 HDR 배율을 곱해 스스로 빛나게 하고, 어두운 칸은 조명 받은 색 그대로 둔다.
                    half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb * input.color.rgb;
                    color.rgb = lerp(color.rgb, albedo * hdrIntensity, HDRGlowWeight(albedo));
                }
                else
                {
                    color.rgb *= hdrIntensity;
                }
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
                UNITY_DEFINE_INSTANCED_PROP(float, _HDRGlowOnly)
                UNITY_DEFINE_INSTANCED_PROP(float4, _HDRGlowRange)
                UNITY_DEFINE_INSTANCED_PROP(float, _BreathAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _BreathSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _BreathSharpness)
                UNITY_DEFINE_INSTANCED_PROP(float4, _DecoSpriteWorldSize)
                UNITY_DEFINE_INSTANCED_PROP(float, _SteamFlashAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _SteamFlashRadius)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            // 밝은 칸만 빛나게 할 때의 빛 비율(0 = 그대로, 1 = 다 빛남)
            half HDRGlowWeight(half3 _albedo)
            {
                float4 range = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HDRGlowRange);
                half brightness = max(_albedo.r, max(_albedo.g, _albedo.b));
                return saturate((brightness - range.x) / max(range.y - range.x, 0.0001));
            }

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
                // 불빛 HDR 배율에 곱할 숨쉬기 배율(장식 하나 안에서 같은 값)
                half glowBoost : TEXCOORD6;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            #include "Include/BloomDecoBreath.hlsl"
          
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _HDRIntensity)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlashAmount)
                UNITY_DEFINE_INSTANCED_PROP(half4, _FlashColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _HDRGlowOnly)
                UNITY_DEFINE_INSTANCED_PROP(float4, _HDRGlowRange)
                UNITY_DEFINE_INSTANCED_PROP(float, _BreathAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _BreathSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _BreathSharpness)
                UNITY_DEFINE_INSTANCED_PROP(float4, _DecoSpriteWorldSize)
                UNITY_DEFINE_INSTANCED_PROP(float, _SteamFlashAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _SteamFlashRadius)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            // 밝은 칸만 빛나게 할 때의 빛 비율(0 = 그대로, 1 = 다 빛남)
            half HDRGlowWeight(half3 _albedo)
            {
                float4 range = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HDRGlowRange);
                half brightness = max(_albedo.r, max(_albedo.g, _albedo.b));
                return saturate((brightness - range.x) / max(range.y - range.x, 0.0001));
            }

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.worldPos = TransformObjectToWorld(input.positionOS);
                o.glowBoost = BloomDecoBreathBoost(input.uv, o.worldPos.xy,
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DecoSpriteWorldSize).xy,
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BreathAmount),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BreathSpeed),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BreathSharpness),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SteamFlashAmount),
                    UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SteamFlashRadius));
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
                float hdrIntensity = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HDRIntensity) * input.glowBoost;
                if (UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HDRGlowOnly) > 0.5)
                {
                    color.rgb *= lerp(1.0, hdrIntensity, HDRGlowWeight(color.rgb));
                }
                else
                {
                    color.rgb *= hdrIntensity;
                }
                color.rgb = lerp(color.rgb, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FlashColor).rgb, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FlashAmount) * color.a);
                return color;
            }
            ENDHLSL
        }
    }
}