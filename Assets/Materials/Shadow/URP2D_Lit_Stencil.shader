Shader "Custom/URP2D_Lit_Stencil"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _MainTex("Sprite Texture", 2D) = "white" {}
        [NoScaleOffset] _NormalMap("Normal Map", 2D) = "bump" {}
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "TransparentCutout" 
            "Queue" = "AlphaTest" 
            "RenderPipeline" = "UniversalPipeline" 
        }
        
        Cull Off

        // 1. Universal2D Pass: 조명 연산 및 스텐실 기록 패스
        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            Stencil
            {
                Ref 2
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 screenPos : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_ShapeLightTexture0);
            SAMPLER(sampler_ShapeLightTexture0);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Cutoff;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 baseColor = texColor * _BaseColor * IN.color;
                clip(baseColor.a - _Cutoff);
                
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                half4 lightColor = SAMPLE_TEXTURE2D(_ShapeLightTexture0, sampler_ShapeLightTexture0, screenUV);

                return baseColor * lightColor;
            }
            ENDHLSL
        }

        // 2. NormalsRendering Pass: 2D 조명이 노말 맵을 인식하게 해주는 패스
        Pass
        {
            Tags { "LightMode" = "NormalsRendering" }

            Stencil
            {
                Ref 2
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Cutoff;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 알파 테스트
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 baseColor = texColor * _BaseColor * IN.color;
                clip(baseColor.a - _Cutoff);

                // 1. 노말맵에서 탄젠트 공간 노말 데이터 추출
                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv));
                
                // 2. 스프라이트의 평면 방향 벡터들을 월드 공간으로 변환
                float3 worldTangent = TransformObjectToWorldDir(float3(1, 0, 0));
                float3 worldBitangent = TransformObjectToWorldDir(float3(0, 1, 0));
                
                // 💡 [핵심 수정] 스프라이트의 앞면(카메라/빛 방향)은 -Z입니다. 
                // 기존의 (0,0,1)은 뒷면을 향하고 있어 조명 거리가 멀어질수록 어두워지는 문제가 있었습니다.
                float3 worldNormal = TransformObjectToWorldNormal(float3(0, 0, -1));
                
                // 3. 탄젠트 공간 노말을 월드 공간으로 최종 변환
                float3 finalNormalWS = normalTS.x * worldTangent + normalTS.y * worldBitangent + normalTS.z * worldNormal;
                
                return half4(normalize(finalNormalWS), 1.0);
            }
            ENDHLSL
        }
    }
}
