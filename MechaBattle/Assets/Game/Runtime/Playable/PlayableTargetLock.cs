using System;
using System.Collections.Generic;
using System.IO;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
using UnityEngine;
namespace MechaBattle.Playable
{
    public sealed partial class PlayableEntry
    {
        [Serializable] private sealed class LockSettings {public int schemaVersion=1;public LockSelectionMode selectionMode=LockSelectionMode.ReticleCenter;}
        private LockSettings lockSettings=new LockSettings();
        private readonly List<LockCandidate> lockCandidates=new List<LockCandidate>(612);
        public LockSelectionMode TargetLockMode=>lockSettings.selectionMode;
        private string LockSettingsPath=>Path.Combine(Application.persistentDataPath,"mechabattle-controls-v1.json");
        private void LoadLockSettings()
        {
            if(!persistProfile || !File.Exists(LockSettingsPath))return;
            try{var loaded=JsonUtility.FromJson<LockSettings>(File.ReadAllText(LockSettingsPath));if(loaded!=null && loaded.schemaVersion==1 && loaded.selectionMode>=LockSelectionMode.ReticleCenter && loaded.selectionMode<=LockSelectionMode.NearestCycle)lockSettings=loaded;}
            catch(Exception e)when(e is IOException || e is ArgumentException || e is UnauthorizedAccessException){Debug.LogWarning("ロック設定を読めないため中心優先を使います: "+e.Message);}
        }
        public void SetLockSelectionMode(LockSelectionMode mode)
        {
            if(mode<LockSelectionMode.ReticleCenter || mode>LockSelectionMode.NearestCycle)throw new ArgumentOutOfRangeException(nameof(mode));
            lockSettings.selectionMode=mode;RefreshLockSettingLabels();
            if(!persistProfile)return;
            try{Directory.CreateDirectory(Path.GetDirectoryName(LockSettingsPath));string temporary=LockSettingsPath+".tmp";File.WriteAllText(temporary,JsonUtility.ToJson(lockSettings,true));if(File.Exists(LockSettingsPath))File.Replace(temporary,LockSettingsPath,LockSettingsPath+".bak");else File.Move(temporary,LockSettingsPath);}
            catch(Exception e)when(e is IOException || e is UnauthorizedAccessException){Debug.LogWarning("ロック設定を保存できませんでした: "+e.Message);}
        }
        private void ToggleLockSelectionMode()=>SetLockSelectionMode(TargetLockMode==LockSelectionMode.ReticleCenter?LockSelectionMode.NearestCycle:LockSelectionMode.ReticleCenter);
        private void RefreshLockSettingLabels(){if(ui!=null)ui.SetLockSelectionMode(TargetLockMode==LockSelectionMode.ReticleCenter?"中心優先":"距離順切替");}
        private void ClearTargetLock(){lockedTargetSlot=-1;lockedTargetUnitId=0;lockedDeployableIndex=-1;lockedDeployableId=0;snapLockPending=false;}
        public void ToggleTargetLock()
        {
            if(Simulation==null || !Simulation.GetUnit(HumanSlotId).IsAlive)return;
            ValidateTargetLock();if(lockedTargetSlot>=0 || lockedDeployableIndex>=0){ClearTargetLock();return;}
            CycleTargetLock();
        }
        public void CycleTargetLock()
        {
            if(Simulation==null || !Simulation.GetUnit(HumanSlotId).IsAlive)return;
            lockCandidates.Clear();var self=Simulation.GetUnit(HumanSlotId);Vector3 own=new Vector3(self.X,self.Y+Simulation.ShotHeight,self.Z);
            for(int i=0;i<100;i++)
            {
                var u=Simulation.GetUnit(i);if(!Simulation.IsManualUnitYellowLocked(HumanSlotId,i))continue;
                Vector3 p=new Vector3(u.X,u.Y+Simulation.ShotHeight,u.Z);if(!InLockView(p))continue;
                lockCandidates.Add(new LockCandidate(LockTargetKind.Unit,i,u.UnitId,Vector3.Angle(followCamera.transform.forward,p-followCamera.transform.position),Vector3.Distance(own,p)));
            }
            for(int i=0;i<Simulation.DeployableCapacity;i++)
            {
                var d=Simulation.GetDeployable(i);if(!Simulation.IsDeployableYellowLocked(HumanSlotId,i,d.Id))continue;
                Vector3 p=new Vector3(d.X,d.Y+d.HitHeight*0.5f,d.Z);if(!InLockView(p))continue;
                lockCandidates.Add(new LockCandidate(LockTargetKind.Deployable,i,d.Id,Vector3.Angle(followCamera.transform.forward,p-followCamera.transform.position),Vector3.Distance(own,p)));
            }
            LockTargetKind current=lockedTargetSlot>=0?LockTargetKind.Unit:lockedDeployableIndex>=0?LockTargetKind.Deployable:LockTargetKind.None;
            int index=current==LockTargetKind.Unit?lockedTargetSlot:lockedDeployableIndex,id=current==LockTargetKind.Unit?lockedTargetUnitId:lockedDeployableId;
            if(!TargetLockSelection.TrySelect(lockCandidates,TargetLockMode,current,index,id,out var selected)){ClearTargetLock();return;}
            bool changed=current!=selected.Kind || index!=selected.Index || id!=selected.Id;
            ClearTargetLock();if(selected.Kind==LockTargetKind.Unit){lockedTargetSlot=selected.Index;lockedTargetUnitId=selected.Id;}
            else{lockedDeployableIndex=selected.Index;lockedDeployableId=selected.Id;}
            snapLockPending=changed && IsCurrentLockRed();
        }
        private bool InLockView(Vector3 p){var v=followCamera.WorldToViewportPoint(p);return v.z>followCamera.nearClipPlane && v.x>=0 && v.x<=1 && v.y>=0 && v.y<=1;}
        private void ValidateTargetLock()
        {
            if(Simulation==null)return;
            if(lockedTargetSlot>=0){var u=Simulation.GetUnit(lockedTargetSlot);if(u.UnitId!=lockedTargetUnitId || !Simulation.IsManualUnitYellowLocked(HumanSlotId,lockedTargetSlot))ClearTargetLock();}
            else if(lockedDeployableIndex>=0 && !Simulation.IsDeployableYellowLocked(HumanSlotId,lockedDeployableIndex,lockedDeployableId))ClearTargetLock();
        }
        private bool IsCurrentLockRed()=>lockedTargetSlot>=0?Simulation.IsManualUnitRedLocked(HumanSlotId,lockedTargetSlot):lockedDeployableIndex>=0&&Simulation.IsDeployableRedLocked(HumanSlotId,lockedDeployableIndex,lockedDeployableId);
        private bool TryGetLockedPoint(out Vector3 point)
        {
            point=Vector3.zero;if(Simulation==null)return false;
            if(lockedTargetSlot>=0){var t=Simulation.GetUnit(lockedTargetSlot);point=new Vector3(t.X,t.Y+Simulation.ShotHeight,t.Z);return t.IsAlive && t.UnitId==lockedTargetUnitId;}
            if(lockedDeployableIndex>=0){var d=Simulation.GetDeployable(lockedDeployableIndex);point=new Vector3(d.X,d.Y+d.HitHeight*0.5f,d.Z);return d.Active && d.Id==lockedDeployableId;}
            return false;
        }
    }
}
