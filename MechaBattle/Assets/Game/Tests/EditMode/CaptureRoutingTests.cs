using MechaBattle.AI.Bots;
using MechaBattle.Contracts;
using MechaBattle.Observation;
using NUnit.Framework;

namespace MechaBattle.Tests
{
    public sealed class CaptureRoutingTests
    {
        private sealed class PublicView : ISelfObservationSource, INearbyThreatSensor, ICapturePointStatusSource
        {
            public int Owner = -1, Ammo = 30;
            public int CapturePointCount => 1;
            public CapturePointStatus GetCapturePoint(int index) => new CapturePointStatus(Owner, 0, 0);
            public bool TryGetSelf(int slot, out SelfObservation self)
            {
                self = new SelfObservation(slot, 1, 0, ControllerType.Bot, true, 0, 0, Ammo);
                return true;
            }
            public NearbyEnemy FindNearestEnemy(int slot, float range) => NearbyEnemy.None;
        }
        [TestCase(-1)] [TestCase(1)]
        public void PublicRelayRoutingWaitsInsideUnownedRelayWithoutShootingIt(int owner)
        {
            var view = new PublicView { Owner = owner }; var map = SimulationTests.Map();
            map.capturePoints[0].x = map.capturePoints[0].z = 0;
            var bot = new ForwardMarchBotCommandSource(map, view, view, 20, 30, 1, view);
            Assert.That(bot.TryGetCommand(0, 0, out var command), Is.True);
            Assert.That(command.MoveX, Is.Zero); Assert.That(command.MoveY, Is.Zero);
            Assert.That(command.FirePrimary, Is.False);
        }
        [Test] public void PublicRelayOwnershipChangesResumeObjectiveAndAmmoStillForcesRetreat()
        {
            var view = new PublicView { Owner = 0 }; var map = SimulationTests.Map();
            map.capturePoints[0].x = map.capturePoints[0].z = 0;
            var bot = new ForwardMarchBotCommandSource(map, view, view, 20, 30, 1, view);
            Assert.That(bot.TryGetCommand(0, 0, out var attack), Is.True);
            Assert.That(attack.MoveX, Is.GreaterThan(0));
            view.Owner = 1;
            Assert.That(bot.TryGetCommand(0, 1, out var recapture), Is.True);
            Assert.That(recapture.MoveX, Is.Zero); Assert.That(recapture.FirePrimary, Is.False);
            view.Ammo = 0;
            Assert.That(bot.TryGetCommand(0, 2, out var retreat), Is.True);
            Assert.That(retreat.MoveX, Is.LessThan(0)); Assert.That(retreat.FirePrimary, Is.False);
        }
    }
}
