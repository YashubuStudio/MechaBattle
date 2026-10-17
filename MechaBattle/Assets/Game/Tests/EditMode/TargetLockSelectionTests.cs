using System;
using NUnit.Framework;
using MechaBattle.Contracts;
using MechaBattle.Simulation;
namespace MechaBattle.Tests
{
    public sealed class TargetLockSelectionTests
    {
        private static LockCandidate Unit(int index,float angle,float distance)=>new LockCandidate(LockTargetKind.Unit,index,index+1,angle,distance);
        private static LockCandidate Object(int index,float angle,float distance)=>new LockCandidate(LockTargetKind.Deployable,index,index+1000,angle,distance);
        private static LockCandidate Pick(LockCandidate[] list,LockSelectionMode mode=LockSelectionMode.ReticleCenter,LockTargetKind kind=LockTargetKind.None,int index=-1,int id=0)
        {Assert.That(TargetLockSelection.TrySelect(list,mode,kind,index,id,out var c),Is.True);return c;}
        [TestCase(LockSelectionMode.ReticleCenter)] [TestCase(LockSelectionMode.NearestCycle)]
        public void CentralEnemyAlwaysBeatsNearerEquipment(LockSelectionMode mode)
        {var c=Pick(new[]{Object(0,0,2),Unit(50,1,80)},mode);Assert.That(c.Kind,Is.EqualTo(LockTargetKind.Unit));Assert.That(c.Index,Is.EqualTo(50));}
        [Test] public void VisibleEnemyStillHasPriorityOverEquipmentAtCenter()
        {Assert.That(Pick(new[]{Object(0,0,2),Unit(50,20,80)}).Kind,Is.EqualTo(LockTargetKind.Unit));}
        [Test] public void CenterModeChoosesAngleBeforeDistance()
        {Assert.That(Pick(new[]{Unit(50,7,10),Unit(51,1,60)}).Index,Is.EqualTo(51));}
        [Test] public void NearestModeChoosesDistanceBeforeAngle()
        {Assert.That(Pick(new[]{Unit(50,7,10),Unit(51,1,60)},LockSelectionMode.NearestCycle).Index,Is.EqualTo(50));}
        [Test] public void DistanceCycleVisitsEachEnemyThenWraps()
        {
            var list=new[]{Unit(52,5,30),Unit(50,7,10),Unit(51,2,20),Object(0,0,1)};var c=Pick(list,LockSelectionMode.NearestCycle);
            Assert.That(c.Index,Is.EqualTo(50));c=Pick(list,LockSelectionMode.NearestCycle,c.Kind,c.Index,c.Id);Assert.That(c.Index,Is.EqualTo(51));
            c=Pick(list,LockSelectionMode.NearestCycle,c.Kind,c.Index,c.Id);Assert.That(c.Index,Is.EqualTo(52));c=Pick(list,LockSelectionMode.NearestCycle,c.Kind,c.Index,c.Id);Assert.That(c.Index,Is.EqualTo(50));
        }
        [Test] public void MissingOrReplacedCurrentTargetRestartsAtNearest()
        {var list=new[]{Unit(50,1,10),Unit(51,2,20)};Assert.That(Pick(list,LockSelectionMode.NearestCycle,LockTargetKind.Unit,51,999).Index,Is.EqualTo(50));}
        [Test] public void CenterModeDoesNotCycleAwayFromTheBestReticleCandidate()
        {var list=new[]{Unit(50,1,20),Unit(51,2,10)};Assert.That(Pick(list,LockSelectionMode.ReticleCenter,LockTargetKind.Unit,50,51).Index,Is.EqualTo(50));}
        [Test] public void EquipmentIsSelectableWhenNoVisibleEnemyUnitQualifies()
        {var c=Pick(new[]{Object(2,8,4),Object(1,1,10)});Assert.That(c.Kind,Is.EqualTo(LockTargetKind.Deployable));Assert.That(c.Index,Is.EqualTo(1));}
        [Test] public void EquipmentAlsoCyclesAndWrapsWhenItIsTheOnlyCategory()
        {var list=new[]{Object(2,8,4),Object(1,1,10)};var c=Pick(list,LockSelectionMode.NearestCycle);Assert.That(c.Index,Is.EqualTo(2));Assert.That(Pick(list,LockSelectionMode.NearestCycle,c.Kind,c.Index,c.Id).Index,Is.EqualTo(1));}
        [Test] public void TiesAreStableRegardlessOfCandidateListOrder()
        {Assert.That(Pick(new[]{Unit(52,1,10),Unit(50,1,10)}).Index,Is.EqualTo(50));Assert.That(Pick(new[]{Unit(50,1,10),Unit(52,1,10)},LockSelectionMode.NearestCycle).Index,Is.EqualTo(50));}
        [Test] public void EmptyListAndInvalidInputsCannotSelectAPhantomTarget()
        {
            Assert.That(TargetLockSelection.TrySelect(Array.Empty<LockCandidate>(),LockSelectionMode.ReticleCenter,LockTargetKind.None,-1,0,out _),Is.False);
            Assert.Throws<ArgumentException>(()=>new LockCandidate(LockTargetKind.Unit,0,1,float.NaN,2));
            Assert.Throws<ArgumentException>(()=>new LockCandidate(LockTargetKind.Deployable,0,0,1,2));
            Assert.Throws<ArgumentOutOfRangeException>(()=>TargetLockSelection.TrySelect(Array.Empty<LockCandidate>(),(LockSelectionMode)9,LockTargetKind.None,-1,0,out _));
        }
    }
}
