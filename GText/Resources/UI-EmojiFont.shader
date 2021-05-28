Shader "UI/EmojiFont" {
    Properties {
        [PerRendererData] _MainTex ("Font Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        
        _ColorMask ("Color Mask", Float) = 15
        
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        
        [Space(15)]
        _EmojiTex ("Emoji Texture", 2D) = "white" {}
        _EmojiSize ("Emoji Size", Range(0, 1)) = 0.125
        _LineCount ("Line Count",float) = 8
        _FrameSpeed ("FrameSpeed",Range(0,10)) = 3
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
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile __ UNITY_UI_ALPHACLIP
            
            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                half2 texcoord  : TEXCOORD0;
                half2 texcoord1 : TEXCOORD1;
            };
            
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(float4(IN.vertex.x, IN.vertex.y, IN.vertex.z, 1.0));

                OUT.texcoord1 = half2(floor(IN.texcoord.x*0.1), floor(IN.texcoord.y*0.1));
                OUT.texcoord = IN.texcoord - OUT.texcoord1*10;
                
                #ifdef UNITY_HALF_TEXEL_OFFSET
                OUT.vertex.xy += (_ScreenParams.zw-1.0) * float2(-1,1) * OUT.vertex.w;
                #endif
                
                OUT.color = IN.color * _Color;
                return OUT;
            }

            sampler2D _MainTex;
            sampler2D _EmojiTex;
            float _EmojiSize;
            float _LineCount;
            float _FrameSpeed;

            fixed4 emoji(half2 offset, half2 uv)
            {
                // compute current frame index of emoji
                half index = abs(fmod(floor(_Time.x * _FrameSpeed * 50), offset.y));
                // compute real uv xy
                float x = fmod((offset.x+index), _LineCount)*_EmojiSize + uv.x*_EmojiSize;
                float y = floor((offset.x+index)/_LineCount)*_EmojiSize + uv.y*_EmojiSize;

                fixed4 color = tex2D(_EmojiTex, float2(x, y));
                return color;
            }

            fixed4 font(half2 uv)
            {
                return tex2D(_MainTex, uv) + _TextureSampleAdd;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half fi = step(IN.texcoord1.y, 0);
                half eq = IN.texcoord1.y == 0;

                fixed4 color = font(IN.texcoord)*IN.color*eq + (IN.color*fi + emoji(IN.texcoord1, IN.texcoord)*(1-fi))*(1-eq);
                
                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }

    CustomEditor "EmojiFontShaderEditor"
}