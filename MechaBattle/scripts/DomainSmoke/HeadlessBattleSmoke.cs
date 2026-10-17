using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using MechaBattle.Battlefield;
using MechaBattle.Contracts;
using MechaBattle.Simulation;

internal static class HeadlessBattleSmoke
{
    internal static object Run(string project)
    {
        var options = new JsonSerializerOptions { IncludeFields = true };
        var map = JsonSerializer.Deserialize<MapDefinition>(File.ReadAllText(Path.Combine(project, "config/standard-map.json")), options);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(project, "config/prototype-defaults.json")));
        var config = JsonSerializer.Deserialize<SimulationConfig>(document.RootElement.GetProperty("p0Simulation").GetRawText(), options);
        double maximumUnitLossPerDamage = 0;
        foreach (int cost in config.deckCosts) maximumUnitLossPerDamage = Math.Max(maximumUnitLossPerDamage, cost * (double)config.unitGaugeLossPerCost / (config.maxHealth + config.maxShield));
        if (config.baseGaugeDamageFactor <= maximumUnitLossPerDamage) throw new Exception("FR-102: base damage must be the most efficient gauge-loss path in the P0 defaults.");
        var world = new BattleSimulation(map, config, -1);
        int peakAlive = world.AliveCount, minimumAlive = world.AliveCount;
        long warmTick = config.tickHz;
        double simulatedSeconds = 0;
        long allocatedAtWarmup = 0, allocationAfterLoop = 0;
        var samples = new List<object>();
        var timer = Stopwatch.StartNew();
        var stepTimer = new Stopwatch();
        long measuredTicks = 0;
        while (world.Phase == MatchPhase.Running)
        {
            long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int slot = 0; slot < MatchLimits.TotalControllerSlots; slot++)
            {
                var self = world.GetUnit(slot); if (!self.IsAlive) continue;
                MainBaseSnapshot goal = default;
                int desiredTeam = self.Ammo == 0 ? self.Team : 1 - self.Team;
                int index = slot % MatchLimits.ControllerSlotsPerTeam % 3;
                int found = 0;
                for (int b = 0; b < world.MainBaseCount; b++)
                {
                    var candidate = world.GetBase(b);
                    if (candidate.Team != desiredTeam) continue;
                    goal = candidate;
                    if (found++ == index) break;
                }
                double dx = goal.X - self.X, dz = goal.Z - self.Z;
                double distance = Math.Sqrt(dx * dx + dz * dz);
                bool fire = desiredTeam != self.Team && distance < config.fireRange * 0.8;
                double stoppingRange = goal.BuildingRadius > 0 ? Math.Min(config.fireRange * 0.8, goal.BuildingRadius * 0.7) : config.fireRange * 0.8;
                bool holdingAtBaseRange = desiredTeam != self.Team && distance < stoppingRange;
                // First 30s exercise local combat, then a scripted base-rush phase.
                // This is a test scenario, not the production BOT implementation.
                double aimX = dx, aimZ = dz;
                if (desiredTeam != self.Team && world.CurrentTick < config.tickHz * 30L)
                {
                    double nearestObserved = (double)config.redLockRange * config.redLockRange;
                    for (int other = 0; other < 100; other++)
                    {
                        var candidate = world.GetUnit(other);
                        if (!candidate.IsAlive || candidate.Team == self.Team) continue;
                        double ex = candidate.X - self.X, ez = candidate.Z - self.Z;
                        double squared = ex * ex + ez * ez;
                        if (squared < nearestObserved)
                        { nearestObserved = squared; aimX = ex; aimZ = ez; fire = true; }
                    }
                }
                double denominator = Math.Max(distance, config.moveSpeed * world.FixedDeltaTime);
                float mx = holdingAtBaseRange ? 0 : (float)(dx / denominator), mz = holdingAtBaseRange ? 0 : (float)(dz / denominator);
                float yaw = (float)(Math.Atan2(aimX, aimZ) * 180 / Math.PI);
                bool accepted = world.Submit(slot, new UnitCommand(self.UnitId, world.CurrentTick,
                    unchecked((uint)world.CurrentTick + 1), mx, mz, yaw, 0, false, fire, false, false, false));
                if (!accepted) throw new Exception("Valid command rejected at slot " + slot);
            }
            stepTimer.Start(); world.Step(); stepTimer.Stop();
            long allocationAfter = GC.GetAllocatedBytesForCurrentThread();
            if (world.CurrentTick > warmTick) { allocatedAtWarmup += allocationAfter - allocationBefore; measuredTicks++; }
            peakAlive = Math.Max(peakAlive, world.AliveCount); minimumAlive = Math.Min(minimumAlive, world.AliveCount);
            if (world.AliveCount > 100) throw new Exception("More than 100 alive units.");
            for (int slot = 0; slot < 100; slot++)
            {
                var unit = world.GetUnit(slot);
                if (unit.Health < 0 || unit.Health > config.maxHealth || unit.Shield < 0 || unit.Shield > config.maxShield ||
                    unit.Ammo < 0 || unit.Ammo > config.maxAmmo || !float.IsFinite(unit.Health) || !float.IsFinite(unit.Shield) || !float.IsFinite(unit.X) || !float.IsFinite(unit.Z))
                    throw new Exception("Resource/position invariant failed at slot " + slot);
                if (unit.IsAlive && world.GetDeckReadyTick(slot, unit.DeckIndex) > world.CurrentTick)
                    throw new Exception("Spawned from a deck entry still on cooldown.");
            }
            if (world.CurrentTick % config.tickHz == 0)
                samples.Add(new { tick = world.CurrentTick, alive = world.AliveCount, blueGauge = world.GetGauge(0), orangeGauge = world.GetGauge(1) });
        }
        timer.Stop();
        allocationAfterLoop = allocatedAtWarmup;
        simulatedSeconds = world.CurrentTick / (double)config.tickHz;
        var counters = world.Counters;
        if (peakAlive != 100 || counters.ShotsFired <= 0 || counters.UnitsDestroyed <= 0 || counters.UnitsSpawned <= 100 || counters.BaseHits <= 0)
            throw new Exception("100-slot combat/respawn/base-hit smoke scenario did not exercise all required paths.");
        var baseDamage = new List<object>();
        for (int i = 0; i < world.MainBaseCount; i++)
        { var b = world.GetBase(i); baseDamage.Add(new { id = b.Id, team = b.Team, damageReceived = b.DamageReceived }); }
        var report = new
        {
            scope = "dotnet-domain-only-not-unity-fps-or-ai-acceptance",
            driver = "test-only command scenario: public-base goals; first 30s use range-limited enemy observations, then base rush",
            commanders = "roster slots only; commander AI is not connected",
            executedUtc = DateTime.UtcNow, tickHz = config.tickHz, simulatedSeconds,
            wallSeconds = timer.Elapsed.TotalSeconds, simulationStepCpuSeconds = stepTimer.Elapsed.TotalSeconds,
            peakAlive, minimumAlive, activeControllerSlots = 100, winnerTeam = world.WinnerTeam,
            blueGauge = world.GetGauge(0), orangeGauge = world.GetGauge(1),
            counters.ShotsFired, counters.UnitsDestroyed, counters.UnitsSpawned, counters.BaseHits,
            measuredTicks, warmupTicks = warmTick, commandAndStepAllocatedBytesAfterWarmup = allocationAfterLoop,
            bases = baseDamage, samples
        };
        string output = Path.Combine(project, "Build/Reports/headless-domain-battle.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return new { name = "ActualConfig_100SlotPublicObjectiveBattle", status = "passed", simulatedSeconds,
            peakAlive, counters.UnitsDestroyed, counters.UnitsSpawned, counters.BaseHits, reportPath = output };
    }
}
