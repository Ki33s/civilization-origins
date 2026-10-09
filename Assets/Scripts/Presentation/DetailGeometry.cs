using System.Collections.Generic;
using UnityEngine;

namespace Origins
{
    // Finite shared shape library: scene instances keep their own transforms and renderers.
    public static class DetailGeometry
    {
        static readonly Dictionary<string,Mesh> cache=new Dictionary<string,Mesh>();
        public static Mesh Lathe(string key,Vector2[] profile,int sides=48)
        {
            Mesh mesh; if(cache.TryGetValue(key,out mesh) && mesh) return mesh;
            var vertices=new List<Vector3>(); var uv=new List<Vector2>(); var triangles=new List<int>();
            for(int row=0;row<profile.Length;row++) for(int i=0;i<=sides;i++)
            {
                float a=i*Mathf.PI*2/sides;
                vertices.Add(new Vector3(Mathf.Cos(a)*profile[row].x,profile[row].y,Mathf.Sin(a)*profile[row].x)); uv.Add(new Vector2((float)i/sides,(float)row/(profile.Length-1)));
            }
            for(int row=0;row<profile.Length-1;row++) for(int i=0;i<sides;i++)
            {
                int a=row*(sides+1)+i,b=a+sides+1;
                triangles.Add(a); triangles.Add(b); triangles.Add(a+1); triangles.Add(a+1); triangles.Add(b); triangles.Add(b+1);
            }
            mesh=new Mesh{name="Detailed shape · "+key}; mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); cache[key]=mesh; return mesh;
        }
        public static Mesh Roof()
        {
            return Lathe("thatch roof",new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1.01f,.055f),new Vector2(.9f,.12f),new Vector2(.74f,.3f),new Vector2(.5f,.58f),new Vector2(.28f,.85f),new Vector2(.08f,1),new Vector2(0,1.02f)},64);
        }
        public static Mesh Leaf()
        {
            return Lathe("curved tapered leaf",new[]{new Vector2(0,0),new Vector2(.12f,.08f),new Vector2(.42f,.3f),new Vector2(.35f,.6f),new Vector2(.16f,.85f),new Vector2(0,1)},12);
        }
        public static Mesh WeatheredStone(int variant)
        {
            return Lathe("weathered stone "+variant,new[]{new Vector2(0,-.5f),new Vector2(.40f,-.5f),new Vector2(.48f,-.35f),new Vector2(.44f,-.12f),new Vector2(.49f,.08f),new Vector2(.39f,.30f),new Vector2(.25f,.46f),new Vector2(0,.5f)},16+variant*4);
        }
        public static Mesh BevelledBlock()
        {
            Mesh mesh; if(cache.TryGetValue("bevelled block",out mesh) && mesh) return mesh;
            const float inset=.44f,outer=.5f;
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            System.Action<Vector3[]> face=points=>
            {
                int first=vertices.Count; vertices.AddRange(points); Vector3 center=Vector3.zero; foreach(var p in points) center+=p;
                bool reverse=Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),center)<0;
                for(int i=1;i<points.Length-1;i++) { triangles.Add(first); triangles.Add(first+(reverse ? i+1 : i)); triangles.Add(first+(reverse ? i : i+1)); }
            };
            for(int axis=0;axis<3;axis++) for(int sign=-1;sign<=1;sign+=2)
            {
                int b=(axis+1)%3,c=(axis+2)%3; var points=new Vector3[4];
                for(int i=0;i<4;i++) { var p=Vector3.zero; p[axis]=outer*sign; p[b]=(i==0 || i==3 ? -inset : inset); p[c]=(i<2 ? -inset : inset); points[i]=p; } face(points);
            }
            for(int axis=0;axis<3;axis++) for(int sb=-1;sb<=1;sb+=2) for(int sc=-1;sc<=1;sc+=2)
            {
                int b=(axis+1)%3,c=(axis+2)%3; var points=new Vector3[4];
                for(int i=0;i<4;i++) { var p=Vector3.zero; p[axis]=i==0 || i==3 ? -inset : inset; p[b]=sb*(i<2 ? outer : inset); p[c]=sc*(i<2 ? inset : outer); points[i]=p; } face(points);
            }
            for(int sx=-1;sx<=1;sx+=2) for(int sy=-1;sy<=1;sy+=2) for(int sz=-1;sz<=1;sz+=2)
                face(new[]{new Vector3(sx*outer,sy*inset,sz*inset),new Vector3(sx*inset,sy*outer,sz*inset),new Vector3(sx*inset,sy*inset,sz*outer)});
            mesh=new Mesh{name="Detailed bevelled block"}; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); cache["bevelled block"]=mesh; return mesh;
        }
        public static Mesh Pot()
        {
            return Lathe("hollow clay jar",new[]{new Vector2(0,0),new Vector2(.6f,0),new Vector2(.8f,.12f),new Vector2(1,.42f),new Vector2(.92f,.72f),new Vector2(.62f,.9f),new Vector2(.58f,1),new Vector2(.67f,1.04f),new Vector2(.68f,1.10f),new Vector2(.5f,1.10f),new Vector2(.48f,1.01f),new Vector2(.5f,.85f),new Vector2(.74f,.64f),new Vector2(.79f,.42f),new Vector2(.6f,.16f),new Vector2(0,.14f)},48);
        }
        public static Mesh Torso()
        {
            return Lathe("tailored tunic",new[]{new Vector2(0,0),new Vector2(.068f,0),new Vector2(.064f,.018f),new Vector2(.052f,.08f),new Vector2(.065f,.15f),new Vector2(.052f,.18f),new Vector2(.021f,.20f),new Vector2(0,.20f)},32);
        }
        public static Mesh Ring()
        {
            return Lathe("round rope band",new[]{new Vector2(.90f,0),new Vector2(.94f,-.06f),new Vector2(1,-.1f),new Vector2(1.06f,-.06f),new Vector2(1.1f,0),new Vector2(1.06f,.06f),new Vector2(1,.1f),new Vector2(.94f,.06f),new Vector2(.9f,0)},48);
        }
    }
}
