Shader "Custom/VFX/ParticleHDR_IntensityOnly"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _EmissionIntensity ("HDR Emission Intensity", Float) = 1.0
        
        // Blending state (유니티 URP 표준 프로퍼티)
        [HideInInspector] _Surface("__surface", Float) = 1.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 5.0
        [HideInInspector] _DstBlend("__dst", Float) = 10.0
        [HideInInspector] _ZWrite("__zw", Float) = 0.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True"}
        LOD 100

        // 기본적으로 Alpha Blending (SrcAlpha OneMinusSrcAlpha) 사용
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR; // 파티클 시스템의 색상 (Start Color 등)
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
                float _EmissionIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                
                // 파티클 시스템에서 설정한 색상을 그대로 넘겨줍니다.
                output.color = input.color; 
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                // 텍스처 원본 색상 * 파티클 시스템 색상
                half4 baseColor = texColor * input.color;
                
                // RGB 채널에만 우리가 설정한 Intensity를 곱해 HDR을 만듭니다. 알파(투명도)는 건드리지 않습니다.
                half3 hdrColor = baseColor.rgb * _EmissionIntensity;
                
                return half4(hdrColor, baseColor.a);
            }
            ENDHLSL
        }
    }
}
