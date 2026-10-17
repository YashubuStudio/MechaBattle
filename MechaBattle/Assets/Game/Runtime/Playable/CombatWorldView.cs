using MechaBattle.Contracts;
using MechaBattle.Simulation;
using UnityEngine;
namespace MechaBattle.Playable
{
    // Presentation follows authoritative attack/guard/disruption state; it never creates hits.
    internal sealed class CombatWorldView
    {
        private readonly LocomotionWorldView locomotion = new LocomotionWorldView();
        private readonly Transform[] units;
        private readonly Transform[] shields = new Transform[100], swords = new Transform[100], charges = new Transform[100];
        private readonly GameObject[] previousModels = new GameObject[100];
        private readonly Transform[] arms = new Transform[100], guns = new Transform[100], muzzles = new Transform[100];
        private readonly Renderer[] shieldFaces = new Renderer[100];
        private readonly LineRenderer[] beams = new LineRenderer[100];
        private readonly Transform[] rockets;
        private readonly LineRenderer[] bulletStreaks;
        private readonly LineRenderer[] blasts;
        private readonly Transform root;
        private readonly Transform[] deployed;
        private readonly WeaponKind[] deployedKinds;private readonly int[] deployedTeams;
        private readonly MechaMeshLibrary library;
        private readonly LineRenderer[] podZones;
        private readonly Material[] armor, energy;
        private readonly Material flash,repair;
        private readonly CombatImpactView impacts;
        public CombatWorldView(Transform root, Transform[] units, BattleSimulation world, Material[] armor, Material[] energy, Material flash, Material repair,MechaMeshLibrary library)
        {
            this.library=library;this.repair=repair;impacts=new CombatImpactView(root,flash,repair);
            deployedKinds=new WeaponKind[world.DeployableCapacity];deployedTeams=new int[world.DeployableCapacity];
            this.root=root; deployed=new Transform[world.DeployableCapacity]; podZones=new LineRenderer[world.DeployableCapacity];
            this.units = units; this.armor = armor; this.energy = energy; this.flash = flash;
            for (int i = 0; i < units.Length; i++)
            {
                shields[i] = Shape("LeftArmShield", units[i], PrimitiveType.Cube, new Vector3(-0.35f, 1.45f, 0.55f), new Vector3(0.72f, 1.02f, 0.14f), armor[i / 50]);
                shieldFaces[i] = shields[i].GetComponent<Renderer>();
                Shape("ShieldSpine", shields[i], PrimitiveType.Cube, new Vector3(0, 0, 0.6f), new Vector3(0.13f, 0.85f, 0.2f), energy[i / 50]);
                swords[i] = new GameObject("MeleeSweepPivot").transform; swords[i].SetParent(units[i], false); swords[i].localPosition = new Vector3(0.62f, 1.4f, 0.12f);
                Shape("EnergyBlade", swords[i], PrimitiveType.Cube, new Vector3(0, 0, 0.95f), new Vector3(0.12f, 0.08f, 1.9f), energy[i / 50]);
                charges[i] = Shape("WeaponCharge", units[i], PrimitiveType.Sphere, new Vector3(0.65f, 1.27f, 1.1f), Vector3.one * 0.1f, energy[i / 50]);
                beams[i] = Line("ContinuousBeam", root, energy[i / 50], 0.24f, 2); beams[i].enabled = false;
            }
            rockets = new Transform[world.ProjectileCapacity]; bulletStreaks=new LineRenderer[world.ProjectileCapacity];
            for (int i = 0; i < rockets.Length; i++) { rockets[i] = Shape("VisibleProjectile", root, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.28f, energy[0]); bulletStreaks[i]=Line("BulletStreak",root,energy[0],0.10f,2); rockets[i].gameObject.SetActive(false); bulletStreaks[i].enabled=false; }
            blasts = new LineRenderer[world.ExplosionCapacity];
            for (int i = 0; i < blasts.Length; i++) { blasts[i] = Line("Explosion", root, energy[0], 0.14f, 32); blasts[i].loop = true; blasts[i].enabled = false; }
        }
        public void Sync(BattleSimulation world, GameObject[] models)
        {
            locomotion.Sync(world, models);impacts.Sync(world);
            for (int i = 0; i < units.Length; i++)
            {
                var u = world.GetUnit(i);
                if (models[i] != previousModels[i])
                { previousModels[i] = models[i]; arms[i] = models[i] == null ? null : models[i].transform.Find("RightArm");
                    guns[i] = arms[i] == null ? null : arms[i].Find("PrimaryWeapon"); muzzles[i] = guns[i] == null ? null : guns[i].Find("Muzzle"); }
                shields[i].gameObject.SetActive(u.IsAlive && !world.IsCarryingNuclear(i) && u.Shield > 0 && u.Disruption != DisruptionState.KnockedDown);
                shields[i].localPosition = u.Guarding ? new Vector3(-0.35f, 1.45f, 0.55f) : new Vector3(-0.72f, 1.32f, 0.04f);
                shields[i].localRotation = Quaternion.Euler(0, u.Guarding ? -8 : 80, u.Guarding ? -8 : 0);
                shieldFaces[i].sharedMaterial = u.LastShieldBlockTick >= 0 && (world.CurrentTick - u.LastShieldBlockTick) * world.FixedDeltaTime < 0.14f ? flash : armor[u.Team];
                bool swing = u.IsAlive && u.MeleePhase >= 0;
                swords[i].gameObject.SetActive(swing || u.MeleeChargeFraction > 0);
                float phase = u.MeleePhase;
                // A brief backward windup, followed by a broad lateral sweep and visible recovery.
                float contact=world.GetMeleeContactPhase(i);float recovery=phase<contact?0:Mathf.Clamp01((phase-contact)/Mathf.Max(0.001f,1-contact));
                float arc=phase<contact?Mathf.Lerp(-30,-105,Mathf.SmoothStep(0,1,Mathf.Max(0,phase)/Mathf.Max(0.001f,contact))):recovery<0.4f?Mathf.Lerp(-105,100,Mathf.SmoothStep(0,1,recovery/0.4f)):Mathf.Lerp(100,0,Mathf.SmoothStep(0,1,(recovery-0.4f)/0.6f));
                float blend=1-Mathf.Exp(-28*Mathf.Min(0.05f,Time.unscaledDeltaTime));
                if (u.ComboStep % 2 == 0) arc = -arc;
                float recoil=u.LastShotTick<0?0:Mathf.Clamp01(1-(world.CurrentTick-u.LastShotTick)*world.FixedDeltaTime/0.12f);
                if (arms[i] != null)
                {
                    arms[i].localRotation = Quaternion.Slerp(arms[i].localRotation,world.IsCarryingNuclear(i)?Quaternion.Euler(-65,0,18):swing || u.MeleeChargeFraction > 0 ? Quaternion.Euler(-65, arc, 0) : Quaternion.Euler(-recoil*5,0,0),blend);
                    swords[i].position = arms[i].TransformPoint(new Vector3(0.16f, -0.64f, 0.25f));
                    if (guns[i] != null) { guns[i].gameObject.SetActive(!world.IsCarryingNuclear(i) && !swing && u.MeleeChargeFraction <= 0); guns[i].localRotation = Quaternion.Euler(u.AimPitch-recoil*4, 0, 0); }
                }
                swords[i].localRotation = Quaternion.Slerp(swords[i].localRotation,Quaternion.Euler(u.MeleeStyle == MeleeStyle.Charge ? -15 : -5, arc, -15),blend);
                swords[i].localScale = Vector3.one * (u.MeleeChargeFraction > 0 ? Mathf.Lerp(0.3f, 1.25f, u.MeleeChargeFraction) : u.MeleeStyle == MeleeStyle.Dash ? 1.2f : 1);
                if (muzzles[i] != null) charges[i].position = muzzles[i].position;
                charges[i].gameObject.SetActive(u.IsAlive && (u.ChargeFraction > 0 || recoil > 0 && world.GetActiveWeapon(i).Kind==WeaponKind.Ranged)); charges[i].localScale = Vector3.one * (u.ChargeFraction>0?Mathf.Lerp(0.12f,0.6f,u.ChargeFraction):0.48f*recoil);
                bool repairing=world.GetActiveWeapon(i).Kind==WeaponKind.RepairGun;
                beams[i].sharedMaterial=repairing?repair:energy[u.Team];
                beams[i].widthMultiplier=repairing?0.10f:0.24f;
                beams[i].enabled = u.IsAlive && (u.BeamActive || repairing && u.LastShotTick>=0 && (world.CurrentTick-u.LastShotTick)*world.FixedDeltaTime<0.14f);
                if (beams[i].enabled) { beams[i].SetPosition(0, muzzles[i] != null ? muzzles[i].position : units[i].TransformPoint(new Vector3(0.65f, 1.27f, 0.95f))); beams[i].SetPosition(1, new Vector3(u.ShotEndX, u.ShotEndY, u.ShotEndZ)); }
            }
            for (int i = 0; i < rockets.Length; i++)
            {
                var p = world.GetProjectile(i); bool wasActive = rockets[i].gameObject.activeSelf; if (p.Active != wasActive) rockets[i].gameObject.SetActive(p.Active); bulletStreaks[i].enabled=p.Active;
                if (p.Active) { rockets[i].localScale=Vector3.one*(p.BlastRadius>0?0.38f:0.20f); rockets[i].position = new Vector3(p.X, p.Y, p.Z); rockets[i].GetComponent<Renderer>().sharedMaterial = energy[p.Team];
                    var streak=bulletStreaks[i];streak.sharedMaterial=energy[p.Team];streak.widthMultiplier=p.BlastRadius>0?0.18f:0.10f;streak.SetPosition(0,rockets[i].position);streak.SetPosition(1,rockets[i].position-new Vector3(p.Dx,p.Dy,p.Dz).normalized*(p.BlastRadius>0?1.8f:2.1f)); }
            }
            for(int i=0;i<deployed.Length;i++)
            {
                var d=world.GetDeployable(i);
                if(!d.Active||d.Kind==WeaponKind.StrategicNuclear){if(deployed[i]!=null)deployed[i].gameObject.SetActive(false);if(podZones[i]!=null)podZones[i].enabled=false;continue;}
                if(deployed[i]==null || deployedKinds[i]!=d.Kind || deployedTeams[i]!=d.Team)
                {if(deployed[i]!=null)Object.Destroy(deployed[i].gameObject);deployed[i]=library.CreateDeployable(d.Kind,d.Team,root).transform;deployedKinds[i]=d.Kind;deployedTeams[i]=d.Team;}
                if(podZones[i]==null){podZones[i]=Line("PodRadius",root,energy[d.Team],0.08f,32);podZones[i].loop=true;}
                podZones[i].sharedMaterial=energy[d.Team];deployed[i].gameObject.SetActive(true);
                var bounds=deployed[i].GetComponent<MeshFilter>().sharedMesh.bounds;
                var scale=new Vector3(d.HitRadius*2/Mathf.Max(0.01f,bounds.size.x),d.HitHeight/Mathf.Max(0.01f,bounds.size.y),d.HitRadius*2/Mathf.Max(0.01f,bounds.size.z));
                deployed[i].localScale=scale;deployed[i].position=new Vector3(d.X-bounds.center.x*scale.x,d.Y-bounds.min.y*scale.y,d.Z-bounds.center.z*scale.z);
                podZones[i].enabled=d.Kind==WeaponKind.RepairPod;
                if(podZones[i].enabled)for(int j=0;j<32;j++){float a=j*Mathf.PI/16;podZones[i].SetPosition(j,new Vector3(d.X+Mathf.Cos(a)*d.Radius,d.Y+0.03f,d.Z+Mathf.Sin(a)*d.Radius));}
            }
            for (int i = 0; i < blasts.Length; i++)
            {
                var e = world.GetExplosion(i); float age = (world.CurrentTick - e.Tick) * world.FixedDeltaTime;
                bool visible = e.Valid && age >= 0 && age < 0.3f; blasts[i].enabled = visible; if (!visible) continue;
                blasts[i].sharedMaterial = energy[e.Team]; float radius = e.Radius * Mathf.Clamp01(age / 0.18f);
                for (int j = 0; j < 32; j++) { float a = j * Mathf.PI / 16; blasts[i].SetPosition(j, new Vector3(e.X + Mathf.Cos(a) * radius, Mathf.Max(0.08f, e.Y), e.Z + Mathf.Sin(a) * radius)); }
            }
        }
        private static Transform Shape(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material; Object.Destroy(go.GetComponent<Collider>()); return go.transform;
        }
        private static LineRenderer Line(string name, Transform parent, Material material, float width, int count)
        {
            var go = new GameObject(name, typeof(LineRenderer)); go.transform.SetParent(parent, false); var line = go.GetComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = true; line.positionCount = count; line.widthMultiplier = width;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false; return line;
        }
    }
}
