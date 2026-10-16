Shader "SlimeBattle/UI/ReferenceBackdrop"
{
    Properties { [PerRendererData] _MainTex ("Texture", 2D) = "white" {} _Color ("Tint", Color) = (1,1,1,1) _Blur ("Blur", Float) = 3 }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex; float4 _MainTex_TexelSize; fixed4 _Color; float _Blur;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 c=0; float weight=0;
                for(int y=-2;y<=2;y++) for(int x=-2;x<=2;x++)
                {
                    float w=(3-abs(x))*(3-abs(y));
                    c+=tex2D(_MainTex,i.uv+float2(x,y)*_MainTex_TexelSize.xy*_Blur)*w; weight+=w;
                }
                c/=weight; return c*i.color;
            }
            ENDCG
        }
    }
}
