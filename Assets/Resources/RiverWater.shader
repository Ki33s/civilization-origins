Shader "Origins/River Water"
{
    Properties { _Color ("Water", Color) = (0.19,0.47,0.53,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            fixed4 _Color;
            v2f vert(appdata v) { v2f o; o.position=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                float wave=sin(i.uv.y*22-_Time.y*1.6+i.uv.x*9)*sin(i.uv.y*7-_Time.y*.8);
                float edge=smoothstep(.12,.48,abs(i.uv.x-.5));
                fixed3 rgb=_Color.rgb+wave*.025+edge*fixed3(.05,.07,.025);
                return fixed4(rgb,i.color.a);
            }
            ENDCG
        }
    }
}
