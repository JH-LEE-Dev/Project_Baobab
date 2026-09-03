Shader "Custom/2D/Custom-Sprite-Default_LogItem"
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

        [Header(Shiny Settings)]
        [HDR] _ShinyColor("Shiny Color", Color) = (1, 1, 1, 0.5)
        _ShinyWidth("Shiny Width", Range(0.01, 1.0)) = 0.2
        _ShinySoftness("Shiny Softness", Range(0.01, 1.0)) = 0.1
        _ShinyAngle("Shiny Angle", Range(0, 360)) = 45.0
        _ShinyDuration("Shiny Duration", Float) = 1.0
        _ShinyDelay("Shiny Delay", Float) = 0.5
        [MaterialToggle] _ShinyEnabled("Shiny Enabled", Float) = 0
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

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(half4, _ShinyColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShinyWidth)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShinySoftness)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShinyAngle)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShinyDuration)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShinyDelay)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShinyEnabled)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            Varyings LitVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                float3 pivotWorldPos = TransformObjectToWorld(float3(0,0,0));
                float floatingOffset = (pivotWorldPos.x + pivotWorldPos.y) * 10.0;
                float floatOffset = sin(_Time.y * 2.5 + floatingOffset) * (1.0 / 32.0);
                input.positionOS.y += floatOffset;

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

                // Shiny effect (셰이더 내부 시간 기반, GPU 인스턴싱 호환)
                float shinyDuration = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ShinyDuration);
                float shinyDelay = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ShinyDelay);
                float totalCycle = shinyDuration + shinyDelay;
                float3 pivotPos = float3(UNITY_MATRIX_M[0][3], UNITY_MATRIX_M[1][3], UNITY_MATRIX_M[2][3]);
                float phaseOffset = frac((pivotPos.x + pivotPos.y) * 0.37);
                float shinyTime = fmod(_Time.y + phaseOffset * totalCycle, totalCycle);
                float shinyLocation = lerp(-1.0, 2.0, saturate(shinyTime / shinyDuration));

                float shinyAngle = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ShinyAngle);
                float rad = shinyAngle * 0.01745329;
                float sinA = sin(rad);
                float cosA = cos(rad);
                float2 centeredUV = input.uv - 0.5;
                float2 rotatedUV = float2(centeredUV.x * cosA - centeredUV.y * sinA,
                                           centeredUV.x * sinA + centeredUV.y * cosA) + 0.5;
                float dist = abs(rotatedUV.x - shinyLocation);
                float shinyWidth = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ShinyWidth);
                float shinySoftness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ShinySoftness);
                float glow = smoothstep(shinyWidth, shinyWidth - shinySoftness, dist);
                half4 shinyColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ShinyColor);
                float shinyEnabled = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ShinyEnabled);
                color.rgb += shinyColor.rgb * glow * shinyColor.a * color.a * shinyEnabled;

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
                UNITY_DEFINE_INSTANCED_PROP(half4, _ShinyColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShinyWidth)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShinySoftness)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShinyAngle)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShinyDuration)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShinyDelay)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                float3 pivotWorldPos = TransformObjectToWorld(float3(0,0,0));
                float floatingOffset = (pivotWorldPos.x + pivotWorldPos.y) * 10.0;
                float floatOffset = sin(_Time.y * 2.5 + floatingOffset) * (1.0 / 32.0);
                input.positionOS.y += floatOffset;

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
          
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(half4, _ShinyColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShinyWidth)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShinySoftness)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShinyAngle)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShinyDuration)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShinyDelay)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShinyEnabled)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                float3 pivotWorldPos = TransformObjectToWorld(float3(0,0,0));
                float floatingOffset = (pivotWorldPos.x + pivotWorldPos.y) * 10.0;
                float floatOffset = sin(_Time.y * 2.5 + floatingOffset) * (1.0 / 32.0);
                input.positionOS.y += floatOffset;

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

                // Shiny effect (셰이더 내부 시간 기반, GPU 인스턴싱 호환)
                float shinyDuration = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ShinyDuration);
                float shinyDelay = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ShinyDelay);
                float totalCycle = shinyDuration + shinyDelay;
                float3 pivotPos = float3(UNITY_MATRIX_M[0][3], UNITY_MATRIX_M[1][3], UNITY_MATRIX_M[2][3]);
                float phaseOffset = frac((pivotPos.x + pivotPos.y) * 0.37);
                float shinyTime = fmod(_Time.y + phaseOffset * totalCycle, totalCycle);
                float shinyLocation = lerp(-1.0, 2.0, saturate(shinyTime / shinyDuration));

                float shinyAngle = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ShinyAngle);
                float rad = shinyAngle * 0.01745329;
                float sinA = sin(rad);
                float cosA = cos(rad);
                float2 centeredUV = input.uv - 0.5;
                float2 rotatedUV = float2(centeredUV.x * cosA - centeredUV.y * sinA,
                                           centeredUV.x * sinA + centeredUV.y * cosA) + 0.5;
                float dist = abs(rotatedUV.x - shinyLocation);
                float shinyWidth = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ShinyWidth);
                float shinySoftness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ShinySoftness);
                float glow = smoothstep(shinyWidth, shinyWidth - shinySoftness, dist);
                half4 shinyColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ShinyColor);
                float shinyEnabled = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ShinyEnabled);
                color.rgb += shinyColor.rgb * glow * shinyColor.a * color.a * shinyEnabled;

                return color;
            }
            ENDHLSL
        }
    }
}