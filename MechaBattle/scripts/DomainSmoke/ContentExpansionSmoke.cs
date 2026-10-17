using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using MechaBattle.Contracts;
using MechaBattle.Contracts.Construction;
using MechaBattle.Battlefield;
using MechaBattle.Construction;
internal static class ContentExpansionSmoke
{
    public static object Run(string project)
    {
        var options=new JsonSerializerOptions{IncludeFields=true};
        string catalogText=File.ReadAllText(Path.Combine(project,"config/map-catalog.json"));
        if(catalogText!=File.ReadAllText(Path.Combine(project,"Assets/Game/Resources/MechaBattleMaps/MapCatalog.json")))throw new Exception("Resource map catalog is stale.");
        var choices=JsonSerializer.Deserialize<MapCatalog>(catalogText,options);
        var maps=MapCatalogPolicy.ValidateAndLoad(choices,path=>JsonSerializer.Deserialize<MapDefinition>(File.ReadAllText(Path.Combine(project,"Assets/Game/Resources",path+".json")),options));
        if(maps.Length!=4||maps.Any(m=>m.id=="p0-three-lanes"||m.id=="terraced-works"))throw new Exception("Expected four distinct selectable maps without retired maps.");
        for(int i=0;i<maps.Length;i++)
        {
            var map=maps[i];if(map.mainBases.Count(b=>b.team==0)!=3 || map.mainBases.Count(b=>b.team==1)!=3 || map.capturePoints.Length!=(map.navigationNodes.Length>0?6:3) || map.obstacles.Length<80)
                throw new Exception("Selectable map lacks its required objectives/terrain.");
            if(map.obstacles.Where(o=>o.platform).Max(o=>o.y+o.height/2)<5)throw new Exception("Selectable map lacks vertical routes.");
            if(File.ReadAllText(Path.Combine(project,"config",choices.entries[i].sourceFile))!=File.ReadAllText(Path.Combine(project,"Assets/Game/Resources",choices.entries[i].resourcePath+".json")))throw new Exception("Resource map differs from its source.");
        }
        var yardRoutes=ValidateYardMap(project,maps.Single(m=>m.id=="reclamation-yard"));
        ValidateUrbanMap(maps.Single(m=>m.id=="urban-overpass"));ValidateLayeredMap(maps.Single(m=>m.id=="layered-city"));
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(project,"config/prototype-defaults.json")));
        var catalog=JsonSerializer.Deserialize<ConstructionCatalog>(doc.RootElement.GetProperty("constructionPrototype").GetProperty("catalog").GetRawText(),options);
        if(catalog.frames.Length!=4 || catalog.parts.Count(p=>p.slot==PartSlot.LeftArm)!=3 || catalog.presets.Length!=10)throw new Exception("Expanded frame/arm/preset content missing.");
        using var kit=JsonDocument.Parse(File.ReadAllText(Path.Combine(project,"Assets/Game/Content/Models/MB_Worker01/module-kit.json")));
        var visualIds=kit.RootElement.GetProperty("modules").EnumerateArray().Select(m=>m.GetProperty("partId").GetString()).OrderBy(x=>x).ToArray();
        if(!visualIds.SequenceEqual(catalog.frames.Select(f=>f.id).Concat(catalog.parts.Select(p=>p.id)).OrderBy(x=>x)))throw new Exception("A gameplay frame/part lacks a visual module.");
        int paintChecks=0;
        foreach(var preset in catalog.presets)
        {
            var baseline=ConstructionEvaluator.Evaluate(catalog,preset);
            for(int i=0;i<BuildAppearance.PaintCount;i++)
            {
                var painted=DeckConstruction.Copy(preset);painted.appearance.paintId=BuildAppearance.PaintId(i);
                var restored=JsonSerializer.Deserialize<BuildDefinition>(JsonSerializer.Serialize(painted,options),options);var resolved=ConstructionEvaluator.Evaluate(catalog,restored);
                if(resolved.PaintId!=painted.appearance.paintId || resolved.Cost!=baseline.Cost || resolved.CooldownSeconds!=baseline.CooldownSeconds || resolved.MaxHealth!=baseline.MaxHealth || resolved.MaxShield!=baseline.MaxShield || resolved.MoveSpeed!=baseline.MoveSpeed)throw new Exception("Cosmetic paint changed performance or failed save/load.");paintChecks++;
            }
        }
        int triples=0;
        foreach(var frame in catalog.frames)
        foreach(var left in catalog.parts.Where(p=>p.slot==PartSlot.LeftArm))
        foreach(var right in catalog.parts.Where(p=>p.slot==PartSlot.RightArm && p.armFamily==left.armFamily))
        {
            var weapons=catalog.parts.Where(p=>p.slot==PartSlot.PrimaryWeapon && FrameConstruction.CanMount(frame,p,PartSlot.PrimaryWeapon)).ToArray();
            foreach(var a in weapons)foreach(var b in weapons)foreach(var c in weapons)
            {
                var build=new BuildDefinition{id="content-matrix",frameId=frame.id,equipment=new[]{new EquipmentSelection{slot=PartSlot.LeftArm,partId=left.id},new EquipmentSelection{slot=PartSlot.RightArm,partId=right.id},new EquipmentSelection{slot=PartSlot.PrimaryWeapon,partId=a.id},new EquipmentSelection{slot=PartSlot.SecondaryWeapon,partId=b.id},new EquipmentSelection{slot=PartSlot.TertiaryWeapon,partId=c.id}}};
                var resolved=ConstructionEvaluator.Evaluate(catalog,build);if(resolved.MaxHealth<2000 || resolved.CombatLoadout.WeaponCount!=3)throw new Exception("Expanded build violates HP/inventory requirements.");triples++;
            }
        }
        if(triples!=13314)throw new Exception("Expected13314 valid frame/arm/three-weapon builds, got "+triples);
        var battles=new List<object>();for(int i=1;i<choices.entries.Length;i++)battles.Add(ConstructedBattleSmoke.Run(project,choices.entries[i].sourceFile));
        return new{name="ExpandedContent_AllFramesPaintsMapsAnd100BotMatches",status="passed",maps=maps.Select(m=>new{m.id,m.halfWidth,m.halfDepth,obstacles=m.obstacles.Length}),paintChecks,allFrameTripleBuilds=triples,yardRoutes,battles};
    }
    private sealed class LayeredPublicStatus:MechaBattle.Observation.ICapturePointStatusSource,MechaBattle.Observation.IMainBaseStatusSource
    {
        private readonly MechaBattle.Simulation.BattleSimulation world;public LayeredPublicStatus(MechaBattle.Simulation.BattleSimulation world){this.world=world;}
        public int MainBaseCount=>world.MainBaseCount;public bool IsMainBaseDestroyed(int i)=>world.GetBase(i).Destroyed;public int CapturePointCount=>world.CapturePointCount;
        public MechaBattle.Observation.CapturePointStatus GetCapturePoint(int i){var c=world.GetCapturePoint(i);return new MechaBattle.Observation.CapturePointStatus(c.OwnerTeam,c.X,c.Z);}
    }
    internal static void ValidateLayeredMap(MapDefinition map)
    {
        if(map.surfaceY!=16||map.capturePoints.Length!=6||map.capturePoints[3].y!=0||map.capturePoints[4].y!=16||map.navigationNodes.Length<50)throw new Exception("Layered objectives/routes missing.");
        for(int i=0;i<6;i++)if(map.MainBaseName(i)!=(i<3?((char)('A'+i)).ToString():((char)('X'+i-3)).ToString()))throw new Exception("Main base north order is wrong.");
        var c=MechaBattle.Tests.FieldFixture.Config();c.unitRadius=.65f;c.baseCollisionEnabled=true;c.redLockAssistEnabled=false;c.maxDurationSeconds=1000;
        var world=new MechaBattle.Simulation.BattleSimulation(map,c,0,null,0,1);
        void Move(float x,float z)
        {for(int tick=0;tick<5000;tick++){var u=world.GetUnit(0);float dx=x-u.X,dz=z-u.Z,d=(float)Math.Sqrt(dx*dx+dz*dz);if(d<.03f)return;float scale=Math.Max(d,.16f);MechaBattle.Tests.FieldFixture.Cmd(world,0,dx/scale,dz/scale);world.Step();}throw new Exception("Layered path blocked at "+world.GetUnit(0).X+","+world.GetUnit(0).Y+","+world.GetUnit(0).Z+" to "+x+","+z);}
        Move(-205,-35);Move(-180,-65);Move(-180,-80);for(int i=1;i<=16;i++)Move(-180,-80-i*4);Move(-180,-153);Move(-180,-170);
        if(world.GetUnit(0).Y>.05f)throw new Exception("Entrance 5 stairs did not reach underground.");
        Move(-130,-170);Move(-130,-225);Move(-60,-225);Move(-60,-135);Move(0,-135);if(world.GetUnit(0).Y>.05f)throw new Exception("Relay 4 is not underground.");
        Move(-60,-135);Move(-60,-225);Move(-130,-225);Move(-130,-170);Move(-230,-170);Move(-230,-220);Move(-280,-220+10);
        Move(-230,-210);Move(-230,-170);Move(-130,-170);Move(-130,-225);Move(-60,-225);Move(-60,-135);Move(60,-135);Move(60,-225);Move(130,-225);Move(130,-170);Move(230,-170);Move(230,-220);Move(280,-210);
        Move(230,-210);Move(230,-170);Move(180,-170);Move(180,-153);Move(180,-144);for(int i=15;i>=0;i--)Move(180,-80-i*4);Move(180,-65);
        if(Math.Abs(world.GetUnit(0).Y-16)>.05f)throw new Exception("Entrance 6 stairs did not return to street.");
        c.botCentralApproachEnabled=true;
        var botWorld=new MechaBattle.Simulation.BattleSimulation(map,c,-1);var status=new LayeredPublicStatus(botWorld);var adapter=new MechaBattle.Simulation.BotTraversalAdapter(map,c,status);bool below=false;bool capturedLower=false;
        for(int tick=0;tick<18000&&botWorld.Counters.BaseHits==0;tick++)
        {var self=botWorld.GetUnit(1);var desired=new MechaBattle.Contracts.UnitCommand(self.UnitId,botWorld.CurrentTick,(uint)botWorld.CurrentTick+1,0,0,0,0,false,false,false,false,false);var cmd=adapter.Adapt(self,botWorld.GetLoadout(1,self.DeckIndex),desired);if(!botWorld.Submit(1,cmd))throw new Exception("Layer BOT command rejected.");botWorld.Step();below|=self.Y<1;capturedLower|=botWorld.GetCapturePoint(3).OwnerTeam==0;}
        if(!below||!capturedLower||botWorld.Counters.BaseHits==0)throw new Exception("Public layer BOT did not traverse entrance 5, capture 4 and attack Z: "+botWorld.GetUnit(1).X+","+botWorld.GetUnit(1).Y+","+botWorld.GetUnit(1).Z);
        bool Blocked(float ax,float ay,float az,float bx,float by,float bz)=>map.obstacles.Any(o=>MapGeometry.RayBox(o,ax,ay,az,bx-ax,by-ay,bz-az)<.9999);
        if(Blocked(0,17.3f,0,0,17.3f,155))throw new Exception("Central victory route to relay 1 must stay open.");
        if(!Blocked(100,17.3f,-65,-180,17.3f,-65)||!Blocked(-100,17.3f,-65,180,17.3f,-65))throw new Exception("Entrance 5/6 sniper shields missing.");
    }
    internal static object ValidateYardMap(string project,MapDefinition map)
    {
        MapValidator.ThrowIfInvalid(map);
        if(map.surfaceY!=18||map.LowerLayerName!="低地"||map.capturePoints.Length!=6||map.capturePoints[3].y!=0)throw new Exception("Yard height/objectives/lowland label missing.");
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(project,"config/prototype-defaults.json")));
        var opts=new JsonSerializerOptions{IncludeFields=true};
        var c=JsonSerializer.Deserialize<MechaBattle.Simulation.SimulationConfig>(json.RootElement.GetProperty("p0Simulation").GetRawText(),opts);
        JsonSerializer.Deserialize<MechaBattle.Playable.PlayablePrototype>(json.RootElement.GetProperty("playablePrototype").GetRawText(),opts).Apply(c);
        // Isolate authored movement from turret damage. The separate 100BOT match uses unmodified combat settings.
        c.maxDurationSeconds=1200;c.baseCounterFireDamage=c.baseCounterFireRadius=c.baseCounterFireIntervalSeconds=0;
        var world=new MechaBattle.Simulation.BattleSimulation(map,c,0,null,0,1);
        for(int i=0;i<map.mainBases.Length;i++){var b=map.mainBases[i];if(Math.Abs(world.GetSurfaceHeightBelow(b.SpawnX,b.SpawnZ,b.SpawnY,.65f)-b.SpawnY)>.01f)throw new Exception("Yard spawn is not on its declared floor: "+b.id);}
        void Move(float x,float z)
        {
            for(int tick=0;tick<5000;tick++)
            {var u=world.GetUnit(0);float dx=x-u.X,dz=z-u.Z,d=(float)Math.Sqrt(dx*dx+dz*dz);if(d<.03f)return;float scale=Math.Max(d,.16f);MechaBattle.Tests.FieldFixture.Cmd(world,0,dx/scale,dz/scale);world.Step();}
            throw new Exception("Yard path blocked at "+world.GetUnit(0).X+","+world.GetUnit(0).Y+","+world.GetUnit(0).Z+" toward "+x+","+z);
        }
        Move(-210,-15);Move(-210,-60);Move(-160,-60);Move(-160,-77);Move(-160,-110);Move(-190,-180);Move(0,-180);
        if(world.GetUnit(0).Y>.01f)throw new Exception("Central yard drop did not reach the lower service area.");
        for(int i=0;i<(int)Math.Ceiling(c.captureSecondsPerAdvantage*c.tickHz)+10;i++)world.Step();
        if(world.GetCapturePoint(3).OwnerTeam!=0)throw new Exception("Lower relay 4 cannot be captured at its own height.");
        Move(0,-105);float dropJumpPeak=0;
        for(int i=0;i<220;i++){MechaBattle.Tests.FieldFixture.Cmd(world,z:1,jump:true);world.Step();dropJumpPeak=Math.Max(dropJumpPeak,world.GetUnit(0).Y);}
        if(world.GetUnit(0).Z>=-80||dropJumpPeak>=18)throw new Exception("The 18m center cliff is not a drop-only passage under the shared boost limit.");
        Move(0,-180);Move(-190,-180);Move(-280,-180);Move(-320,-215);Move(-350,-205);Move(-350,-200);
        for(int k=1;k<=12;k++)Move(-350,-200+k*10);
        Move(-350,-60);Move(-300,-45);Move(-235,0);Move(-210,90);Move(-210,120);Move(-235,120);Move(-235,150);Move(-235,185);Move(-235,210);
        if(Math.Abs(world.GetUnit(0).Y-18)>.01f)throw new Exception("West ramp/factory door raised the unit into a solid room or failed to return to Y18.");
        Move(-300,210);Move(-350,172);Move(-350,150);Move(-300,120);Move(-210,120);Move(-210,90);Move(-235,0);Move(-210,-15);Move(-210,-60);Move(-160,-60);Move(-160,-110);Move(-190,-180);Move(0,-180);Move(190,-180);Move(280,-180);Move(320,-215);Move(350,-205);Move(350,-200);
        for(int k=1;k<=12;k++)Move(350,-200+k*10);
        Move(350,-60);Move(300,-45);Move(235,0);Move(250,0);MechaBattle.Tests.FieldFixture.Cmd(world,fire:true,yaw:90);world.Step();
        if(Math.Abs(world.GetUnit(0).Y-18)>.01f||world.Counters.BaseHits==0)throw new Exception("East return ramp or enemy Y core approach cannot be used.");
        bool Blocked(float ax,float ay,float az,float bx,float by,float bz)=>map.obstacles.Any(o=>MapGeometry.RayBox(o,ax,ay,az,bx-ax,by-ay,bz-az)<.9999);
        if(!Blocked(-280,19.3f,160,-235,19.3f,160)||Blocked(-235,19.3f,125,-235,19.3f,210))throw new Exception("Factory side wall/door/room sightline does not match the authored geometry.");
        // Exercise the actual public directed graph: a BOT must descend, capture 4 and reach Z.
        var bot=new MechaBattle.Simulation.BattleSimulation(map,c,-1);var status=new LayeredPublicStatus(bot);var adapter=new MechaBattle.Simulation.BotTraversalAdapter(map,c,status);bool below=false,captured=false;
        for(int tick=0;tick<18000&&bot.Counters.BaseHits==0;tick++)
        {var self=bot.GetUnit(1);var cmd=new MechaBattle.Contracts.UnitCommand(self.UnitId,bot.CurrentTick,(uint)bot.CurrentTick+1,0,0,0,0,false,false,false,false,false);cmd=adapter.Adapt(self,bot.GetLoadout(1,self.DeckIndex),cmd);if(!bot.Submit(1,cmd))throw new Exception("Yard BOT command rejected.");bot.Step();below|=self.Y<1;captured|=bot.GetCapturePoint(3).OwnerTeam==0;}
        if(!below||!captured||bot.Counters.BaseHits==0)throw new Exception("Yard public-route BOT did not descend/capture/attack: "+bot.GetUnit(1).X+","+bot.GetUnit(1).Y+","+bot.GetUnit(1).Z);
        return new{scope="actual-map-production-motor-public-nav-not-unity-rendering",motorTicks=world.CurrentTick,dropJumpPeak,publicBotTicks=bot.CurrentTick,bot.Counters.BaseHits};
    }
    internal static void ValidateUrbanMap(MapDefinition urban)
    {
        if(urban.obstacles.Count(o=>o.id.Contains("-floor-"))!=18 || urban.obstacles.Count(o=>o.id.StartsWith("Skybridge-"))!=2 || urban.obstacles.Count(o=>o.id.Contains("-stair-"))!=96)throw new Exception("Urban multi-storey content is incomplete.");
        // Verify real authored doors/rooms and the real staircase using the production motor.
        var urbanConfig=MechaBattle.Tests.FieldFixture.Config();urbanConfig.unitRadius=.65f;urbanConfig.maxDurationSeconds=600;
        var urbanWorld=new MechaBattle.Simulation.BattleSimulation(urban,urbanConfig,-1);
        void MoveUrban(float x,float z)
        {
            for(int n=0;n<5000;n++)
            {
                var u=urbanWorld.GetUnit(0);float dx=x-u.X,dz=z-u.Z,d=(float)Math.Sqrt(dx*dx+dz*dz);if(d<.03f)return;
                float scale=Math.Max(d,.2f);MechaBattle.Tests.FieldFixture.Cmd(urbanWorld,0,dx/scale,dz/scale);urbanWorld.Step();
            }
            throw new Exception("Authored urban path is blocked at "+urbanWorld.GetUnit(0).X+","+urbanWorld.GetUnit(0).Z+" toward "+x+","+z);
        }
        MoveUrban(-70,35);
        MoveUrban(-70,65);
        if(urbanWorld.GetUnit(0).Y>0.01f)throw new Exception("An urban room is filled or has a ground collision obstruction.");
        MoveUrban(-70,52);MoveUrban(-52.5f,52);
        MoveUrban(-52.5f,76.5f);
        if(urbanWorld.GetUnit(0).Y<5.9f)throw new Exception("Authored urban stairs do not reach the first storey.");
        MoveUrban(-52.5f,80);MoveUrban(-70,80);MoveUrban(-70,65);MoveUrban(-70,30);MoveUrban(-70,-30);MoveUrban(-70,-65);
        if(Math.Abs(urbanWorld.GetUnit(0).Y-6)>.01f)throw new Exception("Urban stair/landing/storey/skybridge connection is not continuous.");
    }
}
