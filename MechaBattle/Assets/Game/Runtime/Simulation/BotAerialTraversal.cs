using System;
using MechaBattle.Contracts;
namespace MechaBattle.Simulation
{
    public sealed partial class BotTraversalAdapter
    {
        private readonly int[] aerialUnitIds=new int[100];
        private readonly long[] aerialUntil=new long[100],nextAerial=new long[100];
        private float JumpableRise()=>config.aerialBoostEnabled
            ? Math.Max(config.jumpSpeed*config.jumpSpeed/(2*config.gravity)*0.85f,config.aerialMaximumRise*0.85f)
            : config.jumpSpeed*config.jumpSpeed/(2*config.gravity)*0.85f;
        private bool WantsAerialManeuver(UnitSnapshot self,UnitLoadout frame,UnitLoadout active,long tick,int visible,bool withdrawing,bool resupply)
        {
            if(!config.botAerialManeuversEnabled || self.Controller!=ControllerType.Bot)return false;
            int slot=self.SlotId;
            if(aerialUnitIds[slot]!=self.UnitId)
            {aerialUnitIds[slot]=self.UnitId;aerialUntil[slot]=0;nextAerial[slot]=tick+(slot%7)*config.tickHz/2;}
            // One in five ranged troops makes a brief vertical firing move. Most hold the line.
            // The decision uses only self state and the caller's bounded visible contact.
            bool ready=slot%5==2 && visible>=0 && !withdrawing && !resupply && active.Kind==WeaponKind.Ranged
                && self.Ammo>0 && self.Health>=frame.MaxHealth*0.5f && !self.BoostOverheated
                && self.Disruption==DisruptionState.Ready && self.MeleePhase<0 && self.MeleeChargeFraction<=0;
            if(!ready){aerialUntil[slot]=0;return false;}
            if(self.Grounded && tick>=nextAerial[slot] && self.Boost>=config.boostCapacity*0.65f)
            {aerialUntil[slot]=tick+(long)Math.Ceiling(config.botAerialBurstSeconds*config.tickHz);nextAerial[slot]=tick+(long)Math.Ceiling(config.botAerialIntervalSeconds*config.tickHz);}
            return tick<aerialUntil[slot];
        }
    }
}
