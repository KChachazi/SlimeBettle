using UnityEngine;
using UnityEngine.UI;

namespace SlimeBattle.UI
{
    /// <summary>Editable monochrome doodles used by the reference UI, without texture dependencies.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SlimeDoodleGraphic : MaskableGraphic
    {
        [SerializeField] private bool grass;
        [SerializeField] private bool sprout;
        public bool Grass { get => grass; set { grass=value; SetVerticesDirty(); } }
        public bool Sprout { get => sprout; set { sprout=value; SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var r=GetPixelAdjustedRect();
            if(grass)
            {
                Stem(mesh,r,new Vector2(-0.06f,-0.34f),new Vector2(-0.39f,0.09f),0.16f,color);
                Stem(mesh,r,new Vector2(-0.03f,-0.34f),new Vector2(0,0.39f),0.17f,color);
                Stem(mesh,r,new Vector2(0.02f,-0.34f),new Vector2(0.37f,0.20f),0.16f,color);
                return;
            }
            Blob(mesh,r,new Vector2(0.03f,-0.35f),new Vector2(0.43f,0.12f),new Color(color.r*0.62f,color.g*0.66f,color.b*0.57f,color.a*0.6f),0.4f);
            Blob(mesh,r,new Vector2(0,0),new Vector2(0.49f,0.47f),color,1);
            var eye=new Color(0.24f,0.20f,0.15f,color.a);
            Blob(mesh,r,new Vector2(-0.10f,0.02f),new Vector2(0.025f,0.077f),eye,0);
            Blob(mesh,r,new Vector2(0.09f,-0.005f),new Vector2(0.025f,0.071f),eye,0);
            if(sprout)
            {
                Stem(mesh,r,new Vector2(0.21f,0.37f),new Vector2(0.28f,0.68f),0.045f,color);
                Stem(mesh,r,new Vector2(0.27f,0.52f),new Vector2(0.45f,0.53f),0.07f,color);
            }
        }
        private static void Blob(VertexHelper mesh,Rect r,Vector2 c,Vector2 radius,Color tint,float wobble)
        {
            var start=mesh.currentVertCount;
            mesh.AddVert(r.center+Vector2.Scale(c,r.size),tint,Vector2.zero);
            const int count=48;
            for(var i=0;i<count;i++)
            {
                var t=i*Mathf.PI*2/count;
                var variation=1+wobble*(Mathf.Sin(t*3+0.5f)*0.025f+Mathf.Cos(t*5)*0.012f);
                var p=c+Vector2.Scale(new Vector2(Mathf.Cos(t),Mathf.Sin(t))*variation,radius);
                mesh.AddVert(r.center+Vector2.Scale(p,r.size),tint,Vector2.zero);
            }
            for(var i=0;i<count;i++) mesh.AddTriangle(start,start+i+1,start+(i+1)%count+1);
        }
        private static void Stem(VertexHelper mesh,Rect r,Vector2 a,Vector2 b,float width,Color tint)
        {
            var start=mesh.currentVertCount;
            var n=new Vector2(-(b-a).normalized.y,(b-a).normalized.x)*width*0.5f;
            foreach(var p in new[]{a-n,a+n,b+n,b-n}) mesh.AddVert(r.center+Vector2.Scale(p,r.size),tint,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2); mesh.AddTriangle(start,start+2,start+3);
            Blob(mesh,r,a,Vector2.one*width*0.5f,tint,0); Blob(mesh,r,b,Vector2.one*width*0.5f,tint,0);
        }
    }
}
