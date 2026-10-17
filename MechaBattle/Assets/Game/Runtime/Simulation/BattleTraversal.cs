using System;
using MechaBattle.Battlefield;
namespace MechaBattle.Simulation
{
    public sealed partial class BattleSimulation
    {
        private void IntegrateVertical(UnitState u)
        {
            if (!config.traversalEnabled) return;
            if (u.Grounded) u.AerialOriginY=u.Y;
            bool canJump = !u.BoostOverheated && CurrentTick >= u.DisabledUntil && CurrentTick >= u.MeleeUntil && !u.MeleeCharging && u.Grounded && !u.JumpHeld
                && u.HasCommand && u.Command.Jump && u.Boost >= config.jumpBoostCost;
            if (canJump) { u.VerticalVelocity = config.jumpSpeed; u.Grounded = false; ConsumeBoost(u,config.jumpBoostCost); }
            if (u.Grounded) return;
            bool powered=false;
            if(config.aerialBoostEnabled) powered=IntegrateAerialVelocity(u);
            else
            {
            float gravity = config.gravity;
            if (!u.BoostOverheated && CurrentTick >= u.DisabledUntil && u.HasCommand && u.Command.Jump && config.jumpHoldGravityMultiplier > 0 && u.Boost >= config.jumpHoldDrainPerSecond * FixedDeltaTime)
            { gravity *= config.jumpHoldGravityMultiplier; ConsumeBoost(u,config.jumpHoldDrainPerSecond * FixedDeltaTime); }
            u.VerticalVelocity -= gravity * FixedDeltaTime;
            }
            float next = u.Y + u.VerticalVelocity * FixedDeltaTime;
            float ceiling=u.AerialOriginY+config.aerialMaximumRise;
            if(powered && next>ceiling && u.Y<=ceiling) { next=ceiling;u.VerticalVelocity=0; }
            if (next > u.Y)
            {
                foreach (var o in obstacles)
                {
                    float bottom = o.y - o.height / 2;
                    if (u.Y + config.unitHeight <= bottom + 0.001f && next + config.unitHeight >= bottom && MapGeometry.CircleOverlaps(o, u.X, u.Z, config.unitRadius))
                    { next = bottom - config.unitHeight; u.VerticalVelocity = 0; }
                }
            }
            else
            {
                float floor = 0;
                foreach (var o in obstacles)
                {
                    float top = o.y + o.height / 2;
                    if (u.Y >= top - 0.001f && next <= top && top > floor && MapGeometry.CircleOverlaps(o, u.X, u.Z, config.unitRadius)) floor = top;
                }
                if (next <= floor) { next = floor; u.VerticalVelocity = 0; u.Grounded = true; }
            }
            u.Y = Math.Max(0, next);
        }
        private void MoveHorizontal(UnitState u, float dx, float dz)
        {
            if (!config.traversalEnabled)
            { u.X = Clamp(u.X + dx, -halfWidth + config.unitRadius, halfWidth - config.unitRadius); u.Z = Clamp(u.Z + dz, -halfDepth + config.unitRadius, halfDepth - config.unitRadius); return; }
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dz * dz) / Math.Max(0.1f, config.unitRadius * 0.4f)));
            for (int i = 0; i < steps; i++)
            {
                float x = Clamp(u.X + dx / steps, -halfWidth + config.unitRadius, halfWidth - config.unitRadius);
                if (CanStandAt(u, x, u.Z, out float stepX)) { u.X = x; u.Y = stepX; }
                float z = Clamp(u.Z + dz / steps, -halfDepth + config.unitRadius, halfDepth - config.unitRadius);
                if (CanStandAt(u, u.X, z, out float stepZ)) { u.Z = z; u.Y = stepZ; }
            }
            if (u.Grounded && u.Y > 0.001f)
            {
                bool supported = false;
                foreach (var o in obstacles) if (Math.Abs(u.Y - o.y - o.height / 2) < 0.01f && MapGeometry.CircleOverlaps(o, u.X, u.Z, config.unitRadius)) { supported = true; break; }
                if (!supported) u.Grounded = false;
            }
        }
        private bool CanStandAt(UnitState u, float x, float z, out float floor)
        {
            floor = u.Y;
            if(config.baseCollisionEnabled)foreach(var b in bases)
            {
                if(u.Y>=b.Y+config.coreHeight-.001f||u.Y+config.unitHeight<=b.Y+.001f)continue;
                double radius=b.AttackRadius+config.unitRadius,dist=DistanceSquared(x,z,b.X,b.Z);
                if(dist<radius*radius-0.000001 && dist<DistanceSquared(u.X,u.Z,b.X,b.Z)+0.000001)return false;
            }
            foreach (var o in obstacles)
            {
                if (!MapGeometry.CircleOverlaps(o, x, z, config.unitRadius) || u.Y >= o.y + o.height / 2 - 0.001f || u.Y + config.unitHeight <= o.y - o.height / 2 + 0.001f) continue;
                float top = o.y + o.height / 2;
                if (u.Grounded && top - u.Y <= config.maximumStepHeight) floor = Math.Max(floor, top);
                else return false;
            }
            if (floor > u.Y)
                foreach (var o in obstacles)
                    if (MapGeometry.CircleOverlaps(o, x, z, config.unitRadius) && floor < o.y + o.height / 2 - 0.001f && floor + config.unitHeight > o.y - o.height / 2 + 0.001f) return false;
            return true;
        }
        public float GetSurfaceHeightBelow(float x,float z,float maximumY,float radius=0)
        {
            if(!SimulationConfig.IsFinite(maximumY)||maximumY<0)throw new ArgumentException("Invalid floor ceiling.");
            float floor=0;foreach(var o in obstacles)if(o.y+o.height/2<=maximumY+.001f && (radius==0?Math.Abs(x-o.x)<=o.width/2&&Math.Abs(z-o.z)<=o.depth/2:MapGeometry.CircleOverlaps(o,x,z,radius)))floor=Math.Max(floor,o.y+o.height/2);return floor;
        }
        public float GetSurfaceHeight(float x,float z,float radius=0)
        {
            if(!SimulationConfig.IsFinite(x)||!SimulationConfig.IsFinite(z)||!SimulationConfig.IsFinite(radius)||radius<0)throw new ArgumentException("Invalid surface query.");
            float top=0;foreach(var o in obstacles)if(radius==0 ? Math.Abs(x-o.x)<=o.width/2 && Math.Abs(z-o.z)<=o.depth/2 : MapGeometry.CircleOverlaps(o,x,z,radius))top=Math.Max(top,o.y+o.height/2);return top;
        }
        public bool HasLineOfSight(int fromSlot, int toSlot)
        {
            var a = Unit(fromSlot); var b = Unit(toSlot);
            return a.Alive && b.Alive && ClearSegment(a.X, a.Y + ShotHeight, a.Z, b.X, b.Y + ShotHeight, b.Z);
        }
        private bool ClearSegment(float x, float y, float z, float endX, float endY, float endZ)
        {
            double dx = endX - x, dy = endY - y, dz = endZ - z;
            double length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (length < 0.003) return true;
            x += (float)(dx / length * 0.002); y += (float)(dy / length * 0.002); z += (float)(dz / length * 0.002);
            foreach (var o in obstacles) if (MapGeometry.RayBox(o, x, y, z, dx, dy, dz) < 0.99999) return false;
            if(config.baseCollisionEnabled)foreach(var b in bases)
            {
                if(DistanceSquared(x,z,b.X,b.Z)<=b.AttackRadius*b.AttackRadius || DistanceSquared(endX,endZ,b.X,b.Z)<=b.AttackRadius*b.AttackRadius)continue;
                if(RayCylinder(x,y-b.Y,z,dx,dy,dz,b.X,b.Z,b.AttackRadius,config.coreHeight)<.99999)return false;
            }
            return true;
        }
        // Sphere-swept from the player's head to the intended camera position.
        // Used both for third-person clipping and the automatic head/first-person fallback.
        public float CameraClearDistance(float headX, float headY, float headZ, float cameraX, float cameraY, float cameraZ, float radius)
        {
            double dx = cameraX - headX, dy = cameraY - headY, dz = cameraZ - headZ;
            double length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (length < 0.000001) return 0;
            double nearest = length; dx /= length; dy /= length; dz /= length;
            foreach (var o in obstacles) nearest = Math.Min(nearest, MapGeometry.RayBox(o, headX, headY, headZ, dx, dy, dz, radius));
            if(config.baseCollisionEnabled)foreach(var b in bases)nearest=Math.Min(nearest,RayCylinder(headX,headY-b.Y+radius,headZ,dx,dy,dz,b.X,b.Z,b.AttackRadius+radius,config.coreHeight+2*radius));
            if (dy < 0) nearest = Math.Min(nearest, Math.Max(0, (radius - headY) / dy));
            return (float)Math.Max(0, nearest - 0.05);
        }
    }
}
