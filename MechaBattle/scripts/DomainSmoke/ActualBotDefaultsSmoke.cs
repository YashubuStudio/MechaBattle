using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
using MechaBattle.Simulation;
using MechaBattle.Observation;
using MechaBattle.AI.Bots;

internal static class ActualBotDefaultsSmoke
{
    private sealed class OwnUnitView : ISelfObservationSource
    {
        private readonly BattleSimulation world;
        internal int AllowedSlot;
        internal OwnUnitView(BattleSimulation world) { this.world = world; }
        public bool TryGetSelf(int slotId, out SelfObservation self)
        {
            if (slotId != AllowedSlot) throw new Exception("BOT queried a slot other than its own.");
            var u = world.GetUnit(slotId);
            self = new SelfObservation(slotId, u.UnitId, u.Team, u.Controller, u.IsAlive, u.X, u.Z, u.Ammo);
            return true;
        }
    }
    internal static object Run(string project)
    {
        var options = new JsonSerializerOptions { IncludeFields = true };
        var map = JsonSerializer.Deserialize<MapDefinition>(File.ReadAllText(Path.Combine(project, "config/standard-map.json")), options);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(project, "config/prototype-defaults.json")));
        var config = JsonSerializer.Deserialize<SimulationConfig>(document.RootElement.GetProperty("p0Simulation").GetRawText(), options);
        var world = new BattleSimulation(map, config, -1);
        var view = new OwnUnitView(world);
        var bot = new ForwardMarchBotCommandSource(map, view, config.fireRange, config.maxAmmo, config.homeSupplyRadius);
        var timer = Stopwatch.StartNew();
        int peakAlive = world.AliveCount;
        while (world.Phase == MatchPhase.Running)
        {
            for (int slot = 0; slot < 100; slot++)
            {
                view.AllowedSlot = slot;
                if (bot.TryGetCommand(slot, world.CurrentTick, out var command) && !world.Submit(slot, command))
                    throw new Exception("Actual BOT command rejected.");
            }
            world.Step(); peakAlive = Math.Max(peakAlive, world.AliveCount);
            if (world.AliveCount > 100) throw new Exception("Alive count exceeded 100.");
            for (int slot = 0; slot < 100; slot++)
            {
                var u = world.GetUnit(slot);
                if (!float.IsFinite(u.Health) || !float.IsFinite(u.Shield) || u.Health < 0 || u.Shield < 0 || u.Ammo < 0 || u.Ammo > config.maxAmmo)
                    throw new Exception("Invalid resources in actual BOT integration.");
            }
        }
        timer.Stop();
        if (peakAlive != 100 || world.Counters.BaseHits <= 0 || Math.Min(world.GetGauge(0), world.GetGauge(1)) != 0)
            throw new Exception("Actual BOT/default-config scenario did not finish through main-base gauge damage.");
        var counters = world.Counters;
        var report = new
        {
            scope = "actual-claude-p0-bot-plus-gpt-domain-dotnet-only",
            aiLimit = "public static goals and own resources only; no enemy-unit perception or commander AI",
            executedUtc = DateTime.UtcNow, activeControllerSlots = 100, peakAlive,
            simulatedSeconds = world.CurrentTick / (double)config.tickHz,
            wallSeconds = timer.Elapsed.TotalSeconds, winnerTeam = world.WinnerTeam,
            blueGauge = world.GetGauge(0), orangeGauge = world.GetGauge(1),
            counters.ShotsFired, counters.BaseHits, counters.UnitsDestroyed, counters.UnitsSpawned
        };
        string output = Path.Combine(project, "Build/Reports/actual-bot-domain-battle.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return new { name = "ActualClaudeBot_ActualConfig_100Slots", status = "passed", reportPath = output };
    }
}
