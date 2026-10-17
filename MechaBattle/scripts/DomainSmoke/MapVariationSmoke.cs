using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
using MechaBattle.Simulation;
using MechaBattle.Observation;
using MechaBattle.AI.Bots;

// Validates the new forward-center-skirmish map variant requested 2026-10-16: a
// deliberately asymmetric, ~3x larger map where the center lane's bases are pushed
// forward (close together, short/fast) while the north/south flank lanes are pushed
// back (far apart, long/slow). This checks the declared topology AND that a full
// 100-slot BOT-only match actually runs to completion on it without engine errors -
// not that it is balanced (that needs real playtesting), just that nothing breaks
// when the map is far bigger/less symmetric than config/standard-map.json.
internal static class MapVariationSmoke
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
        var standard = JsonSerializer.Deserialize<MapDefinition>(File.ReadAllText(Path.Combine(project, "config/standard-map.json")), options);
        var map = JsonSerializer.Deserialize<MapDefinition>(File.ReadAllText(Path.Combine(project, "config/forward-center-map.json")), options);
        MapValidator.ThrowIfInvalid(map);

        if (map.mainBases.Count(b => b.team == 0) != 3 || map.mainBases.Count(b => b.team == 1) != 3)
            throw new Exception("Forward-center map must keep three main bases per team, like the standard map.");

        if (map.halfWidth < standard.halfWidth * 2.5f || map.halfWidth > standard.halfWidth * 3.5f
            || map.halfDepth < standard.halfDepth * 2.5f || map.halfDepth > standard.halfDepth * 3.5f)
            throw new Exception("Forward-center map bounds must be roughly 3x the standard map's halfWidth/halfDepth.");

        float CenterLaneAbsX() => map.mainBases.Where(b => b.approachRoute == "center").Select(b => Math.Abs(b.x)).Max();
        float FlankLaneAbsX(string route) => map.mainBases.Where(b => b.approachRoute == route).Select(b => Math.Abs(b.x)).Max();
        float centerAbsX = CenterLaneAbsX();
        float northAbsX = FlankLaneAbsX("north");
        float southAbsX = FlankLaneAbsX("south");
        if (!(centerAbsX < northAbsX && centerAbsX < southAbsX))
            throw new Exception("Center lane bases must sit closer to the map's midline than the north/south flank bases (deliberate lane bias).");
        if (northAbsX - centerAbsX < 150f || southAbsX - centerAbsX < 150f)
            throw new Exception("Flank lane bases must be meaningfully further back than the center lane, not just marginally.");

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(project, "config/prototype-defaults.json")));
        var config = JsonSerializer.Deserialize<SimulationConfig>(document.RootElement.GetProperty("p0Simulation").GetRawText(), options);
        var world = new BattleSimulation(map, config, -1);
        var view = new OwnUnitView(world);
        var bot = new ForwardMarchBotCommandSource(map, view, config.fireRange, config.maxAmmo, config.homeSupplyRadius);
        int peakAlive = world.AliveCount;
        while (world.Phase == MatchPhase.Running)
        {
            for (int slot = 0; slot < 100; slot++)
            {
                view.AllowedSlot = slot;
                if (bot.TryGetCommand(slot, world.CurrentTick, out var command) && !world.Submit(slot, command))
                    throw new Exception("BOT command rejected on the forward-center map.");
            }
            world.Step(); peakAlive = Math.Max(peakAlive, world.AliveCount);
            if (world.AliveCount > 100) throw new Exception("Alive count exceeded 100 on the forward-center map.");
            for (int slot = 0; slot < 100; slot++)
            {
                var u = world.GetUnit(slot);
                if (!float.IsFinite(u.X) || !float.IsFinite(u.Z) || Math.Abs(u.X) > map.halfWidth + 1f || Math.Abs(u.Z) > map.halfDepth + 1f
                    || !float.IsFinite(u.Health) || !float.IsFinite(u.Shield) || u.Health < 0 || u.Shield < 0 || u.Ammo < 0 || u.Ammo > config.maxAmmo)
                    throw new Exception("Invalid unit state on the forward-center map.");
            }
        }
        if (peakAlive != 100) throw new Exception("Forward-center map match did not spawn the full 100-slot roster.");
        var counters = world.Counters;
        var report = new
        {
            scope = "forward-center-map-asymmetric-3x-bot-only",
            executedUtc = DateTime.UtcNow, peakAlive,
            winnerTeam = world.WinnerTeam, blueGauge = world.GetGauge(0), orangeGauge = world.GetGauge(1),
            centerLaneAbsX = centerAbsX, northLaneAbsX = northAbsX, southLaneAbsX = southAbsX,
            counters.ShotsFired, counters.BaseHits, counters.UnitsDestroyed, counters.UnitsSpawned
        };
        string output = Path.Combine(project, "Build/Reports/map-variation-forward-center.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return new { name = "ForwardCenterMap_AsymmetricTopologyAndFullBotMatch", status = "passed", reportPath = output };
    }
}
