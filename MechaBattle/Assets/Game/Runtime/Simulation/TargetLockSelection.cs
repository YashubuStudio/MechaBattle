using System;
using System.Collections.Generic;
using MechaBattle.Contracts;
namespace MechaBattle.Simulation
{
    // Caller supplies range/LOS/frustum-filtered observations, never enemy truth.
    public readonly struct LockCandidate
    {
        public readonly LockTargetKind Kind;public readonly int Index,Id;public readonly float Angle,Distance;
        public LockCandidate(LockTargetKind kind,int index,int id,float angle,float distance)
        {
            if(kind<LockTargetKind.Unit || kind>LockTargetKind.Deployable || index<0 || id<=0 || !SimulationConfig.IsFinite(angle) || !SimulationConfig.IsFinite(distance) || angle<0 || angle>180 || distance<0)throw new ArgumentException("Invalid lock candidate.");
            Kind=kind;Index=index;Id=id;Angle=angle;Distance=distance;
        }
    }
    public static class TargetLockSelection
    {
        public static bool TrySelect(IReadOnlyList<LockCandidate> candidates,LockSelectionMode mode,LockTargetKind currentKind,int currentIndex,int currentId,out LockCandidate selected)
        {
            if(candidates==null)throw new ArgumentNullException(nameof(candidates));
            if(mode<LockSelectionMode.ReticleCenter || mode>LockSelectionMode.NearestCycle)throw new ArgumentOutOfRangeException(nameof(mode));
            selected=default;bool anyUnit=false;
            for(int i=0;i<candidates.Count;i++)if(candidates[i].Kind==LockTargetKind.Unit)anyUnit=true;
            var category=anyUnit?LockTargetKind.Unit:LockTargetKind.Deployable;
            bool found=false,hasCurrent=false;LockCandidate current=default;
            for(int i=0;i<candidates.Count;i++)
            {
                var c=candidates[i];if(c.Kind!=category)continue;
                if(!found || Compare(c,selected,mode)<0){selected=c;found=true;}
                if(c.Kind==currentKind && c.Index==currentIndex && c.Id==currentId){current=c;hasCurrent=true;}
            }
            if(!found || mode!=LockSelectionMode.NearestCycle || !hasCurrent)return found;
            bool next=false;LockCandidate after=default;
            for(int i=0;i<candidates.Count;i++)
            {var c=candidates[i];if(c.Kind!=category || Compare(c,current,mode)<=0)continue;if(!next || Compare(c,after,mode)<0){after=c;next=true;}}
            if(next)selected=after;return true; // Wrap to the nearest when no later candidate remains.
        }
        private static int Compare(LockCandidate a,LockCandidate b,LockSelectionMode mode)
        {
            int result=mode==LockSelectionMode.ReticleCenter?a.Angle.CompareTo(b.Angle):a.Distance.CompareTo(b.Distance);
            if(result==0)result=mode==LockSelectionMode.ReticleCenter?a.Distance.CompareTo(b.Distance):a.Angle.CompareTo(b.Angle);
            if(result==0)result=a.Index.CompareTo(b.Index);if(result==0)result=a.Id.CompareTo(b.Id);return result;
        }
    }
}
