using System.Collections.Generic;
using UnityEngine;

namespace Origins
{
    public sealed partial class WorldView
    {
        readonly List<Mesh> riverMeshes=new List<Mesh>();
        void BuildSmoothRivers()
        {
            var outgoing=new Dictionary<Hex,Hex>(); var incoming=new Dictionary<Hex,List<Hex>>();
            foreach(var edge in game.World.RiverEdges)
            {
                outgoing[edge.From]=edge.To;
                if(!incoming.ContainsKey(edge.To)) incoming[edge.To]=new List<Hex>();
                incoming[edge.To].Add(edge.From);
            }
            var water=Mat("river water",new Color(.22f,.48f,.54f)); water.shader=Shader.Find("Origins/River Water");
            var bank=Mat("river sediment",new Color(.26f,.31f,.20f)); bank.shader=water.shader;
            var flow=new Dictionary<Hex,int>();
            foreach(var edge in game.World.RiverEdges)
            {
                if(incoming.ContainsKey(edge.From)) continue;
                Hex h=edge.From;
                while(outgoing.ContainsKey(h)) { if(!flow.ContainsKey(h)) flow[h]=0; flow[h]++; h=outgoing[h]; }
                if(!flow.ContainsKey(h)) flow[h]=0; flow[h]++;
            }
            foreach(var edge in game.World.RiverEdges)
            {
                Vector3 a=RiverPoint(edge.From),b=RiverPoint(edge.To);
                Vector3 da=RiverTangent(edge.From,outgoing,incoming),db=RiverTangent(edge.To,outgoing,incoming);
                const int samples=32;
                var curve=new Vector3[samples+1]; var widths=new float[samples+1]; var fades=new float[samples+1];
                float widthA=.105f+Mathf.Sqrt(flow.ContainsKey(edge.From)?flow[edge.From]:1)*.025f;
                float widthB=.105f+Mathf.Sqrt(flow.ContainsKey(edge.To)?flow[edge.To]:1)*.025f;
                bool mouth=game.World.Get(edge.To).Terrain==Terrain.Ocean;
                bool spring=!incoming.ContainsKey(edge.From);
                Vector3 bend=Vector3.Cross(Vector3.up,(b-a).normalized);
                float meander=((edge.From.Q*17+edge.From.R*31+game.World.Seed)%2==0 ? 1 : -1)*.15f;
                for(int i=0;i<=samples;i++)
                {
                    float t=i/(float)samples;
                    float wave=Mathf.Sin(t*Mathf.PI);
                    curve[i]=Bezier(a,a+da*.60f,b-db*.60f,b,t)+bend*(wave*wave*meander);
                    widths[i]=Mathf.Lerp(widthA,widthB,t)*(mouth ? 1+t*t*2.5f : 1)*(spring ? Mathf.Lerp(.06f,1,Mathf.SmoothStep(0,1,t*2)) : 1);
                    fades[i]=(mouth ? 1-t*t : 1)*(spring ? Mathf.Clamp01(t*8) : 1);
                }
                // Only four ribbon renderers per logical edge; decor remains independent model instances.
                for(int half=0;half<2;half++)
                {
                    Transform owner=details[half==0 ? edge.From : edge.To].transform;
                    AddRiverRibbon(owner,curve,widths,fades,half*16,16,bank,1.55f,.012f);
                    AddRiverRibbon(owner,curve,widths,fades,half*16,16,water,1,.021f);
                }
            }
        }
        Vector3 RiverPoint(Hex h)
        {
            uint n=unchecked((uint)(h.Q*73856093 ^ h.R*19349663 ^ game.World.Seed));
            float angle=(n%360)*Mathf.Deg2Rad;
            return FlatPosition(h)+new Vector3(Mathf.Cos(angle)*.32f,0,Mathf.Sin(angle)*.32f);
        }
        Vector3 RiverTangent(Hex h,Dictionary<Hex,Hex> outgoing,Dictionary<Hex,List<Hex>> incoming)
        {
            Vector3 d=Vector3.zero;
            if(outgoing.ContainsKey(h)) d+=(RiverPoint(outgoing[h])-RiverPoint(h)).normalized;
            if(incoming.ContainsKey(h)) foreach(var before in incoming[h]) d+=(RiverPoint(h)-RiverPoint(before)).normalized/incoming[h].Count;
            return d.normalized;
        }
        static Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t)
        { float s=1-t; return a*s*s*s+b*3*s*s*t+c*3*s*t*t+d*t*t*t; }
        void AddRiverRibbon(Transform parent,Vector3[] curve,float[] widths,float[] fades,int start,int count,Material material,float widthScale,float lift)
        {
            var points=new Vector3[(count+1)*2]; var uv=new Vector2[points.Length]; var colors=new Color[points.Length]; var triangles=new int[count*6];
            for(int i=0;i<=count;i++)
            {
                int index=start+i; Vector3 center=curve[index];
                Vector3 side=Vector3.Cross(Vector3.up,(curve[Mathf.Min(curve.Length-1,index+1)]-curve[Mathf.Max(0,index-1)]).normalized);
                Vector3 left=OnSurface(center-side*widths[index]*widthScale*.5f),right=OnSurface(center+side*widths[index]*widthScale*.5f);
                float height=Mathf.Max(left.y,right.y)+lift; left.y=right.y=height;
                points[i*2]=parent.InverseTransformPoint(left); points[i*2+1]=parent.InverseTransformPoint(right);
                uv[i*2]=new Vector2(0,center.x+center.z); uv[i*2+1]=new Vector2(1,center.x+center.z);
                colors[i*2]=colors[i*2+1]=new Color(1,1,1,fades[index]);
                if(i<count) { int v=i*2,k=i*6; triangles[k]=v; triangles[k+1]=v+2; triangles[k+2]=v+1; triangles[k+3]=v+1; triangles[k+4]=v+2; triangles[k+5]=v+3; }
            }
            var mesh=new Mesh{name="River ribbon"}; mesh.vertices=points; mesh.triangles=triangles; mesh.uv=uv; mesh.colors=colors;
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); riverMeshes.Add(mesh);
            Detail(parent,"Smooth river · "+material.name,mesh,Vector3.zero,Vector3.one,material);
        }
    }
}
