using System;
using System.Linq;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
using NUnit.Framework;

namespace MechaBattle.Tests
{
    public sealed class FoundationTests
    {
        public static MapDefinition Map(int blue = 3, int orange = 3)
        {
            return new MapDefinition
            {
                id = "test", prototypeOnly = true, halfWidth = 100, halfDepth = 100,
                mainBases = Enumerable.Range(0, blue + orange).Select(i => new MainBaseDefinition
                {
                    id = "Base-" + i, team = i < blue ? 0 : 1,
                    x = i < blue ? -80 : 80, z = i % 3 * 20,
                    spawnRadius = 4, attackRadius = 3, approachRoute = "Route-" + i
                }).ToArray(),
                capturePoints = new[] { new CapturePointDefinition { id = "Relay", radius = 10 } }
            };
        }

        [Test]
        public void T001_OfflineHasOneHuman99BotsAndSeparateCommanders()
        {
            var roster = new MatchRoster();
            Assert.That(roster.Controllers.Count, Is.EqualTo(100));
            Assert.That(roster.Controllers.Select(s => s.Id).Distinct().Count(), Is.EqualTo(100));
            Assert.That(roster.Controllers.Count(s => s.Controller == ControllerType.Human), Is.EqualTo(1));
            Assert.That(roster.Controllers.Count(s => s.Controller == ControllerType.Bot), Is.EqualTo(99));
            Assert.That(roster.Commanders.Count, Is.EqualTo(2));
            foreach (int team in new[] { 0, 1 })
            {
                Assert.That(roster.Controllers.Count(s => s.Team == team), Is.EqualTo(50));
                Assert.That(roster.Commanders.Count(s => s.Team == team && s.Controller == ControllerType.Bot), Is.EqualTo(1));
            }
        }

        [Test]
        public void T001_FullBotModePreservesAll100Slots()
        {
            var roster = new MatchRoster(-1);
            Assert.That(roster.Controllers.Count(s => s.Controller == ControllerType.Bot), Is.EqualTo(100));
        }

        [TestCase(-2)] [TestCase(100)]
        public void T001_InvalidHumanSlotRejected(int slot)
        { Assert.Throws<ArgumentOutOfRangeException>(() => new MatchRoster(slot)); }

        [TestCase(0, 3)] [TestCase(1, 3)] [TestCase(3, 0)] [TestCase(3, 1)]
        public void T015_RejectsEitherTeamWithFewerThanTwoMainBases(int blue, int orange)
        { Assert.Throws<ArgumentException>(() => MapValidator.ThrowIfInvalid(Map(blue, orange))); }

        [TestCase(2, 2)] [TestCase(3, 3)] [TestCase(4, 2)]
        public void T015_AllowsVariableMainBaseCountsAtOrAboveMinimum(int blue, int orange)
        { Assert.DoesNotThrow(() => MapValidator.ThrowIfInvalid(Map(blue, orange))); }

        [Test]
        public void T015_RelaysDoNotCountAsMainBases()
        {
            var map = Map(1, 3);
            map.capturePoints = Enumerable.Range(0, 12).Select(i => new CapturePointDefinition { id = "Relay-" + i, radius = 1 }).ToArray();
            Assert.Throws<ArgumentException>(() => MapValidator.ThrowIfInvalid(map));
        }

        [Test]
        public void T016_RejectsDuplicateMainBaseIds()
        {
            var map = Map(); map.mainBases[1].id = map.mainBases[0].id;
            Assert.Throws<ArgumentException>(() => MapValidator.ThrowIfInvalid(map));
        }

        [Test]
        public void T015_RejectsIdCollisionBetweenBaseAndRelay()
        {
            var map = Map(); map.capturePoints[0].id = map.mainBases[0].id;
            Assert.Throws<ArgumentException>(() => MapValidator.ThrowIfInvalid(map));
        }

        [Test]
        public void T015_RejectsInvalidTeam()
        {
            var map = Map(); map.mainBases[0].team = 2;
            Assert.Throws<ArgumentException>(() => MapValidator.ThrowIfInvalid(map));
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(101f)]
        public void T015_RejectsInvalidCoordinates(float x)
        {
            var map = Map(); map.mainBases[0].x = x;
            Assert.Throws<ArgumentException>(() => MapValidator.ThrowIfInvalid(map));
        }

        [Test]
        public void T015_RejectsInvalidBoundsAndRadii()
        {
            var map = Map(); map.halfWidth = float.NaN;
            Assert.Throws<ArgumentException>(() => MapValidator.ThrowIfInvalid(map));
            map = Map(); map.mainBases[0].spawnRadius = 0;
            Assert.Throws<ArgumentException>(() => MapValidator.ThrowIfInvalid(map));
            map = Map(); map.capturePoints[0].radius = -1;
            Assert.Throws<ArgumentException>(() => MapValidator.ThrowIfInvalid(map));
        }

        [Test]
        public void T015_RejectsMissingMapOrCollections()
        {
            Assert.Throws<ArgumentException>(() => MapValidator.ThrowIfInvalid(null));
            var map = Map(); map.mainBases = null;
            Assert.Throws<ArgumentException>(() => MapValidator.ThrowIfInvalid(map));
            map = Map(); map.capturePoints = null;
            Assert.Throws<ArgumentException>(() => MapValidator.ThrowIfInvalid(map));
        }

        [Test]
        public void FR110_RejectsSingleDeclaredChokeRoute()
        {
            var map = Map(); foreach (var b in map.mainBases) b.approachRoute = "single-choke";
            Assert.Throws<ArgumentException>(() => MapValidator.ThrowIfInvalid(map));
        }

        [Test]
        public void DomainAssembliesDoNotReferenceUnity()
        {
            foreach (var assembly in new[] { typeof(MatchRoster).Assembly, typeof(MapValidator).Assembly })
                Assert.That(assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("Unity", StringComparison.Ordinal)), Is.False);
        }
    }
}
