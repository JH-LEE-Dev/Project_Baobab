// 제재소 가공 콤보 게이지. 그림은 기존 스프라이트만 쓴다(판 = Boarder.png, 홈 = CutterGauge_background.png,
// 채움 = CutterGauge_fill.png). 이 셰이더는 그 그림을 늘리고 칸으로 나누고 밝히기만 한다.
//
// _Mode 0 : 스프라이트를 그대로 그린다(판).
// _Mode 1 : 홈 스프라이트를 가로로 늘린다(양끝 1px 열은 그대로, 가운데 열은 반복). 채움 스프라이트가 칠해진 자리에만
//           _Segments칸을 그리고, 켜진 칸(_Lit)은 채움색, 새로 켜진 칸·가득 찬 순간·반짝임 칸은 밝게, 식는 칸은 어둡게.
Shader "Custom/VFX/SawmillComboGauge"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FillTex ("Fill Texture", 2D) = "white" {}
        _MainRect ("Main Sprite UV Rect (xMin, yMin, xMax, yMax)", Vector) = (0,0,1,1)
        _FillRect ("Fill Sprite UV Rect (xMin, yMin, xMax, yMax)", Vector) = (0,0,1,1)
        _Mode ("Mode (0 sprite, 1 gauge)", Float) = 0
        _SizePx ("Drawn Size (px)", Vector) = (41,4,0,0)
        _SrcSizePx ("Source Sprite Size (px)", Vector) = (16,4,0,0)
        _Segments ("Segments", Float) = 10
        _SegWidth ("Segment Width (px)", Float) = 3
        _SegGap ("Segment Gap (px)", Float) = 1
        _Lit ("Lit Segments", Float) = 0
        _PopIndex ("Pop Segment Index", Float) = -1
        _Flash ("Flash (0/1)", Float) = 0
        _Shine ("Shine (0/1)", Float) = 0
        _ShineSpeed ("Shine Speed (segments/s)", Float) = 14
        _ShineCycle ("Shine Cycle (segments)", Float) = 22
        _CoolIndex ("Cooling Segment Index", Float) = -1
        _Brighten ("Brighten Amount", Float) = 0.55
        _Darken ("Cooling Darken", Float) = 0.45
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "SawmillComboGaugePass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_FillTex);
            SAMPLER(sampler_FillTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainRect;
                float4 _FillRect;
                float _Mode;
                float4 _SizePx;
                float4 _SrcSizePx;
                float _Segments;
                float _SegWidth;
                float _SegGap;
                float _Lit;
                float _PopIndex;
                float _Flash;
                float _Shine;
                float _ShineSpeed;
                float _ShineCycle;
                float _CoolIndex;
                float _Brighten;
                float _Darken;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            // 원본 스프라이트의 (열, 행) 텍셀 가운데 UV.
            float2 TexelUV(float4 _rect, float2 _texel)
            {
                return _rect.xy + (_texel + 0.5) / _SrcSizePx.xy * (_rect.zw - _rect.xy);
            }

            half4 frag(Varyings input) : SV_Target
            {
                if (_Mode < 0.5)
                {
                    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                }

                float2 local = (input.uv - _MainRect.xy) / max(_MainRect.zw - _MainRect.xy, 1e-5);
                float px = clamp(floor(local.x * _SizePx.x), 0, _SizePx.x - 1);
                float py = clamp(floor(local.y * _SizePx.y), 0, _SrcSizePx.y - 1);

                // 가로로 늘리기: 양끝 1px 열은 원본 양끝 열, 나머지는 원본 가운데 열을 반복.
                float middle = max(_SrcSizePx.x - 2, 1);
                float srcX = px < 1 ? 0 : (px >= _SizePx.x - 1 ? _SrcSizePx.x - 1 : 1 + fmod(px - 1, middle));
                float2 texel = float2(srcX, py);

                half4 track = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TexelUV(_MainRect, texel));
                half4 fill = SAMPLE_TEXTURE2D(_FillTex, sampler_FillTex, TexelUV(_FillRect, texel));

                half4 result = track;
                if (fill.a > 0.5)
                {
                    float pitch = _SegWidth + _SegGap;
                    float inner = px - 1;
                    float seg = floor(inner / pitch);
                    bool bInSeg = inner >= 0 && fmod(inner, pitch) < _SegWidth && seg < _Segments;

                    if (bInSeg && seg < _Lit)
                    {
                        float shine = _Shine > 0.5 ? fmod(floor(_Time.y * _ShineSpeed), _ShineCycle) : -1;
                        bool bBright = _Flash > 0.5 || abs(seg - _PopIndex) < 0.5 || abs(seg - shine) < 0.5;
                        result = half4(bBright ? lerp(fill.rgb, half3(1, 1, 1), (half)_Brighten) : fill.rgb, 1);
                    }
                    else if (bInSeg && abs(seg - _CoolIndex) < 0.5)
                    {
                        result = half4(fill.rgb * (half)_Darken, 1);
                    }
                }

                return result * input.color;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
