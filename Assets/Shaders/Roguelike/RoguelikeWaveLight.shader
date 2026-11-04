Shader "Custom/Roguelike/WaveLightAdditive"
{
    // 로그라이크 바닥 타일 파도의 빛(마루 빛·바뀌는 칸 반짝임·바다 파문·물빛 틈 빛). 칸 색(알파 = 세기)만큼 화면에 더한다.
    // 흰색을 반투명하게 덮으면 어두운 티어에서 뿌옇게(라벤더빛으로) 보여, 빛처럼 더하는 방식으로 그린다. 조명 영향을 받지 않는다.
    // 픽셀 정렬은 바닥 셰이더(Custom-Sprite-Default)와 같게 월드 픽셀 격자에 맞춘다(카메라가 픽셀 사이에 있어도 칸 테두리와 어긋나지 않게).
    Properties
    {
        [MainTexture] _MainTex("Texture", 2D) = "white" {}
        _Intensity("Intensity", Float) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        // 색은 더하고 알파(대상)는 그대로 둔다.
        Blend SrcAlpha One, Zero One
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            // 칸 색(타일 색)은 버텍스 색이나 스프라이트 인스턴스 색(unity_SpriteColor)으로 온다 - 바닥 셰이더처럼 둘 다 곱한다.
            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4 color       : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                half4 color        : COLOR;
                float3 worldPos    : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Intensity;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                SetUpSpriteInstanceProperties();
                v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);

                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS);
                o.worldPos = TransformObjectToWorld(v.positionOS);
                o.uv = v.uv;
                o.color = v.color * unity_SpriteColor;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // 월드 픽셀(1/32) 중심으로 UV를 옮긴다(Custom-Sprite-Default와 같은 계산).
                float ppu = 32.0;
                float2 worldPos = i.worldPos.xy;
                float2 snapped = (floor(worldPos * ppu) + 0.5) / ppu;
                float2 delta = snapped - worldPos;
                float2 dxWp = ddx(worldPos);
                float2 dyWp = ddy(worldPos);
                float2 dxUv = ddx(i.uv);
                float2 dyUv = ddy(i.uv);
                float det = dxWp.x * dyWp.y - dxWp.y * dyWp.x;
                float2 uv = i.uv;
                if (abs(det) > 1e-8)
                {
                    uv += (delta.x * (dyWp.y * dxUv - dyWp.x * dyUv) + delta.y * (dxWp.x * dyUv - dxWp.y * dxUv)) / det;
                }

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half alpha = tex.a * i.color.a;
                clip(alpha - 0.002);
                return half4(tex.rgb * i.color.rgb * _Intensity, alpha);
            }
            ENDHLSL
        }
    }
}
