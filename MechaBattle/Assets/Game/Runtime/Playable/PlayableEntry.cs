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
    public sealed class PlayableEntry : MonoBehaviour, ISelfObservationSource, INearbyThreatSensor
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
        private GameObject preview;
        private Camera followCamera;
        private IUnitCommandSource[] botByDeck;
        private ResolvedBuild[][] activeBuilds;
        private AudioSource audioSource;
        private AudioClip shotSound, hitSound;
        private int selectedDeck, respawnDeck, spawnPoint = -1, lastSelfId;
        private uint inputSequence;
        private long lastSelfShot = -1;
        private float yaw = 90, pitch = 15, aimYaw, nextUiTime, previousVital;
        private double accumulatedSeconds;
        private bool inputArmed, lastAlive;
        private string notice = "構築した4機が順番に出撃できます。";
        public BattleSimulation Simulation { get; private set; }
        public int HumanSlotId { get; private set; }
        public bool IsPaused { get; private set; }
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
            configuration = JsonUtility.FromJson<PlayableConfiguration>(configurationAsset.text);
            catalog = configuration.constructionPrototype.catalog; configuration.playablePrototype.Apply(configuration.p0Simulation);
            profile = DeckConstruction.CreateDefault(catalog, configuration.playablePrototype.starterDeckSlots);
            LoadProfile();
            library = new MechaMeshLibrary(modularKitAsset.text, litShader);
            font = CreateFont();
            followCamera = new GameObject("PlayerCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            followCamera.tag = "MainCamera"; followCamera.backgroundColor = new Color(0.04f, 0.07f, 0.10f); followCamera.clearFlags = CameraClearFlags.SolidColor;
            followCamera.nearClipPlane = 0.1f; followCamera.farClipPlane = 650; followCamera.fieldOfView = 55;
            audioSource = followCamera.gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false; audioSource.spatialBlend = 0;
            shotSound = Sound("PrimaryShot", 180, 0.11f); hitSound = Sound("Damage", 70, 0.2f);
            var light = new GameObject("Sun", typeof(Light)); light.transform.rotation = Quaternion.Euler(48, -32, 0);
            light.transform.SetParent(transform, true);
            light.GetComponent<Light>().type = LightType.Directional; light.GetComponent<Light>().intensity = 1.6f;
            RenderSettings.ambientLight = new Color(0.42f, 0.47f, 0.52f); RenderSettings.fog = true; RenderSettings.fogColor = followCamera.backgroundColor;
            RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = 0.002f;
            ui = new PlayableUI(transform, font, SelectTeam, SelectDeckSlot, ApplyPreset, CyclePart, SaveProfile, BeginMatch,
                ResumeMatch, ReturnToPreparation, SelectRespawnDeck, CycleSpawnPoint, Deploy);
            minimap = new PlayableMinimap(ui.MinimapPanel, map);
            ShowPreparation();
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
            if (ui != null) ui.Status.text = notice;
        }
        public void SelectTeam(int team)
        {
            if (!IsInPreparation || team < 0 || team > 1) return;
            profile.team = team; RefreshPreparation();
        }
        public void SelectDeckSlot(int index)
        {
            if (!IsInPreparation || index < 0 || index >= profile.deck.Length) return;
            selectedDeck = index; RefreshPreparation();
        }
        public void ApplyPreset(int index)
        {
            if (!IsInPreparation || index < 0 || index >= catalog.presets.Length) return;
            profile.deck[selectedDeck] = DeckConstruction.Copy(catalog.presets[index], "player-" + (selectedDeck + 1)); RefreshPreparation();
        }
        public void CyclePart(int slotIndex)
        {
            if (!IsInPreparation || slotIndex < 0 || slotIndex > 2) return;
            var slot = (PartSlot)slotIndex;
            var build = profile.deck[selectedDeck]; EquipmentSelection choice = null;
            foreach (var e in build.equipment) if (e.slot == slot) choice = e;
            if (choice == null) return;
            var available = new System.Collections.Generic.List<PartDefinition>();
            foreach (var part in catalog.parts) if (part.slot == slot) available.Add(part);
            int current = available.FindIndex(p => p.id == choice.partId);
            if (available.Count > 0) choice.partId = available[(current + 1) % available.Count].id;
            RefreshPreparation();
        }
        private void ShowPreparation()
        {
            IsPaused = false; SetCursor(false); ui.ShowMenu();
            followCamera.rect = new Rect(0.55f, 0.23f, 0.40f, 0.62f); followCamera.fieldOfView = 35;
            followCamera.transform.position = new Vector3(4.3f, 2.9f, 6.5f); followCamera.transform.LookAt(new Vector3(0, 1.2f, 0));
            RefreshPreparation();
        }
        private void RefreshPreparation()
        {
            if (preview != null) Destroy(preview);
            for (int i = 0; i < profile.deck.Length; i++)
            {
                bool valid = ConstructionEvaluator.TryEvaluate(catalog, profile.deck[i], out var resolved, out var errors);
                ui.DeckLabels[i].text = "機体 " + (i + 1) + (i == selectedDeck ? " [編集中]" : "") + "\n" + (valid ? "コスト " + resolved.Cost : "構成エラー");
            }
            var build = profile.deck[selectedDeck];
            foreach (var e in build.equipment) ui.PartLabels[(int)e.slot].text = PartName(e.slot) + " : " + PartTitle(e.partId) + "  ›";
            if (ConstructionEvaluator.TryEvaluate(catalog, build, out var data, out var issues))
            {
                FrameDefinition frame = null; foreach (var f in catalog.frames) if (f.id == data.FrameId) frame = f;
                ui.Stats.text = "HP " + data.MaxHealth.ToString("0") + "   シールド " + data.MaxShield.ToString("0") + "   速度 " + data.MoveSpeed.ToString("0.0")
                    + "\n重量 " + data.Weight.ToString("0") + "/" + frame.maxWeight.ToString("0") + "   出力 " + data.Energy.ToString("0") + "/" + frame.maxEnergy.ToString("0")
                    + "\n威力 " + data.FireDamage.ToString("0") + " / 間隔 " + data.FireIntervalSeconds.ToString("0.0") + "秒 / 射程 " + data.FireRange.ToString("0") + "m / 弾数 " + data.MaxAmmo
                    + "\n自動コスト " + data.Cost + "    再出撃CT " + data.CooldownSeconds.ToString("0.0") + "秒";
                preview = library.Create(data, profile.team, transform); preview.transform.rotation = Quaternion.Euler(0, 15, 0);
            }
            else ui.Stats.text = "この構成では出撃できません。\n" + string.Join("\n", issues);
            ui.PreviewTitle.text = (profile.team == 0 ? "青軍" : "橙軍") + "  /  機体 " + (selectedDeck + 1) + "  /  標準フレーム";
            ui.Status.text = notice;
        }
        private static string PartName(PartSlot slot) { return slot == PartSlot.LeftArm ? "左腕" : slot == PartSlot.RightArm ? "右腕" : "主武器"; }
        private static string PartTitle(string id)
        {
            if (id == "weapon.carbine") return "カービン";
            if (id == "weapon.longrifle") return "ロングライフル";
            return id.Contains("reinforced") ? "補強アーム" : "標準アーム";
        }
        private static UnitLoadout Combat(ResolvedBuild b)
        { return new UnitLoadout(b.Id, b.MaxHealth, b.MaxShield, b.MoveSpeed, b.MaxAmmo, b.FireDamage, b.FireRange, b.FireIntervalSeconds, b.Cost, b.CooldownSeconds); }
        public void BeginMatch()
        {
            ResolvedBuild[] player;
            try { player = DeckConstruction.Validate(catalog, profile, configuration.playablePrototype.starterDeckSlots); }
            catch (ArgumentException e) { notice = "出撃できません: " + e.Message; ui.Status.text = notice; return; }
            SaveProfile();
            if (view != null) { view.Dispose(); view = null; }
            if (preview != null) { Destroy(preview); preview = null; }
            int count = player.Length; var bot = new ResolvedBuild[count];
            for (int i = 0; i < count; i++) bot[i] = ConstructionEvaluator.Evaluate(catalog, catalog.presets[i % catalog.presets.Length]);
            HumanSlotId = profile.team * MatchLimits.ControllerSlotsPerTeam;
            var loadouts = new UnitLoadout[MatchLimits.TotalControllerSlots][];
            activeBuilds = new ResolvedBuild[MatchLimits.TotalControllerSlots][];
            for (int slot = 0; slot < loadouts.Length; slot++)
            {
                activeBuilds[slot] = slot == HumanSlotId ? player : bot; loadouts[slot] = new UnitLoadout[count];
                for (int d = 0; d < count; d++) loadouts[slot][d] = Combat(activeBuilds[slot][d]);
            }
            Simulation = new BattleSimulation(map, configuration.p0Simulation, HumanSlotId, loadouts);
            Simulation.SetAutomaticRespawn(HumanSlotId, false);
            botByDeck = new IUnitCommandSource[count];
            for (int d = 0; d < count; d++) botByDeck[d] = new ForwardMarchBotCommandSource(map, this, this, bot[d].FireRange, bot[d].MaxAmmo, configuration.p0Simulation.homeSupplyRadius);
            view = new PlayableWorldView(map, Simulation, activeBuilds, library, followCamera, font, litShader, unlitShader, configuration.p0Simulation.baseCounterFireRadius);
            yaw = profile.team == 0 ? 90 : -90; aimYaw = yaw; pitch = 15; followCamera.rect = new Rect(0, 0, 1, 1); followCamera.fieldOfView = 65;
            accumulatedSeconds = 0; inputSequence = 0; inputArmed = false; IsPaused = false; lastAlive = true;
            lastSelfId = Simulation.GetUnit(HumanSlotId).UnitId; previousVital = player[0].MaxHealth + player[0].MaxShield; lastSelfShot = -1;
            SetCursor(true); ui.ShowBattle(false, false); RefreshBattle(); UpdateCamera(true);
        }
        public void ReturnToPreparation()
        {
            if (view != null) { view.Dispose(); view = null; }
            Simulation = null; activeBuilds = null; botByDeck = null; audioSource.Stop(); ShowPreparation();
        }
        public void PauseMatch()
        {
            if (Simulation == null || Simulation.Phase != MatchPhase.Running) return;
            IsPaused = true; inputArmed = false; SetCursor(false); ui.ShowBattle(!Simulation.GetUnit(HumanSlotId).IsAlive, true);
        }
        public void ResumeMatch()
        {
            if (Simulation == null || Simulation.Phase != MatchPhase.Running) return;
            IsPaused = false; inputArmed = false; bool alive = Simulation.GetUnit(HumanSlotId).IsAlive;
            SetCursor(alive); ui.ShowBattle(!alive, false);
        }
        private void SelectRespawnDeck(int index) { if (index >= 0 && index < profile.deck.Length) { respawnDeck = index; RefreshBattle(); } }
        private void CycleSpawnPoint()
        {
            if (Simulation == null) return;
            for (int i = 0; i <= map.capturePoints.Length; i++)
            {
                spawnPoint++; if (spawnPoint >= map.capturePoints.Length) spawnPoint = -1;
                if (spawnPoint == -1 || Simulation.CanSpawnAtCapture(profile.team, spawnPoint)) break;
            }
            RefreshBattle();
        }
        public void Deploy()
        {
            if (Simulation != null && Simulation.RequestSpawn(HumanSlotId, respawnDeck, spawnPoint))
                ui.Waiting.text = "出撃準備中…";
        }
        public bool TryGetSelf(int slotId, out SelfObservation self)
        {
            if (Simulation == null || slotId < 0 || slotId >= MatchLimits.TotalControllerSlots) { self = default; return false; }
            var u = Simulation.GetUnit(slotId); self = new SelfObservation(slotId, u.UnitId, u.Team, u.Controller, u.IsAlive, u.X, u.Z, u.Ammo); return true;
        }
        public NearbyEnemy FindNearestEnemy(int slotId, float rangeMeters)
        {
            if (Simulation == null || slotId < 0 || slotId >= MatchLimits.TotalControllerSlots || !float.IsFinite(rangeMeters) || rangeMeters <= 0) return NearbyEnemy.None;
            var self = Simulation.GetUnit(slotId); if (!self.IsAlive) return NearbyEnemy.None;
            float range = Mathf.Min(rangeMeters, Simulation.GetLoadout(slotId, self.DeckIndex).FireRange);
            float best = range * range; var result = NearbyEnemy.None;
            for (int i = 0; i < MatchLimits.TotalControllerSlots; i++)
            {
                var u = Simulation.GetUnit(i); if (!u.IsAlive || u.Team == self.Team) continue;
                float dx = u.X - self.X, dz = u.Z - self.Z, distance = dx * dx + dz * dz;
                if (distance <= best) { best = distance; result = new NearbyEnemy(true, u.X, u.Z); }
            }
            return result;
        }
        private void Update()
        {
            if (ui == null) return;
            if (Simulation == null) { if (preview != null) preview.transform.Rotate(Vector3.up, Time.unscaledDeltaTime * 16, Space.World); return; }
            if (Simulation.Phase == MatchPhase.Running && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            { if (IsPaused) ResumeMatch(); else PauseMatch(); }
            if (Simulation.Phase == MatchPhase.Running && !IsPaused)
            {
                var self = Simulation.GetUnit(HumanSlotId);
                if (self.IsAlive && Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
                {
                    Vector2 delta = Mouse.current.delta.ReadValue(); yaw += delta.x * configuration.playablePrototype.mouseSensitivity;
                    yaw %= 360; pitch = Mathf.Clamp(pitch - delta.y * configuration.playablePrototype.mouseSensitivity, 5, 48);
                    if (!Mouse.current.leftButton.isPressed) inputArmed = true;
                }
                accumulatedSeconds += Time.deltaTime; int guard = 0;
                while (accumulatedSeconds >= Simulation.FixedDeltaTime && guard++ < 10)
                { AdvanceTick(); accumulatedSeconds -= Simulation.FixedDeltaTime; }
                view.Sync(Simulation);
                self = Simulation.GetUnit(HumanSlotId);
                if (self.IsAlive != lastAlive)
                {
                    lastAlive = self.IsAlive; inputArmed = false;
                    if (!self.IsAlive) { respawnDeck = (self.DeckIndex + 1) % profile.deck.Length; spawnPoint = -1; audioSource.PlayOneShot(hitSound, 0.6f); }
                    SetCursor(self.IsAlive); ui.ShowBattle(!self.IsAlive, false);
                }
                if (self.UnitId != lastSelfId) { lastSelfId = self.UnitId; lastSelfShot = -1; previousVital = self.Health + self.Shield; UpdateCamera(true); }
                if (self.IsAlive && self.LastShotTick > lastSelfShot) { lastSelfShot = self.LastShotTick; audioSource.PlayOneShot(shotSound, 0.35f); }
                float vital = self.Health + self.Shield;
                if (self.IsAlive && vital < previousVital - 0.1f) audioSource.PlayOneShot(hitSound, 0.35f); previousVital = vital;
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
                if (unit.IsAlive && botByDeck[unit.DeckIndex].TryGetCommand(slot, Simulation.CurrentTick, out var command)) Simulation.Submit(slot, command);
            }
            var self = Simulation.GetUnit(HumanSlotId);
            if (self.IsAlive)
            {
                Vector2 move = Vector2.zero; var keyboard = Keyboard.current; var mouse = Mouse.current;
                bool active = Cursor.lockState == CursorLockMode.Locked;
                if (active && keyboard != null) move = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0), (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                move = Vector2.ClampMagnitude(move, 1); var rotation = Quaternion.Euler(0, yaw, 0); Vector3 worldMove = rotation * new Vector3(move.x, 0, move.y);
                aimYaw = yaw;
                Ray ray = followCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
                var plane = new Plane(Vector3.up, new Vector3(0, 1.3f, 0));
                if (plane.Raycast(ray, out float distance))
                {
                    Vector3 point = ray.GetPoint(distance); Vector3 diff = point - new Vector3(self.X, 1.3f, self.Z);
                    if (diff.sqrMagnitude > 0.1f) aimYaw = Mathf.Atan2(diff.x, diff.z) * Mathf.Rad2Deg;
                }
                Simulation.Submit(HumanSlotId, new UnitCommand(self.UnitId, Simulation.CurrentTick, ++inputSequence, worldMove.x, worldMove.z,
                    aimYaw, pitch, active && keyboard != null && keyboard.leftShiftKey.isPressed,
                    active && inputArmed && mouse != null && mouse.leftButton.isPressed, false,
                    active && keyboard != null && keyboard.eKey.isPressed, false));
            }
            Simulation.Step();
            if (Simulation.Phase == MatchPhase.Finished) FinishMatch();
        }
        private void LateUpdate()
        {
            if (view != null) view.LateUpdate();
            if (Simulation != null && Simulation.Phase == MatchPhase.Running && !IsPaused) UpdateCamera(false);
        }
        private void UpdateCamera(bool snap)
        {
            var self = Simulation.GetUnit(HumanSlotId); var rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 target = new Vector3(self.X, configuration.playablePrototype.cameraHeight, self.Z);
            Vector3 desired = target - rotation * Vector3.forward * configuration.playablePrototype.cameraDistance + rotation * Vector3.right * 0.75f;
            followCamera.transform.position = snap ? desired : Vector3.Lerp(followCamera.transform.position, desired, 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
            followCamera.transform.rotation = rotation;
            float fov = self.IsAlive && Mouse.current != null && Mouse.current.rightButton.isPressed ? 42 : 65;
            followCamera.fieldOfView = Mathf.Lerp(followCamera.fieldOfView, fov, Time.unscaledDeltaTime * 12);
        }
        private void RefreshBattle()
        {
            if (Simulation == null) return;
            var self = Simulation.GetUnit(HumanSlotId); var loadout = Simulation.GetLoadout(HumanSlotId, self.DeckIndex);
            double blue = Simulation.GetGauge(0), orange = Simulation.GetGauge(1);
            ui.Gauges.text = "青軍 " + blue.ToString("0") + " / " + configuration.p0Simulation.initialGauge.ToString("0")
                + "";
            ui.OrangeText.text = "橙軍 " + orange.ToString("0") + " / " + configuration.p0Simulation.initialGauge.ToString("0");
            PlayableUI.Fill(ui.BlueGauge, (float)(blue / configuration.p0Simulation.initialGauge)); PlayableUI.Fill(ui.OrangeGauge, (float)(orange / configuration.p0Simulation.initialGauge));
            int remaining = Mathf.Max(0, Mathf.CeilToInt(configuration.p0Simulation.maxDurationSeconds - Simulation.CurrentTick * Simulation.FixedDeltaTime));
            ui.Timer.text = (remaining / 60).ToString("00") + ":" + (remaining % 60).ToString("00");
            ui.Vitals.text = "機体 " + (self.DeckIndex + 1) + "   コスト " + loadout.Cost + "\nHP " + self.Health.ToString("0") + "/" + loadout.MaxHealth.ToString("0") + "   盾 " + self.Shield.ToString("0")
                + "\n弾薬 " + self.Ammo + "/" + loadout.MaxAmmo + "    BOOST " + self.Boost.ToString("0");
            PlayableUI.Fill(ui.HealthFill, self.Health / loadout.MaxHealth); PlayableUI.Fill(ui.ShieldFill, loadout.MaxShield > 0 ? self.Shield / loadout.MaxShield : 0);
            float boostCapacity = configuration.p0Simulation.boostCapacity;
            PlayableUI.Fill(ui.BoostFill, boostCapacity > 0 ? self.Boost / boostCapacity : 0);
            var objectives = new StringBuilder("中継拠点\n");
            for (int i = 0; i < map.capturePoints.Length; i++) { var c = Simulation.GetCapturePoint(i); objectives.Append(c.Id).Append("  ").Append(c.OwnerTeam < 0 ? "中立" : c.OwnerTeam == 0 ? "青軍" : "橙軍").Append("  ").Append(Mathf.Abs(c.Progress * 100).ToString("0")).Append("%\n"); }
            objectives.Append("\n敵建屋へ侵入しコアを攻撃\n味方本拠点で自動補給"); ui.Objective.text = objectives.ToString();
            var deckStatus = new StringBuilder("機体デッキ / 再出撃CT\n");
            for (int d = 0; d < profile.deck.Length; d++)
            {
                float wait = Mathf.Max(0, (Simulation.GetDeckReadyTick(HumanSlotId, d) - Simulation.CurrentTick) * Simulation.FixedDeltaTime);
                ui.RespawnLabels[d].text = "機体 " + (d + 1) + (d == respawnDeck ? " [選択]" : "") + "\n" + (wait > 0 ? "CT " + wait.ToString("0.0") + "秒" : "出撃可能");
                ui.RespawnChoices[d].interactable = wait <= 0;
                deckStatus.Append(d + 1).Append(d == self.DeckIndex && self.IsAlive ? " 操縦中" : wait > 0 ? " CT " + wait.ToString("0") + "秒" : " 待機可能").Append("\n");
            }
            ui.DeckStatus.text = deckStatus.ToString(); minimap.Update(Simulation, HumanSlotId);
            float delay = Mathf.Max(0, (Simulation.GetRespawnReadyTick(HumanSlotId) - Simulation.CurrentTick) * Simulation.FixedDeltaTime);
            ui.Waiting.text = delay > 0 ? "再出撃まで " + delay.ToString("0.0") + "秒。次の機体と出撃先を選択してください。" : "出撃可能な機体を選んでください。全機CT中は時間経過を待ちます。";
            bool captureAvailable = spawnPoint < 0 || Simulation.CanSpawnAtCapture(profile.team, spawnPoint);
            ui.SpawnLabel.text = "出撃先 : " + (spawnPoint < 0 ? "自軍本拠点" : map.capturePoints[spawnPoint].id + (captureAvailable ? " 中継" : " [敵が接近・出撃不可]")) + "  ›";
            ui.Deploy.interactable = !self.IsAlive && delay <= 0 && Simulation.GetDeckReadyTick(HumanSlotId, respawnDeck) <= Simulation.CurrentTick && captureAvailable;
            ui.Crosshair.color = Color.white;
            if (self.IsAlive)
            {
                var nearest = FindNearestEnemy(HumanSlotId, configuration.p0Simulation.redLockRange);
                if (nearest.Found)
                {
                    float targetYaw = Mathf.Atan2(nearest.X - self.X, nearest.Z - self.Z) * Mathf.Rad2Deg;
                    if (Mathf.Abs(Mathf.DeltaAngle(aimYaw, targetYaw)) <= configuration.playablePrototype.aimAssistDegrees) ui.Crosshair.color = new Color(1, 0.3f, 0.25f);
                }
            }
        }
        private void FinishMatch()
        {
            SetCursor(false); ui.ShowResults();
            ui.ResultTitle.text = Simulation.WinnerTeam < 0 ? "引き分け" : Simulation.WinnerTeam == profile.team ? "勝利" : "敗北";
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
        private void OnApplicationFocus(bool focused) { if (!focused && Simulation != null && Simulation.Phase == MatchPhase.Running) PauseMatch(); }
        private void OnDestroy()
        {
            SetCursor(false); if (view != null) view.Dispose(); if (preview != null) Destroy(preview);
            if (library != null) library.Dispose(); if (followCamera != null) Destroy(followCamera.gameObject);
            if (shotSound != null) Destroy(shotSound); if (hitSound != null) Destroy(hitSound);
            if (font != null) { foreach (var atlas in font.atlasTextures) if (atlas != null) Destroy(atlas); if (font.material != null) Destroy(font.material); Destroy(font); }
        }
    }
}
