// 용암 아지랑이. 용암 칸 위 화면이 군데군데 가로로 1텍셀씩 일렁여 열기가 피어오르는 것처럼 보인다.
// StageSporeHaze가 카메라를 따라다니는 흰 스프라이트 한 장에 이 머티리얼을 끼워, 화면 캡처(Objects 레이어까지,
// _CameraSortingLayerTexture) 뒤의 정렬 자리에 그린다. 캡처한 화면을 텍셀 단위로 옮겨 읽어 그대로 덮어쓴다.
// 캡처는 쓰는 곳이 있을 때만 켜지므로 설정에서 usesScreenCapture를 켜 둔다(떠 있는 동안 캡처 사용자로 등록).
// - 물(용암) 칸에만 그린다: 물 스텐실이 켜 둔 1번 비트가 있는 픽셀만 통과한다. 나무 그림은 외곽선 스텐실이
//   값을 바꿔 두므로 용암 위에 겹친 나무는 일렁이지 않는다.
// - 뜨거운 색(빨강이 강하고 밝은 색)끼리만 옮긴다: 용암 안쪽 무늬·테두리·반짝임만 일렁이고, 용암과 바닥의 경계나
//   캐릭터처럼 다른 색이 섞이는 곳은 그대로 둔다.
// 2D 조명은 받지 않는다(캡처한 화면이 이미 조명을 받은 색이다).
Shader "Custom/2D/LavaHeatHaze"
{
    Properties
    {
        [HideInInspector] _MainTex("Diffuse", 2D) = "white" {}
        _HazeAmplitude("일렁임 폭(텍셀)", Range(0, 2)) = 1
        _HazeRowFrequency("가로줄 물결 촘촘함(텍셀당)", Float) = 0.7
        _HazeSpeed("물결이 위로 오르는 속도", Float) = 3
        _HazePatchScale("일렁이는 구역 크기(월드)", Float) = 1.5
        _HazePatchCoverage("일렁이는 구역 문턱(클수록 덜 일렁임)", Range(0, 1)) = 0.45
        _HazePatchRise("구역이 위로 오르는 속도(월드/초)", Float) = 0.6
        _HazeHotMinRed("뜨거운 색 최소 빨강", Float) = 0.9
        _HazeHotMaxBlueRatio("뜨거운 색 파랑/빨강 최대 비율", Range(0, 1)) = 0.6

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend One Zero
        Cull Off
        ZWrite Off
        ZTest Always

        Stencil
        {
            Ref 1
            ReadMask 1
            WriteMask 0
            Comp Equal
            Pass Keep
        }

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
                float2 positionWS : TEXCOORD1;
            };

            TEXTURE2D(_CameraSortingLayerTexture);

            CBUFFER_START(UnityPerMaterial)
                float _HazeAmplitude;
                float _HazeRowFrequency;
                float _HazeSpeed;
                float _HazePatchScale;
                float _HazePatchCoverage;
                float _HazePatchRise;
                float _HazeHotMinRed;
                float _HazeHotMaxBlueRatio;
                half4 _Color;
            CBUFFER_END

            float HeatHash21(float2 _p)
            {
                float3 p3 = frac(float3(_p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float HeatNoise(float2 _p)
            {
                float2 i = floor(_p);
                float2 f = frac(_p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = HeatHash21(i);
                float b = HeatHash21(i + float2(1.0, 0.0));
                float c = HeatHash21(i + float2(0.0, 1.0));
                float d = HeatHash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            bool IsHot(half3 _color)
            {
                return _color.r >= _HazeHotMinRed && _color.b <= _color.r * _HazeHotMaxBlueRatio;
            }

            Varyings vert(Attributes _input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(_input.positionOS.xyz);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.positionWS = TransformObjectToWorld(_input.positionOS.xyz).xy;
                return o;
            }

            half4 frag(Varyings _input) : SV_Target
            {
                const float ppu = 32.0;
                float time = _Time.y;
                // 텍셀 하나가 한 값을 갖도록 월드 픽셀 격자로 내림한다.
                float2 texel = floor(_input.positionWS * ppu);
                float2 texelCenterWS = (texel + 0.5) / ppu;

                // 위로 피어오르는 구역 안에서만 일렁인다.
                float patch = HeatNoise(float2(texelCenterWS.x, texelCenterWS.y - time * _HazePatchRise) / max(_HazePatchScale, 0.1));
                float strength = smoothstep(_HazePatchCoverage, _HazePatchCoverage + 0.2, patch);
                if (strength <= 0.0)
                {
                    discard;
                }

                // 가로줄마다 위로 오르는 물결만큼 옆으로 옮긴다(텍셀 단위로 끊는다).
                float jitter = HeatHash21(float2(floor(texel.x / 6.0), texel.y)) * 6.2832;
                float wave = sin(texel.y * _HazeRowFrequency - time * _HazeSpeed + jitter * 0.35);
                float shift = round(wave * _HazeAmplitude * strength);
                if (abs(shift) < 0.5)
                {
                    discard;
                }

                float2 screenUV = _input.screenPos.xy / _input.screenPos.w;
                // 텍셀 수를 화면 비율로 바꾼다(정사영 카메라 가로 반폭 = unity_OrthoParams.x).
                float2 shiftedUV = screenUV + float2(shift / ppu / (2.0 * unity_OrthoParams.x), 0.0);

                half4 source = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_PointClamp, screenUV);
                half4 shifted = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_PointClamp, shiftedUV);
                if (false == IsHot(source.rgb) || false == IsHot(shifted.rgb))
                {
                    discard;
                }

                return half4(shifted.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
