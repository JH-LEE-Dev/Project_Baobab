Shader "UI/PixelDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Grid Settings)]
        _TextureSampleAdd ("Texture Sample Add", Color) = (0,0,0,0)
        
        // --- Custom Pixel Dissolve Properties ---
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _GridSize ("Grid Size (Rows)", Float) = 12
        _AspectRatio ("Aspect Ratio (Width/Height)", Float) = 1
        
        [Toggle(_USEDITHERING_ON)] _UseDithering ("Use Dithering", Float) = 1
        _DitherBandWidth ("Dither Strength", Float) = 0.5
        
        // UI Mask Support
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ _USEDITHERING_ON
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            
            // --- Custom Properties ---
            float _DissolveAmount;
            float _GridSize;
            float _AspectRatio;
            float _DitherBandWidth;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                
                // 세로축을 기준으로 GridSize만큼 나누고, 가로축은 AspectRatio(종횡비)를 곱해 1:1 정사각형 타일 비율을 보장함
                float rows = max(1.0, _GridSize);
                float columns = max(1.0, _GridSize * _AspectRatio);
                
                // X, Y 정수 형태의 블록 인덱스 (Floor로 완벽한 정수화 유지)
                float2 blockIndex = floor(uv * float2(columns, rows));
                float blockCenterX = (blockIndex.x + 0.5) / columns;
                
                // 화면 중앙(0.5)으로부터의 X축 거리 (0.0 ~ 1.0)
                float dist = abs(blockCenterX - 0.5) * 2.0; 
                
                // [Fix] 정밀도가 낮은 환경에서 dot 연산 시 같은 난수가 연속으로 튀어나와 
                // 블록들이 묶여버리는 해시 충돌(Hash Collision)을 원천 차단하는 개별 곱 연산으로 교체
                float blockHash = frac(sin(blockIndex.x * 12.9898 + blockIndex.y * 78.233) * 43758.5453);
                
                float visible = 1.0;
                
                // 순수 픽셀 조각화: 기본 거리(dist) 비례 + 픽셀 개별 랜덤 난수(blockHash)
                // 가로줄/세로줄 묶음 연산을 모두 폐기하여 막대기 현상을 원천 차단하고,
                // 오직 완벽하게 독립적인 정사각형 픽셀 단위로만 타다다닥 채워지도록 연산
                float revealTime = (dist * 0.5) + (blockHash * 0.5);
                
                // 현재 진행도가 해당 픽셀의 등장 타이밍보다 작으면 투명하게 처리
                if (_DissolveAmount < revealTime)
                {
                    visible = 0.0;
                }
                
                // 완벽한 초기화 보장 (찌꺼기 방지)
                if (_DissolveAmount <= 0.001)
                {
                    visible = 0.0;
                }
                
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                
                color.a *= visible;
                
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
