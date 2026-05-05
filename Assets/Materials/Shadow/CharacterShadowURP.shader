Shader "Custom/CharacterShadowURP"
{
    Properties
    {
        [MainColor] _BaseColor("Shadow Color", Color) = (0, 0, 0, 0.5)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _BottomWidth("Bottom Width Taper", Range(0.1, 2.0)) = 0.5
        _TaperCenter("Taper Center", Range(0.0, 1.0)) = 0.5
        _TaperHeight("Taper Height Range", Range(0.0, 1.0)) = 1.0
        _PPU("Pixels Per Unit", Float) = 32
        
        [HideInInspector] _UvRect("Sprite UV Rect", Vector) = (0, 0, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+1" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            Stencil
            {
                Ref 0
                Comp Equal
                ReadMask 7
                WriteMask 7
                Pass IncrSat
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _BaseMap_TexelSize;
                float4 _UvRect; 
                float _BottomWidth;
                float _TaperCenter;
                float _TaperHeight;
                float _PPU;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float ppu = _PPU;
                float2 worldPos = IN.worldPos.xy;

                float2 snappedWorldPos = (floor(worldPos * ppu) + 0.5) / ppu;
                float2 worldDelta = snappedWorldPos - worldPos;

                float2 dx_wp = ddx(worldPos);
                float2 dy_wp = ddy(worldPos);
                float2 dx_uv = ddx(IN.uv);
                float2 dy_uv = ddy(IN.uv);

                float det = dx_wp.x * dy_wp.y - dx_wp.y * dy_wp.x;
                float2 snappedUV = IN.uv;

                if (abs(det) > 1e-8)
                {
                    float2 deltaScreen;
                    deltaScreen.x = (dy_wp.y * worldDelta.x - dy_wp.x * worldDelta.y) / det;
                    deltaScreen.y = (-dx_wp.y * worldDelta.x + dx_wp.x * worldDelta.y) / det;
                    snappedUV += dx_uv * deltaScreen.x + dy_uv * deltaScreen.y;
                }

                float2 localUV;
                localUV.x = (snappedUV.x - _UvRect.x) / max(_UvRect.z - _UvRect.x, 0.0001);
                localUV.y = (snappedUV.y - _UvRect.y) / max(_UvRect.w - _UvRect.y, 0.0001);

                float normalizedY = saturate(localUV.y / max(_TaperHeight, 0.0001));
                float taper = lerp(_BottomWidth, 1.0, normalizedY);
                
                float offsetFromCenter = localUV.x - _TaperCenter;
                float dist = abs(offsetFromCenter);
                float taperedDist = dist / max(taper, 0.0001);
                
                if (taperedDist > 0.501) clip(-1);

                float finalLocalX = (offsetFromCenter / max(taper, 0.0001)) + _TaperCenter;

                float2 finalUV;
                finalUV.x = finalLocalX * (_UvRect.z - _UvRect.x) + _UvRect.x;
                finalUV.y = snappedUV.y;

                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, finalUV);
                clip(texColor.a - 0.1);
                
                return half4(_BaseColor.rgb, texColor.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
