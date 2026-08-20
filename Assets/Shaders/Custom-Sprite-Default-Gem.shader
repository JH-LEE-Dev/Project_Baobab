Shader "Custom/2D/Custom-Sprite-Default-Gem"
{
    // Custom-Sprite-Default 의 보석(결정) 변종 (범용 셰이더).
    // Custom-Sprite-Default-Tree-Gem 에서 나무 전용 결합(Wind Sway, 다중 렌더러 피봇 등)을 제거하고,
    // 보로노이 패싯 기반 반사, 영롱함(Iridescence), 무지개(Rainbow), 스파클(Sparkle) 등
    // 보석 비주얼 효과를 어떤 2D 스프라이트에서도 단독으로 사용할 수 있도록 구성된 범용 셰이더입니다.
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

        _FlashAmount("Flash Amount", Range(0,1)) = 0

        [Header(Gem Facets)]
        _GemAmount("Gem Amount", Range(0,1)) = 1
        _FacetSize("Facet Size (px)", Range(2,24)) = 7
        _ShadeSteps("Shade Steps", Range(2,8)) = 5
        _FacetRandomness("Facet Randomness", Range(0,1)) = 0.45
        _FormBulge("Form Bulge", Range(0,4)) = 1.6
        _FormCenterY("Form Center Y", Float) = 1

        [Header(Gem Light)]
        // 광원 위치는 C#(GemLightSource)에서 전역으로 넣는다. 여기 프로퍼티로 두면 안 된다.
        _LightFollow("Follow Character Light", Range(0,1)) = 1
        _LightHeight("Character Light Height", Float) = 2
        _SweepSpeed("Fallback Sweep Speed", Float) = 0.8

        [Header(Gem Color)]
        [HDR] _GemColor("Gem Color", Color) = (0.22, 0.45, 1, 1)
        [HDR] _GemColorB("Gem Color B (iridescence)", Color) = (0.45, 0.92, 1, 1)
        _Iridescence("Iridescence", Range(0,1)) = 0.55
        _RainbowAmount("Rainbow Amount", Range(0,1)) = 0
        _RainbowHueBase("Rainbow Hue Base", Range(0,1)) = 0.55
        _RainbowHueRange("Rainbow Hue Range", Range(0,1)) = 0.45
        _RainbowSaturation("Rainbow Saturation", Range(0,1)) = 0.7
        _GemAlpha("Gem Alpha", Range(0,1)) = 0.82
        _DeepShade("Deep Shade (unlit facet)", Range(0,1)) = 0.25
        _FacetVariation("Facet Brightness Variation", Range(0,0.5)) = 0.08
        _LumaInfluence("Sprite Luma Influence", Range(0,2)) = 0.75
        _LumaBias("Luma Bias", Range(0,1)) = 0.35

        [Header(Gem Flash)]
        _FlashThreshold("Flash Threshold", Range(0.3,1)) = 0.82
        _FlashStrength("Flash Strength", Range(0,1)) = 1
        _Whiteness("Flash Whiteness", Range(0,1)) = 0.85
        _SpecStrength("Flash Bloom", Range(0,2)) = 0.5

        [Header(Gem Sparkle)]
        _SparkleRatio("Sparkle Facet Ratio", Range(0,1)) = 0.15
        _SparkleSpeed("Sparkle Speed", Float) = 2.5
        _SparkleSize("Sparkle Size", Range(0,0.4)) = 0.06
        _SparkleBrightness("Sparkle Brightness", Range(0,3)) = 1.2
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
            #include "Include/TreeGem.hlsl"

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
                // xy = 월드 좌표, zw = 오브젝트 피봇 월드 좌표.
                float4 worldPos    : TEXCOORD7;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(half4, _GemColor)
                UNITY_DEFINE_INSTANCED_PROP(half4, _GemColorB)
                UNITY_DEFINE_INSTANCED_PROP(float, _Iridescence)
                UNITY_DEFINE_INSTANCED_PROP(float, _RainbowAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _RainbowHueBase)
                UNITY_DEFINE_INSTANCED_PROP(float, _RainbowHueRange)
                UNITY_DEFINE_INSTANCED_PROP(float, _RainbowSaturation)
                UNITY_DEFINE_INSTANCED_PROP(float, _GemAlpha)
                UNITY_DEFINE_INSTANCED_PROP(float, _HDRIntensity)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlashAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _GemAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _FacetSize)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShadeSteps)
                UNITY_DEFINE_INSTANCED_PROP(float, _SweepSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _LightFollow)
                UNITY_DEFINE_INSTANCED_PROP(float, _LightHeight)
                UNITY_DEFINE_INSTANCED_PROP(float, _FacetRandomness)
                UNITY_DEFINE_INSTANCED_PROP(float, _FormBulge)
                UNITY_DEFINE_INSTANCED_PROP(float, _FormCenterY)
                UNITY_DEFINE_INSTANCED_PROP(float, _DeepShade)
                UNITY_DEFINE_INSTANCED_PROP(float, _Whiteness)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlashThreshold)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlashStrength)
                UNITY_DEFINE_INSTANCED_PROP(float, _FacetVariation)
                UNITY_DEFINE_INSTANCED_PROP(float, _LumaInfluence)
                UNITY_DEFINE_INSTANCED_PROP(float, _LumaBias)
                UNITY_DEFINE_INSTANCED_PROP(float, _SpecStrength)
                UNITY_DEFINE_INSTANCED_PROP(float, _SparkleRatio)
                UNITY_DEFINE_INSTANCED_PROP(float, _SparkleSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _SparkleSize)
                UNITY_DEFINE_INSTANCED_PROP(float, _SparkleBrightness)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            TreeGemParams BuildGemParams()
            {
                TreeGemParams p;
                p.amount            = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _GemAmount);
                p.gemColor          = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _GemColor).rgb;
                p.gemColorB         = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _GemColorB).rgb;
                p.iridescence       = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Iridescence);
                p.rainbowAmount     = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _RainbowAmount);
                p.rainbowHueBase    = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _RainbowHueBase);
                p.rainbowHueRange   = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _RainbowHueRange);
                p.rainbowSaturation = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _RainbowSaturation);
                p.facetSize         = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FacetSize);
                p.shadeSteps        = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ShadeSteps);
                p.sweepSpeed        = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SweepSpeed);
                p.lightFollow       = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _LightFollow);
                p.lightHeight       = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _LightHeight);
                p.facetRandomness   = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FacetRandomness);
                p.formBulge         = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FormBulge);
                p.formCenterY       = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FormCenterY);
                p.deepShade         = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DeepShade);
                p.whiteness         = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Whiteness);
                p.flashThreshold    = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FlashThreshold);
                p.flashStrength     = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FlashStrength);
                p.facetVariation    = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FacetVariation);
                p.lumaInfluence     = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _LumaInfluence);
                p.lumaBias          = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _LumaBias);
                p.specStrength      = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SpecStrength);
                p.sparkleRatio      = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SparkleRatio);
                p.sparkleSpeed      = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SparkleSpeed);
                p.sparkleSize       = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SparkleSize);
                p.sparkleBrightness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SparkleBrightness);
                return p;
            }

            Varyings LitVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                float3 objectPivotWorldPos = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);

                Varyings o = CommonLitVertex(input);
                o.worldPos = float4(TransformObjectToWorld(input.positionOS).xy, objectPivotWorldPos.xy);
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

                // 픽셀 격자에 맞춰 보석(결정) 반사/영롱함/스파클 셰이딩 적용
                color.rgb = ApplyTreeGem(color.rgb, worldPos, input.worldPos.zw, ppu, BuildGemParams());

                color.rgb *= UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HDRIntensity);
                color.rgb = lerp(color.rgb, half3(1,1,1), UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FlashAmount) * color.a);

                // 살짝 비치게 하여 투명도 적용. 피격 플래시가 color.a를 참조하므로 플래시 뒤에 곱연산
                color.a *= UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _GemAlpha);
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
            // 노멀 패스는 색을 만들지 않으므로 보석 파라미터를 쓰지 않지만, 머티리얼 프로퍼티
            // 레이아웃을 컬러 패스와 동일하게 유지하기 위해 같은 순서로 선언한다.
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(half4, _GemColor)
                UNITY_DEFINE_INSTANCED_PROP(half4, _GemColorB)
                UNITY_DEFINE_INSTANCED_PROP(float, _Iridescence)
                UNITY_DEFINE_INSTANCED_PROP(float, _RainbowAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _RainbowHueBase)
                UNITY_DEFINE_INSTANCED_PROP(float, _RainbowHueRange)
                UNITY_DEFINE_INSTANCED_PROP(float, _RainbowSaturation)
                UNITY_DEFINE_INSTANCED_PROP(float, _GemAlpha)
                UNITY_DEFINE_INSTANCED_PROP(float, _HDRIntensity)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlashAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _GemAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _FacetSize)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShadeSteps)
                UNITY_DEFINE_INSTANCED_PROP(float, _SweepSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _LightFollow)
                UNITY_DEFINE_INSTANCED_PROP(float, _LightHeight)
                UNITY_DEFINE_INSTANCED_PROP(float, _FacetRandomness)
                UNITY_DEFINE_INSTANCED_PROP(float, _FormBulge)
                UNITY_DEFINE_INSTANCED_PROP(float, _FormCenterY)
                UNITY_DEFINE_INSTANCED_PROP(float, _DeepShade)
                UNITY_DEFINE_INSTANCED_PROP(float, _Whiteness)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlashThreshold)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlashStrength)
                UNITY_DEFINE_INSTANCED_PROP(float, _FacetVariation)
                UNITY_DEFINE_INSTANCED_PROP(float, _LumaInfluence)
                UNITY_DEFINE_INSTANCED_PROP(float, _LumaBias)
                UNITY_DEFINE_INSTANCED_PROP(float, _SpecStrength)
                UNITY_DEFINE_INSTANCED_PROP(float, _SparkleRatio)
                UNITY_DEFINE_INSTANCED_PROP(float, _SparkleSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _SparkleSize)
                UNITY_DEFINE_INSTANCED_PROP(float, _SparkleBrightness)
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
            #include "Include/TreeGem.hlsl"

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
                // xy = 월드 좌표, zw = 오브젝트 피봇 월드 좌표.
                float4 worldPos : TEXCOORD7;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(half4, _GemColor)
                UNITY_DEFINE_INSTANCED_PROP(half4, _GemColorB)
                UNITY_DEFINE_INSTANCED_PROP(float, _Iridescence)
                UNITY_DEFINE_INSTANCED_PROP(float, _RainbowAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _RainbowHueBase)
                UNITY_DEFINE_INSTANCED_PROP(float, _RainbowHueRange)
                UNITY_DEFINE_INSTANCED_PROP(float, _RainbowSaturation)
                UNITY_DEFINE_INSTANCED_PROP(float, _GemAlpha)
                UNITY_DEFINE_INSTANCED_PROP(float, _HDRIntensity)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlashAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _GemAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _FacetSize)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShadeSteps)
                UNITY_DEFINE_INSTANCED_PROP(float, _SweepSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _LightFollow)
                UNITY_DEFINE_INSTANCED_PROP(float, _LightHeight)
                UNITY_DEFINE_INSTANCED_PROP(float, _FacetRandomness)
                UNITY_DEFINE_INSTANCED_PROP(float, _FormBulge)
                UNITY_DEFINE_INSTANCED_PROP(float, _FormCenterY)
                UNITY_DEFINE_INSTANCED_PROP(float, _DeepShade)
                UNITY_DEFINE_INSTANCED_PROP(float, _Whiteness)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlashThreshold)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlashStrength)
                UNITY_DEFINE_INSTANCED_PROP(float, _FacetVariation)
                UNITY_DEFINE_INSTANCED_PROP(float, _LumaInfluence)
                UNITY_DEFINE_INSTANCED_PROP(float, _LumaBias)
                UNITY_DEFINE_INSTANCED_PROP(float, _SpecStrength)
                UNITY_DEFINE_INSTANCED_PROP(float, _SparkleRatio)
                UNITY_DEFINE_INSTANCED_PROP(float, _SparkleSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _SparkleSize)
                UNITY_DEFINE_INSTANCED_PROP(float, _SparkleBrightness)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            TreeGemParams BuildGemParams()
            {
                TreeGemParams p;
                p.amount            = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _GemAmount);
                p.gemColor          = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _GemColor).rgb;
                p.gemColorB         = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _GemColorB).rgb;
                p.iridescence       = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Iridescence);
                p.rainbowAmount     = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _RainbowAmount);
                p.rainbowHueBase    = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _RainbowHueBase);
                p.rainbowHueRange   = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _RainbowHueRange);
                p.rainbowSaturation = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _RainbowSaturation);
                p.facetSize         = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FacetSize);
                p.shadeSteps        = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ShadeSteps);
                p.sweepSpeed        = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SweepSpeed);
                p.lightFollow       = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _LightFollow);
                p.lightHeight       = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _LightHeight);
                p.facetRandomness   = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FacetRandomness);
                p.formBulge         = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FormBulge);
                p.formCenterY       = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FormCenterY);
                p.deepShade         = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DeepShade);
                p.whiteness         = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Whiteness);
                p.flashThreshold    = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FlashThreshold);
                p.flashStrength     = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FlashStrength);
                p.facetVariation    = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FacetVariation);
                p.lumaInfluence     = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _LumaInfluence);
                p.lumaBias          = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _LumaBias);
                p.specStrength      = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SpecStrength);
                p.sparkleRatio      = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SparkleRatio);
                p.sparkleSpeed      = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SparkleSpeed);
                p.sparkleSize       = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SparkleSize);
                p.sparkleBrightness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SparkleBrightness);
                return p;
            }

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                float3 objectPivotWorldPos = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);

                Varyings o = CommonUnlitVertex(input);
                o.worldPos = float4(TransformObjectToWorld(input.positionOS).xy, objectPivotWorldPos.xy);
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

                // 픽셀 격자에 맞춰 보석(결정) 반사/영롱함/스파클 셰이딩 적용
                color.rgb = ApplyTreeGem(color.rgb, worldPos, input.worldPos.zw, ppu, BuildGemParams());

                color.rgb *= UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HDRIntensity);
                color.rgb = lerp(color.rgb, half3(1,1,1), UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _FlashAmount) * color.a);

                // 살짝 비치게 하여 투명도 적용. 피격 플래시가 color.a를 참조하므로 플래시 뒤에 곱연산
                color.a *= UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _GemAlpha);
                return color;
            }
            ENDHLSL
        }
    }
}
