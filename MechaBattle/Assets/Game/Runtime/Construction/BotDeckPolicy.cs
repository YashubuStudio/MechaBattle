using System;
using MechaBattle.Contracts;
namespace MechaBattle.Construction
{
    public static class BotDeckPolicy
    {
        public static int SelectPreset(UnitLoadout[] presets,int slot,int deck,BotRole role)
        {
            if(presets==null||presets.Length==0)throw new ArgumentException("BOT presets are required.");
            if(role==BotRole.RadarSupport){for(int i=0;i<presets.Length;i++)for(int w=0;w<presets[i].WeaponCount;w++)if(presets[i].GetWeapon(w).Kind==WeaponKind.Radar)return i;throw new ArgumentException("A radar BOT needs a legal radar preset.");}
            return(slot%50+deck)%presets.Length;
        }
    }
}
