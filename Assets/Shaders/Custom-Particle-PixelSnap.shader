Shader "Custom/2D/Particle-PixelSnap"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        [HideInInspector] _TilesX("Tiles X", Float) = 3
        [HideInInspector] _TilesY("Tiles Y", Float) = 1
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                float3 centerOS     : TEXCOORD1;
                half4 color         : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                half4 color         : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _TilesX;
                float _TilesY;
            CBUFFER_END

            float4 SnapClipPositionByParticleCenter(float4 vertexCS, float4 centerCS)
            {
                #if UNITY_UV_STARTS_AT_TOP
                float signY = -1.0;
                #else
                float signY = 1.0;
                #endif

                float2 centerNDC = centerCS.xy / centerCS.w;
                float2 centerScreenPixel = (centerNDC + float2(1.0, signY)) * 0.5 * _ScreenParams.xy;
                float2 vertexNDC = vertexCS.xy / vertexCS.w;
                float2 vertexScreenPixel = (vertexNDC + float2(1.0, signY)) * 0.5 * _ScreenParams.xy;
                float2 vertexOffset = vertexScreenPixel - centerScreenPixel;
                float2 snappedHalfSize = max(round(abs(vertexOffset) * 2.0), float2(1.0, 1.0)) * 0.5;
                float2 centerOffset = frac(snappedHalfSize);
                float2 snappedCenterScreenPixel = floor(centerScreenPixel - centerOffset + 0.5) + centerOffset;
                float2 snappedVertexScreenPixel = snappedCenterScreenPixel + sign(vertexOffset) * snappedHalfSize;
                float2 snappedVertexNDC = snappedVertexScreenPixel / _ScreenParams.xy * 2.0 - float2(1.0, signY);

                vertexCS.xy = snappedVertexNDC * vertexCS.w;
                return vertexCS;
            }

            Varyings UnlitVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 vertexWS = TransformObjectToWorld(input.positionOS.xyz);
                float4 vertexCS = TransformWorldToHClip(vertexWS);
                float4 centerCS = TransformWorldToHClip(TransformObjectToWorld(input.centerOS));

                o.positionCS = SnapClipPositionByParticleCenter(vertexCS, centerCS);
                o.uv = input.uv;
                o.color = input.color * _Color;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                float2 tileCount = max(float2(_TilesX, _TilesY), float2(1.0, 1.0));
                float2 textureSize = _MainTex_TexelSize.zw;
                float2 tileSize = textureSize / tileCount;
                float2 texelCoord = input.uv * _MainTex_TexelSize.zw;
                float2 tileIndex = clamp(floor(texelCoord / tileSize), float2(0.0, 0.0), tileCount - 1.0);
                float2 tileOrigin = tileIndex * tileSize;
                float2 localTexel = texelCoord - tileOrigin;
                localTexel = clamp(floor(localTexel) + 0.5, float2(0.5, 0.5), tileSize - 0.5);
                float2 snappedUV = (tileOrigin + localTexel) * _MainTex_TexelSize.xy;

                half4 texColor = tex2D(_MainTex, snappedUV);
                half4 finalColor = texColor * input.color;

                clip(finalColor.a - 0.01);
                return finalColor;
            }
            ENDHLSL
        }
    }
}
