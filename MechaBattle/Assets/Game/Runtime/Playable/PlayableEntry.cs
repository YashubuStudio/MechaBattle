using System;
using System.IO;
using System.Text;
using MechaBattle.AI.Bots;
using MechaBattle.Battlefield;
using MechaBattle.Construction;
using MechaBattle.Contracts;
using MechaBattle.Contracts.Construction;
using MechaBattle.Observation;
using MechaBattle.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MechaBattle.Playable
{
    public sealed partial class PlayableEntry : MonoBehaviour, ISelfObservationSource, INearbyThreatSensor, ICapturePointStatusSource, IMainBaseStatusSource
    {
        [SerializeField] private TextAsset mapAsset, configurationAsset, modularKitAsset;
        [SerializeField] private Shader litShader, unlitShader, textShader;
        [SerializeField] private bool persistProfile = true;
        private PlayableConfiguration configuration;
        private MapDefinition map;
        private ConstructionCatalog catalog;
        private SavedDeck profile;
        private PlayableUI ui;
        private TMP_FontAsset font;
        private MechaMeshLibrary library;
        private PlayableWorldView view;
        private PlayableMinimap minimap;
        private DeploymentMapView prepDeploymentMap, respawnDeploymentMap;
        private BattleFeedbackView combatFeedback;
        private MatchEventFeedView matchEventFeed;
        private AudioClip confirmSound,killSound,repairSound;private float nextConfirmSound,nextRepairSound;
        // User request 2026-10-16: show which direction incoming damage came from.
        private long damageDirectionSequence;private float damageDirectionWorldYaw,damageDirectionUntil;
        private const float DamageDirectionSeconds=1.2f;
        private HangarPreview hangarPreview;
        private GameObject battleSun;
        private Camera followCamera;
        private IUnitCommandSource[] botByDeck;
        private CombatInputAdapter botTriggers;
        private BotTraversalAdapter botTraversal;
        private BotPerception botPerception;
        private BotPopulation botPopulation;private BotBattlefieldPlanner botPlanner;private int[,] botPresets;private int botMatchNumber;
        public BotRole GetBotRole(int slot)=>botPopulation==null?BotRole.Frontline:botPopulation.RoleOf(slot);
        private int lockedDeployableIndex=-1,lockedDeployableId;
        private int queuedWeaponSwitch, requestedWeaponIndex = -1, lockedTargetSlot = -1, lockedTargetUnitId;
        private bool jumpQueued, firstPerson;
        private bool snapLockPending;
        private int previewWeapon;
        private bool shotQueued, meleeQueued, reloadQueued, humanTriggerDown, meleeArmed;
        private MeleeStyle meleeStyle;
        private ResolvedBuild[][] activeBuilds;
        private AudioSource audioSource;
        private AudioClip shotSound, hitSound, lockSound;
        private float nextLockSound; private long lastLockHitSound = -1; private bool wasIncomingLocked;
        private int aimTargetSlot = -1, aimTargetCore = -1;
        private int selectedDeck, respawnDeck, spawnPoint = -1, selectedMainBase = -1, initialMainBase = -1, lastSelfId;
        private uint inputSequence;
        private long lastSelfShot = -1;
        private float yaw = 90, pitch = 0, aimYaw, nextUiTime, previousVital, wheelCooldown;
        private double accumulatedSeconds;
        private bool inputArmed, lastAlive;
        private string notice = "構築した4機が順番に出撃できます。";
        public BattleSimulation Simulation { get; private set; }
        public int HumanSlotId { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsFirstPerson => firstPerson;
        public int LockedTargetSlot => lockedTargetSlot;
        public int LockedDeployableIndex=>lockedDeployableIndex;
        public bool IsInPreparation => Simulation == null;
        public int SelectedDeckSlot => selectedDeck;
        public int Team => profile == null ? 0 : profile.team;
        public SavedDeck CopyProfile()
        {
            var result = new SavedDeck { team = profile.team, deck = new BuildDefinition[profile.deck.Length] };
            for (int i = 0; i < result.deck.Length; i++) result.deck[i] = DeckConstruction.Copy(profile.deck[i]);
            return result;
        }
        public void Configure(TextAsset map, TextAsset configuration, TextAsset kit, Shader lit, Shader unlit, Shader text, bool persistence = true)
        {
            mapAsset = map; configurationAsset = configuration; modularKitAsset = kit;
            litShader = lit; unlitShader = unlit; textShader = text; persistProfile = persistence;
        }
        private void Awake()
        {
            if (mapAsset == null || configurationAsset == null || modularKitAsset == null || litShader == null || unlitShader == null || textShader == null)
                throw new InvalidOperationException("Generate the playable scene with MechaBattle > Playable > Generate and Open Playable Scene.");
            map = JsonUtility.FromJson<MapDefinition>(mapAsset.text); MapValidator.ThrowIfInvalid(map);
            LoadMapChoices();
            configuration = JsonUtility.FromJson<PlayableConfiguration>(configurationAsset.text);
            catalog = configuration.constructionPrototype.catalog; configuration.playablePrototype.Apply(configuration.p0Simulation);
            profile = DeckConstruction.CreateDefault(catalog, configuration.playablePrototype.starterDeckSlots);
            LoadProfile();LoadLockSettings();LoadPlayerPreferences();
            library = new MechaMeshLibrary(modularKitAsset.text, litShader);
            font = CreateFont();
            followCamera = new GameObject("PlayerCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            followCamera.transform.SetParent(transform, false);
            followCamera.tag = "MainCamera"; followCamera.backgroundColor = new Color(0.04f, 0.07f, 0.10f); followCamera.clearFlags = CameraClearFlags.SolidColor;
            followCamera.nearClipPlane = 0.1f; followCamera.farClipPlane = 650; followCamera.fieldOfView = 55;
            audioSource = followCamera.gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false; audioSource.spatialBlend = 0;
            shotSound = Sound("PrimaryShot", 180, 0.11f); hitSound = Sound("Damage", 70, 0.2f); lockSound = Sound("IncomingLock", 880, 0.12f);
            battleSun = new GameObject("Sun", typeof(Light)); battleSun.transform.rotation = Quaternion.Euler(48, -32, 0);
            battleSun.transform.SetParent(transform, true);
            battleSun.GetComponent<Light>().type = LightType.Directional; battleSun.GetComponent<Light>().intensity = 1.6f;
            battleSun.GetComponent<Light>().cullingMask = HangarPreview.BattleMask; battleSun.SetActive(false);
            RenderSettings.ambientLight = new Color(0.42f, 0.47f, 0.52f); RenderSettings.fog = true; RenderSettings.fogColor = followCamera.backgroundColor;
            RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = 0.002f;
            ui = new PlayableUI(transform, font, SelectTeam, SelectDeckSlot, ApplyPreset, OpenPartPicker, OpenFramePicker, OpenPaintPicker, OpenMapPicker, SaveProfile, BeginMatch,
                ResumeMatch, ReturnToPreparation, SelectRespawnDeck, Deploy, ReturnToGarage, ShowMainMenuScreen,ToggleLockSelectionMode,OpenSettings, value => ApplyPlayerPreferences(value), CancelSettings);
            hangarPreview = new HangarPreview(transform, library); ui.SetPreviewTexture(hangarPreview.Texture);
            minimap = new PlayableMinimap(ui.MinimapPanel, map);
            prepDeploymentMap = new DeploymentMapView(ui.PrepDeploymentPanel, map, font, SelectDeploymentPoint);
            respawnDeploymentMap = new DeploymentMapView(ui.RespawnDeploymentPanel, map, font, SelectDeploymentPoint);
            combatFeedback=new BattleFeedbackView(ui.Hud.transform,font);
            matchEventFeed=new MatchEventFeedView();
            confirmSound=Sound("ConfirmedHit",1300,0.07f);killSound=Sound("ConfirmedKill",1700,0.32f);repairSound=Sound("RepairConfirmed",620,0.08f);
            RefreshMapLabel();RefreshLockSettingLabels();ShowMainMenuScreen();
        }
        private void ShowMainMenuScreen() { TeardownBattle(); SetFrontendCamera(false); ui.ShowMainMenu(); }
        private void SetFrontendCamera(bool showPreview)
        {
            IsPaused = false; firstPerson = false; SetCursor(false); ResetInput();
            followCamera.rect = new Rect(0, 0, 1, 1); followCamera.fieldOfView = 55; followCamera.nearClipPlane = 0.1f;
            followCamera.cullingMask = 0; followCamera.clearFlags = CameraClearFlags.SolidColor;
            battleSun.SetActive(false); hangarPreview.SetVisible(showPreview);
        }
        private TMP_FontAsset CreateFont()
        {
            // Windows-only prototype: use installed CJK faces without copying system font files.
            foreach (string family in new[] { "Yu Gothic", "Yu Gothic UI", "Meiryo", "Noto Sans CJK JP", "MS Gothic" })
            {
                var candidate = TMP_FontAsset.CreateFontAsset(family, "Regular", 48);
                if (candidate == null) continue;
                candidate.name = "MechaBattle_Japanese_OS"; candidate.material.shader = textShader;
                candidate.isMultiAtlasTexturesEnabled = true;
                candidate.TryAddCharacters("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz+-/:.()% HPCT青橙軍出撃機体構築保存結果勝利敗北", out string missing);
                return candidate;
            }
            throw new InvalidOperationException("A Japanese Windows font (Yu Gothic or Meiryo) is required for this prototype UI.");
        }
        private string SavePath => Path.Combine(Application.persistentDataPath, "mechabattle-deck-v1.json");
        private void LoadProfile()
        {
            if (!persistProfile || !File.Exists(SavePath)) return;
            try { var saved = JsonUtility.FromJson<SavedDeck>(File.ReadAllText(SavePath)); DeckConstruction.Validate(catalog, saved, profile.deck.Length); profile = saved; notice = "保存した機体デッキを読み込みました。"; }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException)
            {
                notice = "保存データを読めなかったため初期デッキを表示しています。元のファイルは保存操作まで保持します。";
                Debug.LogWarning(notice + " " + e.Message);
            }
        }
        public void SaveProfile()
        {
            try
            {
                DeckConstruction.Validate(catalog, profile, configuration.playablePrototype.starterDeckSlots);
                if (persistProfile)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(SavePath)); string temporary = SavePath + ".tmp";
                    File.WriteAllText(temporary, JsonUtility.ToJson(profile, true), new UTF8Encoding(false));
                    if (File.Exists(SavePath)) File.Replace(temporary, SavePath, SavePath + ".bak"); else File.Move(temporary, SavePath);
                }
                notice = "デッキを保存しました。";
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException)
            { notice = "保存できませんでした: " + e.Message; }
            if (ui != null) ui.SetNotice(notice);
        }
        public void SelectTeam(int team)
        {
            if (!IsInPreparation || team < 0 || team > 1) return;
            if (profile.team != team) initialMainBase = -1;
            profile.team = team; RefreshPreparation(); RefreshBattlePrep();
        }
        public void SelectDeckSlot(int index)
        {
            if (!IsInPreparation || index < 0 || index >= profile.deck.Length) return;
            selectedDeck = index; RefreshPreparation(); RefreshBattlePrep();
        }
        public void ApplyPreset(int index)
        {
            if (!IsInPreparation || index < 0 || index >= catalog.presets.Length) return;
            profile.deck[selectedDeck] = DeckConstruction.Copy(catalog.presets[index], "player-" + (selectedDeck + 1)); RefreshPreparation();
        }
        public void OpenFramePicker()
        {
            if (!IsInPreparation) return;
            var labels = new string[catalog.frames.Length]; var available = new bool[labels.Length]; int selected = -1;
            for (int i = 0; i < catalog.frames.Length; i++)
            {
                var frame = catalog.frames[i]; if (frame.id == profile.deck[selectedDeck].frameId) selected = i;
                available[i] = FrameConstruction.TryChangeFrame(catalog, profile.deck[selectedDeck], frame.id, out var candidate, out var replacements, out var errors);
                labels[i] = FrameTitle(frame.id);
                if (available[i])
                {
                    var data = ConstructionEvaluator.Evaluate(catalog, candidate);
                    labels[i] += "\n機体HP " + data.MaxHealth.ToString("0") + " / 盾 " + data.MaxShield.ToString("0")
                        + "\n移動 " + data.MoveSpeed.ToString("0.0") + " / DP耐性 " + data.CombatLoadout.DownPointCapacity.ToString("0")
                        + "\n重量 " + data.Weight.ToString("0") + "/" + frame.maxWeight.ToString("0") + " / 出力 " + data.Energy.ToString("0") + "/" + frame.maxEnergy.ToString("0")
                        + "\nコスト " + data.Cost + " / CT " + data.CooldownSeconds.ToString("0.0") + "秒"
                        + "\n" + FrameReplacementSummary(replacements);
                }
                else labels[i] += "\n選択不可: " + string.Join(" / ", errors);
            }
            ui.OpenPartPicker("フレームを選択 / 現在の腕・装備を含む性能", labels, selected,
                index => SelectFrame(catalog.frames[index].id),
                "互換外の武器は、各カードに表示した武器へ置き換えます。対応武器・腕・塗装は維持。\nフレームごとに、装着可能な武器だけを表示します。", available, true);
        }
        public void SelectFrame(string id)
        {
            if (!IsInPreparation) return;
            if (!FrameConstruction.TryChangeFrame(catalog, profile.deck[selectedDeck], id, out var candidate, out var replacements, out var errors))
            { notice = "フレームを変更できません: " + string.Join(" / ", errors); ui.SetNotice(notice); return; }
            profile.deck[selectedDeck] = candidate;
            notice = FrameTitle(id) + "へ変更しました。" + FrameReplacementSummary(replacements);
            RefreshPreparation(); RefreshBattlePrep();
        }
        private static string FrameReplacementSummary(EquipmentSelection[] replacements)
        {
            if (replacements.Length == 0) return "装備の変更なし";
            var changes = new string[replacements.Length];
            for (int i = 0; i < changes.Length; i++) changes[i] = PartName(replacements[i].slot) + "→" + PartTitle(replacements[i].partId);
            return "武器変更: " + string.Join(" / ", changes);
        }
        private static string FrameTitle(string id)
        { return id == "frame.standard.v1" ? "標準フレーム" : id == "frame.heavy.v1" ? "重量フレーム" : id == "frame.light.v1" ? "軽量フレーム" : id == "frame.support.v1" ? "支援フレーム" : id ?? "未選択"; }
        public void OpenPartPicker(int slotIndex)
        {
            if(!IsInPreparation || slotIndex<0 || slotIndex>4)return;
            var slot=(PartSlot)slotIndex;var options=new System.Collections.Generic.List<PartDefinition>();
            foreach(var part in catalog.parts)if(FrameConstruction.CanMount(FrameConstruction.FindFrame(catalog,profile.deck[selectedDeck].frameId),part,slot))options.Add(part);
            string current="";foreach(var e in profile.deck[selectedDeck].equipment)if(e.slot==slot)current=e.partId;
            var labels=new System.Collections.Generic.List<string>();int selected=-1;
            for(int i=0;i<options.Count;i++)
            {
                var part=options[i]; if(part.id==current)selected=i;
                string detail=slot>=PartSlot.PrimaryWeapon?PartSummary(part.weapon):"左右一組 HP "+(2*part.healthDelta).ToString("+0;-0;0")+" / 盾 "+(2*part.shieldDelta).ToString("+0;-0;0")+"\n移動 "+(2*part.moveSpeedDelta).ToString("+0.0;-0.0;0")+" / DP耐性 "+(2*part.downPointCapacityDelta).ToString("+0;-0;0");
                labels.Add(PartTitle(part.id)+"\n"+detail+" / 重量 "+(slot<PartSlot.PrimaryWeapon?2*part.weight:part.weight).ToString("0"));
            }
            if(slot>PartSlot.PrimaryWeapon){if(current=="")selected=labels.Count;labels.Add("なし\nこの装備枠を空ける");}
            ui.OpenPartPicker(PartName(slot)+"を選択",labels.ToArray(),selected,index=>SelectPart(slotIndex,index<options.Count?options[index].id:null),detailed:slot<PartSlot.PrimaryWeapon);
        }
        private static string PartSummary(WeaponDefinition w)
        {
            if(w.kind==WeaponKind.RepairPod)return "盾 "+w.podShieldPerSecond.ToString("0")+"/秒・弾薬 "+w.podAmmoPerSecond.ToString("0")+"/秒 / 補給不可";
            if(w.kind==WeaponKind.RepairGun)return "HP回復FA "+w.damage.ToString("0")+"/発 / 味方へ照準 / "+w.range.ToString("0")+"m";
            if(w.kind==WeaponKind.Radar)return "索敵 "+w.utilityRadius.ToString("0")+"m / "+w.utilityDuration.ToString("0")+"秒 / 補給不可";
            if(w.kind==WeaponKind.Mine)return "爆風 "+w.splashDamage.ToString("0")+" / 半径 "+w.blastRadius.ToString("0")+"m / DP "+w.splashDownPoints.ToString("0");
            if(w.kind==WeaponKind.Grenade)return "威力 "+w.damage.ToString("0")+" / 爆風 "+w.blastRadius.ToString("0")+"m / 起爆 "+w.fuseSeconds.ToString("0.0")+"秒";
            return "威力 "+w.damage.ToString("0")+" / DP "+w.downPoints.ToString("0")+" / 射程 "+w.range.ToString("0.#")+"m";
        }
        public void SelectPart(int slotIndex,string id)
        {
            if(!IsInPreparation || slotIndex<0 || slotIndex>4)return;var slot=(PartSlot)slotIndex;
            if(string.IsNullOrEmpty(id) && slot<=PartSlot.PrimaryWeapon)return;
            bool valid=string.IsNullOrEmpty(id);foreach(var p in catalog.parts)if(p.id==id&&FrameConstruction.CanMount(FrameConstruction.FindFrame(catalog,profile.deck[selectedDeck].frameId),p,slot))valid=true;
            if(!valid)return;var build=profile.deck[selectedDeck];var entries=new System.Collections.Generic.List<EquipmentSelection>();
            foreach(var e in build.equipment)if(e.slot!=slot)entries.Add(e);
            if(!string.IsNullOrEmpty(id))entries.Add(new EquipmentSelection{slot=slot,partId=id});build.equipment=entries.ToArray();
            if(slot==PartSlot.LeftArm)SyncMatchingRightArm(build,id);
            CompactEquipment(build);
            if(slot>=PartSlot.PrimaryWeapon){previewWeapon=0;foreach(var e in build.equipment)if(e.slot>=PartSlot.PrimaryWeapon && e.slot<slot)previewWeapon++;}
            RefreshPreparation();
        }
        public void CyclePart(int slotIndex)
        {
            if (!IsInPreparation || slotIndex < 0 || slotIndex > 4) return;
            var slot = (PartSlot)slotIndex;
            var build = profile.deck[selectedDeck]; EquipmentSelection choice = null;
            foreach (var e in build.equipment) if (e.slot == slot) choice = e;
            if (choice == null)
            {
                var entries = new System.Collections.Generic.List<EquipmentSelection>(build.equipment); choice = new EquipmentSelection { slot = slot, partId = "" }; entries.Add(choice); build.equipment = entries.ToArray();
            }
            var available = new System.Collections.Generic.List<PartDefinition>();
            foreach (var part in catalog.parts) if (FrameConstruction.CanMount(FrameConstruction.FindFrame(catalog,build.frameId),part,slot)) available.Add(part);
            int current = available.FindIndex(p => p.id == choice.partId);
            if (slot > PartSlot.PrimaryWeapon && current == available.Count - 1)
            { var entries = new System.Collections.Generic.List<EquipmentSelection>(build.equipment); entries.Remove(choice); build.equipment = entries.ToArray(); }
            else if (available.Count > 0) choice.partId = available[(current + 1) % available.Count].id;
            if (slot == PartSlot.LeftArm) SyncMatchingRightArm(build, choice.partId);
            CompactEquipment(build);
            if (slot >= PartSlot.PrimaryWeapon && ConstructionEvaluator.TryEvaluate(catalog, build, out var candidate, out var ignored)) previewWeapon = Math.Min(slotIndex - 2, candidate.CombatLoadout.WeaponCount - 1);
            RefreshPreparation();
        }
        // User request 2026-10-16: left/right arms must be the same family, so the Garage
        // exposes only one arm control (LeftArm) and this keeps RightArm in sync with it,
        // instead of letting the two be picked independently.
        private void SyncMatchingRightArm(BuildDefinition build, string leftPartId)
        {
            PartDefinition chosenLeft = null; foreach (var p in catalog.parts) if (p.id == leftPartId) chosenLeft = p;
            if (chosenLeft == null || string.IsNullOrEmpty(chosenLeft.armFamily)) return;
            PartDefinition matchingRight = null; foreach (var p in catalog.parts) if (p.slot == PartSlot.RightArm && p.armFamily == chosenLeft.armFamily) { matchingRight = p; break; }
            if (matchingRight == null) return;
            var entries = new System.Collections.Generic.List<EquipmentSelection>(); foreach (var e in build.equipment) if (e.slot != PartSlot.RightArm) entries.Add(e);
            entries.Add(new EquipmentSelection { slot = PartSlot.RightArm, partId = matchingRight.id }); build.equipment = entries.ToArray();
        }
        private static void CompactEquipment(BuildDefinition build)
        {
            var entries=new System.Collections.Generic.List<EquipmentSelection>();int weaponSlot=2;
            for(int slot=0;slot<5;slot++)foreach(var e in build.equipment)if((int)e.slot==slot)
                entries.Add(new EquipmentSelection{slot=slot<2?e.slot:(PartSlot)weaponSlot++,partId=e.partId});
            build.equipment=entries.ToArray();
        }
        private void ShowPreparation()
        {
            SetFrontendCamera(true); ui.ShowMenu();
            RefreshPreparation();
        }
        private void RefreshPreparation()
        {
            ui.HighlightPreparation(profile.team, selectedDeck);
            for (int i = 0; i < profile.deck.Length; i++)
            {
                bool valid = ConstructionEvaluator.TryEvaluate(catalog, profile.deck[i], out var resolved, out var errors);
                ui.DeckLabels[i].text = "機体 " + (i + 1) + (i == selectedDeck ? " [編集中]" : "") + "\n" + (valid ? "コスト " + resolved.Cost : "構成エラー");
            }
            var build = profile.deck[selectedDeck];
            for (int i = 2; i < 5; i++) ui.PartLabels[i].text = "装備 " + (i - 1) + " : なし  ›";
            foreach (var e in build.equipment) ui.PartLabels[(int)e.slot].text = PartName(e.slot) + " : " + PartTitle(e.partId) + "  ›";
            if (ConstructionEvaluator.TryEvaluate(catalog, build, out var data, out var issues))
            {
                previewWeapon = Mathf.Clamp(previewWeapon, 0, data.CombatLoadout.WeaponCount - 1); var selection = data.CombatLoadout.GetWeapon(previewWeapon);
                FrameDefinition frame = null; foreach (var f in catalog.frames) if (f.id == data.FrameId) frame = f;
                ui.Stats.text = "HP " + data.MaxHealth.ToString("0") + "   シールド " + data.MaxShield.ToString("0") + "   速度 " + data.MoveSpeed.ToString("0.0")
                    + "\n重量 " + data.Weight.ToString("0") + "/" + frame.maxWeight.ToString("0") + "   出力 " + data.Energy.ToString("0") + "/" + frame.maxEnergy.ToString("0")
                    + "\n" + ModeName(selection) + "  威力 " + selection.FireDamage.ToString("0") + " / DP " + selection.FireDownPoints.ToString("0") + " / 射程 " + selection.FireRange.ToString("0") + "m"
                    + "\n黄 " + SimulationRange(selection).ToString("0") + "m / 赤 " + selection.RedLockRange.ToString("0") + "m   弾速 " + (selection.ProjectileSpeed > 0 ? selection.ProjectileSpeed.ToString("0") + "m/s" : "即時 / 設置")
                    + "\n弾倉 " + selection.MagazineCapacity + " / 総弾数 " + selection.MaxAmmo + "  装填 " + selection.ReloadSeconds.ToString("0.0") + "秒  DP耐性 " + data.CombatLoadout.DownPointCapacity.ToString("0")
                    + "\n自動コスト " + data.Cost + "    再出撃CT " + data.CooldownSeconds.ToString("0.0") + "秒";
                previewWeapon = Mathf.Clamp(previewWeapon, 0, data.CombatLoadout.WeaponCount - 1);
                if (ui.Menu.activeSelf) hangarPreview.SetBuild(data, profile.team, previewWeapon);
            }
            else { ui.Stats.text = "この構成では出撃できません。\n" + string.Join("\n", issues); if (ui.Menu.activeSelf) hangarPreview.Clear(); }
            ui.PaintLabel.text = "塗装 : " + PaintTitle(build.appearance == null ? "Neutral" : build.appearance.paintId) + "  ›";
            ui.FrameLabel.text = "フレーム : " + FrameTitle(build.frameId) + "  ›";
            ui.PreviewTitle.text = (profile.team == 0 ? "青軍" : "橙軍") + "  /  機体 " + (selectedDeck + 1) + "  /  " + FrameTitle(build.frameId);
            ui.SetNotice(notice);
        }
        // User request 2026-10-16: left/right arms are now a single unified choice (see
        // SyncMatchingRightArm), so both slots share the same display label.
        private static string PartName(PartSlot slot) { return slot == PartSlot.LeftArm || slot == PartSlot.RightArm ? "腕" : "装備 " + ((int)slot - 1); }
        private static string PartTitle(string id)
        {
            if (id == "weapon.carbine") return "FAカービン";
            if (id == "weapon.assaultrifle") return "FAアサルトライフル";
            if (id == "weapon.longrifle") return "SAライフル";
            if (id == "weapon.cannon") return "単発キャノン";
            if (id == "weapon.charge") return "チャージライフル";
            if (id == "weapon.beam") return "継続ビーム";
            if (id == "weapon.bazooka") return "バズーカ";
            if (id == "weapon.melee") return "格闘ブレード";
            if (id == "weapon.repairgun") return "HP回復FA";
            if (id == "weapon.radar") return "設置レーダー";
            if (id == "weapon.repairpod") return "盾・弾薬ポッド";
            if (id == "weapon.grenade") return "グレネード";
            if (id == "weapon.mine") return "地雷";
            if (id == "arm.mobile.left" || id == "arm.mobile.right") return "軽量アーム";
            if (id == "arm.standard.left" || id == "arm.standard.right") return "標準アーム";
            if (id == "arm.reinforced.left" || id == "arm.reinforced.right") return "補強アーム";
            return string.IsNullOrEmpty(id) ? "なし" : id;
        }
        private static float SimulationRange(UnitLoadout w)=>w.YellowLockRange>0?w.YellowLockRange:w.FireRange;
        private static UnitLoadout Combat(ResolvedBuild b)
        { return b.CombatLoadout; }
        public void BeginMatch()
        {
            ResolvedBuild[] player;
            try { player = DeckConstruction.Validate(catalog, profile, configuration.playablePrototype.starterDeckSlots); }
            catch (ArgumentException e) { notice = "出撃できません: " + e.Message; ui.SetNotice(notice); return; }
            SaveProfile();
            if (view != null) { view.Dispose(); view = null; }
            hangarPreview.SetVisible(false); battleSun.SetActive(true); followCamera.cullingMask = HangarPreview.BattleMask;
            int count = player.Length; var bot = new ResolvedBuild[catalog.presets.Length];
            for (int i = 0; i < bot.Length; i++) bot[i] = ConstructionEvaluator.Evaluate(catalog, catalog.presets[i % catalog.presets.Length]);
            HumanSlotId = profile.team * MatchLimits.ControllerSlotsPerTeam;
            var botRules=(configuration.p0Simulation.botBehavior??new BotBehaviorRules()).Copy();botRules.radarFraction=playerPreferences.botRadarFraction;botRules.interceptorFraction=playerPreferences.botInterceptorFraction;botRules.frontlineMapReaderFraction=playerPreferences.botMapReaderFraction;
            botPopulation=new BotPopulation(botRules,unchecked(botRules.seed+ ++botMatchNumber*7919),HumanSlotId);
            var botWeapons=new UnitLoadout[bot.Length];for(int i=0;i<bot.Length;i++)botWeapons[i]=bot[i].CombatLoadout;botPresets=new int[100,count];
            var loadouts = new UnitLoadout[MatchLimits.TotalControllerSlots][];
            activeBuilds = new ResolvedBuild[MatchLimits.TotalControllerSlots][];
            for (int slot = 0; slot < loadouts.Length; slot++)
            {
                activeBuilds[slot] = new ResolvedBuild[count]; loadouts[slot] = new UnitLoadout[count];
                for (int d = 0; d < count; d++) { int preset=BotDeckPolicy.SelectPreset(botWeapons,slot,d,botPopulation.RoleOf(slot));botPresets[slot,d]=preset;activeBuilds[slot][d] = slot == HumanSlotId ? player[d] : bot[preset]; loadouts[slot][d] = Combat(activeBuilds[slot][d]); }
            }
            combatFeedback.Reset();matchEventFeed.Reset();nextConfirmSound=nextRepairSound=0;
            damageDirectionSequence=0;damageDirectionUntil=0;
            Simulation = new BattleSimulation(map, configuration.p0Simulation, HumanSlotId, loadouts, selectedDeck, EnsureInitialMainBase());
            botPerception = new BotPerception(Simulation, configuration.p0Simulation,botPopulation);
            botPlanner=new BotBattlefieldPlanner(Simulation,map,botRules,botPopulation);
            Simulation.SetAutomaticRespawn(HumanSlotId, false);
            botTraversal = new BotTraversalAdapter(map, configuration.p0Simulation, this);
            ClearTargetLock(); firstPerson = false;
            botTriggers = new CombatInputAdapter(); botByDeck = new IUnitCommandSource[bot.Length];
            for (int d = 0; d < bot.Length; d++) botByDeck[d] = new ForwardMarchBotCommandSource(map, this, this, bot[d].FireRange, bot[d].MaxAmmo, configuration.p0Simulation.homeSupplyRadius, this);
            view = new PlayableWorldView(map, Simulation, activeBuilds, library, followCamera, font, litShader, unlitShader, configuration.p0Simulation.baseCounterFireRadius, MapTheme,botPlanner.Squads);
            yaw = profile.team == 0 ? 90 : -90; aimYaw = yaw; pitch = 0; followCamera.rect = new Rect(0, 0, 1, 1); followCamera.fieldOfView = 65;
            accumulatedSeconds = 0; inputSequence = 0; inputArmed = false; ResetInput(); IsPaused = false; lastAlive = true;
            respawnDeck = selectedDeck; spawnPoint = -1; selectedMainBase = initialMainBase;
            lastSelfId = Simulation.GetUnit(HumanSlotId).UnitId; previousVital = player[selectedDeck].MaxHealth + player[selectedDeck].MaxShield; lastSelfShot = -1; wasIncomingLocked=false; lastLockHitSound=-1; nextLockSound=0;
            InitializeStrategyViews();
            SetCursor(true); ui.ShowBattle(false, false); RefreshBattle(); UpdateCamera(true);
        }
        private void TeardownBattle()
        {
            DisposeStrategyViews();
            if (view != null) { view.Dispose(); view = null; }
            Simulation = null; activeBuilds = null; botByDeck = null; botTraversal = null; botTriggers = null; botPerception = null;botPopulation=null;botPlanner=null;botPresets=null;
            accumulatedSeconds = 0; IsPaused = false; ClearTargetLock(); ResetInput(); audioSource.Stop();
        }
        public void ReturnToPreparation() { TeardownBattle(); EnterBattlePrep(); }
        public void ReturnToGarage() { TeardownBattle(); ShowPreparation(); }
        private void EnterBattlePrep()
        {
            SetFrontendCamera(true); ui.ShowBattlePrep();
            RefreshBattlePrep();
        }
        private void RefreshBattlePrep()
        {
            ui.HighlightBattlePrep(profile.team, selectedDeck);
            EnsureInitialMainBase();
            prepDeploymentMap.Refresh(null, profile.team, -1, initialMainBase);
            ui.PrepSpawnLabel.text = "出撃先 : " + map.MainBaseName(initialMainBase) + " 本拠点";
            for (int i = 0; i < profile.deck.Length; i++)
            {
                bool valid = ConstructionEvaluator.TryEvaluate(catalog, profile.deck[i], out var resolved, out var errors);
                string kind = valid && MatchesAnyPreset(profile.deck[i]) ? "汎用機" : "専用機";
                ui.BattleDeckLabels[i].text = "機体 " + (i + 1) + (i == selectedDeck ? " [選択中]" : "") + "\n" + (valid ? kind + " / コスト " + resolved.Cost : "構成エラー");
            }
            RefreshBattlePreview(); ui.SetNotice(notice);
        }
        private void RefreshBattlePreview()
        {
            if (!ui.BattlePrep.activeSelf) return;
            if (ConstructionEvaluator.TryEvaluate(catalog, profile.deck[selectedDeck], out var data, out var issues))
                hangarPreview.SetBuild(data, profile.team, 0);
            else hangarPreview.Clear();
        }
        private bool MatchesAnyPreset(BuildDefinition build)
        {
            foreach (var preset in catalog.presets)
            {
                if (preset.frameId != build.frameId || preset.equipment.Length != build.equipment.Length) continue;
                bool same = true;
                foreach (var choice in build.equipment)
                {
                    bool found = false;
                    foreach (var reference in preset.equipment) if (reference.slot == choice.slot && reference.partId == choice.partId) { found = true; break; }
                    if (!found) { same = false; break; }
                }
                if (same) return true;
            }
            return false;
        }
        public void PauseMatch()
        {
            if (Simulation == null || Simulation.Phase != MatchPhase.Running) return;
            CloseTacticalMap();
            Simulation.CancelHeldActions(HumanSlotId);
            IsPaused = true; inputArmed = false; ResetInput(); SetCursor(false); ui.ShowBattle(!Simulation.GetUnit(HumanSlotId).IsAlive, true);
        }
        public void ResumeMatch()
        {
            if (Simulation == null || Simulation.Phase != MatchPhase.Running) return;
            IsPaused = false; inputArmed = false; ResetInput(); bool alive = Simulation.GetUnit(HumanSlotId).IsAlive;
            SetCursor(alive); ui.ShowBattle(!alive, false);
        }
        private void SelectRespawnDeck(int index) { if (index >= 0 && index < profile.deck.Length) { respawnDeck = index; RefreshBattle(); } }
        private int EnsureInitialMainBase()
        {
            if (initialMainBase >= 0 && initialMainBase < map.mainBases.Length && map.mainBases[initialMainBase].team == profile.team) return initialMainBase;
            for (int i = 0; i < map.mainBases.Length; i++) if (map.mainBases[i].team == profile.team) { initialMainBase = i; break; }
            return initialMainBase;
        }
        private void SelectDeploymentPoint(int captureIndex, int mainBaseIndex)
        {
            if (Simulation == null)
            {
                if (!ui.BattlePrep.activeSelf || captureIndex != -1 || mainBaseIndex < 0 || mainBaseIndex >= map.mainBases.Length || map.mainBases[mainBaseIndex].team != profile.team) return;
                initialMainBase = mainBaseIndex; RefreshBattlePrep(); return;
            }
            if (Simulation.GetUnit(HumanSlotId).IsAlive || IsPaused || Simulation.Phase != MatchPhase.Running) return;
            bool available = captureIndex >= 0 && captureIndex < map.capturePoints.Length && mainBaseIndex == -1
                ? Simulation.CanSpawnAtCapture(profile.team, captureIndex)
                : captureIndex == -1 && Simulation.CanSpawnAtMainBase(profile.team, mainBaseIndex);
            if (!available) return;
            spawnPoint = captureIndex; selectedMainBase = mainBaseIndex; RefreshBattle();
        }
        public void Deploy()
        {
            if (Simulation != null && Simulation.RequestSpawn(HumanSlotId, respawnDeck, spawnPoint, selectedMainBase))
                ui.Waiting.text = "出撃準備中…";
        }
        public bool TryGetSelf(int slotId, out SelfObservation self)
        {
            if (Simulation == null || slotId < 0 || slotId >= MatchLimits.TotalControllerSlots) { self = default; return false; }
            var u = Simulation.GetUnit(slotId); self = new SelfObservation(slotId, u.UnitId, u.Team, u.Controller, u.IsAlive, u.X, u.Z, u.Ammo, u.Y); return true;
        }
        public int MainBaseCount=>Simulation==null?0:Simulation.MainBaseCount;
        public bool IsMainBaseDestroyed(int index)=>Simulation.GetBase(index).Destroyed;
        public int CapturePointCount => Simulation == null ? 0 : Simulation.CapturePointCount;
        public CapturePointStatus GetCapturePoint(int index)
        {
            var point = Simulation.GetCapturePoint(index);
            return new CapturePointStatus(point.OwnerTeam, point.X, point.Z);
        }
        public NearbyEnemy FindNearestEnemy(int slotId, float rangeMeters)
        { return botPerception == null ? NearbyEnemy.None : botPerception.FindNearestEnemy(slotId,rangeMeters); }
        private void Update()
        {
            if (ui == null) return;
            if (IsSettingsOpen)
            { if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CancelSettings(); return; }
            if (Simulation == null) { if (ui.PartPicker.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ui.ClosePartPicker(); hangarPreview.Update(Time.unscaledDeltaTime); return; }
            bool mapWasOpen=IsTacticalMapOpen;
            if(Simulation.Phase==MatchPhase.Running&&Keyboard.current!=null&&Keyboard.current.mKey.wasPressedThisFrame&&!IsPaused)
            {if(IsTacticalMapOpen)CloseTacticalMap();else OpenTacticalMap();}
            if (Simulation.Phase == MatchPhase.Running && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            { if(mapWasOpen)CloseTacticalMap();else if (IsPaused) ResumeMatch(); else PauseMatch(); }
            if (Simulation.Phase == MatchPhase.Running && !IsPaused)
            {
                var self = Simulation.GetUnit(HumanSlotId);
                if (self.IsAlive && Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
                {
                    if (Mouse.current.rightButton.wasPressedThisFrame && !Simulation.IsCarryingNuclear(HumanSlotId)) ToggleTargetLock();
                    // A single physical notch can report a nonzero scroll delta across more than
                    // one Update() frame (driver/OS-level smoothing), which otherwise advances the
                    // weapon index multiple times per click ("chattering"). Debounce with a short
                    // cooldown so each notch registers as exactly one switch.
                    wheelCooldown = Mathf.Max(0, wheelCooldown - Time.unscaledDeltaTime);
                    float scroll = Mouse.current.scroll.y.ReadValue();
                    if (scroll != 0 && wheelCooldown <= 0)
                    { queuedWeaponSwitch = (queuedWeaponSwitch - (int)Mathf.Sign(scroll) + self.WeaponCount) % self.WeaponCount; wheelCooldown = 0.12f; }
                    Vector2 rawLook=Mouse.current.delta.ReadValue();
                    Vector2 delta = rawLook; yaw += delta.x * configuration.playablePrototype.mouseSensitivity;
                    yaw %= 360; pitch = Mathf.Clamp(pitch - delta.y * configuration.playablePrototype.mouseSensitivity, -25, 65);
                    if (!Mouse.current.leftButton.isPressed) inputArmed = true;
                    if (inputArmed && Mouse.current.leftButton.wasPressedThisFrame) shotQueued = true;
                    var keys = Keyboard.current;
                    if (keys != null)
                    {
                        if (!keys.eKey.isPressed) meleeArmed = true;
                        if (meleeArmed && keys.eKey.wasPressedThisFrame) meleeQueued = true;
                        if (keys.rKey.wasPressedThisFrame) reloadQueued = true;
                        if (keys.bKey.wasPressedThisFrame) emergencyReturnQueued = true;
                        if (keys.fKey.wasPressedThisFrame) nuclearInteractQueued = true;
                        if (keys.spaceKey.wasPressedThisFrame) jumpQueued = true;
                        if (keys.digit1Key.wasPressedThisFrame) SelectWeapon(0);
                        if (keys.digit2Key.wasPressedThisFrame) SelectWeapon(1);
                        if (keys.digit3Key.wasPressedThisFrame) SelectWeapon(2);
                        if(keys.tabKey.wasPressedThisFrame&&!Simulation.IsCarryingNuclear(HumanSlotId))CycleTargetLock();
                        if (keys.qKey.wasPressedThisFrame) meleeStyle = (MeleeStyle)(((int)meleeStyle + 1) % 5);
                    }
                }
                if(Simulation.IsCarryingNuclear(HumanSlotId))ClearTargetLock();
                ValidateTargetLock(); UpdateCamera(false);
                accumulatedSeconds += Time.deltaTime; int guard = 0;
                while (accumulatedSeconds >= Simulation.FixedDeltaTime && guard++ < 10)
                { AdvanceTick(); accumulatedSeconds -= Simulation.FixedDeltaTime; }
                view.Sync(Simulation); view.SetLocalBodyVisible(HumanSlotId,!firstPerson); RefreshAimFeedback();
                self = Simulation.GetUnit(HumanSlotId);
                if (self.IsAlive != lastAlive)
                {
                    lastAlive = self.IsAlive; inputArmed = false; ResetInput();
                    if (!self.IsAlive) { ClearTargetLock(); bool returned=Simulation.GetEmergencyReturn(HumanSlotId).InReserve;respawnDeck = returned?self.DeckIndex:(self.DeckIndex + 1) % profile.deck.Length; spawnPoint = -1; selectedMainBase = EnsureInitialMainBase(); if(!returned)audioSource.PlayOneShot(hitSound, 0.6f); }
                    SetCursor(self.IsAlive && !IsTacticalMapOpen); ui.ShowBattle(!self.IsAlive, false);
                }
                if (self.UnitId != lastSelfId) { lastSelfId = self.UnitId; lastSelfShot = -1; previousVital = self.Health + self.Shield; wasIncomingLocked=false; lastLockHitSound=-1; nextLockSound=0; UpdateCamera(true); }
                if (self.IsAlive && self.LastShotTick > lastSelfShot) { lastSelfShot = self.LastShotTick; audioSource.PlayOneShot(shotSound, 0.35f); }
                float vital = self.Health + self.Shield;
                bool wasHit = self.IsAlive && vital < previousVital - 0.1f;
                if (wasHit) audioSource.PlayOneShot(hitSound, 0.35f); ui.UpdateDamageFeedback(wasHit, Time.unscaledDeltaTime); previousVital = vital;
                RefreshIncomingLockWarning(self,true);
                combatFeedback.Update(Simulation,HumanSlotId,followCamera,lockedTargetSlot);
                ui.EventFeed.text=matchEventFeed.Update(Simulation,self.Team,map);
                UpdateDamageDirection(self);
                if(combatFeedback.KillSound)audioSource.PlayOneShot(killSound,0.6f);
                else if(combatFeedback.HitSound && Time.unscaledTime>=nextConfirmSound){audioSource.PlayOneShot(confirmSound,0.35f);nextConfirmSound=Time.unscaledTime+0.065f;}
                if(combatFeedback.HealSound && Time.unscaledTime>=nextRepairSound){audioSource.PlayOneShot(repairSound,0.25f);nextRepairSound=Time.unscaledTime+0.15f;}
                if (Simulation.Phase == MatchPhase.Finished) FinishMatch();
            }
            if (Time.unscaledTime >= nextUiTime) { nextUiTime = Time.unscaledTime + 1 / configuration.playablePrototype.uiRefreshHz; RefreshBattle(); }
        }
        public void AdvanceTick()
        {
            if (Simulation == null || IsPaused || Simulation.Phase != MatchPhase.Running) return;
            for (int slot = 0; slot < MatchLimits.TotalControllerSlots; slot++)
            {
                if (slot == HumanSlotId) continue;
                var unit = Simulation.GetUnit(slot);
                if (unit.IsAlive && botByDeck[botPresets[slot,unit.DeckIndex]].TryGetCommand(slot, Simulation.CurrentTick, out var command))
                {
                    var own = Simulation.GetLoadout(slot, unit.DeckIndex); var contact = FindNearestEnemy(slot, Simulation.GetActiveWeapon(slot).FireRange);
                    command = botTraversal.Adapt(unit, own, command, contact.Found ? contact.SlotId : -1, contact.UnitId, contact.X, contact.Y, contact.Z,botPlanner.GetIntent(slot,contact));
                    int next = (unit.ActiveWeaponIndex + command.WeaponSwitchDelta + own.WeaponCount) % own.WeaponCount;
                    Simulation.Submit(slot, botTriggers.Adapt(unit, own.GetWeapon(next), command));
                }
            }
            var self = Simulation.GetUnit(HumanSlotId);
            if (self.IsAlive)
            {
                Vector2 move = Vector2.zero; var keyboard = Keyboard.current; var mouse = Mouse.current;
                bool active = Cursor.lockState == CursorLockMode.Locked;
                if (active && keyboard != null) move = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0), (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                move = Vector2.ClampMagnitude(move, 1); var rotation = Quaternion.Euler(0, yaw, 0); Vector3 worldMove = rotation * new Vector3(move.x, 0, move.y);
                Ray ray = followCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
                var currentWeapon = Simulation.GetActiveWeapon(HumanSlotId);
                var point = Simulation.TraceCameraAim(HumanSlotId, ray.origin.x, ray.origin.y, ray.origin.z, ray.direction.x, ray.direction.y, ray.direction.z,
                    currentWeapon.FireRange + configuration.playablePrototype.cameraDistance + 2);
                aimTargetSlot = point.TargetSlot; aimTargetCore = point.CoreIndex;
                Vector3 shotDirection = new Vector3(point.X - self.X, point.Y - self.Y - Simulation.ShotHeight, point.Z - self.Z);
                aimYaw = shotDirection.x * shotDirection.x + shotDirection.z * shotDirection.z > 0.0001f ? Mathf.Atan2(shotDirection.x, shotDirection.z) * Mathf.Rad2Deg : yaw;
                float shotPitch = -Mathf.Atan2(shotDirection.y, new Vector2(shotDirection.x, shotDirection.z).magnitude) * Mathf.Rad2Deg;
                var weapon = Simulation.GetActiveWeapon(HumanSlotId);
                bool edgeMode = Simulation.IsCarryingNuclear(HumanSlotId) || weapon.FireMode == WeaponFireMode.SemiAutomatic || weapon.FireMode == WeaponFireMode.SingleShot;
                bool fire = active && inputArmed && (edgeMode ? shotQueued && !humanTriggerDown : mouse != null && mouse.leftButton.isPressed);
                if (fire || !edgeMode) shotQueued = false; humanTriggerDown = fire;
                bool melee = active && meleeArmed && (meleeQueued || keyboard != null && keyboard.eKey.isPressed);
                Simulation.Submit(HumanSlotId, new UnitCommand(self.UnitId, Simulation.CurrentTick, ++inputSequence, worldMove.x, worldMove.z,
                    aimYaw, shotPitch, active && keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed),
                    fire, false, melee, active && nuclearInteractQueued, active && reloadQueued, meleeStyle, true, point.X, point.Y, point.Z, lockedTargetSlot, lockedTargetUnitId,
                    active && (jumpQueued || keyboard != null && keyboard.spaceKey.isPressed),
                    requestedWeaponIndex >= 0 && requestedWeaponIndex < self.WeaponCount ? (requestedWeaponIndex - self.ActiveWeaponIndex + self.WeaponCount) % self.WeaponCount : queuedWeaponSwitch,lockedDeployableIndex:lockedDeployableIndex,lockedDeployableId:lockedDeployableId,emergencyReturn:active && emergencyReturnQueued,crouch:active && keyboard!=null && keyboard.leftCtrlKey.isPressed));
                meleeQueued = reloadQueued = jumpQueued = nuclearInteractQueued = emergencyReturnQueued = false; queuedWeaponSwitch = 0; requestedWeaponIndex = -1;
            }
            Simulation.Step();HandleReturnTransition();ValidateTargetLock();
            if (Simulation.Phase == MatchPhase.Finished) FinishMatch();
        }
        public void SelectWeapon(int index) { if (Simulation != null && index >= 0 && index < Simulation.GetUnit(HumanSlotId).WeaponCount) requestedWeaponIndex = index; }
        private void ResetInput() { nuclearInteractQueued = emergencyReturnQueued = false; jumpQueued = false; queuedWeaponSwitch = 0; requestedWeaponIndex = -1; shotQueued = meleeQueued = reloadQueued = humanTriggerDown = meleeArmed = false; }
        private static string ModeName(UnitLoadout w) { return w.Kind == WeaponKind.RepairGun ? "HP回復FA" : w.Kind == WeaponKind.Radar ? "レーダー" : w.Kind == WeaponKind.Melee ? "格闘" : w.Kind == WeaponKind.RepairPod ? "回復ポッド" : w.Kind == WeaponKind.Grenade ? "グレネード" : w.Kind == WeaponKind.Mine ? "地雷" : w.BlastRadius > 0 ? "バズーカ" : w.FireMode == WeaponFireMode.FullAutomatic ? "FA" : w.FireMode == WeaponFireMode.SemiAutomatic ? "SA" : w.FireMode == WeaponFireMode.SingleShot ? "単発" : w.FireMode == WeaponFireMode.Charge ? "チャージ" : "継続ビーム"; }
        private static string MeleeName(MeleeStyle style) { return style == MeleeStyle.Normal ? "通常格闘" : style == MeleeStyle.Combo ? "連撃格闘" : style == MeleeStyle.Dash ? "ダッシュ格闘" : style == MeleeStyle.DashCombo ? "ダッシュ連撃格闘" : "チャージ格闘"; }
        private void LateUpdate()
        {
            if (view != null) view.LateUpdate();
            // Camera is updated before tracing/stepping so this frame uses one consistent center ray.
        }
        private void UpdateCamera(bool snap)
        {
            var self = Simulation.GetUnit(HumanSlotId); var options = configuration.playablePrototype;
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            float crouchDrop=self.Crouching?LocomotionWorldView.CrouchDrop:0;
            Vector3 pivot = new Vector3(self.X, self.Y + options.cameraHeight-crouchDrop, self.Z), head = new Vector3(self.X,self.Y+(options.cameraHeadHeight>0?options.cameraHeadHeight:2.2f)-crouchDrop,self.Z);
            Vector3 desired = pivot - rotation*Vector3.forward*options.cameraDistance + rotation*Vector3.right*(options.cameraShoulderOffset>0?options.cameraShoulderOffset:0.75f);
            float radius=options.cameraCollisionRadius>0?options.cameraCollisionRadius:0.2f, threshold=options.firstPersonDistance>0?options.firstPersonDistance:1.15f;
            float available=Simulation.CameraClearDistance(head.x,head.y,head.z,desired.x,desired.y,desired.z,radius), length=(desired-head).magnitude;
            firstPerson=available<(firstPerson?threshold+0.4f:threshold);
            if(firstPerson) desired=head;
            else desired=head+(desired-head).normalized*Mathf.Min(length,available);
            Vector3 position=snap||firstPerson||available<length-0.1f?desired:Vector3.Lerp(followCamera.transform.position,desired,1-Mathf.Exp(-18*Time.unscaledDeltaTime));
            if(!firstPerson){float safe=Simulation.CameraClearDistance(head.x,head.y,head.z,position.x,position.y,position.z,radius);if(safe<(position-head).magnitude)position=head+(position-head).normalized*safe;}
            followCamera.transform.position=position; followCamera.transform.rotation=rotation; followCamera.nearClipPlane=firstPerson?0.03f:0.1f;
            if(view!=null)view.SetLocalBodyVisible(HumanSlotId,!firstPerson);
            followCamera.fieldOfView=65;
            if(snapLockPending && TryGetLockedPoint(out var lockPoint))
            {
                snapLockPending=false;
                for(int i=0;i<2;i++)
                {var direction=lockPoint-followCamera.transform.position;if(direction.sqrMagnitude<0.001f)break;var angles=Quaternion.LookRotation(direction).eulerAngles;
                    yaw=angles.y;pitch=Mathf.Clamp(angles.x>180?angles.x-360:angles.x,-25,65);UpdateCamera(true);}
            }
        }
        private void RefreshBattle()
        {
            if (Simulation == null) return;
            var self = Simulation.GetUnit(HumanSlotId); var loadout = Simulation.GetLoadout(HumanSlotId, self.DeckIndex);
            var equipped = Simulation.GetActiveWeapon(HumanSlotId);
            double blue = Simulation.GetGauge(0), orange = Simulation.GetGauge(1);
            ui.Gauges.text = "青軍 " + blue.ToString("0") + " / " + configuration.p0Simulation.initialGauge.ToString("0")
                + "";
            ui.OrangeText.text = "橙軍 " + orange.ToString("0") + " / " + configuration.p0Simulation.initialGauge.ToString("0");
            PlayableUI.Fill(ui.BlueGauge, (float)(blue / configuration.p0Simulation.initialGauge)); PlayableUI.Fill(ui.OrangeGauge, (float)(orange / configuration.p0Simulation.initialGauge));
            int remaining = Mathf.Max(0, Mathf.CeilToInt(configuration.p0Simulation.maxDurationSeconds - Simulation.CurrentTick * Simulation.FixedDeltaTime));
            ui.Timer.text = (remaining / 60).ToString("00") + ":" + (remaining % 60).ToString("00");
            ui.Vitals.text = "機体 " + (self.DeckIndex + 1) + "   コスト " + loadout.Cost + "\nHP " + self.Health.ToString("0") + "/" + loadout.MaxHealth.ToString("0") + "   盾 " + self.Shield.ToString("0")
                + "\n弾薬 " + self.Ammo + "/" + equipped.MaxAmmo + "\nBOOST " + self.Boost.ToString("0")+"/"+Simulation.GetBoostCapacity(HumanSlotId).ToString("0")
                +(self.BoostOverheated?"  OH":self.Grounded?"  地上":"  高度 "+self.Y.ToString("0.0")+"m");
            ui.CombatStatus.text = ModeName(equipped) + "  弾倉 " + self.Magazine + " / " + equipped.MagazineCapacity + (self.ReloadSeconds > 0 ? "  装填 " + self.ReloadSeconds.ToString("0.0") + "秒" : self.ChargeFraction > 0 ? "  チャージ " + (self.ChargeFraction * 100).ToString("0") + "%" : self.BeamActive ? "  照射中" : "")
                + "\n[" + "Q" + "] " + MeleeName(meleeStyle) + (self.MeleeChargeFraction > 0 ? "  溜め " + (self.MeleeChargeFraction * 100).ToString("0") + "%" : self.MeleePhase >= 0 ? "  " + self.ComboStep + "段目" : "")
                + "\nDP " + self.DownPoints.ToString("0") + "/" + self.DownPointCapacity.ToString("0") + (self.Disruption == DisruptionState.KnockedDown ? "  転倒 " + self.DisruptionSeconds.ToString("0.0") + "秒" : self.Disruption == DisruptionState.Staggered ? "  よろけ" : self.Guarding ? "  盾: 正面防御" : self.Shield <= 0 ? "  盾: 破損" : "  盾: 待機");
            for (int i=0;i<3;i++) ui.SetWeaponSlot(i,i<loadout.WeaponCount?(i+1)+"  "+PartTitle(activeBuilds[HumanSlotId][self.DeckIndex].WeaponPartId(i))+"  残弾 "+Simulation.GetWeaponAmmo(HumanSlotId,i): (i+1)+"  未装備",i==self.ActiveWeaponIndex);
            ui.SetDanger(self.Health/loadout.MaxHealth,self.Disruption!=DisruptionState.Ready,self.BoostOverheated);
            RefreshIncomingLockWarning(self,false);
            bool hasLock=lockedTargetSlot>=0 || lockedDeployableIndex>=0;
            bool friendlyLock=lockedTargetSlot>=0 && Simulation.GetUnit(lockedTargetSlot).Team==self.Team;
            ui.LockStatus.text=hasLock?(friendlyLock?"味方 / ":lockedDeployableIndex>=0?"敵設置物 / ":"")+(friendlyLock?(IsCurrentLockRed()?"近距離回復ロック / 取得時のみ吸着":"味方ロック / 自由照準"):(IsCurrentLockRed()?"赤ロック / 取得時のみ吸着":"黄ロック / 自由照準"))+"\nTab: 対象切替 / 右クリック: 解除":"右クリック: ロック / Tab: 対象切替\n"+(TargetLockMode==LockSelectionMode.ReticleCenter?"中心優先":"距離順切替")+(firstPerson?" / 頭部視点":"");
            ui.WeaponInfo.text=equipped.Kind==WeaponKind.Ranged?"射程 "+equipped.FireRange.ToString("0")+"m / 黄 "+Simulation.GetYellowLockRange(HumanSlotId).ToString("0")+"m / 赤 "+Simulation.GetRedLockRange(HumanSlotId,HumanSlotId).ToString("0")+"m\n弾速 "+(equipped.ProjectileSpeed>0?equipped.ProjectileSpeed.ToString("0")+"m/s":"照射")+" / 非ロック散布 "+equipped.HipSpreadDegrees.ToString("0.00")+"°":ModeName(equipped)+" / 威力 "+equipped.FireDamage.ToString("0")+" / DP "+equipped.FireDownPoints.ToString("0");
            if(equipped.Kind==WeaponKind.Melee)
            {var attack=configuration.playablePrototype.meleeAttacks[(int)meleeStyle];ui.WeaponInfo.text=MeleeName(meleeStyle)+" / 威力 "+attack.damage.ToString("0")+" / DP "+attack.downPoints.ToString("0")+"\nQで型変更 / Shift+通常・連撃でD格";}
            else if(equipped.Kind==WeaponKind.RepairPod)ui.WeaponInfo.text="盾 "+equipped.PodShieldPerSecond.ToString("0")+"/秒・弾薬 "+equipped.PodAmmoPerSecond.ToString("0")+"/秒 / "+equipped.UtilityRadius.ToString("0")+"m\nHPは少量 / 補給不可 / 壁越し無効";
            else if(equipped.Kind==WeaponKind.RepairGun)ui.WeaponInfo.text="味方HP +"+equipped.FireDamage.ToString("0")+"/発 / "+equipped.FireRange.ToString("0")+"m / ロック "+Simulation.GetFriendlyLockRange(HumanSlotId).ToString("0")+"m\n右クリックで味方ロック / HPのみ回復";
            else if(equipped.Kind==WeaponKind.Radar)ui.WeaponInfo.text="味方マップへ索敵共有 / 半径 "+equipped.UtilityRadius.ToString("0")+"m\n持続 "+equipped.UtilityDuration.ToString("0")+"秒 / 補給不可";
            ui.LockStatus.color=friendlyLock?new Color(0.3f,1,0.65f):hasLock&&IsCurrentLockRed()?new Color(1,0.35f,0.25f):Color.white;
            float downMaximum = self.DownPointCapacity * configuration.p0Simulation.knockdownThresholdMultiplier;
            PlayableUI.Fill(ui.DownFill, downMaximum > 0 ? self.DownPoints / downMaximum : 0);
            PlayableUI.Fill(ui.HealthFill, self.Health / loadout.MaxHealth); PlayableUI.Fill(ui.ShieldFill, loadout.MaxShield > 0 ? self.Shield / loadout.MaxShield : 0);
            float boostCapacity = Simulation.GetBoostCapacity(HumanSlotId);
            PlayableUI.Fill(ui.BoostFill, boostCapacity > 0 ? self.Boost / boostCapacity : 0);
            ui.BoostFill.color=self.BoostOverheated?new Color(1,0.25f,0.1f):new Color(0.23f,0.79f,0.74f);
            var objectives = new StringBuilder();
            for(int i=0;i<Simulation.MainBaseCount;i++){var core=Simulation.GetBase(i);objectives.Append(core.Name).Append(core.Destroyed?" 陥落":core.MaxHealth>0?" "+(core.Health/1000).ToString("0.0")+"k":" 本拠点").Append(i%3==2?"\n":" / ");}
            objectives.Append("中継拠点\n");
            for (int i = 0; i < map.capturePoints.Length; i++) { var c = Simulation.GetCapturePoint(i); objectives.Append(c.Id).Append("  ").Append(c.OwnerTeam < 0 ? "中立" : c.OwnerTeam == 0 ? "青軍" : "橙軍").Append("  ").Append(Mathf.Abs(c.Progress * 100).ToString("0")).Append("%\n"); }
            objectives.Append("本拠点陥落でゲージに大損害"); ui.Objective.text = objectives.ToString();
            var deckStatus = new StringBuilder("機体デッキ / 再出撃CT\n");
            for (int d = 0; d < profile.deck.Length; d++)
            {
                float wait = Mathf.Max(0, (Simulation.GetDeckReadyTick(HumanSlotId, d) - Simulation.CurrentTick) * Simulation.FixedDeltaTime);
                ui.RespawnLabels[d].text = "機体 " + (d + 1) + (d == respawnDeck ? " [選択]" : "") + "\n" + (wait > 0 ? "CT " + wait.ToString("0.0") + "秒" : "出撃可能");
                ui.RespawnChoices[d].interactable = wait <= 0;
                deckStatus.Append(d + 1).Append(d == self.DeckIndex && self.IsAlive ? " 操縦中" : wait > 0 ? " CT " + wait.ToString("0") + "秒" : " 待機可能").Append("\n");
            }
            RefreshStrategy();
            ui.DeckStatus.text = deckStatus.ToString(); minimap.Update(Simulation, HumanSlotId,lockedDeployableIndex,lockedDeployableId);
            float delay = Mathf.Max(0, (Simulation.GetRespawnReadyTick(HumanSlotId) - Simulation.CurrentTick) * Simulation.FixedDeltaTime);
            ui.Waiting.text = delay > 0 ? "再出撃まで " + delay.ToString("0.0") + "秒。次の機体と出撃先を選択してください。" : "出撃可能な機体を選んでください。全機CT中は時間経過を待ちます。";
            bool captureAvailable = spawnPoint >= 0 ? Simulation.CanSpawnAtCapture(profile.team, spawnPoint) : Simulation.CanSpawnAtMainBase(profile.team, selectedMainBase);
            string destination = spawnPoint >= 0 ? map.capturePoints[spawnPoint].id + " 中継" : selectedMainBase >= 0 ? map.MainBaseName(selectedMainBase) + " 本拠点" : "未選択";
            ui.SpawnLabel.text = "出撃先 : " + destination + (captureAvailable ? "" : "\n[出撃不可・別の拠点を選択]");
            if (!self.IsAlive) respawnDeploymentMap.Refresh(Simulation, profile.team, spawnPoint, selectedMainBase);
            ui.Deploy.interactable = !self.IsAlive && delay <= 0 && Simulation.GetDeckReadyTick(HumanSlotId, respawnDeck) <= Simulation.CurrentTick && captureAvailable;
            RefreshReturnUI();
            RefreshAimFeedback();
            // Low priority (GPT review 2026-10-16): only the Pause screen shows this panel, so
            // skip the per-slot sort/string-build entirely on every other RefreshBattle tick.
            if (ui.Pause.activeSelf) ui.Scoreboard.text = BuildScoreboard();
        }
        // User request 2026-10-16: scoreboard. Top-8-per-team by kills; a full 100-row table
        // would not fit legibly on one panel. Presentation only, built from the same
        // GetSlotStats data every player can see about themselves and teammates/opponents.
        private string BuildScoreboard()
        {
            const int topN = 8;
            var blue = new SlotStatsSnapshot[MatchLimits.ControllerSlotsPerTeam];
            var orange = new SlotStatsSnapshot[MatchLimits.ControllerSlotsPerTeam];
            for (int i = 0; i < MatchLimits.TotalControllerSlots; i++)
            {
                var s = Simulation.GetSlotStats(i);
                (s.Team == 0 ? blue : orange)[i % MatchLimits.ControllerSlotsPerTeam] = s;
            }
            Array.Sort(blue, (a, b) => b.Kills != a.Kills ? b.Kills - a.Kills : b.DamageDealt.CompareTo(a.DamageDealt));
            Array.Sort(orange, (a, b) => b.Kills != a.Kills ? b.Kills - a.Kills : b.DamageDealt.CompareTo(a.DamageDealt));
            var text = new StringBuilder("青軍                       撃破 被撃破  ダメージ\n");
            AppendScoreboardRows(text, blue, topN);
            text.Append("\n橙軍                       撃破 被撃破  ダメージ\n");
            AppendScoreboardRows(text, orange, topN);
            return text.ToString();
        }
        private static void AppendScoreboardRows(StringBuilder text, SlotStatsSnapshot[] rows, int count)
        {
            for (int i = 0; i < count && i < rows.Length; i++)
            {
                var s = rows[i];
                text.Append("#").Append(s.Slot).Append(s.Controller == ControllerType.Human ? " (操縦中)" : "").Append("  ")
                    .Append(s.Kills).Append("     ").Append(s.Deaths).Append("     ").Append(s.DamageDealt.ToString("0")).Append('\n');
            }
        }
        // User request 2026-10-16: show which direction incoming damage came from. Self-only
        // information about an attack against the player - never fed into BOT decisions.
        private void UpdateDamageDirection(UnitSnapshot self)
        {
            long first=Math.Max(damageDirectionSequence+1,Simulation.CombatEventSequence-Simulation.CombatEventCapacity+1);
            for(long n=first;n<=Simulation.CombatEventSequence;n++)
            {
                if(!Simulation.TryGetCombatEvent(n,out var e) || e.Kind!=CombatEventKind.Hit || e.TargetSlot!=HumanSlotId || e.TargetUnitId!=self.UnitId || e.SourceSlot<0)continue;
                var attacker=Simulation.GetUnit(e.SourceSlot);if(attacker.UnitId!=e.SourceUnitId)continue;
                float dx=attacker.X-self.X,dz=attacker.Z-self.Z;
                if(dx*dx+dz*dz<=0.0001f)continue;
                // Store the world-space direction, not a camera-relative snapshot (GPT review,
                // 2026-10-16): the camera-relative angle must track the player's own subsequent
                // turning during the display window, not stay pinned to where it was pointed at
                // the instant of the hit. attacker.UnitId is re-checked against the event's
                // SourceUnitId because the slot may have respawned a different unit by the time
                // this backlog entry is processed.
                damageDirectionWorldYaw=Mathf.Atan2(dx,dz)*Mathf.Rad2Deg;
                damageDirectionUntil=Time.unscaledTime+DamageDirectionSeconds;
            }
            damageDirectionSequence=Simulation.CombatEventSequence;
            float remaining=damageDirectionUntil-Time.unscaledTime;
            float relativeAngle=Mathf.DeltaAngle(followCamera.transform.eulerAngles.y,damageDirectionWorldYaw);
            ui.SetDamageDirection(relativeAngle,remaining>0?Mathf.Clamp01(remaining/DamageDirectionSeconds):0);
        }
        private void RefreshIncomingLockWarning(UnitSnapshot self,bool playSound)
        {
            bool locked=self.IsAlive && self.IncomingLockCount>0;
            bool recent=self.IsAlive && self.LastLockedHitTick>=0 && (Simulation.CurrentTick-self.LastLockedHitTick)*Simulation.FixedDeltaTime<=2.5f;
            ui.SetIncomingLockWarning(locked?self.IncomingLockCount:0,recent,Time.unscaledTime);
            if(!playSound)return;
            bool freshHit=recent && self.LastLockedHitTick>lastLockHitSound;
            if(freshHit || locked && (!wasIncomingLocked || Time.unscaledTime>=nextLockSound))
            {audioSource.PlayOneShot(lockSound,freshHit?0.5f:0.3f);nextLockSound=Time.unscaledTime+2;}
            lastLockHitSound=self.LastLockedHitTick;wasIncomingLocked=locked;
        }
        private void RefreshAimFeedback()
        {
            if(Simulation==null || ui==null)return;var self=Simulation.GetUnit(HumanSlotId);var weapon=Simulation.GetActiveWeapon(HumanSlotId);
            Ray ray=followCamera.ViewportPointToRay(new Vector3(0.5f,0.5f));
            var point=Simulation.TraceCameraAim(HumanSlotId,ray.origin.x,ray.origin.y,ray.origin.z,ray.direction.x,ray.direction.y,ray.direction.z,weapon.FireRange+configuration.playablePrototype.cameraDistance+2);
            bool centered=self.IsAlive && point.TargetSlot>=0,confident=false;
            if(centered && weapon.Kind==WeaponKind.Ranged)
            {
                var target=Simulation.GetUnit(point.TargetSlot);Vector3 origin=new Vector3(self.X,self.Y+Simulation.ShotHeight,self.Z),direction=new Vector3(point.X,point.Y,point.Z)-origin;
                var barrel=Simulation.TraceAim(HumanSlotId,origin.x,origin.y,origin.z,direction.x,direction.y,direction.z,weapon.FireRange);
                bool precise=point.TargetSlot==lockedTargetSlot && target.UnitId==lockedTargetUnitId && Simulation.IsYellowLocked(HumanSlotId,point.TargetSlot);
                Vector3 center=new Vector3(target.X,target.Y+Simulation.ShotHeight,target.Z)-origin;
                float margin=Mathf.Atan2(configuration.p0Simulation.unitRadius,Mathf.Max(0.01f,center.magnitude))*Mathf.Rad2Deg-Vector3.Angle(direction,center);
                confident=barrel.TargetSlot==point.TargetSlot && (precise || margin>=Simulation.GetHipSpreadDegrees(HumanSlotId));
            }
            if(centered && weapon.Kind==WeaponKind.RepairGun)
            {
                var friend=Simulation.GetUnit(point.TargetSlot);centered=friend.Team==self.Team;
                Vector3 origin=new Vector3(self.X,self.Y+Simulation.ShotHeight,self.Z),direction=new Vector3(point.X,point.Y,point.Z)-origin;
                var barrel=Simulation.TraceAim(HumanSlotId,origin.x,origin.y,origin.z,direction.x,direction.y,direction.z,weapon.FireRange);
                confident=centered && barrel.TargetSlot==point.TargetSlot && friend.Health<Simulation.GetLoadout(point.TargetSlot,friend.DeckIndex).MaxHealth;
            }
            if(self.IsAlive && point.DeployableIndex>=0 && weapon.Kind==WeaponKind.Ranged)
            {
                centered=true;var d=Simulation.GetDeployable(point.DeployableIndex);Vector3 origin=new Vector3(self.X,self.Y+Simulation.ShotHeight,self.Z),direction=new Vector3(point.X,point.Y,point.Z)-origin;
                var barrel=Simulation.TraceAim(HumanSlotId,origin.x,origin.y,origin.z,direction.x,direction.y,direction.z,weapon.FireRange);
                bool precise=point.DeployableIndex==lockedDeployableIndex && point.DeployableId==lockedDeployableId && Simulation.IsDeployableYellowLocked(HumanSlotId,lockedDeployableIndex,lockedDeployableId);
                Vector3 center=new Vector3(d.X,d.Y+d.HitHeight*0.5f,d.Z)-origin;float margin=Mathf.Atan2(Mathf.Min(d.HitRadius,d.HitHeight*0.5f),Mathf.Max(0.01f,center.magnitude))*Mathf.Rad2Deg-Vector3.Angle(direction,center);
                confident=barrel.DeployableIndex==point.DeployableIndex && (precise || margin>=Simulation.GetHipSpreadDegrees(HumanSlotId));
            }
            Vector3 viewport=Vector3.zero,lockPoint=Vector3.zero;float distance=0;bool marked=self.IsAlive&&TryGetLockedPoint(out lockPoint);
            if(marked){viewport=followCamera.WorldToViewportPoint(lockPoint);distance=(lockPoint-new Vector3(self.X,self.Y+Simulation.ShotHeight,self.Z)).magnitude;}
            bool friendly=marked && lockedTargetSlot>=0 && Simulation.GetUnit(lockedTargetSlot).Team==self.Team;
            string targetInfo="";if(friendly){var ally=Simulation.GetUnit(lockedTargetSlot);targetInfo="味方 HP "+ally.Health.ToString("0")+"/"+Simulation.GetLoadout(lockedTargetSlot,ally.DeckIndex).MaxHealth.ToString("0");}
            if(marked && lockedDeployableIndex>=0){var d=Simulation.GetDeployable(lockedDeployableIndex);targetInfo=(d.Kind==WeaponKind.StrategicNuclear?"戦略核":d.Kind==WeaponKind.Radar?"レーダー":d.Kind==WeaponKind.Mine?"地雷":"補給ポッド")+" HP "+d.Health.ToString("0")+"/"+d.MaxHealth.ToString("0");}
            ui.SetAimFeedback(centered,confident,marked,viewport,distance,marked&&IsCurrentLockRed(),targetInfo,friendly);ui.Crosshair.color=Color.white;
        }
        private void FinishMatch()
        {
            CloseTacticalMap();
            SetCursor(false); ui.ShowResults();
            ui.ResultTitle.text = Simulation.WinnerTeam < 0 ? "引き分け" : Simulation.WinnerTeam == profile.team ? "勝利" : "敗北";
            var personal = Simulation.GetSlotStats(HumanSlotId);
            ui.PersonalResultCombat.text = "撃破 " + personal.Kills + "    被撃破 " + personal.Deaths;
            ui.PersonalResultValues[0].text = personal.DamageDealt.ToString("N0");
            ui.PersonalResultValues[1].text = personal.DamageReceived.ToString("N0");
            ui.PersonalResultValues[2].text = personal.HealingDealt.ToString("N0");
            ui.PersonalResultValues[3].text = personal.HealingReceived.ToString("N0");
            ui.PersonalResultValues[4].text = personal.CoreDamageDealt.ToString("N0");
            var c = Simulation.Counters;
            ui.ResultStats.text = "青軍ゲージ  " + Simulation.GetGauge(0).ToString("0") + "\n橙軍ゲージ  " + Simulation.GetGauge(1).ToString("0")
                + "\n全軍の撃破数  " + c.UnitsDestroyed + "\n本拠点への命中  " + c.BaseHits + "\n経過時間  " + (Simulation.CurrentTick * Simulation.FixedDeltaTime).ToString("0.0") + "秒";
        }
        private static void SetCursor(bool locked) { Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !locked; }
        private static AudioClip Sound(string name, float frequency, float seconds)
        {
            const int rate = 16000; var samples = new float[(int)(rate * seconds)]; uint noise = 17;
            for (int i = 0; i < samples.Length; i++) { noise = noise * 1664525 + 1013904223; float t = i / (float)rate; samples[i] = (Mathf.Sin(t * frequency * 6.283185f) * 0.7f + ((noise & 65535) / 32768f - 1) * 0.3f) * Mathf.Exp(-t * 35); }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
        }
        private void OnApplicationFocus(bool focused) { if (!focused && Simulation != null && Simulation.Phase == MatchPhase.Running && !IsPaused) PauseMatch(); }
        private void OnDestroy()
        {
            DisposeStrategyViews();
            RestoreEnginePreferences();
            SetCursor(false); if (view != null) view.Dispose();
            if (ui != null) ui.SetPreviewTexture(null); if (hangarPreview != null) hangarPreview.Dispose();
            if (library != null) library.Dispose(); if (followCamera != null) Destroy(followCamera.gameObject);
            if(confirmSound!=null)Destroy(confirmSound);if(killSound!=null)Destroy(killSound);if(repairSound!=null)Destroy(repairSound);
            if (shotSound != null) Destroy(shotSound); if (hitSound != null) Destroy(hitSound); if (lockSound != null) Destroy(lockSound);
            if (font != null) { foreach (var atlas in font.atlasTextures) if (atlas != null) Destroy(atlas); if (font.material != null) Destroy(font.material); Destroy(font); }
        }
    }
}
