Shader "Custom/OnWaterObject"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Alpha ("Alpha", Range(0, 1)) = 1.0
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0
        
        _WaveSpeed("Wave Speed", Float) = 2.0
        _WaveStrength("Wave Strength", Float) = 0.01
        _WaveFreq("Wave Frequency", Float) = 15.0
        _DistortionAmount("Distortion Amount", Range(0, 2)) = 1.0

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
                float2 worldPos : TEXCOORD1;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
          
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Alpha;
                float _WaveSpeed;
                float _WaveStrength;
                float _WaveFreq;
                float _DistortionAmount;
            CBUFFER_END

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                
                // 일렁임 계산을 위해 월드 XY 좌표 전달
                float3 wPos = TransformObjectToWorld(input.positionOS.xyz);
                o.worldPos = wPos.xy;
                
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                // 월드 좌표를 기준으로 일렁임 계산
                float2 waveCoord = input.worldPos;
                
                float2 distortion;
                distortion.x = sin(_Time.y * _WaveSpeed + waveCoord.y * _WaveFreq) * _WaveStrength;
                distortion.y = cos(_Time.y * _WaveSpeed * 0.7 + waveCoord.x * _WaveFreq * 0.8) * _WaveStrength;

                // 최종 샘플링 UV에 왜곡 적용
                float2 finalUV = input.uv + (distortion * _DistortionAmount);

                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, finalUV) * input.color;
                color.a *= _Alpha;
                
                if (color.a < 0.01) discard;

                return color;
            }
            ENDHLSL
        }
    }
}
