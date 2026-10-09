using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Origins.Editor.ForestArt
{
    // All meshes are authored as reusable assets, not created by the preview player.
    public sealed class ForestMeshBuilder
    {
        readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<Color> colors=new List<Color>();
        readonly List<int> triangles=new List<int>();
        public int VertexCount { get { return vertices.Count; } }
        public int IndexCount { get { return triangles.Count; } }
        public void AddRange(ForestMeshBuilder source,int firstVertex,int vertexCount,int firstIndex,int indexCount)
        { AddRange(source,firstVertex,vertexCount,firstIndex,indexCount,Matrix4x4.identity); }
        public void AddRange(ForestMeshBuilder source,int firstVertex,int vertexCount,int firstIndex,int indexCount,Matrix4x4 matrix)
        {
            int offset=vertices.Count-firstVertex;
            for(int i=firstVertex;i<firstVertex+vertexCount;i++) { vertices.Add(matrix.MultiplyPoint3x4(source.vertices[i])); colors.Add(source.colors[i]); }
            for(int i=firstIndex;i<firstIndex+indexCount;i++) triangles.Add(source.triangles[i]+offset);
        }
        public Vector3 RangeRoot(int start,int count)
        {
            Vector3 min=vertices[start],max=min;
            for(int i=start+1;i<start+count;i++) { min=Vector3.Min(min,vertices[i]); max=Vector3.Max(max,vertices[i]); }
            return new Vector3((min.x+max.x)*.5f,min.y,(min.z+max.z)*.5f);
        }
        public static Color RGB(string hex)
        {
            Color color; ColorUtility.TryParseHtmlString("#"+hex,out color); return color;
        }
        public void Triangle(Vector3 a,Vector3 b,Vector3 c,Color ca,Color cb,Color cc)
        {
            int i=vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
            colors.Add(ca); colors.Add(cb); colors.Add(cc);
            triangles.Add(i); triangles.Add(i+1); triangles.Add(i+2);
        }
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
        { Triangle(a,b,c,color,color,color); Triangle(a,c,d,color,color,color); }
        public void AddMesh(Mesh mesh,Matrix4x4 matrix)
        {
            int start=vertices.Count; var points=mesh.vertices; var tint=mesh.colors;
            for(int i=0;i<points.Length;i++) { vertices.Add(matrix.MultiplyPoint3x4(points[i])); colors.Add(tint.Length==points.Length ? tint[i] : Color.white); }
            foreach(int index in mesh.triangles) triangles.Add(start+index);
        }
        public Mesh Finish(string name,bool smooth=true)
        {
            var mesh=new Mesh{name=name,indexFormat=vertices.Count>65000 ? IndexFormat.UInt32 : IndexFormat.UInt16};
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if(smooth)
            {
                var summed=new Dictionary<Vector3,Vector3>(); var keys=new Vector3[vertices.Count]; var normals=mesh.normals;
                for(int i=0;i<vertices.Count;i++)
                {
                    Vector3 p=vertices[i]; var key=new Vector3(Mathf.Round(p.x*10000),Mathf.Round(p.y*10000),Mathf.Round(p.z*10000));
                    keys[i]=key; Vector3 n; summed.TryGetValue(key,out n); summed[key]=n+normals[i];
                }
                for(int i=0;i<vertices.Count;i++) normals[i]=summed[keys[i]].normalized;
                mesh.normals=normals;
            }
            return mesh;
        }
        public void Tube(Vector3 a,Vector3 b,float ra,float rb,Color color,int segments=14,int rings=5,float corrugation=.08f)
        {
            Vector3 axis=(b-a).normalized;
            Vector3 side=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 other=Vector3.Cross(axis,side).normalized;
            for(int r=0;r<rings;r++) for(int s=0;s<segments;s++)
            {
                float t0=r/(float)rings,t1=(r+1)/(float)rings;
                float angle0=s*Mathf.PI*2/segments,angle1=(s+1)*Mathf.PI*2/segments;
                Vector3 p0=TubePoint(a,b,side,other,ra,rb,t0,angle0,corrugation);
                Vector3 p1=TubePoint(a,b,side,other,ra,rb,t0,angle1,corrugation);
                Vector3 p2=TubePoint(a,b,side,other,ra,rb,t1,angle1,corrugation);
                Vector3 p3=TubePoint(a,b,side,other,ra,rb,t1,angle0,corrugation);
                Color shade=color*(.83f+.17f*Mathf.Sin(angle0*3+1)); shade.a=1;
                Quad(p0,p1,p2,p3,shade);
            }
            for(int s=0;s<segments;s++)
            {
                float x=s*Mathf.PI*2/segments,y=(s+1)*Mathf.PI*2/segments;
                Triangle(b,TubePoint(a,b,side,other,ra,rb,1,x,corrugation),TubePoint(a,b,side,other,ra,rb,1,y,corrugation),color,color,color);
            }
        }
        static Vector3 TubePoint(Vector3 a,Vector3 b,Vector3 side,Vector3 other,float ra,float rb,float t,float angle,float ridges)
        {
            float radius=Mathf.Lerp(ra,rb,t)*(1+ridges*Mathf.Sin(angle*7+t*4));
            return Vector3.Lerp(a,b,t)+(side*Mathf.Cos(angle)+other*Mathf.Sin(angle))*radius;
        }
        public void Blob(Vector3 center,Vector3 scale,Color color,int seed,int slices=18,int stacks=12,float rough=.12f)
        {
            for(int y=0;y<stacks;y++) for(int x=0;x<slices;x++)
            {
                Vector3 a=BlobPoint(center,scale,x,y,slices,stacks,seed,rough),b=BlobPoint(center,scale,x+1,y,slices,stacks,seed,rough);
                Vector3 c=BlobPoint(center,scale,x+1,y+1,slices,stacks,seed,rough),d=BlobPoint(center,scale,x,y+1,slices,stacks,seed,rough);
                float light=.95f+.045f*Mathf.Sin(x*.72f+y*.5f+seed);
                Color low=color*light; low.a=1; Color high=Color.Lerp(low,color*1.04f,Mathf.Clamp01(y/(float)stacks)*.1f);
                Triangle(a,b,c,low,low,high); Triangle(a,c,d,low,high,high);
            }
        }
        static Vector3 BlobPoint(Vector3 center,Vector3 scale,int x,int y,int slices,int stacks,int seed,float rough)
        {
            float yaw=x*Mathf.PI*2/slices,pitch=y*Mathf.PI/stacks;
            Vector3 n=new Vector3(Mathf.Sin(pitch)*Mathf.Cos(yaw),Mathf.Cos(pitch),Mathf.Sin(pitch)*Mathf.Sin(yaw));
            float noise=1+rough*(Mathf.Sin(n.x*7+seed)*Mathf.Cos(n.z*6-seed)+Mathf.Sin(n.y*9+seed)*.35f);
            return center+Vector3.Scale(n,scale)*noise;
        }
        public void Blade(Vector3 root,float yaw,float length,float width,float lean,Color color,int segments=5)
        {
            Vector3 forward=new Vector3(Mathf.Cos(yaw),0,Mathf.Sin(yaw)),side=new Vector3(-forward.z,0,forward.x);
            for(int s=0;s<segments;s++)
            {
                float t=s/(float)segments,u=(s+1)/(float)segments;
                Vector3 a=root+Vector3.up*length*t+forward*lean*t*t,b=root+Vector3.up*length*u+forward*lean*u*u;
                float wa=width*(1-t)*.5f,wb=width*(1-u)*.5f;
                Color baseColor=color*.72f; baseColor.a=1; Color topColor=Color.Lerp(color,new Color(.68f,.77f,.32f),u*.3f);
                Triangle(a-side*wa,b+side*wb,a+side*wa,baseColor,topColor,baseColor);
                Triangle(a-side*wa,b-side*wb,b+side*wb,baseColor,topColor,topColor);
            }
        }
        public static float Height(float x,float z)
        { return (Mathf.PerlinNoise(x*.16f+32,z*.16f+71)-.5f)*.38f+(Mathf.PerlinNoise(x*.53f+9,z*.53f+24)-.5f)*.08f; }
    }
}
