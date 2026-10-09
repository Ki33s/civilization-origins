Shader "Origins/Simple Waves"
{
    Properties
    {
        _Amplitude ("Wave height", Range(0,0.12)) = 0.035
        _WaveScale ("Wave spacing", Float) = 2.7
        _WaveSpeed ("Wave speed", Float) = 0.55
        _RippleStrength ("Ripple visibility", Range(0,1)) = 1
        _PreviewTime ("Capture time (-1 = live)", Float) = -1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 150
        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow
        #pragma target 3.0
        #pragma multi_compile_instancing
        struct Input { float4 color : COLOR; float3 worldPos; };
        float _Amplitude, _WaveScale, _WaveSpeed, _PreviewTime, _RippleStrength;
        float WaveTime() { return _PreviewTime<0 ? _Time.y : _PreviewTime; }
        void vert(inout appdata_full v)
        {
            float3 p=mul(unity_ObjectToWorld,v.vertex).xyz;
            float t=WaveTime()*_WaveSpeed;
            float a=dot(p.xz,float2(.8,.3))*_WaveScale-t;
            float b=dot(p.xz,float2(-.3,.9))*_WaveScale*1.7+t*.8;
            float amp=_Amplitude*v.color.a;
            v.vertex.y+=amp*(sin(a)*.7+sin(b)*.3);
            float2 gradient=amp*_WaveScale*(cos(a)*.7*float2(.8,.3)+cos(b)*.51*float2(-.3,.9));
            v.normal=normalize(mul((float3x3)unity_WorldToObject,float3(-gradient.x,1,-gradient.y)));
        }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            float t=WaveTime()*_WaveSpeed;
            float a=dot(IN.worldPos.xz,float2(.8,.3))*_WaveScale-t;
            float b=dot(IN.worldPos.xz,float2(-.3,.9))*_WaveScale*1.7+t*.8;
            // Long, gently curved ripple lines rather than isolated bright wave intersections.
            float ripple=sin(a+.3*sin(b));
            float crest=smoothstep(.86,.99,ripple)*(.4+.6*smoothstep(-.5,.5,sin(b*.65)));
            float shore=1-IN.color.a;
            float foam=shore*shore*shore*(.48+.18*sin(a*1.5+t));
            o.Albedo=GammaToLinearSpace(IN.color.rgb)*(.98+.12*ripple*_RippleStrength)+float3(.045,.085,.09)*crest*_RippleStrength+float3(.45,.52,.51)*foam;
            o.Metallic=0;
            o.Smoothness=.25;
            o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
