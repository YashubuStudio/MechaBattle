using System;
namespace MechaBattle.Construction
{
    // Keep saved IDs stable; team recognition colors are independent of hull paint.
    public static class BuildAppearance
    {
        private static readonly string[] ids={"Neutral","White","Sand","Olive","Graphite","Red","Cobalt"};
        private static readonly float[,] colors={
            {.59f,.65f,.70f},{1f,1f,1f},{1f,.85f,.015f},{.025f,.88f,.075f},
            {.045f,.05f,.06f},{1f,.025f,.025f},{.025f,.15f,1f}};
        public static int PaintCount=>ids.Length;
        public static string PaintId(int index)
        {if(index<0 || index>=ids.Length)throw new ArgumentOutOfRangeException(nameof(index));return ids[index];}
        public static int PaintIndex(string id)=>Array.IndexOf(ids,id);
        public static void Tint(int index,out float r,out float g,out float b)
        {
            if(index<0 || index>=ids.Length)throw new ArgumentOutOfRangeException(nameof(index));
            r=colors[index,0];g=colors[index,1];b=colors[index,2];
        }
        public static void TeamTint(int team,out float r,out float g,out float b)
        {
            if(team<0 || team>2)throw new ArgumentOutOfRangeException(nameof(team));
            r=team==0?.04f:team==1?1f:.59f;
            g=team==0?.26f:team==1?.36f:.65f;
            b=team==0?1f:team==1?.015f:.70f;
        }
    }
}
