using System;
using MechaBattle.Contracts;
using MechaBattle.Battlefield;
using MechaBattle.Observation;
namespace MechaBattle.Simulation
{
    // Uses public static terrain, this controller's own state, and one bounded visible contact.
    // It never queries world truth. Both motion and attacks still go through UnitCommand.
    public sealed partial class BotTraversalAdapter
    {
        private readonly MapDefinition map; private readonly SimulationConfig config;
        private readonly ICapturePointStatusSource captures; private readonly bool[] resupplying = new bool[100];
        private readonly int[] observedUnits=new int[100];private readonly long[] observedTicks=new long[100];private readonly float[] observedX=new float[100],observedY=new float[100],observedZ=new float[100],velocityX=new float[100],velocityY=new float[100],velocityZ=new float[100];
        private readonly bool[] jumpRequested = new bool[100];
        private readonly int[] ids = new int[100]; private readonly bool[] waypoint = new bool[100], podPlaced = new bool[100];
        private readonly bool[,] exhausted = new bool[100,3]; private readonly float[] wayX = new float[100], wayZ = new float[100];
        public BotTraversalAdapter(MapDefinition map, SimulationConfig config, ICapturePointStatusSource captures = null) { this.map = map; this.config = config; this.captures = captures;for(int i=0;i<100;i++)layerBase[i]=layerGoal[i]=-1; }
        public UnitCommand Adapt(UnitSnapshot self, UnitLoadout frame, UnitCommand desired, int visibleSlot = -1, int visibleUnit = 0, float targetX = 0, float targetY = 0, float targetZ = 0, BotTacticalIntent intent = default)
        {
            int slot = self.SlotId;
            if (ids[slot] != self.UnitId) { ids[slot] = self.UnitId; waypoint[slot] = podPlaced[slot] = resupplying[slot] = false; observedUnits[slot]=0; ResetRecovery(slot); jumpRequested[slot]=false; for (int i = 0; i < 3; i++) exhausted[slot,i] = false; }
            exhausted[slot,self.ActiveWeaponIndex] = self.Ammo == 0;
            if(self.ActiveWeaponIndex==0){if(self.Ammo==0)resupplying[slot]=true;else if(self.Ammo>=frame.MaxAmmo)resupplying[slot]=false;}
            var current = frame.GetWeapon(self.ActiveWeaponIndex);
            if (current.Kind == WeaponKind.RepairPod && self.LastShotTick >= 0) podPlaced[slot] = true;
            if (self.Health > frame.MaxHealth * 0.9f && !retreating[slot]) podPlaced[slot] = false;
            float distance = visibleSlot < 0 ? float.PositiveInfinity : (float)Math.Sqrt((targetX-self.X)*(targetX-self.X)+(targetZ-self.Z)*(targetZ-self.Z));
            UpdateRecovery(self,frame,desired.Tick,visibleSlot,targetX,targetY,targetZ);
            bool flank = config.botCentralApproachEnabled && slot % 50 % 9 == 0;
            bool assault = !config.botLineCombatEnabled || flank || slot % 50 % config.botAssaultModulo == 0;
            bool withdrawing = retreating[slot] || config.botLineCombatEnabled && self.Health < frame.MaxHealth * config.botRetreatHealthFraction;
            bool committingCore=false;
            if(config.botCentralApproachEnabled && assault && (distance>12 || config.botLockRetreatEnabled) && !resupplying[slot] && !retreating[slot])
                foreach(var b in map.mainBases)if(b.team!=self.Team && Math.Abs(b.y-self.Y)<3 && !BaseFallen(Array.IndexOf(map.mainBases,b)) && (b.x-self.X)*(b.x-self.X)+(b.z-self.Z)*(b.z-self.Z)<b.buildingRadius*b.buildingRadius)committingCore=true;
            bool fighting = !resupplying[slot] && !retreating[slot] && (!withdrawing || committingCore);
            int equipment = 0;
            for (int i = 0; i < frame.WeaponCount; i++)
            {
                var w = frame.GetWeapon(i);
                if (w.Kind == WeaponKind.Melee && fighting && (committingCore || distance < (assault ? 5.8f : Math.Min(3, w.FireRange)))) equipment = i;
                if (!committingCore && w.Kind == WeaponKind.RepairPod && !exhausted[slot,i] && !podPlaced[slot] && (self.Health < frame.MaxHealth * (retreating[slot]?config.botRecoveryHealthFraction:0.65f) || retreating[slot] && self.Shield < frame.MaxShield * config.botRecoveryShieldFraction) && (retreating[slot] ? CanDeployRecoveryPod(self,desired.Tick,visibleSlot) : (config.botLineCombatEnabled ? visibleSlot < 0 : distance > 15)) && self.Grounded) equipment = i;
            }
            bool tactical = intent.HasGoal && fighting && !committingCore && !withdrawing && !resupplying[slot] && visibleSlot < 0 && frame.GetWeapon(equipment).Kind != WeaponKind.RepairPod;
            if(tactical && intent.DeployRadar && self.Grounded)for(int i=0;i<frame.WeaponCount;i++)if(frame.GetWeapon(i).Kind==WeaponKind.Radar){equipment=i;break;}
            var active = frame.GetWeapon(equipment); float mx = desired.MoveX, mz = desired.MoveY; bool fire = desired.FirePrimary, melee = desired.Melee;
            var style = desired.MeleeStyle; bool boost = desired.Boost;
            float aimYaw = desired.AimYaw;
            if(config.botCentralApproachEnabled && captures!=null && !tactical)
                RoutePublicObjective(self,desired,flank,assault,resupplying[slot],ref mx,ref mz,ref fire,ref aimYaw);
            if(visibleSlot>=0 && active.Kind==WeaponKind.Ranged && fighting)fire=true;
            if (active.Kind == WeaponKind.RepairPod) { fire = true; mx = mz = 0; }
            else if (active.Kind == WeaponKind.Melee && visibleSlot >= 0 && !committingCore)
            {
                float d = Math.Max(0.001f, distance); mx = distance > 2.5f ? (targetX-self.X)/d : 0; mz = distance > 2.5f ? (targetZ-self.Z)/d : 0;
                melee = distance <= active.FireRange || self.Boost >= 30 && distance < 5.8f; fire = false; style = distance > active.FireRange ? MeleeStyle.Dash : MeleeStyle.Combo;
            }
            else if (active.Kind == WeaponKind.Melee)
            {
                float best = float.PositiveInfinity, bx = self.X, bz = self.Z, radius = 0;
                foreach (var b in map.mainBases) if (b.team != self.Team && !BaseFallen(Array.IndexOf(map.mainBases,b)))
                { float dx = b.x-self.X, dz = b.z-self.Z, d = dx*dx+dz*dz; if (d < best) { best = d; bx = b.x; bz = b.z; radius = b.attackRadius; } }
                aimYaw=(float)(Math.Atan2(bx-self.X,bz-self.Z)*180/Math.PI);
                float d2 = (float)Math.Sqrt(best); bool close = d2 <= radius + active.FireRange * 0.7f;
                mx = close ? 0 : (bx-self.X)/Math.Max(0.001f,d2); mz = close ? 0 : (bz-self.Z)/Math.Max(0.001f,d2); melee = close && self.LastMeleeTick < desired.Tick - 50; fire = false;
            }
            if (config.botLineCombatEnabled && active.Kind == WeaponKind.Ranged && visibleSlot >= 0 && !resupplying[slot])
            {
                float d = Math.Max(0.001f, distance), dx = (targetX-self.X)/d, dz = (targetZ-self.Z)/d;
                if (withdrawing)
                { mx = -dx * 0.9f; mz = -dz * 0.9f; fire = false; boost = self.Boost > 15; }
                else if (!assault)
                {
                    float preferred = Math.Min(active.FireRange * 0.75f, Math.Max(18, active.RedLockRange * config.botPreferredRangeFraction));
                    // A controller-local cadence spreads firing positions without reading
                    // allied/enemy world truth. Preserve fractional speed; don't normalize it.
                    float radial = distance < preferred-3 ? -0.7f : distance > preferred+4 ? 0.55f : 0;
                    float side = ((desired.Tick / 130 + slot) % 2 == 0 ? 1 : -1) * config.botStrafeFraction;
                    mx = dx * radial - dz * side; mz = dz * radial + dx * side;
                }
            }
            if (retreating[slot]) ApplyRecoveryMotion(self,active,desired.Tick,visibleSlot,ref mx,ref mz,ref fire,ref melee,ref boost,ref aimYaw);
            // Preserve a requested melee gesture: automatic evasion must not force it into a higher-cost dash.
            if(config.botLockEvadeEnabled && self.IncomingLockCount>0 && !retreating[slot] && !melee)
            {
                // Self warning plus current motion/visible contact only. No bearing
                // or position of an unseen locking enemy is available to this bot.
                float dx=visibleSlot>=0?targetX-self.X:mx,dz=visibleSlot>=0?targetZ-self.Z:mz;
                float length=(float)Math.Sqrt(dx*dx+dz*dz);
                if(length<0.001f){double a=self.AimYaw*Math.PI/180;dx=(float)Math.Sin(a);dz=(float)Math.Cos(a);length=1;}
                float side=((desired.Tick/Math.Max(1,(long)Math.Ceiling(config.botLockEvadePeriodSeconds*config.tickHz))+slot)%2==0?1:-1)*config.botLockEvadeStrafeFraction;
                mx=mx*0.85f-dz/length*side;mz=mz*0.85f+dx/length*side;
                float combined=(float)Math.Sqrt(mx*mx+mz*mz);if(combined>1){mx/=combined;mz/=combined;}
                boost=self.Boost>15;
            }
            if(tactical){float dx=intent.X-self.X,dz=intent.Z-self.Z,d=(float)Math.Sqrt(dx*dx+dz*dz);mx=d>intent.StopDistance?dx/Math.Max(.001f,d):0;mz=d>intent.StopDistance?dz/Math.Max(.001f,d):0;fire=intent.DeployRadar&&active.Kind==WeaponKind.Radar;melee=false;boost=false;}
            bool layeredRoute=map.navigationNodes!=null&&map.navigationNodes.Length>0&&active.Kind!=WeaponKind.RepairPod&&!committingCore&&(visibleSlot<0||retreating[slot]||resupplying[slot]);
            if(layeredRoute){waypoint[slot]=false;RouteLayeredPublicObjective(self,desired,resupplying[slot]||retreating[slot]||withdrawing,ref mx,ref mz,ref fire,ref aimYaw,tactical?intent:default);if(retreating[slot]||resupplying[slot])melee=false;}
            bool jump = desired.Jump || WantsAerialManeuver(self,frame,active,desired.Tick,visibleSlot,withdrawing,resupplying[slot]);
            if(layeredRoute)jump=false;
            if(tactical&&intent.DeployRadar&&active.Kind==WeaponKind.Radar){mx=mz=0;fire=true;melee=jump=false;}
            if (config.traversalEnabled && mx*mx+mz*mz > 0.01f && !layeredRoute)
            {
                float length = (float)Math.Sqrt(mx*mx+mz*mz), travelScale = Math.Min(1,length); mx/=length; mz/=length;
                if (waypoint[slot])
                {
                    float dx = wayX[slot]-self.X, dz = wayZ[slot]-self.Z, d = (float)Math.Sqrt(dx*dx+dz*dz);
                    if (d > 0.5f) { mx=dx/d; mz=dz/d; } else waypoint[slot] = false;
                }
                var blocked = Blocking(self.X,self.Y,self.Z,mx,mz,Math.Max(4,frame.MoveSpeed*0.45f),false);
                if (blocked != null)
                {
                    float top = blocked.y+blocked.height/2;
                    if (top-self.Y <= JumpableRise() && top-self.Y > config.maximumStepHeight+0.001f) jump = true;
                    else if (!waypoint[slot])
                    {
                        float best = float.PositiveInfinity, goalX=self.X+mx*80, goalZ=self.Z+mz*80, margin=config.unitRadius+0.85f;
                        for (int ix=-1;ix<=1;ix+=2) for (int iz=-1;iz<=1;iz+=2)
                        {
                            float x=blocked.x+ix*(blocked.width/2+margin), z=blocked.z+iz*(blocked.depth/2+margin), dx=x-self.X,dz=z-self.Z,d=(float)Math.Sqrt(dx*dx+dz*dz);
                            if (d < 0.2f || Math.Abs(x)>map.halfWidth-config.unitRadius || Math.Abs(z)>map.halfDepth-config.unitRadius || Blocking(self.X,self.Y,self.Z,dx/d,dz/d,d,true)!=null) continue;
                            float score=d+(float)Math.Sqrt((x-goalX)*(x-goalX)+(z-goalZ)*(z-goalZ))+(iz==(slot%2==0?-1:1)?0:0.01f);
                            if (score < best) { best=score; wayX[slot]=x; wayZ[slot]=z; }
                        }
                        if (best<float.PositiveInfinity) { waypoint[slot]=true; float dx=wayX[slot]-self.X,dz=wayZ[slot]-self.Z,d=(float)Math.Sqrt(dx*dx+dz*dz); mx=dx/d; mz=dz/d; }
                    }
                }
                mx *= travelScale; mz *= travelScale;
            }
            // A grounded failed/finished jump must release before another press.
            // Keep holding only while airborne to preserve the shared hover mechanic.
            if (self.Grounded && jump && jumpRequested[slot]) jump=false;
            jumpRequested[slot]=jump;
            float aimX=targetX,aimY=targetY,aimZ=targetZ;
            if(config.botLineCombatEnabled && visibleSlot>=0)
            {
                if(observedUnits[slot]==visibleUnit && desired.Tick>observedTicks[slot])
                {
                    float seconds=(desired.Tick-observedTicks[slot])*(1f/config.tickHz);
                    // Estimate motion only from this controller's consecutive visible samples.
                    float vx=(targetX-observedX[slot])/seconds,vy=(targetY-observedY[slot])/seconds,vz=(targetZ-observedZ[slot])/seconds;
                    velocityX[slot]=ClampVelocity(vx);velocityY[slot]=ClampVelocity(vy);velocityZ[slot]=ClampVelocity(vz);
                }
                else if(observedUnits[slot]!=visibleUnit)velocityX[slot]=velocityY[slot]=velocityZ[slot]=0;
                observedUnits[slot]=visibleUnit;observedTicks[slot]=desired.Tick;observedX[slot]=targetX;observedY[slot]=targetY;observedZ[slot]=targetZ;
                float lead=active.ProjectileSpeed>0?Math.Min(2.5f,distance/active.ProjectileSpeed):0;
                lead*=config.botProjectileLeadFraction>0?config.botProjectileLeadFraction:1;
                aimX+=velocityX[slot]*lead;aimY+=velocityY[slot]*lead;aimZ+=velocityZ[slot]*lead;
            }
            else observedUnits[slot]=0;
            if(visibleSlot<0&&map.navigationNodes!=null&&map.navigationNodes.Length>0&&layerBase[slot]>=0)
            {var core=map.mainBases[layerBase[slot]];aimX=core.x;aimY=core.y;aimZ=core.z;}
            int change = (equipment-self.ActiveWeaponIndex+frame.WeaponCount)%frame.WeaponCount;
            var adapted=new UnitCommand(desired.UnitId,desired.Tick,desired.Sequence,mx,mz,aimYaw,desired.AimPitch,boost,fire,desired.FireSecondary,melee,desired.UseAbility,
                desired.Reload,style,visibleSlot>=0&&!committingCore || visibleSlot<0&&map.navigationNodes!=null&&map.navigationNodes.Length>0&&layerBase[slot]>=0,aimX,aimY+config.shotOriginHeight,aimZ,active.Kind==WeaponKind.Ranged&&!retreating[slot]?visibleSlot:-1,visibleUnit,jump,change,retreating[slot]&&active.Kind!=WeaponKind.RepairPod||desired.CancelHeldAttack,desired.LockedDeployableIndex,desired.LockedDeployableId,desired.EmergencyReturn,desired.Crouch);
            return RecoverStuckMovement(self,frame,adapted);
        }
        private static float ClampVelocity(float value)=>Math.Max(-30,Math.Min(30,value));
        private void RoutePublicObjective(UnitSnapshot self,UnitCommand command,bool flank,bool assault,bool resupply,ref float mx,ref float mz,ref bool fire,ref float yaw)
        {
            if(map.navigationNodes!=null&&map.navigationNodes.Length>0){RouteLayeredPublicObjective(self,command,resupply,ref mx,ref mz,ref fire,ref yaw);return;}
            // Four raiders and two relay roles use opposite sides on each team.
            // This is a symmetric public role plan, without inspecting enemy positions.
            bool raider=self.SlotId%50/9%3!=0;
            float lane=flank?(raider?(self.Team==0?80:-80):(self.Team==0?-80:80)):0;
            float x=self.X,z=lane,stop=1,range=0;bool baseAttack=false;
            if(resupply)
            {
                float nearest=float.PositiveInfinity;
                foreach(var b in map.mainBases)if(b.team==self.Team&&!BaseFallen(Array.IndexOf(map.mainBases,b)))
                {float d=(b.x-self.X)*(b.x-self.X)+(b.z-self.Z)*(b.z-self.Z);if(d<nearest){nearest=d;x=b.x;z=b.z;}}
                stop=config.homeSupplyRadius*0.5f;
            }
            else
            {
                int relay=-1;float best=float.PositiveInfinity;
                for(int i=0;i<map.capturePoints.Length;i++){float d=Math.Abs(map.capturePoints[i].z-lane);if(d<best){best=d;relay=i;}}
                if(!(flank && self.SlotId%50/9%3!=0) && relay>=0 && relay<captures.CapturePointCount && captures.GetCapturePoint(relay).OwnerTeam!=self.Team)
                {var p=map.capturePoints[relay];x=p.x;z=p.z;stop=p.radius*0.5f;}
                else
                {
                    best=float.PositiveInfinity;
                    foreach(var b in map.mainBases)if(b.team!=self.Team && !BaseFallen(Array.IndexOf(map.mainBases,b)) && Math.Abs(b.z-lane)<best)
                    {best=Math.Abs(b.z-lane);x=b.x;z=b.z;range=b.buildingRadius>0?b.buildingRadius:config.fireRange;stop=assault?(b.attackRadius+2.4f):range*0.7f;baseAttack=true;}
                }
            }
            float travelLane=lane;
            if(flank && raider && !resupply && baseAttack && config.botFlankOuterOffset>0)
            {
                float forward=self.Team==0?1:-1,ingressX=x-forward*config.botFlankIngressDistance;
                if((self.X-ingressX)*forward < -3)
                {
                    travelLane=Math.Max(-map.halfDepth+config.unitRadius+2,Math.Min(map.halfDepth-config.unitRadius-2,lane+Math.Sign(lane)*config.botFlankOuterOffset));
                    x=ingressX;z=travelLane;stop=3;baseAttack=false;
                }
            }
            if(flank && !resupply && Math.Abs(self.Z-travelLane)>6)
            {
                float forward=self.Team==0?1:-1,homeX=0;
                foreach(var home in map.mainBases)if(home.team==self.Team){homeX=home.x;break;}
                float stagingX=homeX+forward*Math.Min(50,map.halfWidth*0.2f);
                if((self.X-stagingX)*forward<0){x=self.X;z=travelLane;stop=1;baseAttack=false;}
            }
            float dx=x-self.X,dz=z-self.Z,distance=(float)Math.Sqrt(dx*dx+dz*dz),normal=Math.Max(0.001f,distance);
            // Individual deterministic spacing keeps the main route broad without allied truth.
            if(!flank && !resupply && Math.Abs(self.X)>95){dz+=(self.SlotId%7-3)*3;normal=(float)Math.Sqrt(dx*dx+dz*dz);}
            mx=distance>stop?dx/normal:0;mz=distance>stop?dz/normal:0;
            yaw=(float)(Math.Atan2(dx,dz)*180/Math.PI);fire=baseAttack && distance<=range;
        }
        private ObstacleDefinition Blocking(float x,float y,float z,float dx,float dz,float distance,bool ignoreJumpable)
        {
            ObstacleDefinition best=null; double nearest=distance;
            foreach(var o in map.obstacles ?? Array.Empty<ObstacleDefinition>())
            {
                float top=o.y+o.height/2;
                if(top-y<=config.maximumStepHeight+0.001f || y>=top-0.01f || y+config.unitHeight<=o.y-o.height/2 || ignoreJumpable && top-y<=JumpableRise()) continue;
                double hit=MapGeometry.RayBox(o,x,y+0.1f,z,dx,0,dz,config.unitRadius+0.15f);
                if(hit<nearest){nearest=hit;best=o;}
            }
            return best;
        }
    }
}
