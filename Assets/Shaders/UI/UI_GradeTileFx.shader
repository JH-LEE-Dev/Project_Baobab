Shader "UI/GradeTileFx"
{
    // 슬롯 타일(배경) 위에 얹는 스테인드글라스 보석 레이어. 원목(ItemImg)은 이 레이어 위에 그려지므로 가려지지 않는다.
    //
    // 면(보로노이)·가짜 법선·회전 광원·명암 단계·플래시·십자 반짝임·영롱함·무지개는 희귀나무 보석 셰이더와
    // 같은 코드(Include/TreeGem.hlsl)를 그대로 쓴다. 이 셰이더가 더하는 것은 세 가지뿐이다.
    //  1. 면 사이의 납선(스테인드글라스의 어두운 테두리)
    //  2. 원목이 놓이는 중앙의 방사형 감쇠(사각 상자가 아니라 부드럽게 옅어진다)
    //  3. 획득 버스트: Image.color 의 R 채널(1 - 세기)로 받는다(머티리얼 복제 없이 슬롯별 값 전달)
    Properties
    {
        [PerRendererData] _MainTex("Tile Sprite", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)

        // UI 정점의 로컬 좌표는 캔버스 배치 때 캔버스 기준으로 바뀌므로(슬롯 위치가 달라지면 패턴이 어긋난다)
        // 픽셀 좌표는 UV와 스프라이트 크기로 구한다. 스프라이트 한 장짜리 텍스처(아틀라스 아님)가 전제다.
        _PixelSize("Pixel Size (sprite w, h)", Vector) = (24, 24, 0, 0)

        [Header(Gem Color)]
        [HDR] _GemColor("Gem Color", Color) = (1, 0.75, 0.2, 1)
        [HDR] _GemColorB("Gem Color B (iridescence)", Color) = (1, 0.93, 0.55, 1)
        _Iridescence("Iridescence", Range(0,1)) = 0.55
        _RainbowAmount("Rainbow Amount", Range(0,1)) = 0
        _RainbowHueBase("Rainbow Hue Base", Range(0,1)) = 0
        _RainbowHueRange("Rainbow Hue Range", Range(0,1)) = 1
        _RainbowSaturation("Rainbow Saturation", Range(0,1)) = 0.75

        [Header(Gem Facets)]
        _FacetSize("Facet Size (px)", Range(3,12)) = 6
        _ShadeSteps("Shade Steps", Range(2,8)) = 5
        _FacetRandomness("Facet Randomness", Range(0,1)) = 0.6
        _FormBulge("Form Bulge", Range(0,4)) = 1.2
        _DeepShade("Deep Shade (unlit facet)", Range(0,1)) = 0.3
        _FacetVariation("Facet Brightness Variation", Range(0,0.5)) = 0.08

        [Header(Gem Light)]
        _SweepSpeed("Light Rotation Speed", Float) = 0.8
        _LightHeight("Light Height", Float) = 0.8

        [Header(Gem Flash)]
        _Whiteness("Flash Whiteness", Range(0,1)) = 0.8
        _FlashThreshold("Flash Threshold", Range(0.3,1)) = 0.8
        _FlashStrength("Flash Strength", Range(0,1)) = 1
        _SpecStrength("Flash Bloom", Range(0,2)) = 0.35

        [Header(Gem Sparkle)]
        _SparkleRatio("Sparkle Facet Ratio", Range(0,1)) = 0.25
        _SparkleSpeed("Sparkle Speed", Float) = 2.5
        _SparkleSize("Sparkle Size", Range(0,0.4)) = 0.08
        _SparkleBrightness("Sparkle Brightness", Range(0,3)) = 1.2

        [Header(Stained Glass)]
        _LeadWidth("Lead Line Width (facet units)", Range(0,0.5)) = 0.12
        _LeadDarkness("Lead Line Brightness", Range(0,1)) = 0.28
        _GemAlpha("Glass Alpha", Range(0,1)) = 0.9
        _CenterAmp("Center Amount (log area)", Range(0,1)) = 0.35

        [HideInInspector] _StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask("Color Mask", Float) = 15
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
            float4 _PixelSize;

            float4 _GemColor;
            float4 _GemColorB;
            float _Iridescence;
            float _RainbowAmount;
            float _RainbowHueBase;
            float _RainbowHueRange;
            float _RainbowSaturation;
            float _FacetSize;
            float _ShadeSteps;
            float _FacetRandomness;
            float _FormBulge;
            float _DeepShade;
            float _FacetVariation;
            float _SweepSpeed;
            float _LightHeight;
            float _Whiteness;
            float _FlashThreshold;
            float _FlashStrength;
            float _SpecStrength;
            float _SparkleRatio;
            float _SparkleSpeed;
            float _SparkleSize;
            float _SparkleBrightness;
            float _LeadWidth;
            float _LeadDarkness;
            float _GemAlpha;
            float _CenterAmp;

            TreeGemParams BuildGemParams()
            {
                TreeGemParams g;
                g.amount            = 1.0;
                g.gemColor          = _GemColor.rgb;
                g.gemColorB         = _GemColorB.rgb;
                g.iridescence       = _Iridescence;
                g.rainbowAmount     = _RainbowAmount;
                g.rainbowHueBase    = _RainbowHueBase;
                g.rainbowHueRange   = _RainbowHueRange;
                g.rainbowSaturation = _RainbowSaturation;
                g.facetSize         = _FacetSize;
                g.shadeSteps        = _ShadeSteps;
                g.sweepSpeed        = _SweepSpeed;
                g.lightFollow       = 0.0;
                g.lightHeight       = _LightHeight;
                g.facetRandomness   = _FacetRandomness;
                g.formBulge         = _FormBulge;
                g.formCenterY       = 0.0;
                g.deepShade         = _DeepShade;
                g.whiteness         = _Whiteness;
                g.flashThreshold    = _FlashThreshold;
                g.flashStrength     = _FlashStrength;
                g.facetVariation    = _FacetVariation;
                g.lumaInfluence     = 0.0;
                g.lumaBias          = 1.0;
                g.specStrength      = _SpecStrength;
                g.sparkleRatio      = _SparkleRatio;
                g.sparkleSpeed      = _SparkleSpeed;
                g.sparkleSize       = _SparkleSize;
                g.sparkleBrightness = _SparkleBrightness;
                return g;
            }

            // 면 경계까지의 거리(면 크기 단위). GemVoronoi와 같은 좌표/해시로 가장 가까운 사이트와
            // 두 번째로 가까운 사이트의 거리 차를 구한다. 이 값이 작을수록 경계 근처다.
            float GemBorder(float2 _p, float _facetSize)
            {
                float2 c = _p / max(_facetSize, 1.0);
                float2 baseCell = floor(c);
                float2 f = c - baseCell;
                float d1 = 8.0;
                float d2 = 8.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y);
                        float2 site = offset + GemHash2(baseCell + offset);
                        float2 diff = site - f;
                        float d = sqrt(dot(diff, diff));
                        if (d < d1) { d2 = d1; d1 = d; }
                        else if (d < d2) { d2 = d; }
                    }
                }

                return d2 - d1;
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

                float2 p = floor((IN.texcoord - 0.5) * _PixelSize.xy) + 0.5;

                // 타일 안쪽 면만(어두운 갈색 테두리와 모서리 컷 바깥 제외).
                // 텍스처는 선형 색 공간으로 샘플되므로(sRGB 226 -> 0.55) 문턱은 선형 기준이다.
                // 갈색 테두리(선형 약 0.05~0.09)만 걸러내고, 밝기가 낮은 베이지 가장자리 줄까지 모두 덮는다.
                float lum = (tex.r + tex.g + tex.b) / 3.0;
                float inner = step(0.5, tex.a) * step(0.2, lum);

                // 희귀나무와 같은 면/광원/반짝임. 슬롯 중심이 형상 중심이라 면 법선이 돔처럼 바깥을 향한다.
                float3 gem = ApplyTreeGem(float3(1.0, 1.0, 1.0), p, float2(0.0, 0.0), 1.0, BuildGemParams());

                // 납선: 면 경계의 어두운 틴트 선
                float border = GemBorder(p, _FacetSize);
                float lead = 1.0 - step(_LeadWidth, border);
                float3 leadColor = _GemColor.rgb * _LeadDarkness;
                if (_RainbowAmount > 0.5)
                    leadColor = float3(0.18, 0.12, 0.22);
                // 테두리 선 바로 안쪽 1px는 어두운 틴트로 둘러서, 면 색과 테두리 선 색이 비슷해도 선이 먹히지 않게 한다.
                float rim = step(9.0, max(abs(p.x), abs(p.y)));
                lead = max(lead, rim);
                float3 rgb = lerp(gem, leadColor, lead);

                // 원목이 놓이는 중앙은 옅게(방사형 감쇠)
                float rr = length(p);
                float amp = lerp(_CenterAmp, 1.0, saturate((rr - 3.0) / 7.0));
                float alpha = amp * _GemAlpha;

                // 획득 버스트
                float burst = saturate(1.0 - IN.color.r);
                rgb = lerp(rgb, float3(1.0, 1.0, 1.0), burst);
                alpha = saturate(alpha + burst * 0.55);

                half4 color = half4(rgb, inner * alpha * IN.color.a);

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
