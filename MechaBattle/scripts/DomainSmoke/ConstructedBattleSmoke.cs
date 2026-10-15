using System;
using System.IO;
using System.Text.Json;
using MechaBattle.Contracts;
using MechaBattle.Contracts.Construction;
using MechaBattle.Construction;
using MechaBattle.Battlefield;
using MechaBattle.Simulation;
using MechaBattle.Observation;
using MechaBattle.AI.Bots;

internal static class ConstructedBattleSmoke
{
    private sealed class LocalObservations : ISelfObservationSource, INearbyThreatSensor
    {
        public BattleSimulation World;
        public bool TryGetSelf(int slot, out SelfObservation self)
        {
            var u = World.GetUnit(slot); self = new SelfObservation(slot, u.UnitId, u.Team, u.Controller, u.IsAlive, u.X, u.Z, u.Ammo); return true;
        }
        public NearbyEnemy FindNearestEnemy(int slot, float range)
        {
            var self = World.GetUnit(slot); if (!self.IsAlive) return NearbyEnemy.None;
            range = Math.Min(range, World.GetLoadout(slot, self.DeckIndex).FireRange);
            double best = range * range; var result = NearbyEnemy.None;
            for (int other = 0; other < 100; other++)
            {
                var u = World.GetUnit(other); if (!u.IsAlive || u.Team == self.Team) continue;
                double dx = u.X - self.X, dz = u.Z - self.Z, distance = dx * dx + dz * dz;
                if (distance <= best) { best = distance; result = new NearbyEnemy(true, u.X, u.Z); }
            }
            return result;
        }
    }
    public static object Run(string project)
    {
        var options = new JsonSerializerOptions { IncludeFields = true };
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(project, "config/prototype-defaults.json")));
        var config = JsonSerializer.Deserialize<SimulationConfig>(doc.RootElement.GetProperty("p0Simulation").GetRawText(), options);
        var capabilities = JsonSerializer.Deserialize<SimulationConfig>(doc.RootElement.GetProperty("playablePrototype").GetRawText(), options);
        config.boostCapacity = capabilities.boostCapacity; config.boostDrainPerSecond = capabilities.boostDrainPerSecond;
        config.boostRecoveryPerSecond = capabilities.boostRecoveryPerSecond; config.boostSpeedMultiplier = capabilities.boostSpeedMultiplier;
        config.meleeDamage = capabilities.meleeDamage; config.meleeRange = capabilities.meleeRange;
        config.meleeIntervalSeconds = capabilities.meleeIntervalSeconds; config.meleeHalfAngleDegrees = capabilities.meleeHalfAngleDegrees;
        var catalog = JsonSerializer.Deserialize<ConstructionCatalog>(doc.RootElement.GetProperty("constructionPrototype").GetProperty("catalog").GetRawText(), options);
        var map = JsonSerializer.Deserialize<MapDefinition>(File.ReadAllText(Path.Combine(project, "config/standard-map.json")), options);
        int count = config.deckCosts.Length; var resolved = new ResolvedBuild[count]; var deck = new UnitLoadout[count];
        for (int i = 0; i < count; i++)
        {
            var b = ConstructionEvaluator.Evaluate(catalog, catalog.presets[i % catalog.presets.Length]); resolved[i] = b;
            deck[i] = new UnitLoadout(b.Id, b.MaxHealth, b.MaxShield, b.MoveSpeed, b.MaxAmmo, b.FireDamage, b.FireRange, b.FireIntervalSeconds, b.Cost, b.CooldownSeconds);
        }
        var loadouts = new UnitLoadout[100][]; for (int slot = 0; slot < 100; slot++) loadouts[slot] = deck;
        var world = new BattleSimulation(map, config, -1, loadouts); var observations = new LocalObservations { World = world };
        var sources = new IUnitCommandSource[count];
        for (int i = 0; i < count; i++) sources[i] = new ForwardMarchBotCommandSource(map, observations, observations, resolved[i].FireRange, resolved[i].MaxAmmo, config.homeSupplyRadius);
        int peak = world.AliveCount;
        while (world.Phase == MatchPhase.Running)
        {
            for (int slot = 0; slot < 100; slot++)
            {
                var u = world.GetUnit(slot);
                if (u.IsAlive && sources[u.DeckIndex].TryGetCommand(slot, world.CurrentTick, out var command) && !world.Submit(slot, command)) throw new Exception("Constructed BOT command rejected.");
            }
            world.Step(); peak = Math.Max(peak, world.AliveCount);
            if (world.AliveCount > 100) throw new Exception("Exceeded 100 live units.");
            for (int slot = 0; slot < 100; slot++)
            {
                var u = world.GetUnit(slot); var b = world.GetLoadout(slot, u.DeckIndex);
                if (u.Health < 0 || u.Health > b.MaxHealth || u.Shield < 0 || u.Shield > b.MaxShield || u.Ammo < 0 || u.Ammo > b.MaxAmmo)
                    throw new Exception("Constructed resource limit violated.");
                if (u.IsAlive && world.GetDeckReadyTick(slot, u.DeckIndex) > world.CurrentTick) throw new Exception("CT bypass detected.");
            }
        }
        var counters = world.Counters;
        if (peak != 100 || counters.ShotsFired == 0 || counters.UnitsDestroyed == 0 || counters.UnitsSpawned <= 100 || counters.BaseHits == 0)
            throw new Exception("Constructed battle did not exercise combat, destruction, respawn and core attack.");
        var report = new { scope = "constructed-domain-bot-match-not-ui-player-or-fps-acceptance", peakAlive = peak,
            simulatedSeconds = world.CurrentTick * world.FixedDeltaTime, world.WinnerTeam, counters.ShotsFired, counters.UnitsDestroyed,
            counters.UnitsSpawned, counters.BaseHits, counters.BaseCounterShotsFired, blueGauge = world.GetGauge(0), orangeGauge = world.GetGauge(1) };
        Directory.CreateDirectory(Path.Combine(project, "Build/Reports"));
        File.WriteAllText(Path.Combine(project, "Build/Reports/constructed-battle.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return new { name = "ConstructedDeck_Actual100BotMatchCompletesWithoutResourceOrCooldownBypass", status = "passed", report };
    }
}
