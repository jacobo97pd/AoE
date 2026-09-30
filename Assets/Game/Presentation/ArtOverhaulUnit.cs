using UnityEngine;

namespace Emberfield.Presentation
{
    [RequireComponent(typeof(Animator),typeof(LODGroup))]
    public sealed class ArtOverhaulUnit:MonoBehaviour
    {
        public ArtUnitKind Kind;
        public ArtBiome Biome;
        public Color Team=new Color(.22f,.39f,.62f);
        public string Label;
        public bool Selected;
        private SkinnedMeshRenderer[] renderers;
        private MaterialPropertyBlock properties;
        public string PoseName{get;private set;}="Idle";
        private void OnEnable()=>ApplyTeam();
        public void SetTeam(Color color){Team=color;ApplyTeam();}
        public void Select(bool selected){Selected=selected;ApplyTeam();}
        public void SetPose(string name)
        {
            PoseName=name;
            if(Application.isPlaying)GetComponent<Animator>().CrossFadeInFixedTime(name,.2f);
        }
        public void ApplyTeam()
        {
            if(properties==null)properties=new MaterialPropertyBlock();
            if(renderers==null)renderers=GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach(var renderer in renderers)
            {renderer.GetPropertyBlock(properties);properties.SetColor("_TeamColor",Team);properties.SetFloat("_Selection",Selected?1:0);renderer.SetPropertyBlock(properties);}
        }
    }
}
