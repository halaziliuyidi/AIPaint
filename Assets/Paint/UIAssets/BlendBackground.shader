Shader "Hidden/BlendBackground"
{
    Properties
    {
        _MainTex ("RenderTexture", 2D) = "white" {}
        _BackgroundTex ("BackgroundTexture", 2D) = "white" {}
    }

    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float2 texcoord : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;          // 前景（RenderTexture）
            sampler2D _BackgroundTex;   // 背景

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 背景颜色
                fixed4 bgColor = tex2D(_BackgroundTex, i.texcoord);

                // 前景颜色
                fixed4 fgColor = tex2D(_MainTex, i.texcoord);

                // 使用 Alpha 混合前景和背景
                return lerp(bgColor, fgColor, fgColor.a);
            }
            ENDCG
        }
    }
}
