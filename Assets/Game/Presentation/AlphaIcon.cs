using UnityEngine;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    public enum AlphaSymbol { Food, Wood, Metal, Stone, Population, Worker, Army, Stop, Select, Formation, Clear, Home, Menu, Settings, Build, Research, Shield, Flag }

    // Original vector marks: shared stroke weight and no font-dependent emoji or raster imports.
    public sealed class AlphaIcon : MaskableGraphic
    {
        [SerializeField] private AlphaSymbol symbol;
        public AlphaSymbol Symbol { get => symbol; set { if (symbol == value) return; symbol = value; SetVerticesDirty(); } }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            switch (symbol)
            {
                case AlphaSymbol.Food:
                    Stroke(mesh, .5f, .14f, .5f, .9f, .07f);
                    Diamond(mesh, .34f, .36f, .14f, .09f); Diamond(mesh, .66f, .48f, .14f, .09f);
                    Diamond(mesh, .34f, .62f, .14f, .09f); Diamond(mesh, .65f, .75f, .14f, .09f); break;
                case AlphaSymbol.Wood:
                    Quad(mesh, .45f, .1f, .1f, .27f); Triangle(mesh, new Vector2(.1f, .35f), new Vector2(.5f, .91f), new Vector2(.9f, .35f));
                    Triangle(mesh, new Vector2(.2f, .61f), new Vector2(.5f, .99f), new Vector2(.8f, .61f)); break;
                case AlphaSymbol.Metal:
                    Polygon(mesh, new[] { new Vector2(.13f,.27f), new Vector2(.23f,.69f), new Vector2(.59f,.88f), new Vector2(.9f,.59f), new Vector2(.78f,.2f) });
                    break;
                case AlphaSymbol.Stone:
                    Polygon(mesh, new[] { new Vector2(.13f,.18f), new Vector2(.11f,.51f), new Vector2(.44f,.85f), new Vector2(.72f,.78f), new Vector2(.91f,.26f), new Vector2(.67f,.14f) }); break;
                case AlphaSymbol.Population:
                    Disc(mesh, .37f, .7f, .15f); Disc(mesh, .73f, .63f, .11f);
                    Polygon(mesh, new[] { new Vector2(.1f,.15f), new Vector2(.16f,.42f), new Vector2(.36f,.5f), new Vector2(.58f,.4f), new Vector2(.65f,.15f) });
                    Polygon(mesh, new[] { new Vector2(.66f,.15f), new Vector2(.62f,.39f), new Vector2(.8f,.43f), new Vector2(.94f,.16f) }); break;
                case AlphaSymbol.Worker:
                    Stroke(mesh, .22f,.15f,.75f,.88f,.11f); Stroke(mesh,.26f,.78f,.9f,.49f,.11f); break;
                case AlphaSymbol.Army:
                    Stroke(mesh,.23f,.17f,.8f,.86f,.08f); Stroke(mesh,.77f,.17f,.2f,.86f,.08f);
                    Stroke(mesh,.2f,.3f,.42f,.12f,.07f); Stroke(mesh,.58f,.12f,.8f,.3f,.07f); break;
                case AlphaSymbol.Stop:
                    Polygon(mesh, new[] { new Vector2(.28f,.08f), new Vector2(.73f,.08f), new Vector2(.92f,.29f), new Vector2(.92f,.72f), new Vector2(.71f,.92f), new Vector2(.29f,.92f), new Vector2(.08f,.71f), new Vector2(.08f,.28f) }); break;
                case AlphaSymbol.Select:
                    Stroke(mesh,.1f,.35f,.1f,.1f,.07f); Stroke(mesh,.1f,.1f,.35f,.1f,.07f);
                    Stroke(mesh,.65f,.1f,.9f,.1f,.07f); Stroke(mesh,.9f,.1f,.9f,.35f,.07f);
                    Stroke(mesh,.9f,.65f,.9f,.9f,.07f); Stroke(mesh,.9f,.9f,.65f,.9f,.07f);
                    Stroke(mesh,.35f,.9f,.1f,.9f,.07f); Stroke(mesh,.1f,.9f,.1f,.65f,.07f); break;
                case AlphaSymbol.Formation: Diamond(mesh,.5f,.73f,.14f,.17f); Diamond(mesh,.23f,.27f,.14f,.17f); Diamond(mesh,.77f,.27f,.14f,.17f); break;
                case AlphaSymbol.Clear: Stroke(mesh,.22f,.22f,.78f,.78f,.09f); Stroke(mesh,.22f,.78f,.78f,.22f,.09f); break;
                case AlphaSymbol.Home:
                    Stroke(mesh,.12f,.5f,.5f,.88f,.09f); Stroke(mesh,.5f,.88f,.88f,.5f,.09f);
                    Stroke(mesh,.25f,.5f,.25f,.15f,.09f); Stroke(mesh,.75f,.5f,.75f,.15f,.09f); Stroke(mesh,.25f,.15f,.75f,.15f,.09f); break;
                case AlphaSymbol.Menu: Quad(mesh,.17f,.22f,.66f,.08f); Quad(mesh,.17f,.46f,.66f,.08f); Quad(mesh,.17f,.7f,.66f,.08f); break;
                case AlphaSymbol.Settings:
                    Ring(mesh,.5f,.5f,.25f,.075f);
                    for (int i=0;i<8;i++) { float angle=i*Mathf.PI/4; Vector2 d=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)); Stroke(mesh,.5f+d.x*.24f,.5f+d.y*.24f,.5f+d.x*.42f,.5f+d.y*.42f,.09f); } break;
                case AlphaSymbol.Build: Quad(mesh,.16f,.15f,.68f,.24f); Quad(mesh,.16f,.43f,.3f,.21f); Quad(mesh,.51f,.43f,.33f,.21f); Quad(mesh,.16f,.69f,.68f,.15f); break;
                case AlphaSymbol.Research:
                    Stroke(mesh,.5f,.14f,.5f,.84f,.065f); Stroke(mesh,.16f,.26f,.5f,.14f,.065f); Stroke(mesh,.5f,.14f,.84f,.26f,.065f);
                    Stroke(mesh,.16f,.26f,.16f,.87f,.065f); Stroke(mesh,.84f,.26f,.84f,.87f,.065f);
                    Stroke(mesh,.16f,.87f,.5f,.76f,.065f); Stroke(mesh,.5f,.76f,.84f,.87f,.065f); break;
                case AlphaSymbol.Flag:
                    Stroke(mesh,.2f,.1f,.2f,.91f,.075f); Polygon(mesh,new[]{new Vector2(.23f,.84f),new Vector2(.85f,.84f),new Vector2(.68f,.62f),new Vector2(.85f,.42f),new Vector2(.23f,.42f)}); break;
                default:
                    Stroke(mesh,.16f,.83f,.5f,.95f,.07f); Stroke(mesh,.5f,.95f,.84f,.83f,.07f);
                    Stroke(mesh,.84f,.83f,.78f,.36f,.07f); Stroke(mesh,.78f,.36f,.5f,.08f,.07f);
                    Stroke(mesh,.5f,.08f,.22f,.36f,.07f); Stroke(mesh,.22f,.36f,.16f,.83f,.07f);
                    Stroke(mesh,.5f,.35f,.5f,.73f,.07f); break;
            }
        }
        private void Quad(VertexHelper h,float x,float y,float w,float height) => Polygon(h,new[]{new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+height),new Vector2(x,y+height)});
        private void Diamond(VertexHelper h,float x,float y,float rx,float ry) => Polygon(h,new[]{new Vector2(x-rx,y),new Vector2(x,y+ry),new Vector2(x+rx,y),new Vector2(x,y-ry)});
        private void Triangle(VertexHelper h,Vector2 a,Vector2 b,Vector2 c) => Polygon(h,new[]{a,b,c});
        private void Disc(VertexHelper h,float x,float y,float radius)
        { var points=new Vector2[12]; for(int i=0;i<points.Length;i++){float a=i*Mathf.PI*2/points.Length;points[i]=new Vector2(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius);} Polygon(h,points); }
        private void Ring(VertexHelper h,float x,float y,float radius,float width)
        { for(int i=0;i<16;i++){float a=i*Mathf.PI/8,b=(i+1)*Mathf.PI/8;Stroke(h,x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius,x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius,width);} }
        private void Stroke(VertexHelper h,float x1,float y1,float x2,float y2,float width)
        { var a=new Vector2(x1,y1);var b=new Vector2(x2,y2);var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*width*.5f;Polygon(h,new[]{a-n,b-n,b+n,a+n}); }
        private void Polygon(VertexHelper h,Vector2[] points)
        {
            var bounds=rectTransform.rect; float size=Mathf.Min(bounds.width,bounds.height); var origin=bounds.center-Vector2.one*size*.5f;int first=h.currentVertCount;
            for(int i=0;i<points.Length;i++)h.AddVert(origin+points[i]*size,color,Vector2.zero);
            for(int i=1;i+1<points.Length;i++)h.AddTriangle(first,first+i,first+i+1);
        }
        public static AlphaIcon Create(Transform parent, AlphaSymbol symbol, Color color)
        {
            var rect=new GameObject("Icon",typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            var icon=rect.gameObject.AddComponent<AlphaIcon>();icon.Symbol=symbol;icon.color=color;icon.raycastTarget=false;return icon;
        }
    }
}
