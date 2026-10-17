using UnityEngine;
namespace MechaBattle.Playable
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ReticleRingGraphic : UnityEngine.UI.MaskableGraphic
    {
        public bool Elliptical { get; set; }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vertices)
        {
            vertices.Clear();const int segments=72;float radius=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*0.5f;
            float rx=Elliptical?rectTransform.rect.width*0.5f:radius,ry=Elliptical?rectTransform.rect.height*0.5f:radius;
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;int start=vertices.currentVertCount;
                vertices.AddVert(new Vector3(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry,0),color,Vector2.zero);
                vertices.AddVert(new Vector3(Mathf.Cos(b)*rx,Mathf.Sin(b)*ry,0),color,Vector2.zero);
                vertices.AddVert(new Vector3(Mathf.Cos(b)*(rx-3),Mathf.Sin(b)*(ry-3),0),color,Vector2.zero);
                vertices.AddVert(new Vector3(Mathf.Cos(a)*(rx-3),Mathf.Sin(a)*(ry-3),0),color,Vector2.zero);
                vertices.AddTriangle(start,start+1,start+2);vertices.AddTriangle(start,start+2,start+3);
            }
        }
    }
}
