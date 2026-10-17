using System;
using MechaBattle.Contracts;
namespace MechaBattle.Simulation
{
    public sealed partial class BattleSimulation
    {
        private UnitLoadout ActiveWeapon(UnitState u) { return u.Loadouts[u.Deck].GetWeapon(u.ActiveWeaponIndex); }
        public UnitLoadout GetActiveWeapon(int slot) { return ActiveWeapon(Unit(slot)); }
        public int GetWeaponAmmo(int slot, int index)
        {
            var u = Unit(slot); if (index < 0 || index >= u.Loadouts[u.Deck].WeaponCount) throw new ArgumentOutOfRangeException(nameof(index));
            return index == u.ActiveWeaponIndex ? u.Ammo : u.WeaponAmmo[index];
        }
        private void ResupplyAmmo(UnitState u,float perSecond)
        {
            if(perSecond<=0)return;
            StoreWeapon(u);var frame=u.Loadouts[u.Deck];
            for(int i=0;i<frame.WeaponCount;i++)
            {
                var w=frame.GetWeapon(i);
                if(w.AmmoResupplyDisabled){u.WeaponAmmoFraction[i]=0;continue;}
                if(u.WeaponAmmo[i]<w.MaxAmmo)
                {
                    u.WeaponAmmoFraction[i]+=perSecond*FixedDeltaTime;
                    int amount=(int)Math.Min(w.MaxAmmo-u.WeaponAmmo[i],Math.Floor(u.WeaponAmmoFraction[i]+0.000001f));
                    u.WeaponAmmo[i]+=amount;u.WeaponAmmoFraction[i]-=amount;
                }
                if(u.WeaponAmmo[i]>=w.MaxAmmo)u.WeaponAmmoFraction[i]=0;
                if(w.MagazineCapacity==0)u.WeaponMagazines[i]=u.WeaponAmmo[i];
            }
            LoadWeapon(u);
        }
        private void InitializeWeapons(UnitState u)
        {
            int count = u.Loadouts[u.Deck].WeaponCount; u.ActiveWeaponIndex = 0;
            u.WeaponAmmo = new int[count]; u.WeaponMagazines = new int[count]; u.WeaponAmmoFraction = new float[count];
            u.WeaponLastShot = new long[count]; u.WeaponNextShot = new long[count]; u.WeaponReloadReady = new long[count];
            for (int i = 0; i < count; i++)
            {
                var w = u.Loadouts[u.Deck].GetWeapon(i); u.WeaponAmmo[i] = w.MaxAmmo; u.WeaponMagazines[i] = w.MagazineCapacity > 0 ? w.MagazineCapacity : w.MaxAmmo;
                u.WeaponLastShot[i] = -1; u.WeaponNextShot[i] = CurrentTick; u.WeaponReloadReady[i] = -1;
            }
            LoadWeapon(u);
        }
        private void StoreWeapon(UnitState u)
        {
            int i = u.ActiveWeaponIndex; u.WeaponAmmo[i] = u.Ammo; u.WeaponMagazines[i] = u.Magazine; u.WeaponAmmoFraction[i] = u.AmmoFraction;
            u.WeaponLastShot[i] = u.LastShotTick; u.WeaponNextShot[i] = u.NextShot; u.WeaponReloadReady[i] = u.ReloadReady;
        }
        private void LoadWeapon(UnitState u)
        {
            int i = u.ActiveWeaponIndex; u.Ammo = u.WeaponAmmo[i]; u.Magazine = u.WeaponMagazines[i]; u.AmmoFraction = u.WeaponAmmoFraction[i];
            u.LastShotTick = u.WeaponLastShot[i]; u.NextShot = u.WeaponNextShot[i]; u.ReloadReady = u.WeaponReloadReady[i];
        }
        private void PrepareInventory(UnitState u)
        {
            if (!u.Alive || !u.HasCommand || CurrentTick < u.DisabledUntil || CurrentTick < u.MeleeUntil) return;
            var loadout = u.Loadouts[u.Deck]; int next = (u.ActiveWeaponIndex + u.Command.WeaponSwitchDelta + loadout.WeaponCount) % loadout.WeaponCount;
            if (config.equipmentRulesEnabled && u.Command.Melee)
                for (int i = 0; i < loadout.WeaponCount; i++) if (loadout.GetWeapon(i).Kind == WeaponKind.Melee) { next = i; break; }
            if (next == u.ActiveWeaponIndex) return;
            StoreWeapon(u); CancelHeldActions(u.Slot); u.ActiveWeaponIndex = next; LoadWeapon(u);
        }
    }
}
