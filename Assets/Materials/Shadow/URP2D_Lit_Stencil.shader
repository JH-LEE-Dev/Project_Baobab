Shader "Custom/URP2D_Lit_Stencil"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        
        // 💡 SpriteRenderer를 사용하는 경우, 유니티 내부 구조상 _MainTex를 요구할 수 있어 숨김 속성으로 추가해두면 안전합니다.
        [HideInInspector] _MainTex("Sprite Texture", 2D) = "white" {}
    }

    SubShader
    {
        // 렌더 타입과 큐를 AlphaTest(Cutout)에 맞게 설정
        Tags 
        { 
            "RenderType" = "TransparentCutout" 
            "Queue" = "AlphaTest" 
            "RenderPipeline" = "UniversalPipeline" 
        }
        
        Cull Off

        Pass
        {
            // [핵심] URP 2D 렌더러가 조명 연산 패스에서 이 객체를 인식하게 하는 태그
            Tags { "LightMode" = "Universal2D" }

            // 요청하신 스텐실 블록: 이 영역을 렌더링할 때 스텐실 버퍼에 2를 기록
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
                float4 color : COLOR; // Sprite Renderer의 Vertex Color 지원
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 screenPos : TEXCOORD1; // 2D 라이팅을 위한 스크린 좌표
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // URP 2D 렌더러가 생성한 2D 조명 텍스처
            TEXTURE2D(_ShapeLightTexture0);
            SAMPLER(sampler_ShapeLightTexture0);

            // SRP Batcher 호환성을 위해 _ST 속성을 제거합니다. (2D Renderer 제약 사항)
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Cutoff;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                
                // TRANSFORM_TEX 대신 입력 UV를 그대로 사용합니다.
                OUT.uv = IN.uv;
                
                OUT.color = IN.color;
                
                // 2D 조명을 받아오기 위해 현재 버텍스의 스크린 좌표 계산
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 1. 베이스 텍스처와 색상 계산
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor * IN.color;
                
                // 2. 알파 테스트 (컷오프)
                // 중요: 여기서 clip으로 픽셀이 버려지면, 해당 픽셀은 스텐실 버퍼에 2를 기록하지 않습니다! (원하시는 외곽선/그림자 마스킹에 완벽히 부합)
                clip(baseColor.a - _Cutoff);
                
                // 3. 2D 스크린 UV 계산 및 2D 조명 텍스처 샘플링
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                half4 lightColor = SAMPLE_TEXTURE2D(_ShapeLightTexture0, sampler_ShapeLightTexture0, screenUV);

                // 4. 최종 색상 = 베이스 색상 * 2D 조명 색상
                return baseColor * lightColor;
            }
            ENDHLSL
        }
    }
}