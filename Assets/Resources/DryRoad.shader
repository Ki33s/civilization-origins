Shader "Origins/Dry Road"
{
    Properties { _Color ("Earth",Color)=(0.42,0.30,0.16,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct input { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            fixed4 _Color;
            output vert(input v) { output o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
            fixed4 frag(output i):SV_Target
            {
                float grain=frac(sin(dot(i.uv,float2(271.1,131.7)))*43758.54);
                return fixed4(_Color.rgb*(.92+grain*.16),1);
            }
            ENDCG
        }
    }
}
