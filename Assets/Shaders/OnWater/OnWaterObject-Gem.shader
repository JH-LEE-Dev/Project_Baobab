Shader "Custom/OnWaterObject-Gem"
{
    // OnWaterObject 의 보석(결정) 변종.
    // 물결 일렁임(UV 왜곡)과 스텐실/블렌딩은 원본과 완전히 동일하고,
    // 샘플링한 색 위에 ApplyTreeGem 으로 면 단위 반사를 얹는 것만 다르다.
    //
    // 면 배치는 월드 좌표 기준이라 물결이 일렁여도 면 자체는 정지해 있다.
    // 스프라이트만 출렁이고 보석 결은 고정되어야 "물에 비친 보석"으로 읽힌다.
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Alpha ("Alpha", Range(0, 1)) = 1.0
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        [Header(HDR)]
        _HDRIntensity("HDR Intensity", Float) = 1

        _WaveSpeed("Wave Speed", Float) = 2.0
        _WaveStrength("Wave Strength", Float) = 0.01
        _WaveFreq("Wave Frequency", Float) = 15.0
        _DistortionAmount("Distortion Amount", Range(0, 2)) = 1.0

        [Header(Gem Facets)]
        _GemAmount("Gem Amount", Range(0,1)) = 1
        // 면 격자는 월드 좌표계라 UV 단위인 _WaveStrength와 단위가 달라 따로 둔다.
        // 면 크기(_FacetSize / 32)의 몇 분의 1 수준이 자연스럽다.
        _GemWaveStrength("Gem Wave Strength (world)", Float) = 0.05
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

        // Legacy properties. They're here so that materials using this shader can gracefully fallback to the legacy sprite shader.
        [HideInInspector] _BaseColor ("Base Color", Color) = (1,1,1,1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+1"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Stencil
            {
                Ref 1
                ReadMask 3
                WriteMask 2
                Comp Equal
                Pass Invert
            }
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
                // xy = 프래그먼트 월드 좌표(물결 + 보석 면 계산용), zw = 오브젝트 피봇 월드 좌표(형상 법선용)
                float4 worldPos : TEXCOORD1;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            #include "../Include/TreeGem.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _HDRIntensity)
                UNITY_DEFINE_INSTANCED_PROP(float, _Alpha)
                UNITY_DEFINE_INSTANCED_PROP(float, _WaveSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _WaveStrength)
                UNITY_DEFINE_INSTANCED_PROP(float, _WaveFreq)
                UNITY_DEFINE_INSTANCED_PROP(float, _DistortionAmount)
                UNITY_DEFINE_INSTANCED_PROP(float4, _GemColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _GemColorB)
                UNITY_DEFINE_INSTANCED_PROP(float, _GemAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _GemWaveStrength)
                UNITY_DEFINE_INSTANCED_PROP(float, _Iridescence)
                UNITY_DEFINE_INSTANCED_PROP(float, _RainbowAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _RainbowHueBase)
                UNITY_DEFINE_INSTANCED_PROP(float, _RainbowHueRange)
                UNITY_DEFINE_INSTANCED_PROP(float, _RainbowSaturation)
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

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Color) * unity_SpriteColor;

                // 일렁임과 보석 면 계산을 위해 월드 XY 좌표 전달.
                // zw에는 이 오브젝트(물 위 반사)의 피봇을 넣어 형상 법선의 중심으로 삼는다.
                float3 wPos = TransformObjectToWorld(input.positionOS.xyz);
                float3 objectPivotWorldPos = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                o.worldPos = float4(wPos.xy, objectPivotWorldPos.xy);

                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                float2 waveCoord = input.worldPos.xy;

                float _waveSpeed = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _WaveSpeed);
                float _waveStrength = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _WaveStrength);
                float _waveFreq = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _WaveFreq);
                float _distortionAmount = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DistortionAmount);

                float2 distortion;
                distortion.x = sin(_Time.y * _waveSpeed + waveCoord.y * _waveFreq) * _waveStrength;
                distortion.y = cos(_Time.y * _waveSpeed * 0.7 + waveCoord.x * _waveFreq * 0.8) * _waveStrength;

                float2 finalUV = input.uv + (distortion * _distortionAmount);

                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, finalUV) * input.color;
                color.a *= UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Alpha);

                if (color.a < 0.01) discard;

                // 면도 물결에 맞춰 함께 출렁이게 한다.
                // 스프라이트 왜곡과 정확히 같은 위상/주파수를 쓰되, 면 격자는 월드 좌표계라
                // UV 단위인 _WaveStrength 대신 월드 단위 _GemWaveStrength로 진폭만 따로 잡는다.
                float _gemWaveStrength = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _GemWaveStrength);

                float2 gemWave;
                gemWave.x = sin(_Time.y * _waveSpeed + waveCoord.y * _waveFreq) * _gemWaveStrength;
                gemWave.y = cos(_Time.y * _waveSpeed * 0.7 + waveCoord.x * _waveFreq * 0.8) * _gemWaveStrength;

                float2 gemWorldPos = input.worldPos.xy + (gemWave * _distortionAmount);

                // 본체와 동일한 ppu(32)를 넘겨 면 격자가 나무 본체의 면과 같은 크기로 맞는다.
                color.rgb = ApplyTreeGem(color.rgb, gemWorldPos, input.worldPos.zw, 32.0, BuildGemParams());

                color.rgb *= UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HDRIntensity);
                return color;
            }
            ENDHLSL
        }
    }
}
