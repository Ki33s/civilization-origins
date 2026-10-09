Shader "Origins/Forest Vertex Lit"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.15
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #pragma multi_compile_instancing
        struct Input { float4 color : COLOR; };
        fixed4 _Color;
        half _Glossiness;
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Authored vertex palette is sRGB, like a painted albedo texture.
            o.Albedo = GammaToLinearSpace(IN.color.rgb) * _Color.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
