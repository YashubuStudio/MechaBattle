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
    private sealed class LocalObservations : ISelfObservationSource, INearbyThreatSensor, ICapturePointStatusSource, IMainBaseStatusSource
    {
        public BattleSimulation World;
        public bool TryGetSelf(int slot, out SelfObservation self)
        {
            var u = World.GetUnit(slot); self = new SelfObservation(slot, u.UnitId, u.Team, u.Controller, u.IsAlive, u.X, u.Z, u.Ammo, u.Y); return true;
        }
        public int MainBaseCount=>World.MainBaseCount;public bool IsMainBaseDestroyed(int index)=>World.GetBase(index).Destroyed;
        public int CapturePointCount => World.CapturePointCount;
        public CapturePointStatus GetCapturePoint(int index)
        {
            var point = World.GetCapturePoint(index);
            return new CapturePointStatus(point.OwnerTeam, point.X, point.Z);
        }
        public BotPerception Perception;
        public NearbyEnemy FindNearestEnemy(int slot,float range)=>Perception.FindNearestEnemy(slot,range);
    }
    private static int EnumerableRadarBots(BotPopulation p){int n=0;for(int i=0;i<100;i++)if(p.IsBot(i)&&p.RoleOf(i)==BotRole.RadarSupport)n++;return n;}
    public static object Run(string project, string mapFile = "canyon-cross-map.json")
    {
        var options = new JsonSerializerOptions { IncludeFields = true };
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(project, "config/prototype-defaults.json")));
        var config = JsonSerializer.Deserialize<SimulationConfig>(doc.RootElement.GetProperty("p0Simulation").GetRawText(), options);
        // The same pure-C# Apply used by PlayableSceneBuilder and PlayableEntry.
        // Avoid a second hand-maintained list silently omitting new movement/combat settings.
        var capabilities=JsonSerializer.Deserialize<MechaBattle.Playable.PlayablePrototype>(doc.RootElement.GetProperty("playablePrototype").GetRawText(),options);
        capabilities.Apply(config);
        var catalog = JsonSerializer.Deserialize<ConstructionCatalog>(doc.RootElement.GetProperty("constructionPrototype").GetProperty("catalog").GetRawText(), options);
        var map = JsonSerializer.Deserialize<MapDefinition>(File.ReadAllText(Path.Combine(project, "config", mapFile)), options);
        int count = catalog.presets.Length; var resolved = new ResolvedBuild[count]; var deck = new UnitLoadout[count];
        for (int i = 0; i < count; i++)
        {
            var b = ConstructionEvaluator.Evaluate(catalog, catalog.presets[i % catalog.presets.Length]); resolved[i] = b;
            deck[i] = b.CombatLoadout;
        }
        var population=new BotPopulation(config.botBehavior,config.botBehavior.seed);var presetIndices=new int[100,config.deckCosts.Length];
        var loadouts = new UnitLoadout[100][]; for (int slot = 0; slot < 100; slot++) { loadouts[slot] = new UnitLoadout[config.deckCosts.Length]; for (int d = 0; d < config.deckCosts.Length; d++){int p=BotDeckPolicy.SelectPreset(deck,slot,d,population.RoleOf(slot));presetIndices[slot,d]=p;loadouts[slot][d]=deck[p];} }
        var world = new BattleSimulation(map, config, -1, loadouts); var observations = new LocalObservations { World = world, Perception = new BotPerception(world,config,population) };
        var sources = new IUnitCommandSource[count];
        for (int i = 0; i < count; i++) sources[i] = new ForwardMarchBotCommandSource(map, observations, observations, resolved[i].FireRange, resolved[i].MaxAmmo, config.homeSupplyRadius, observations);
        var trigger = new CombatInputAdapter(); var terrain = new BotTraversalAdapter(map,config, observations);var planner=new BotBattlefieldPlanner(world,map,config.botBehavior,population);
        int peak = world.AliveCount, minBlue=50, minOrange=50, contestedSamples=0, healthyFrontSamples=0;
        double aliveIntegral=0,completedLifeTicks=0,engagedLifeTicks=0,unitGaugeDamage=0;
        int completedLives=0,engagedDeaths=0,centralSamples=0,totalPositionSamples=0;
        var lifeIds=new int[100];var spawnTicks=new long[100];var firstHit=new long[100];var previousHealth=new float[100];var previousShield=new float[100];
        var raidEvents=new System.Collections.Generic.List<object>();var retreatCommand=new bool[100];var maxAdvance=new float[100];var nearestCore=new float[100];for(int slot=0;slot<100;slot++){maxAdvance[slot]=-10000;nearestCore[slot]=10000;}
        int peakAirborne=0;long airborneUnitTicks=0;float maximumHeight=0;
        int botReturns=0;var returnedLives=new System.Collections.Generic.HashSet<int>();
        int radarPlacements=0,peakRadars=0;var radarIds=new System.Collections.Generic.HashSet<int>();
        bool combatStarted=false;for(int slot=0;slot<100;slot++)firstHit[slot]=-1;
        while (world.Phase == MatchPhase.Running)
        {
            for (int slot = 0; slot < 100; slot++)
            {
                var u = world.GetUnit(slot);
                if (u.IsAlive && sources[presetIndices[slot,u.DeckIndex]].TryGetCommand(slot, world.CurrentTick, out var command))
                {
                    var frame=world.GetLoadout(slot,u.DeckIndex); var contact=observations.FindNearestEnemy(slot,world.GetActiveWeapon(slot).FireRange);
                    var intent=planner.GetIntent(slot,contact);
                    command=terrain.Adapt(u,frame,command,contact.Found?contact.SlotId:-1,contact.UnitId,contact.X,contact.Y,contact.Z,intent);
                    if(slot%50%9==0 && command.CancelHeldAttack && !retreatCommand[slot] && raidEvents.Count<200)raidEvents.Add(new{world.CurrentTick,slot,u.X,u.Z,u.Health,u.Shield,u.LastLockedHitTick,u.IncomingLockCount,command.MoveX,command.MoveY});
                    retreatCommand[slot]=command.CancelHeldAttack;
                    int next=(u.ActiveWeaponIndex+command.WeaponSwitchDelta+frame.WeaponCount)%frame.WeaponCount;
                    if(!world.Submit(slot,trigger.Adapt(u,frame.GetWeapon(next),command)))throw new Exception("Constructed BOT command rejected.");
                }
            }
            world.Step();int activeRadars=0;for(int i=0;i<world.DeployableCapacity;i++){var placed=world.GetDeployable(i);if(placed.Active&&placed.Kind==WeaponKind.Radar){activeRadars++;if(radarIds.Add(placed.Id))radarPlacements++;}}peakRadars=Math.Max(peakRadars,activeRadars); peak = Math.Max(peak, world.AliveCount);
            if (world.AliveCount > 100) throw new Exception("Exceeded 100 live units.");
            int blueAlive=0,orangeAlive=0,airborne=0;
            for (int slot = 0; slot < 100; slot++)
            {
                var u = world.GetUnit(slot); var b = world.GetLoadout(slot, u.DeckIndex);
                bool reserve=world.GetEmergencyReturn(slot).InReserve;if(reserve&&returnedLives.Add(u.UnitId))botReturns++;
                if(u.IsAlive){maxAdvance[slot]=Math.Max(maxAdvance[slot],u.X*(u.Team==0?1:-1));for(int bi=0;bi<world.MainBaseCount;bi++){var core=world.GetBase(bi);if(core.Team!=u.Team)nearestCore[slot]=Math.Min(nearestCore[slot],(float)Math.Sqrt((u.X-core.X)*(u.X-core.X)+(u.Z-core.Z)*(u.Z-core.Z)));}}
                if(lifeIds[slot]!=u.UnitId){lifeIds[slot]=u.UnitId;spawnTicks[slot]=world.CurrentTick;firstHit[slot]=-1;previousHealth[slot]=b.MaxHealth;previousShield[slot]=b.MaxShield;}
                if(u.IsAlive){if(u.Team==0)blueAlive++;else orangeAlive++;if(Math.Abs(u.X)<110){totalPositionSamples++;if(Math.Abs(u.Z)<54)centralSamples++;}}
                bool damaged=u.Health<previousHealth[slot]-0.01f||u.Shield<previousShield[slot]-0.01f;
                if(damaged){combatStarted=true;if(firstHit[slot]<0)firstHit[slot]=world.CurrentTick;}
                if(!u.IsAlive&&!reserve&&previousHealth[slot]>0)
                {completedLives++;completedLifeTicks+=world.CurrentTick-spawnTicks[slot];unitGaugeDamage+=b.Cost*config.unitGaugeLossPerCost;if(firstHit[slot]>=0){engagedDeaths++;engagedLifeTicks+=world.CurrentTick-firstHit[slot];}}
                previousHealth[slot]=u.Health;previousShield[slot]=u.Shield;
                if (u.Health < 0 || u.Health > b.MaxHealth || u.Shield < 0 || u.Shield > b.MaxShield || u.Ammo < 0 || u.Ammo > world.GetActiveWeapon(slot).MaxAmmo)
                    throw new Exception("Constructed resource limit violated.");
                for(int wi=0;wi<b.WeaponCount;wi++) if(world.GetWeaponAmmo(slot,wi)<0||world.GetWeaponAmmo(slot,wi)>b.GetWeapon(wi).MaxAmmo)throw new Exception("Holstered ammo limit violated.");
                if(u.Y<0 || !float.IsFinite(u.Y) || !float.IsFinite(u.VerticalVelocity))throw new Exception("Invalid vertical position.");
                if(u.IsAlive && !u.Grounded){airborne++;airborneUnitTicks++;}maximumHeight=Math.Max(maximumHeight,u.Y);
                if(u.Boost<0 || u.Boost>world.GetBoostCapacity(slot)+0.001f)throw new Exception("Boost pool exceeded effective capacity.");
                if (u.IsAlive && world.GetDeckReadyTick(slot, u.DeckIndex) > world.CurrentTick) throw new Exception("CT bypass detected.");
            }
            peakAirborne=Math.Max(peakAirborne,airborne);
            aliveIntegral+=blueAlive+orangeAlive;
            if(combatStarted){minBlue=Math.Min(minBlue,blueAlive);minOrange=Math.Min(minOrange,orangeAlive);contestedSamples++;if(blueAlive>=20&&orangeAlive>=20)healthyFrontSamples++;}
        }
        double coreGaugeDamage=0;int basesDestroyed=0;for(int i=0;i<world.MainBaseCount;i++){var core=world.GetBase(i);coreGaugeDamage+=core.DamageReceived*config.baseGaugeDamageFactor;if(core.Destroyed){basesDestroyed++;coreGaugeDamage+=config.mainBaseDestructionGaugeLoss;}}
        var counters = world.Counters;
        var report = new { scope = "constructed-domain-bot-match-not-ui-player-or-fps-acceptance", mapId=map.id, peakAlive = peak,botReturns,terrain.StuckAvoidanceCount,terrain.StuckReturnCount,radarPlacements,peakRadars,planner.InterceptionOrders,planner.IntelInvestigations,planner.Squads.ObjectiveOrders,planner.Squads.RegroupOrders,
            simulatedSeconds = world.CurrentTick * world.FixedDeltaTime, world.WinnerTeam, counters.ShotsFired, counters.UnitsDestroyed,
            counters.UnitsSpawned, counters.BaseHits, counters.BaseCounterShotsFired, counters.CaptureChanges,
            peakAirborne,airborneUnitSeconds=airborneUnitTicks*world.FixedDeltaTime,maximumHeight,
            averageAlive=aliveIntegral/world.CurrentTick,minBlueAlive=minBlue,minOrangeAlive=minOrange,
            bothTeamsAtLeast20Fraction=contestedSamples==0?0:(double)healthyFrontSamples/contestedSamples,
            completedLives,meanLifeSeconds=completedLives==0?0:completedLifeTicks/completedLives*world.FixedDeltaTime,
            meanFirstDamageToDeathSeconds=engagedDeaths==0?0:engagedLifeTicks/engagedDeaths*world.FixedDeltaTime,
            centralBattlefieldFraction=totalPositionSamples==0?0:(double)centralSamples/totalPositionSamples,coreGaugeDamage,unitGaugeDamage,basesDestroyed,
            blueGauge = world.GetGauge(0), orangeGauge = world.GetGauge(1) };
        Directory.CreateDirectory(Path.Combine(project, "Build/Reports"));
        string suffix=mapFile=="standard-map.json"?"":"-"+map.id;
        File.WriteAllText(Path.Combine(project, "Build/Reports/constructed-battle"+suffix+".json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        var raidProgress=new System.Collections.Generic.List<object>();for(int slot=0;slot<100;slot++)if(slot%50%9==0)raidProgress.Add(new{slot,maxAdvance=maxAdvance[slot],nearestCore=nearestCore[slot]});
        File.WriteAllText(Path.Combine(project,"Build/Reports/raid-diagnostics"+suffix+".json"),JsonSerializer.Serialize(new{raidProgress,raidEvents},new JsonSerializerOptions{WriteIndented=true}));
        var finalUnits=new object[100];
        for(int slot=0;slot<100;slot++){var u=world.GetUnit(slot);var frame=world.GetLoadout(slot,u.DeckIndex);finalUnits[slot]=new {slot,u.Team,u.IsAlive,u.X,u.Y,u.Z,u.Health,u.Shield,u.ActiveWeaponIndex,u.Ammo,u.IncomingLockCount,u.LastDamageTick,u.LastLockedHitTick,maxHealth=frame.MaxHealth};}
        File.WriteAllText(Path.Combine(project,"Build/Reports/constructed-battle-final-units"+suffix+".json"),JsonSerializer.Serialize(finalUnits,new JsonSerializerOptions{WriteIndented=true}));
        if(config.botBehavior.enabled&&config.botBehavior.radarFraction>0&&EnumerableRadarBots(population)>0&&radarPlacements==0)throw new Exception("Radar BOTs did not place any finite radar in this map.");
        if(config.botBehavior.enabled&&config.botBehavior.interceptorFraction>0&&planner.InterceptionOrders==0)throw new Exception("Interceptor BOTs never investigated a shared threat.");
        if (peak != 100 || counters.ShotsFired == 0 || counters.UnitsDestroyed == 0 || counters.UnitsSpawned <= 100 || counters.BaseHits == 0)
            throw new Exception("Constructed battle did not exercise combat, destruction, respawn and core attack.");
        return new { name = "ConstructedDeck_Actual100BotMatchCompletesWithoutResourceOrCooldownBypass", status = "passed", report };
    }
}
