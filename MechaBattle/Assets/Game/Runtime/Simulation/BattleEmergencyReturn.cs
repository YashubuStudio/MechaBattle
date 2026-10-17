using System;
using MechaBattle.Contracts;
namespace MechaBattle.Simulation
{
    public sealed partial class BattleSimulation
    {
        public EmergencyReturnSnapshot GetEmergencyReturn(int slot)
        {
            var u=Unit(slot);bool channel=u.Alive&&u.ReturnUntil>=0;
            float remaining=channel?Remaining(u.ReturnUntil):0;
            return new EmergencyReturnSnapshot(channel,u.Returned,remaining,channel?Clamp(1-remaining/config.emergencyReturnSeconds,0,1):0);
        }
        public string GetActionFeedback(int slot){var u=Unit(slot);return CurrentTick<u.ActionFeedbackUntil?u.ActionFeedback:"";}
        private void ActionMessage(UnitState u,string text){u.ActionFeedback=text;u.ActionFeedbackUntil=CurrentTick+SecondsToTicks(3);}
        private void PrepareEmergencyReturn(UnitState u)
        {
            if(!u.Alive||!config.emergencyReturnEnabled)return;
            bool requested=u.HasCommand&&u.Command.EmergencyReturn;
            if(requested)
            {
                if(u.ReturnUntil>=0){u.ReturnUntil=-1;ActionMessage(u,"緊急帰投を中断しました");return;}
                if(IsCarryingNuclear(u.Slot)){ActionMessage(u,"核運搬中は帰投できません / Fで手放す");return;}
                if(!u.Grounded||CurrentTick<u.DisabledUntil||CurrentTick<u.MeleeUntil||u.MeleeCharging||u.ChargeStart>=0||u.BeamUntil>CurrentTick)
                {ActionMessage(u,"着地し、攻撃・行動阻害が終わってから帰投してください");return;}
                u.ReturnUntil=CurrentTick+SecondsToTicks(config.emergencyReturnSeconds);CancelHeldActions(u.Slot);ActionMessage(u,"緊急帰投を開始 / 移動・攻撃・被弾で中断");
            }
            if(u.ReturnUntil<0)return;
            var c=u.Command;
            if(!u.Grounded||CurrentTick<u.DisabledUntil||IsCarryingNuclear(u.Slot)||u.HasCommand&&(c.MoveX*c.MoveX+c.MoveY*c.MoveY>.0001f||c.Jump||c.Boost||c.FirePrimary||c.FireSecondary||c.Melee||c.UseAbility||c.Reload||c.WeaponSwitchDelta!=0))
            {u.ReturnUntil=-1;ActionMessage(u,"緊急帰投を中断しました");}
        }
        private void FinishEmergencyReturns()
        {
            foreach(var u in units)
            {
                if(!u.Alive){u.ReturnUntil=-1;continue;}
                if(u.ReturnUntil<0)continue;
                if(u.LastDamageTick==CurrentTick||!u.Grounded||CurrentTick<u.DisabledUntil)
                {u.ReturnUntil=-1;ActionMessage(u,"被弾・行動阻害で帰投中断");continue;}
                if(CurrentTick<u.ReturnUntil)continue;
                CancelHeldActions(u.Slot);StoreWeapon(u);u.ReturnUntil=-1;u.Returned=true;u.Alive=false;AliveCount--;
                u.SpawnRequested=false;u.NextRespawn=CurrentTick;u.IncomingLockCount=0;u.HasCommand=false;
                u.TriggerHeld=u.MeleeHeld=false;u.Guarding=false;u.PreferredDeck=u.Deck;
                ActionMessage(u,"帰投完了 / 同じ機体を補給中。地図で出撃先を選択");
            }
        }
        private bool BotReturnSupplyReady(UnitState u)
        {
            var frame=u.Loadouts[u.Deck];if(u.Health<frame.MaxHealth-.01f||u.Shield<frame.MaxShield-.01f||u.Boost<EffectiveBoostCapacity(u)-.01f)return false;
            for(int i=0;i<frame.WeaponCount;i++)if(!frame.GetWeapon(i).AmmoResupplyDisabled&&u.WeaponAmmo[i]<frame.GetWeapon(i).MaxAmmo)return false;
            return true;
        }
        private void RepairReturnedUnits()
        {
            if(!config.emergencyReturnEnabled)return;
            foreach(var u in units)
            {
                if(!u.Returned||u.Alive)continue;
                var frame=u.Loadouts[u.Deck];RestoreHealth(u,config.returnHealthPerSecond*FixedDeltaTime);
                u.Shield=Math.Min(frame.MaxShield,u.Shield+config.returnShieldPerSecond*FixedDeltaTime);
                ResupplyAmmo(u,config.returnAmmoPerSecond);
                // A stored weapon may finish its existing reload; return never mints
                // forbidden pod/radar charges or resets every weapon to full ammunition.
                for(int i=0;i<frame.WeaponCount;i++)if(u.WeaponReloadReady[i]>=0&&CurrentTick>=u.WeaponReloadReady[i])
                {u.WeaponMagazines[i]=Math.Min(frame.GetWeapon(i).MagazineCapacity,u.WeaponAmmo[i]);u.WeaponReloadReady[i]=-1;}
                LoadWeapon(u);u.Boost=Math.Min(EffectiveBoostCapacity(u),u.Boost+(u.BoostOverheated?config.boostOverheatRecoveryPerSecond:config.boostRecoveryPerSecond)*FixedDeltaTime);
                if(u.BoostOverheated&&u.Boost>=EffectiveBoostCapacity(u))u.BoostOverheated=false;
                if(config.botStuckRecoveryEnabled&&roster.Controllers[u.Slot].Controller==ControllerType.Bot&&BotReturnSupplyReady(u))
                {
                    var homes=teamBaseIndices[u.Team];int last=Array.IndexOf(homes,u.LastSpawnMainBase),start=last>=0?last+1:(u.Slot%50+u.BotReturnTrips);
                    for(int step=0;step<homes.Length;step++){int home=homes[(start+step)%homes.Length];if(CanSpawnAtMainBase(u.Team,home)&&RequestSpawn(u.Slot,u.Deck,-1,home)){u.BotReturnTrips++;break;}}
                }
            }
        }
    }
}
