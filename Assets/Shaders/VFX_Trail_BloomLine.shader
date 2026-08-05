Shader "Custom/VFX/URP2D_Trail_BloomLine"
{
    Properties
    {
        [Header(Trail Bloom Line)]
        [HDR] _TrailColor ("Trail HDR Color", Color) = (3.0, 2.2, 0.5, 1.0)
        _EmissionIntensity ("HDR Emission Intensity", Float) = 2.0
        _MainTex ("Trail Texture (White for Solid Line)", 2D) = "white" {}

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1.0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1.0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 0.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline"="UniversalPipeline" 
            "IgnoreProjector"="True"
        }

        Blend [_SrcBlend] [_DstBlend]
        Cull [_Cull]
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "TrailBloomLine"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _TrailColor;
                float _EmissionIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // TrailRenderer의 vertex color (alpha가 머리~꼬리 페이드아웃을 자동 처리)
                half4 baseColor = texColor * input.color;

                // HDR 색상 * Emission Intensity → 블룸 반응 극대화
                half3 hdrColor = _TrailColor.rgb * baseColor.rgb * _EmissionIntensity;

                // 알파는 TrailRenderer의 vertex color alpha를 그대로 사용 (자연스러운 꼬리 페이드)
                half finalAlpha = baseColor.a * _TrailColor.a;

                return half4(hdrColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
