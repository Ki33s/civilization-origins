using System.Collections.Generic;
using UnityEngine;

namespace Origins
{
    public sealed partial class WorldView
    {
        readonly HashSet<string> visibleRoads=new HashSet<string>();
        void SyncRoads() { foreach(var edge in game.World.RoadEdges) ShowRoad(edge.From,edge.To); }
        public void ShowRoad(Hex from,Hex to)
        {
            if(!game.World.HasRoad(from,to) || !details.ContainsKey(from) || !details.ContainsKey(to)) return;
            string key=from.R<to.R || from.R==to.R && from.Q<to.Q ? from+"/"+to : to+"/"+from;
            if(!visibleRoads.Add(key)) return;
            Vector3 a=FlatPosition(from),b=FlatPosition(to); Vector3 offset=Vector3.Cross(Vector3.up,(b-a).normalized)*.11f;
            var material=Mat("dry earth road",new Color(.42f,.30f,.16f)); material.shader=Shader.Find("Origins/Dry Road");
            for(int half=0;half<2;half++)
            {
                var path=Node(details[half==0?from:to].transform,"Dry land trail",Vector3.zero);
                path.localRotation=Quaternion.Euler(90,0,0);
                var line=path.gameObject.AddComponent<LineRenderer>(); line.useWorldSpace=true; line.alignment=LineAlignment.TransformZ; line.positionCount=17; line.startWidth=line.endWidth=.11f;
                line.numCornerVertices=4; line.numCapVertices=4; line.sharedMaterial=material; line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                for(int i=0;i<=16;i++) { float t=(half*16+i)/32f; Vector3 p=Vector3.Lerp(a,b,t)+offset*Mathf.Sin(t*Mathf.PI); line.SetPosition(i,OnSurface(p)+Vector3.up*.018f); }
            }
        }
    }
}
