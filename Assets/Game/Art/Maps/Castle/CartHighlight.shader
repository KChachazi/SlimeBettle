Shader "SlimeBattle/Sprites/Dining Cart Highlight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1, 0.78, 0.24, 1)
        _PulseSpeed ("Pulse Speed", Float) = 4
        _HighlightStrength ("Highlight Strength", Float) = 1.1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _OutlineColor;
            float _PulseSpeed;
            float _HighlightStrength;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed silhouette = tex2D(_MainTex, input.uv).a;
                fixed pulse = 0.72 + 0.28 * sin(_Time.y * _PulseSpeed);
                fixed alpha = silhouette * _OutlineColor.a * pulse *
                    _HighlightStrength * input.color.a * 0.55;
                return fixed4(_OutlineColor.rgb, alpha);
            }
            ENDCG
        }
    }
}
