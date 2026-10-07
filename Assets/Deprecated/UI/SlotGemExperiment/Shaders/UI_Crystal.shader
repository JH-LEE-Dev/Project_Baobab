Shader "UI/Crystal"
{
    // 슬롯 칸 같은 작은 UI 이미지에 다이아몬드/크리스탈 같은 유리 재질을 입힌다.
    // UI/TreeGem(나무 보석 룩)과 달리 면 사이의 밝은 경계선, 면마다 다른 색상, 면 내부의 빛 번짐,
    // 날카로운 광택을 만든다. 나무 쪽 셰이더와 공유하는 코드가 없어 나무에는 영향이 없다.
    //
    // 좌표: 이미지 로컬 좌표를 쓴다(캔버스 1유닛 = 기준 해상도 1픽셀). 픽셀 중심으로 스냅해서 면과 경계선이
    // 도트 단위로 딱딱 끊어진다.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Mask)]
        // 0보다 크면 스프라이트 밝기가 이 값보다 어두운 픽셀(슬롯의 어두운 테두리)을 투명하게 만든다.
        _BorderLumaMax("Border Luma Max (0 = off)", Range(0,1)) = 0.45
        _Amount("Crystal Amount", Range(0,1)) = 1

        [Header(Facets)]
        _FacetSize("Facet Size (px)", Range(2,16)) = 5
        _FacetRandomness("Facet Randomness", Range(0,1)) = 0.6
        _FormRadius("Form Radius (px)", Float) = 14
        _ShadeSteps("Shade Steps", Range(2,8)) = 4

        [Header(Center Calm)]
        // 아이콘이 놓이는 타일 중앙을 어둡고 잔잔하게 눌러, 면 대비/경계선/광택/스파클을 가장자리에만 남긴다.
        // 밝은 아이콘이 중앙의 어두운 바탕 위에서 또렷하게 읽히게 하는 용도. 0이면 사용하지 않는다.
        _CenterCalm("Center Calm (0 = off)", Range(0,1)) = 0
        _CalmInner("Calm Inner Radius (px)", Range(0,16)) = 4
        _CalmOuter("Calm Outer Radius (px)", Range(1,24)) = 11
        _CenterValue("Center Value", Range(0,1)) = 0.2

        [Header(Edge Highlight)]
        _EdgeWidth("Edge Width (px)", Range(0.2,2)) = 0.7
        _EdgeStrength("Edge Strength", Range(0,1)) = 0.85
        _EdgeColor("Edge Color", Color) = (0.9, 0.97, 1, 1)

        [Header(Color)]
        _HueBase("Hue Base", Range(0,1)) = 0.58
        _HueRange("Hue Range (per facet)", Range(0,1)) = 0.3
        _Saturation("Saturation", Range(0,1)) = 0.75
        _ValueMin("Value Min (dark facet)", Range(0,1)) = 0.25
        _ValueMax("Value Max (lit facet)", Range(0,1.5)) = 0.95
        _InnerGlow("Inner Glow", Range(0,1)) = 0.35
        _GlowFalloff("Inner Glow Falloff", Range(0.5,4)) = 1.6
        _AlphaMin("Facet Alpha Min (translucency)", Range(0.3,1)) = 0.8
        _LumaInfluence("Sprite Luma Influence", Range(0,1)) = 0.3
        _LumaBias("Luma Bias", Range(0,1)) = 0.75

        [Header(Light)]
        _SweepSpeed("Light Sweep Speed", Float) = 0.8
        _LightHeight("Light Height", Float) = 1.2
        _FlashThreshold("Flash Threshold", Range(0.5,1)) = 0.9
        _FlashStrength("Flash Strength", Range(0,1)) = 0.8

        [Header(Sparkle)]
        _SparkleRatio("Sparkle Facet Ratio", Range(0,1)) = 0.25
        _SparkleSpeed("Sparkle Speed", Float) = 2.2
        _SparkleSize("Sparkle Size", Range(0,0.4)) = 0.08
        _SparkleBrightness("Sparkle Brightness", Range(0,3)) = 1

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

            float _BorderLumaMax;
            float _CenterCalm;
            float _CalmInner;
            float _CalmOuter;
            float _CenterValue;
            float _Amount;
            float _FacetSize;
            float _FacetRandomness;
            float _FormRadius;
            float _ShadeSteps;
            float _EdgeWidth;
            float _EdgeStrength;
            float4 _EdgeColor;
            float _HueBase;
            float _HueRange;
            float _Saturation;
            float _ValueMin;
            float _ValueMax;
            float _InnerGlow;
            float _GlowFalloff;
            float _AlphaMin;
            float _LumaInfluence;
            float _LumaBias;
            float _SweepSpeed;
            float _LightHeight;
            float _FlashThreshold;
            float _FlashStrength;
            float _SparkleRatio;
            float _SparkleSpeed;
            float _SparkleSize;
            float _SparkleBrightness;

            float2 Hash2(float2 p)
            {
                float2 h = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(h) * 43758.5453);
            }

            // 채도 s, 명도 1인 색상환 색. 명도는 호출부에서 곱한다.
            float3 HueToRgb(float hue, float sat)
            {
                float3 rgb = saturate(abs(frac(hue + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
                return lerp(float3(1.0, 1.0, 1.0), rgb, sat);
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

                // 어두운 테두리는 이 레이어에서 지운다(아래 SlotImg의 테두리가 그대로 보인다).
                if (_BorderLumaMax > 0.0 && dot(tex.rgb, half3(0.299, 0.587, 0.114)) < _BorderLumaMax)
                    color.a = 0.0;

                if (color.a > 0.001)
                {
                    // 픽셀 중심으로 스냅한 좌표. 면 경계선이 도트 단위로 끊어지게 한다.
                    float2 pixelPos = floor(IN.localPosition.xy) + 0.5;
                    float facetSize = max(_FacetSize, 1.0);
                    float2 g = pixelPos / facetSize;
                    float2 baseCell = floor(g);
                    float2 f = g - baseCell;

                    // 1차: 가장 가까운 면(사이트) 찾기
                    float bestDistSq = 8.0;
                    float2 bestSite = float2(0.0, 0.0);
                    float2 bestOffset = float2(0.0, 0.0);
                    [unroll] for (int y = -1; y <= 1; y++)
                    {
                        [unroll] for (int x = -1; x <= 1; x++)
                        {
                            float2 offset = float2(x, y);
                            float2 site = offset + Hash2(baseCell + offset);
                            float2 diff = site - f;
                            float d = dot(diff, diff);
                            if (d < bestDistSq) { bestDistSq = d; bestSite = site; bestOffset = offset; }
                        }
                    }

                    // 2차: 이웃 면과의 경계(수직이등분선)까지의 거리. 경계선 하이라이트에 쓴다.
                    float borderDist = 8.0;
                    [unroll] for (int y2 = -1; y2 <= 1; y2++)
                    {
                        [unroll] for (int x2 = -1; x2 <= 1; x2++)
                        {
                            float2 offset2 = float2(x2, y2);
                            float2 site2 = offset2 + Hash2(baseCell + offset2);
                            float2 dv = site2 - bestSite;
                            float len = dot(dv, dv);
                            if (len > 1e-5)
                            {
                                float2 mid = 0.5 * (site2 + bestSite);
                                borderDist = min(borderDist, dot(mid - f, dv * rsqrt(len)));
                            }
                        }
                    }

                    float2 cellId = baseCell + bestOffset;
                    float2 rnd = Hash2(cellId + 17.3);
                    float2 rnd2 = Hash2(cellId + 91.7);
                    float2 rnd3 = Hash2(cellId + 53.1);

                    // 면 법선: 고정된 무작위 법선과, 이미지 중심에서 퍼지는 둥근 덩어리 법선을 섞는다.
                    float angle = rnd.x * 6.2831853;
                    float3 randomNormal = normalize(float3(cos(angle), sin(angle), lerp(0.4, 1.2, rnd.y)));
                    float2 siteCenterPx = (baseCell + bestSite) * facetSize;
                    float3 formNormal = normalize(float3(siteCenterPx / max(_FormRadius, 1.0), 1.0));
                    float3 facetNormal = normalize(lerp(formNormal, randomNormal, saturate(_FacetRandomness)));

                    // 시간에 따라 도는 광원
                    float lightAngle = _Time.y * _SweepSpeed;
                    float3 lightDir = normalize(float3(cos(lightAngle), sin(lightAngle), max(_LightHeight, 0.05)));
                    float ndl = saturate(dot(facetNormal, lightDir));
                    float steps = max(_ShadeSteps, 2.0);
                    float shade = round(ndl * (steps - 1.0)) / (steps - 1.0);

                    // 면마다 다른 색상. 채도는 높게 유지해 유리/보석처럼 선명하게 보이게 한다.
                    float hue = frac(_HueBase + (rnd2.x - 0.5) * _HueRange);
                    float value = lerp(_ValueMin, _ValueMax, shade);
                    float3 rgb = HueToRgb(hue, _Saturation) * value;

                    // 면 내부의 빛 번짐: 면 중심에 빛이 고인 듯 밝아지고 흰색 쪽으로 옅어진다.
                    float centerDist = sqrt(bestDistSq);
                    float glow = saturate(1.0 - centerDist * _GlowFalloff);
                    rgb = lerp(rgb, float3(1.0, 1.0, 1.0), glow * _InnerGlow * 0.5);
                    rgb += HueToRgb(hue, _Saturation * 0.6) * glow * _InnerGlow * 0.35;

                    // 경계선 하이라이트: 이웃 면과 닿는 선이 밝게 빛난다. 밝은 쪽 면에서 더 강하다.
                    // borderDist는 경계선까지의 한쪽 거리라, 선 전체 폭이 _EdgeWidth가 되려면 절반만 비교한다.
                    float borderPx = borderDist * facetSize;
                    float edge = step(borderPx, _EdgeWidth * 0.5);
                    float3 edgeColor = lerp(HueToRgb(hue, 0.35), _EdgeColor.rgb, 0.65) * (0.65 + 0.5 * shade);
                    rgb = lerp(rgb, edgeColor, edge * _EdgeStrength);

                    // 날카로운 광택: 광원과 거의 정렬된 소수 면은 흰색에 가깝게 번쩍인다.
                    float ndlFlash = saturate(dot(randomNormal, lightDir));
                    float flash = step(_FlashThreshold, ndlFlash);
                    rgb = lerp(rgb, float3(1.0, 1.0, 1.0), flash * _FlashStrength);

                    // 스파클: 일부 면의 중심에서 십자 별빛이 주기적으로 터진다.
                    float sparkleMask = step(rnd3.x, _SparkleRatio);
                    float pulse = saturate(sin(_Time.y * _SparkleSpeed + rnd3.y * 6.2831853));
                    pulse = pulse * pulse * pulse;
                    float2 d = abs(f - bestSite);
                    float star = _SparkleSize / max(d.x + d.y * 5.0, 1e-4) + _SparkleSize / max(d.y + d.x * 5.0, 1e-4);
                    rgb += float3(1.0, 1.0, 1.0) * saturate(star) * pulse * sparkleMask * _SparkleBrightness;

                    // 중앙 차분 처리: 타일 중심에서 멀어질수록 위에서 만든 화려한 룩이 살아나고,
                    // 중심부는 같은 색 계열의 어둡고 잔잔한 면으로 바뀐다(아이콘 가독성).
                    if (_CenterCalm > 0.0)
                    {
                        float radius = length(IN.localPosition.xy);
                        float calm = _CenterCalm * (1.0 - smoothstep(_CalmInner, max(_CalmOuter, _CalmInner + 0.01), radius));
                        float3 calmRgb = HueToRgb(frac(_HueBase + (rnd2.x - 0.5) * _HueRange * 0.35), _Saturation)
                                       * lerp(_CenterValue * 0.8, _CenterValue * 1.2, shade);
                        rgb = lerp(rgb, calmRgb, calm);
                    }

                    // 슬롯 스프라이트의 안쪽 음영(베벨)을 약하게 남긴다.
                    float luma = dot(tex.rgb, float3(0.299, 0.587, 0.114));
                    rgb *= (luma * _LumaInfluence + _LumaBias);

                    // 면마다 살짝 다른 투명도로 뒤(SlotImg)가 은은히 비치게 한다.
                    float facetAlpha = lerp(_AlphaMin, 1.0, rnd.y);
                    float3 original = color.rgb;
                    color.rgb = lerp(original, rgb, saturate(_Amount));
                    color.a *= lerp(1.0, facetAlpha, saturate(_Amount));
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
