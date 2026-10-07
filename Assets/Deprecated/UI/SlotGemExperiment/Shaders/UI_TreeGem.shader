Shader "UI/TreeGem"
{
    // 인벤토리 슬롯 등 UI 이미지에 나무 보석 셰이더(Custom-Sprite-Default-Tree-Gem)와 같은 보석 룩을 입힌다.
    // 보석 계산은 Include/TreeGem.hlsl 의 ApplyTreeGem 을 그대로 쓰므로 면 분할/명암/번쩍임/스파클/무지개가
    // 나무와 같은 방식으로 나온다. 달라지는 점은 입력뿐이다.
    //  - 좌표: 월드 좌표 대신 이미지의 로컬 좌표를 쓴다. 캔버스 1유닛 = 기준 해상도 1픽셀이라,
    //          캔버스 종류(스크린/월드)와 무관하게 면이 기준 픽셀 격자에 맞는다.
    //  - 광원: 캐릭터 위치(_GemLightWorldPos)를 따라가지 않고 시간 회전 광원만 쓴다(_LightFollow = 0).
    //  - 알파: 나무처럼 반투명(_GemAlpha)하게 만들지 않고 스프라이트/Image 알파를 그대로 유지한다.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(HDR)]
        _HDRIntensity("HDR Intensity", Float) = 1

        [Header(Mask)]
        // 0보다 크면, 스프라이트 밝기가 이 값보다 어두운 픽셀(예: 슬롯의 어두운 테두리)을 투명하게 만든다.
        // 같은 스프라이트를 아래에 한 번 더 깔아 두면 그 픽셀은 아래 그림이 그대로 보이고,
        // 밝은 안쪽 채움 영역에만 보석 룩이 입혀진다. 0이면 마스크를 쓰지 않는다.
        _BorderLumaMax("Border Luma Max (0 = off)", Range(0,1)) = 0

        [Header(Gem Facets)]
        _GemAmount("Gem Amount", Range(0,1)) = 1
        _FacetSize("Facet Size (px)", Range(1,24)) = 3
        _ShadeSteps("Shade Steps", Range(2,8)) = 5
        _FacetRandomness("Facet Randomness", Range(0,1)) = 0.55
        _FormBulge("Form Bulge", Range(0,40)) = 10
        _FormCenterY("Form Center Y", Float) = 0

        [Header(Gem Light)]
        _LightFollow("Follow Character Light", Range(0,1)) = 0
        _LightHeight("Character Light Height", Float) = 2
        _SweepSpeed("Sweep Speed", Float) = 0.8

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

        // UI Mask Support
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "../Include/TreeGem.hlsl"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 localPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;

            float _HDRIntensity;
            float _BorderLumaMax;
            float _GemAmount;
            float _FacetSize;
            float _ShadeSteps;
            float _FacetRandomness;
            float _FormBulge;
            float _FormCenterY;
            float _LightFollow;
            float _LightHeight;
            float _SweepSpeed;
            float4 _GemColor;
            float4 _GemColorB;
            float _Iridescence;
            float _RainbowAmount;
            float _RainbowHueBase;
            float _RainbowHueRange;
            float _RainbowSaturation;
            float _DeepShade;
            float _FacetVariation;
            float _LumaInfluence;
            float _LumaBias;
            float _FlashThreshold;
            float _FlashStrength;
            float _Whiteness;
            float _SpecStrength;
            float _SparkleRatio;
            float _SparkleSpeed;
            float _SparkleSize;
            float _SparkleBrightness;

            // 나무 셰이더의 픽셀 격자와 같은 PPU. 로컬 좌표(= 기준 픽셀)를 이 값으로 나눠 월드 단위처럼 넘기면
            // ApplyTreeGem 안의 floor(좌표 * ppu)가 정확히 기준 픽셀 하나가 된다.
            static const float GemPpu = 32.0;

            TreeGemParams BuildGemParams()
            {
                TreeGemParams p;
                p.amount            = _GemAmount;
                p.gemColor          = _GemColor.rgb;
                p.gemColorB         = _GemColorB.rgb;
                p.iridescence       = _Iridescence;
                p.rainbowAmount     = _RainbowAmount;
                p.rainbowHueBase    = _RainbowHueBase;
                p.rainbowHueRange   = _RainbowHueRange;
                p.rainbowSaturation = _RainbowSaturation;
                p.facetSize         = _FacetSize;
                p.shadeSteps        = _ShadeSteps;
                p.sweepSpeed        = _SweepSpeed;
                p.lightFollow       = _LightFollow;
                p.lightHeight       = _LightHeight;
                p.facetRandomness   = _FacetRandomness;
                p.formBulge         = _FormBulge;
                p.formCenterY       = _FormCenterY;
                p.deepShade         = _DeepShade;
                p.whiteness         = _Whiteness;
                p.flashThreshold    = _FlashThreshold;
                p.flashStrength     = _FlashStrength;
                p.facetVariation    = _FacetVariation;
                p.lumaInfluence     = _LumaInfluence;
                p.lumaBias          = _LumaBias;
                p.specStrength      = _SpecStrength;
                p.sparkleRatio      = _SparkleRatio;
                p.sparkleSpeed      = _SparkleSpeed;
                p.sparkleSize       = _SparkleSize;
                p.sparkleBrightness = _SparkleBrightness;
                return p;
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.localPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 tex = tex2D(_MainTex, IN.texcoord);
                half4 color = tex * IN.color;

                // 어두운 테두리 픽셀은 이 레이어에서 지운다. 밝기는 Image 색(IN.color)이 아니라 스프라이트
                // 원본 기준으로 판정해야 Tint를 바꿔도 마스크가 흔들리지 않는다.
                if (_BorderLumaMax > 0.0 && dot(tex.rgb, half3(0.299, 0.587, 0.114)) < _BorderLumaMax)
                    color.a = 0.0;

                // 투명한 픽셀에는 보석 계산을 하지 않는다(면 계산이 픽셀마다 해시를 여러 번 부른다).
                if (color.a > 0.001)
                {
                    float2 gemWorldPos = IN.localPosition.xy / GemPpu;
                    color.rgb = ApplyTreeGem(color.rgb, gemWorldPos, float2(0.0, 0.0), GemPpu, BuildGemParams());
                    color.rgb *= _HDRIntensity;
                }

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.localPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
