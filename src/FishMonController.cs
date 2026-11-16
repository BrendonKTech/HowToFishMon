using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HowToFishMon
{
    // runs every frame: hotkeys, the companion, healing over time, the party menu and HUD
    public partial class FishMonController : MonoBehaviour
    {
        public static FishMonController Instance { get; private set; }

        public static bool PartyOpen { get; private set; }
        public static bool UIOpen => PartyOpen || ModMenu.Open || WorldPrompt != null;

        // set while we're asking "bring your FishMon or start fresh?" for a world you host
        public static string WorldPrompt { get; private set; }
        public static Companion Active { get; private set; }

        // FishMon that hit the current boss; they share the kill exp
        public static readonly HashSet<FishMon> Participants = new HashSet<FishMon>();

        // toasts
        private struct Toast { public string Text; public float Until; public Color Color; }
        private static readonly List<Toast> Toasts = new List<Toast>();

        public static void ShowToast(string text, Color? color = null, float seconds = 4f)
        {
            Toasts.Add(new Toast { Text = text, Until = Time.unscaledTime + seconds, Color = color ?? Color.white });
            if (Toasts.Count > 5) Toasts.RemoveAt(0);
        }

        // floating world text
        private struct Floater { public Vector3 Pos; public string Text; public Color Color; public float Start; public float Dur; }
        private static readonly List<Floater> Floaters = new List<Floater>();

        public static void AddFloat(Vector3 worldPos, string text, Color color, float seconds = 1.6f)
        {
            Floaters.Add(new Floater { Pos = worldPos, Text = text, Color = color, Start = Time.time, Dur = seconds });
            if (Floaters.Count > 30) Floaters.RemoveAt(0);
        }

        public static void GiveExp(FishMon mon, int exp, int[] evYield)
        {
            float bonus = (mon.Has(Ability.Scholar) ? Abilities.ScholarExp : 1f) * (mon.Holds(HeldItem.LuckyEgg) ? Items.LuckyEggExp : 1f);
            if (bonus != 1f && exp > 0) exp = mon.CarryExp(exp * bonus);
            GrantExp(mon, exp, evYield);

            // Mentor: everyone else in the party learns a bit from it (EXP only, no EVs)
            if (mon.Has(Ability.Mentor) && exp > 0 && FishMonStorage.Party.Contains(mon))
            {
                foreach (var other in FishMonStorage.Party.ToList())
                    if (other != mon && !other.Fainted && (!Active || Active.Mon != other))
                        GrantExp(other, other.CarryExp(exp * Abilities.MentorShare), null);
            }
        }

        private static void GrantExp(FishMon mon, int exp, int[] evYield)
        {
            var lines = mon.GainExp(exp, evYield);
            foreach (var line in lines) ShowToast(line, new Color(0.7f, 0.85f, 1f));
            if (lines.Count > 0 && Active && Active.Mon == mon) Active.RefreshMoves();
        }

        public static void OnCaught(FishMon mon, bool toParty)
        {
            string shiny = mon.Shiny ? "<color=#ffd84a>★ DRIP ★</color> " : "";
            ShowToast($"Gotcha! {shiny}{mon.SpeciesName} was caught!  Lv. {mon.Level}  |  IVs {mon.IVTotal}/186 ({mon.IVPercent:P0})  |  {Natures.All[mon.Nature].name}",
                mon.Shiny ? new Color(1f, 0.85f, 0.3f) : new Color(0.6f, 1f, 0.7f), 6f);
            var dex = Fishdex.Get(mon.SpeciesId);
            if (dex != null && dex.Caught == 1)
            {
                int n = Fishdex.Number(mon.SpeciesId);
                ShowToast($"New Fishdex entry!  #{n:000} {mon.BaseSpecies ?? mon.SpeciesName}  ({Fishdex.CaughtCount}/{Fishdex.Total} caught)", new Color(1f, 0.85f, 0.4f), 6f);
            }
            if (!toParty) ShowToast($"Your party is full, so {mon.SpeciesName} was sent to the Box.", null, 5f);
            else if (FishMonStorage.Party.Count == 1)
                ShowToast($"Press {Plugin.KeyName(Plugin.SummonKey)} to send {mon.SpeciesName} out!", new Color(0.7f, 0.9f, 1f), 6f);

            // the lead FishMon learns a little from every catch
            var lead = Active ? Active.Mon : FishMonStorage.Party.FirstOrDefault();
            if (lead != null && lead != mon)
            {
                int exp = Mathf.Max(1, Mathf.RoundToInt(mon.BaseStats.Sum() / 6f * mon.Level / 14f * Plugin.ExpMultiplier.Value));
                GiveExp(lead, exp, null);
                FishMonStorage.Save();
            }
            try { AudioManager.PlayGlobalClip("Hitmarker"); } catch { }
        }

        // party ui state
        private FishMon _selected;
        private bool _showingBox;
        private Vector2 _listScroll;
        private FishMon _confirmRelease;
        private string _renameBuffer;
        private FishMon _renameTarget;
        private float _regenSaveTimer;
        private bool _regenDirty;
        private float _scale = 1f;
        private float _autoSendAt = -1f;

        private void Awake() => Instance = this;

        private void Start()
        {
            BossManager.OnGlobalBossSpawn += OnBossSpawn;
            BossManager.OnGlobalBossDeath += OnBossDeath;
            BossManager.OnGlobalBossDespawn += OnBossDespawn;
        }

        private static UnityEngine.EventSystems.EventSystem _mutedEvents;

        private static void MuteGameUi(bool mute)
        {
            if (mute)
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es && es.enabled) { es.enabled = false; _mutedEvents = es; }
            }
            else if (_mutedEvents)
            {
                _mutedEvents.enabled = true;
                _mutedEvents = null;
            }
        }

        private void OnDestroy()
        {
            MuteGameUi(false);
            BossManager.OnGlobalBossSpawn -= OnBossSpawn;
            BossManager.OnGlobalBossDeath -= OnBossDeath;
            BossManager.OnGlobalBossDespawn -= OnBossDespawn;
        }

        private void OnBossSpawn()
        {
            Participants.Clear();
            if (BossManager.Boss) Fishdex.MarkSeen(BossManager.Boss);
            if (Active) { ShowToast($"A boss appeared! {Active.Mon.DisplayName} is ready to fight!", new Color(1f, 0.6f, 0.4f), 6f); return; }
            var lead = FishMonStorage.Lead;
            if (lead != null)
                ShowToast($"A boss appeared! Press {Plugin.KeyName(Plugin.SummonKey)} to send out {lead.DisplayName}!", new Color(1f, 0.6f, 0.4f), 7f);
            else if (FishMonStorage.Party.Count == 0)
                ShowToast("A boss appeared! Hook fish with your rod and pick them up to build a FishMon team.", new Color(1f, 0.6f, 0.4f), 7f);
        }

        private void OnBossDeath()
        {
            var boss = BossManager.Boss;
            if (boss) Fishdex.RecordDefeat(boss);
            if (Participants.Count == 0 || !boss) { Participants.Clear(); return; }
            var b = BossMon.From(boss);
            var winners = Participants.Where(p => !p.Fainted).ToList();
            if (winners.Count == 0) winners = Participants.ToList();
            int each = Mathf.Max(1, Mathf.RoundToInt(b.FullExp * 0.5f / winners.Count));
            var evYield = b.IsMain ? new[] { 1, 2, 1, 2, 1, 1 } : new[] { 0, 1, 1, 1, 1, 0 };
            ShowToast($"{b.Name} was defeated!", new Color(1f, 0.85f, 0.4f), 5f);
            foreach (var w in winners)
            {
                w.BossesDefeated++;
                ShowToast($"{w.DisplayName} gained {each} Exp. Points!", new Color(0.7f, 0.85f, 1f));
                GiveExp(w, each, evYield);
            }
            Participants.Clear();
            FishMonStorage.Save();
        }

        private void OnBossDespawn()
        {
            Participants.Clear();
            FishMonStorage.Save();
        }

        // companion control

        private static void SendOut(FishMon mon)
        {
            var p = Player.LocalPlayer;
            if (!p || mon == null) return;
            Recall(false);
            Vector3 pos = Companion.PlayerPos(p) + Companion.PlayerForward(p) * 2f;

            // a FishMon that reached its evolution level (e.g. from an older save) evolves as it comes out.
            var lines = new List<string>();
            bool evolved = mon.TryEvolve(lines);
            mon.JustEvolved = false;

            Active = Companion.Spawn(mon, pos);
            ShowToast($"Go! {mon.DisplayName}!", new Color(0.6f, 0.85f, 1f), 3f);
            if (evolved)
            {
                foreach (var l in lines) ShowToast(l, new Color(1f, 0.9f, 0.5f), 5f);
                Fx.Evolve(Active.transform.position, TypeChart.ColorOf(mon.Type1));
                FishMonStorage.Save();
            }
        }

        // swap the model of the FishMon that's out for its new evolved form, right where it stands
        private static void RebuildActiveAfterEvolution()
        {
            var old = Active;
            var mon = old.Mon;
            mon.JustEvolved = false;
            Vector3 pos = old.transform.position;
            var fresh = Companion.Spawn(mon, pos);
            fresh.CopyOrdersFrom(old);
            old.Despawn(false);
            Active = fresh;
            Fx.Evolve(fresh.transform.position, TypeChart.ColorOf(mon.Type1));
            FishMonStorage.Save();
        }

        // commands

        private void CommandAttack(Player player)
        {
            if (!Active) { ShowToast($"Send out a FishMon first ({Plugin.KeyName(Plugin.SummonKey)})."); return; }
            var target = CreatureUnderCrosshair(player);
            if (target)
            {
                Active.OrderAttack(target);
                ShowToast($"{Active.Mon.DisplayName}, attack the {Species.SafeName(target)}!", new Color(1f, 0.7f, 0.5f), 2.5f);
                Fx.Ping(target.Rig ? target.Rig.worldCenterOfMass : target.transform.position, new Color(1f, 0.35f, 0.3f));
            }
            else
            {
                Active.OrderFollow();
                ShowToast($"{Active.Mon.DisplayName}, come back!", new Color(0.6f, 0.85f, 1f), 2.5f);
            }
        }

        private void CommandStayToggle()
        {
            if (!Active) { ShowToast($"Send out a FishMon first ({Plugin.KeyName(Plugin.SummonKey)})."); return; }
            if (Active.Staying)
            {
                Active.OrderFollow();
                ShowToast($"{Active.Mon.DisplayName}, follow me!", new Color(0.6f, 0.85f, 1f), 2.5f);
            }
            else
            {
                Active.OrderStay();
                ShowToast($"{Active.Mon.DisplayName}, stay! (it'll only fight what you point at with {Plugin.KeyName(Plugin.AttackKey)})", new Color(0.6f, 0.85f, 1f), 3.5f);
            }
        }

        // the living creature closest to where you're aiming (a bit of aim assist, needs line of sight)
        private static Creature CreatureUnderCrosshair(Player player)
        {
            Camera cam = player.CurCam ? player.CurCam : Camera.main;
            if (!cam) return null;
            Vector3 origin = cam.transform.position;
            Vector3 fwd = cam.transform.forward;
            Creature best = null;
            float bestScore = float.MaxValue;
            foreach (var item in ItemManager.Items.Values)
            {
                var c = item ? item.Creature : null;
                if (!c || c.IsDead || c.IsDestroying || c.Holder) continue;
                Vector3 center = c.Rig ? c.Rig.worldCenterOfMass : c.transform.position;
                Vector3 to = center - origin;
                float dist = to.magnitude;
                if (dist > 100f || dist < 0.5f) continue;
                float along = Vector3.Dot(to, fwd);
                if (along <= 0f) continue;
                float offRay = Vector3.Cross(fwd, to).magnitude;       // distance from the aim line
                float allowed = 1.2f + dist * 0.06f;                    // more forgiving far away
                if (c.BossType != BossType.None) allowed += 4f;         // bosses are big
                if (offRay > allowed) continue;
                if (Physics.Linecast(origin, center, out var hit, GameInfo.LevelLayer, QueryTriggerInteraction.Ignore) &&
                    hit.distance < dist - 1.5f) continue;              // behind a wall
                float score = offRay / allowed + dist * 0.002f;
                if (score < bestScore) { bestScore = score; best = c; }
            }
            return best;
        }

        // key clash check

        private bool _checkedKeys;

        // game keyboard key name (lower case, e.g. "g", "f6", "leftshift") -> game action using it
        private static Dictionary<string, string> _gameKeys;

        private static Dictionary<string, string> GameKeys()
        {
            if (_gameKeys != null) return _gameKeys;
            var map = new Dictionary<string, string>();
            try
            {
                var input = GameInfo.Input;
                if (input && input.actions != null)
                    foreach (var action in input.actions)
                        foreach (var b in action.bindings)
                        {
                            string path = b.effectivePath;
                            if (string.IsNullOrEmpty(path) || !path.StartsWith("<Keyboard>/")) continue;
                            string key = path.Substring("<Keyboard>/".Length).ToLowerInvariant();
                            if (!map.ContainsKey(key)) map[key] = action.name;
                        }
                _gameKeys = map; // only cache once the game's input actually exists
            }
            catch (Exception e)
            {
                Plugin.Log.LogInfo("Couldn't read the game's key bindings: " + e.Message);
            }
            return map;
        }

        // forget the cached game bindings (the player may have rebound them in the game's own settings)
        public static void RefreshGameKeys() => _gameKeys = null;

        // the game action that also uses this key, or null
        public static string GameActionFor(KeyCode k)
        {
            if (k == KeyCode.None) return null;
            return GameKeys().TryGetValue(k.ToString().ToLowerInvariant(), out var action) ? action : null;
        }

        private void CheckKeyConflicts()
        {
            _checkedKeys = true;
            foreach (var k in Plugin.KeyBindings)
            {
                string action = GameActionFor(k.Value);
                if (action == null) continue;
                string msg = $"FishMon key {Plugin.KeyName(k)} ({k.Definition.Key}) is also the game's '{action}' key. You can change it in the FishMon Mod Menu ({Plugin.KeyName(Plugin.ModMenuKey)}) → Controls.";
                Plugin.Log.LogWarning(msg);
                ShowToast(msg, new Color(1f, 0.8f, 0.4f), 8f);
            }
        }

        private static void Recall(bool message)
        {
            if (!Active) { Active = null; return; }
            if (message) ShowToast($"{Active.Mon.DisplayName}, come back!", null, 2.5f);
            Active.Despawn();
            Active = null;
            FishMonStorage.Save();
        }

        private void ToggleSummon()
        {
            if (Active) { Recall(true); return; }
            var lead = FishMonStorage.Lead;
            if (lead == null)
            {
                ShowToast(FishMonStorage.Party.Count == 0
                    ? "You don't have any FishMon! Hook a fish with your rod, then pick it up (E) to catch it."
                    : $"All your FishMon have fainted! Revive them in the party menu ({Plugin.KeyName(Plugin.PartyKey)}).",
                    new Color(1f, 0.6f, 0.6f));
                return;
            }
            SendOut(lead);
        }

        private void SwapNext()
        {
            var party = FishMonStorage.Party;
            if (party.Count == 0) return;
            int start = Active ? party.IndexOf(Active.Mon) : -1;
            for (int i = 1; i <= party.Count; i++)
            {
                var m = party[((start + i) % party.Count + party.Count) % party.Count];
                if (!m.Fainted && (!Active || m != Active.Mon)) { SendOut(m); return; }
            }
            ShowToast("No other FishMon can fight right now.");
        }

        // a FishMon leaving in a trade: call it back first if it's out
        public static void ReleaseForTrade(FishMon m)
        {
            if (Active && Active.Mon == m) Recall(false);
            if (Instance && Instance._selected == m) Instance._selected = null;
            FishMonStorage.Release(m);
        }

        public static void OnCompanionFainted(Companion c)
        {
            var mon = c.Mon;
            mon.CurrentHp = 0;
            mon.Status.Clear();
            ShowToast($"{mon.DisplayName} fainted! It needs a Revive ({Plugin.KeyName(Plugin.PartyKey)} menu) before it can fight again.", new Color(1f, 0.5f, 0.5f), 5f);
            if (Active == c) Active = null;
            c.Despawn();
            FishMonStorage.Save();
            if (Plugin.AutoSendNext.Value && FishMonStorage.Lead != null && Instance)
                Instance._autoSendAt = Time.time + 1.5f;
            else if (FishMonStorage.Lead == null)
                ShowToast($"You have no FishMon left that can fight! Revive them in the party menu ({Plugin.KeyName(Plugin.PartyKey)}).", new Color(1f, 0.5f, 0.5f), 6f);
        }

        // UPDATE

        private void Update()
        {
            var player = Player.LocalPlayer;
            bool inGame = player && !MainMenuManager.IsInMenu;

            UpdateWorldSave(inGame);

            if (ModMenu.Listening) ModMenu.TickRebind(); // waiting for a new key: don't fire any hotkeys
            else if (inGame && WorldPrompt == null && !PauseManager.IsPaused && !ChatManager.IsTyping)
            {
                if (Input.GetKeyDown(Plugin.ModMenuKey.Value)) SetModMenu(!ModMenu.Open);
                else if (Input.GetKeyDown(Plugin.PartyKey.Value)) { if (ModMenu.Open) SetModMenu(false); SetPartyOpen(!PartyOpen); }
                if (Input.GetKeyDown(Plugin.SummonKey.Value)) ToggleSummon();
                if (Input.GetKeyDown(Plugin.SwapKey.Value)) SwapNext();
                if (!UIOpen && Input.GetKeyDown(Plugin.AttackKey.Value)) CommandAttack(player);
                if (!UIOpen && Input.GetKeyDown(Plugin.StayKey.Value)) CommandStayToggle();
                if (Input.GetKeyDown(KeyCode.Escape)) { if (ModMenu.Open) SetModMenu(false); else if (PartyOpen) SetPartyOpen(false); }
            }
            if (!inGame) { if (ModMenu.Open) SetModMenu(false); if (PartyOpen) SetPartyOpen(false); if (Active) Recall(false); }
            ModelPreview.Tick();
            if (inGame && !_checkedKeys) CheckKeyConflicts();

            // evolution while out: swap in the new model
            if (Active && Active.Mon.JustEvolved) RebuildActiveAfterEvolution();
            foreach (var m in FishMonStorage.Party) if (m.JustEvolved && (!Active || Active.Mon != m)) m.JustEvolved = false;

            // companion sanity: released / boxed / player died
            if (Active && (!FishMonStorage.Party.Contains(Active.Mon) || Active.Mon.Fainted || (player && player.Dying.IsDead)))
                Recall(false);

            if (_autoSendAt > 0f && Time.time >= _autoSendAt)
            {
                _autoSendAt = -1f;
                if (!Active && inGame && FishMonStorage.Lead != null) SendOut(FishMonStorage.Lead);
            }

            Regenerate();
            if (inGame) ScanNearbyForDex(player);
            try { Statuses.TickFoes(); } catch (Exception e) { Plugin.Log.LogWarning("FishMon status tick: " + e.Message); }
            try { Trade.Tick(); } catch (Exception e) { Plugin.Log.LogWarning("FishMon trade: " + e.Message); }
            try { Net.Tick(); } catch (Exception e) { Plugin.Log.LogWarning("FishMon sync: " + e.Message); }

            if (FishMonStorage.Dirty)
            {
                _dirtyTimer += Time.unscaledDeltaTime;
                if (_dirtyTimer > 5f) { _dirtyTimer = 0f; FishMonStorage.Save(); }
            }

            MuteGameUi(UIOpen && !PauseManager.IsPaused);

            if (UIOpen && Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            Toasts.RemoveAll(t => t.Until < Time.unscaledTime);
            Floaters.RemoveAll(f => Time.time - f.Start > f.Dur);
        }

        // per-world saves. only the host's own worlds can have one, guests always bring their shared FishMon
        private static string HostedWorld()
        {
            if (!Net.IsHost) return null;
            try { return SaveManager.CurServerSave?.Name; } catch { return null; }
        }

        private void UpdateWorldSave(bool inGame)
        {
            string world = inGame ? HostedWorld() : null;
            if (world == null)
            {
                if (WorldPrompt != null) SetWorldPrompt(null);
                if (FishMonStorage.CurrentWorld != null) SwitchSave(null);
                return;
            }
            if (FishMonStorage.HasChoice(world, out bool own))
            {
                string want = own ? world : null;
                if (FishMonStorage.CurrentWorld != want) SwitchSave(want);
                return;
            }
            // first time in this world: ask
            if (WorldPrompt != world)
            {
                Recall(false);
                SetWorldPrompt(world);
            }
        }

        private void SetWorldPrompt(string world)
        {
            bool was = UIOpen;
            WorldPrompt = world;
            if (UIOpen != was) { try { PlayerCamera.ToggleMouse(UIOpen); } catch { } }
        }

        private void SwitchSave(string world)
        {
            Trade.Abort();
            Recall(false);
            Participants.Clear();
            _selected = null;
            FishMonStorage.UseSave(world);
        }

        private void ChooseWorld(bool ownSave)
        {
            string world = WorldPrompt;
            if (world == null) return;
            FishMonStorage.SetChoice(world, ownSave);
            SetWorldPrompt(null);
            SwitchSave(ownSave ? world : null);
            ShowToast(ownSave
                ? $"\"{world}\" has its own FishMon now. Hook a fish and pick it up to catch your first one!"
                : "Your FishMon came with you!", new Color(0.6f, 1f, 0.7f), 6f);
        }

        private void DrawWorldPrompt()
        {
            var r = new Rect(VW / 2f - 280f, VH / 2f - 130f, 560f, 260f);
            Styles.Window(r, 60f);
            GUI.Label(new Rect(r.x + 24, r.y + 18, r.width - 48, 34), "New world!", Styles.Title);
            GUI.Label(new Rect(r.x + 24, r.y + 60, r.width - 48, 100),
                $"This is your first time in \"{WorldPrompt}\" with FishMon. Do you want to bring the FishMon you already have, " +
                "or start fresh with an empty party, Box and Fishdex just for this world?\n\n<size=13>Only asked once per world.</size>",
                Styles.BodyWrap);
            if (GUI.Button(new Rect(r.x + 24, r.yMax - 70, 244, 46), "Bring my FishMon", Styles.Button)) ChooseWorld(false);
            if (GUI.Button(new Rect(r.xMax - 268, r.yMax - 70, 244, 46), "Start fresh", Styles.Button)) ChooseWorld(true);
        }

        private void SetPartyOpen(bool open)
        {
            if (PartyOpen == open) return;
            bool was = UIOpen;
            PartyOpen = open;
            _confirmRelease = null;
            CommitRename();
            if (open && _selected == null) _selected = FishMonStorage.Party.FirstOrDefault();
            if (UIOpen != was) { try { PlayerCamera.ToggleMouse(UIOpen); } catch { } }
        }

        private void SetModMenu(bool open)
        {
            if (ModMenu.Open == open) return;
            bool was = UIOpen;
            ModMenu.Open = open;
            if (open) RefreshGameKeys();
            else
            {
                ModMenu.CancelRebind();
                try { Plugin.Instance.Config.Save(); } catch (Exception e) { Plugin.Log.LogError("Couldn't save config: " + e.Message); }
                FishMonStorage.Save();
            }
            if (UIOpen != was) { try { PlayerCamera.ToggleMouse(UIOpen); } catch { } }
        }

        private float _dirtyTimer, _seenScanTimer;
        private const float SeenRange = 15f;

        // creatures you get close to count as "seen" in the Fishdex
        private void ScanNearbyForDex(Player player)
        {
            _seenScanTimer -= Time.deltaTime;
            if (_seenScanTimer > 0f) return;
            _seenScanTimer = 1f;
            try
            {
                if (Fishdex.All.Count == 0) return; // also backfills from the game's journal the first time
                Vector3 pp = Companion.PlayerPos(player);
                foreach (var item in ItemManager.Items.Values)
                {
                    var c = item ? item.Creature : null;
                    if (!c || c.ExcludeFromJournal) continue;
                    var e = Fishdex.Get(c.ID);
                    if (e != null && e.Seen) continue;
                    Vector3 cp = c.Rig ? c.Rig.worldCenterOfMass : c.transform.position;
                    if ((cp - pp).sqrMagnitude < SeenRange * SeenRange) Fishdex.MarkSeen(c);
                }
            }
            catch { }
        }

        private void Regenerate()
        {
            float pct = Plugin.RegenPercentPerMinute.Value;
            if (pct <= 0f) return;
            float dt = Time.unscaledDeltaTime;
            foreach (var m in FishMonStorage.Party.Concat(FishMonStorage.Box))
            {
                if (m.Fainted) continue;                              // fainted stays fainted until revived
                if (m.CurrentHp >= m.MaxHp) continue;
                if (Active && Active.Mon == m && Active.Engaged) continue; // no healing mid-fight
                m.RegenCarry += m.MaxHp * pct / 100f / 60f * dt;
                if (m.RegenCarry >= 1f)
                {
                    int add = Mathf.FloorToInt(m.RegenCarry);
                    m.RegenCarry -= add;
                    m.CurrentHp = Mathf.Min(m.MaxHp, m.CurrentHp + add);
                    _regenDirty = true;
                }
            }
            _regenSaveTimer += dt;
            if (_regenDirty && _regenSaveTimer > 30f)
            {
                _regenSaveTimer = 0f;
                _regenDirty = false;
                FishMonStorage.Save();
            }
        }

        private void OnApplicationQuit()
        {
            FishMonStorage.Save();
            try { Plugin.Instance.Config.Save(); } catch { }
        }

        // GUI

        private float VW => Screen.width / _scale;
        private float VH => Screen.height / _scale;

        private void OnGUI()
        {
            Styles.Init();
            _scale = Mathf.Max(0.6f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(_scale, _scale, 1f));

            if (Player.LocalPlayer && !MainMenuManager.IsInMenu && !PauseManager.IsPaused)
            {
                DrawWorldText();
                if (WorldPrompt != null) DrawWorldPrompt();
                else if (ModMenu.Open) ModMenu.Draw(VW, VH, () => SetModMenu(false));
                else if (PartyOpen) DrawParty();
                else DrawHud();
            }
            DrawToasts();
            GUI.matrix = Matrix4x4.identity;
        }

        private Camera Cam
        {
            get
            {
                var p = Player.LocalPlayer;
                if (p && p.CurCam) return p.CurCam;
                return Camera.main;
            }
        }

        private bool ToGui(Camera cam, Vector3 world, out Vector2 gui)
        {
            gui = default;
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0.1f) return false;
            gui = new Vector2(sp.x / _scale, (Screen.height - sp.y) / _scale);
            return true;
        }

        private void DrawWorldText()
        {
            var cam = Cam;
            if (!cam) return;

            // nameplate over the companion
            if (Active)
            {
                var m = Active.Mon;
                Vector3 head = Active.transform.position + Vector3.up * (Active.HalfHeight + 0.5f);
                if (Vector3.Distance(cam.transform.position, head) < 60f && ToGui(cam, head, out var g))
                {
                    var r = new Rect(g.x - 80, g.y - 34, 160, 34);
                    Styles.Fill(r, new Color(0f, 0f, 0f, 0.55f));
                    string star = m.Shiny ? "<color=#ffd84a>★</color>" : "";
                    GUI.Label(new Rect(r.x + 6, r.y + 1, r.width - 12, 18), $"{star}{m.DisplayName}  <size=12>Lv.{m.Level}{Statuses.RichTag(m.Status)}</size>", Styles.Plate);
                    Styles.HpBar(new Rect(r.x + 6, r.y + 22, r.width - 12, 7), m.CurrentHp / (float)m.MaxHp);
                }

                // marker over the creature you ordered it to attack
                var t = Active.OrderedTarget;
                if (t && !t.IsDead)
                {
                    Vector3 top = (t.Rig ? t.Rig.worldCenterOfMass : t.transform.position) + Vector3.up * 1.2f;
                    if (ToGui(cam, top, out var tg))
                    {
                        float bob = Mathf.Sin(Time.time * 6f) * 4f;
                        var old = GUI.color;
                        GUI.color = new Color(1f, 0.35f, 0.3f);
                        GUI.Label(new Rect(tg.x - 60, tg.y - 40 + bob, 120, 40), "▼\n<size=12>TARGET</size>", Styles.Plate);
                        GUI.color = old;
                    }
                }

            }

            // status tags over foes (BRN, PAR...), from our FishMon or other players'
            foreach (var kv in Statuses.Shown())
            {
                var c = kv.Key;
                if (c.IsDead) continue;
                Vector3 top = (c.Rig ? c.Rig.worldCenterOfMass : c.transform.position) + Vector3.up * 2f;
                if (Vector3.Distance(cam.transform.position, top) > 80f || !ToGui(cam, top, out var fg)) continue;
                var fr = new Rect(fg.x - 28, fg.y - 12, 56, 24);
                Styles.Fill(fr, new Color(0f, 0f, 0f, 0.6f));
                var old = GUI.color;
                GUI.color = Statuses.ColorOf(kv.Value.Cond);
                GUI.Label(fr, Statuses.Tag(kv.Value.Cond), Styles.Plate);
                GUI.color = old;
            }

            // other players' FishMon
            foreach (var rc in Net.Remotes.Values)
            {
                if (!rc) continue;
                Vector3 head = rc.transform.position + Vector3.up * (rc.HalfHeight + 0.5f);
                if (Vector3.Distance(cam.transform.position, head) > 60f || !ToGui(cam, head, out var g)) continue;
                var owner = Net.PlayerFor(rc.OwnerId);
                string who = owner && !string.IsNullOrEmpty(owner.SteamName) ? owner.SteamName + "'s" : "Friend's";
                var r = new Rect(g.x - 80, g.y - 48, 160, 48);
                Styles.Fill(r, new Color(0f, 0f, 0f, 0.45f));
                GUI.Label(new Rect(r.x + 6, r.y + 1, r.width - 12, 16), $"<size=11><color=#9fc4ff>{who}</color></size>", Styles.Plate);
                string star = rc.Info.Shiny ? "<color=#ffd84a>★</color>" : "";
                GUI.Label(new Rect(r.x + 6, r.y + 15, r.width - 12, 18), $"{star}{rc.Info.Name}  <size=12>Lv.{rc.Info.Level}{Statuses.RichTag(rc.Status)}</size>", Styles.Plate);
                Styles.HpBar(new Rect(r.x + 6, r.y + 36, r.width - 12, 7), rc.HpFrac);
            }

            foreach (var f in Floaters)
            {
                float age = (Time.time - f.Start) / f.Dur;
                if (!ToGui(cam, f.Pos + Vector3.up * age * 1.2f, out var g)) continue;
                var c = f.Color; c.a = 1f - Mathf.Clamp01((age - 0.6f) / 0.4f);
                var size = Styles.Floater.CalcSize(new GUIContent(f.Text));
                var r = new Rect(g.x - size.x / 2f, g.y - size.y / 2f, size.x, size.y);
                var old = GUI.color;
                GUI.color = new Color(0, 0, 0, c.a * 0.8f);
                GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), f.Text, Styles.Floater);
                GUI.color = c;
                GUI.Label(r, f.Text, Styles.Floater);
                GUI.color = old;
            }
        }

        private void DrawToasts()
        {
            float y = 90f;
            foreach (var t in Toasts)
            {
                var size = Styles.Toast.CalcSize(new GUIContent(t.Text));
                float w = Mathf.Min(size.x + 40f, VW - 40f);
                var r = new Rect((VW - w) / 2f, y, w, 40f);
                Styles.Fill(r, new Color(0f, 0f, 0f, 0.75f));
                var old = GUI.color; GUI.color = t.Color;
                GUI.Label(r, t.Text, Styles.Toast);
                GUI.color = old;
                y += 46f;
            }
        }

        private void DrawHud()
        {
            bool bossUp = BossManager.Boss && !BossManager.Boss.IsDead;
            if (Active)
            {
                var m = Active.Mon;
                var r = new Rect(20f, VH - 144f, 420f, 120f);
                Styles.Fill(r, new Color(0.06f, 0.08f, 0.12f, 0.8f));
                Styles.Border(r, Active.Engaged ? new Color(1f, 0.5f, 0.35f) : Styles.Accent, 2f);
                string star = m.Shiny ? "<color=#ffd84a>★ </color>" : "";
                GUI.Label(new Rect(r.x + 12, r.y + 6, 260, 26), star + m.DisplayName, Styles.BodyBold);
                GUI.Label(new Rect(r.xMax - 92, r.y + 6, 80, 26), "Lv. " + m.Level, Styles.BodyRight);
                Styles.HpBar(new Rect(r.x + 12, r.y + 38, r.width - 110, 12), m.CurrentHp / (float)m.MaxHp);
                GUI.Label(new Rect(r.xMax - 94, r.y + 32, 82, 22), $"{m.CurrentHp}/{m.MaxHp}", Styles.SmallRight);
                Styles.Bar(new Rect(r.x + 12, r.y + 56, r.width - 110, 5), m.ExpProgress, new Color(0.35f, 0.7f, 1f));
                string stageTag = m.Stage > 0 ? $"  <size=12><color=#ffd84a>{new string('◆', m.Stage)}</color></size>" : "";
                GUI.Label(new Rect(r.x + 12 + Styles.BodyBold.CalcSize(new GUIContent(star + m.DisplayName)).x, r.y + 6, 120, 26), stageTag + Statuses.RichTag(m.Status), Styles.Small);

                string status;
                if (Active.Engaged && Active.Target != null)
                    status = Active.OrderedTarget ? $"<color=#ff8a7a>Attacking {Active.Target.Name} (your order)</color>"
                                                  : $"<color=#ffb38a>Fighting {Active.Target.Name}!</color>";
                else status = Active.Staying ? "<color=#c7d2ff>Staying put</color>" : "Following you";
                var next = Evolutions.Next(m);
                string evo = next != null ? $"   <color=#9aa6bd>evolves at Lv. {next.Level}</color>" : "   <color=#ffd84a>fully evolved</color>";
                GUI.Label(new Rect(r.x + 12, r.y + 66, r.width - 24, 24), status + evo, Styles.Small);
                GUI.Label(new Rect(r.x + 12, r.y + 90, r.width - 24, 22),
                    $"<color=#9aa6bd>[{Plugin.KeyName(Plugin.AttackKey)}] attack target  [{Plugin.KeyName(Plugin.StayKey)}] stay/follow  [{Plugin.KeyName(Plugin.SummonKey)}] recall  [{Plugin.KeyName(Plugin.SwapKey)}] swap</color>", Styles.Small);
            }
            else if (bossUp && FishMonStorage.Lead != null)
            {
                var lead = FishMonStorage.Lead;
                var r = new Rect(20f, VH - 70f, 420f, 44f);
                Styles.Fill(r, new Color(0f, 0f, 0f, 0.6f));
                GUI.Label(r, $"  [{Plugin.KeyName(Plugin.SummonKey)}]  Send out {lead.DisplayName} (Lv. {lead.Level})!", Styles.Body);
            }
        }

        // party

        private void DrawParty()
        {
            float w = 1040f, h = 700f;
            var win = new Rect((VW - w) / 2f, (VH - h) / 2f, w, h);
            Styles.Window(win, 60f);

            GUI.Label(new Rect(win.x + 20, win.y + 12, 400, 36), "FishMon", Styles.Title);
            var list = _showingBox ? FishMonStorage.Box : FishMonStorage.Party;
            bool other = _showingDex || _showingTrade;
            if (Styles.Tab(new Rect(win.x + 200, win.y + 14, 140, 34), $"Party ({FishMonStorage.Party.Count}/{Plugin.MaxPartySize.Value})", !_showingBox && !other)) { _showingBox = false; _showingDex = _showingTrade = false; _confirmRelease = null; }
            if (Styles.Tab(new Rect(win.x + 346, win.y + 14, 110, 34), $"Box ({FishMonStorage.Box.Count})", _showingBox && !other)) { _showingBox = true; _showingDex = _showingTrade = false; _confirmRelease = null; }
            if (Styles.Tab(new Rect(win.x + 462, win.y + 14, 170, 34), $"Fishdex ({Fishdex.CaughtCount}/{Fishdex.Total})", _showingDex)) { _showingDex = true; _showingTrade = false; _confirmRelease = null; CommitRename(); }
            int invites = Trade.Invites.Count;
            if (Styles.Tab(new Rect(win.x + 638, win.y + 14, 110, 34), invites > 0 ? $"Trade <color=#ffd84a>({invites})</color>" : "Trade", _showingTrade)) { _showingTrade = true; _showingDex = false; _confirmRelease = null; CommitRename(); }

            if (GUI.Button(new Rect(win.x + 756, win.y + 14, 100, 34), "Settings", Styles.Button)) { SetModMenu(true); return; }
            GUI.enabled = !(Active && Active.Engaged);
            if (GUI.Button(new Rect(win.x + 862, win.y + 14, 124, 34), CostLabel("Heal All", Plugin.HealAllCost.Value), Styles.Button)) HealAll(Plugin.HealAllCost.Value);
            GUI.enabled = true;
            if (GUI.Button(new Rect(win.xMax - 48, win.y + 14, 36, 34), "X", Styles.Button)) { SetPartyOpen(false); return; }

            if (_showingDex) { DrawDex(win, h); return; }
            if (_showingTrade) { DrawTrade(win, h); return; }

            var listRect = new Rect(win.x + 16, win.y + 62, 330, h - 78);
            Styles.Fill(listRect, Styles.PanelDark);
            if (list.Count == 0)
            {
                GUI.Label(new Rect(listRect.x + 12, listRect.y + 12, listRect.width - 24, 200),
                    _showingBox ? "The Box is empty." : "No FishMon yet!\n\nHook a fish with your rod, reel it in, then pick it up (E) to catch it.", Styles.BodyWrap);
            }
            else
            {
                float rowH = 66f;
                var view = new Rect(0, 0, listRect.width - 20, list.Count * (rowH + 6));
                _listScroll = GUI.BeginScrollView(listRect, _listScroll, view);
                for (int i = 0; i < list.Count; i++)
                {
                    var r = new Rect(6, i * (rowH + 6) + 6, view.width - 8, rowH);
                    DrawListRow(r, list[i], i == 0 && !_showingBox);
                }
                GUI.EndScrollView();
            }

            float dx = listRect.xMax + 16;
            if (Styles.Tab(new Rect(dx, win.y + 62, 140, 32), "Summary", _detailTab == 0)) { _detailTab = 0; _moveSlot = -1; }
            if (Styles.Tab(new Rect(dx + 146, win.y + 62, 140, 32), "Moves", _detailTab == 1)) { _detailTab = 1; _moveSlot = -1; }
            if (Styles.Tab(new Rect(dx + 292, win.y + 62, 140, 32), "Item", _detailTab == 2)) { _detailTab = 2; _moveSlot = -1; }
            var detail = new Rect(dx, win.y + 100, win.xMax - listRect.xMax - 32, h - 116);
            Styles.Fill(detail, Styles.PanelDark);
            if (_selected != null && (FishMonStorage.Party.Contains(_selected) || FishMonStorage.Box.Contains(_selected)))
                DrawDetails(detail, _selected);
            else
                GUI.Label(new Rect(detail.x + 16, detail.y + 16, 400, 30), "Select a FishMon.", Styles.Dim);
        }

        private void DrawListRow(Rect r, FishMon m, bool isLead)
        {
            bool sel = m == _selected;
            Styles.Fill(r, sel ? new Color(0.25f, 0.45f, 0.7f, 0.9f) : new Color(1f, 1f, 1f, 0.06f));
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { _selected = m; _confirmRelease = null; _moveSlot = -1; CommitRename(); }
            string star = m.Shiny ? "<color=#ffd84a>★</color> " : "";
            string tag = Active && Active.Mon == m ? "  <color=#9fffb0>OUT</color>" : isLead ? "  <color=#9fd3ff>LEAD</color>" : "";
            GUI.Label(new Rect(r.x + 10, r.y + 4, r.width - 90, 28), star + m.DisplayName + tag, Styles.BodyBold);
            GUI.Label(new Rect(r.xMax - 80, r.y + 4, 72, 28), "Lv. " + m.Level, Styles.BodyRight);
            Styles.TypeChip(new Rect(r.x + 10, r.y + 34, 70, 20), m.Type1);
            if (m.Type2 != MonType.None) Styles.TypeChip(new Rect(r.x + 84, r.y + 34, 70, 20), m.Type2);
            Styles.HpBar(new Rect(r.x + 164, r.y + 40, r.width - 176, 10), m.CurrentHp / (float)m.MaxHp);
            if (m.Fainted) GUI.Label(new Rect(r.x + 164, r.y + 22, 120, 20), "<color=#ff7070>FAINTED</color>", Styles.Small);
        }

        private void DrawDetails(Rect d, FishMon m)
        {
            float x = d.x + 20, y = d.y + 14, w = d.width - 40;

            if (_renameTarget != m) { _renameTarget = m; _renameBuffer = m.Nickname; }
            string star = m.Shiny ? "<color=#ffd84a>★ </color>" : "";
            GUI.Label(new Rect(x, y, w - 200, 36), star + m.DisplayName, Styles.Title);
            GUI.Label(new Rect(x + w - 190, y + 4, 190, 30), $"Lv. {m.Level}", Styles.TitleRight);
            y += 38;
            string origin = string.IsNullOrEmpty(m.TradedFrom) ? $"caught {m.CaughtOn}" : $"traded from {m.TradedFrom}";
            GUI.Label(new Rect(x, y, w, 24), $"{m.SpeciesName}  ·  {Natures.All[m.Nature].name} nature  ·  {m.Weight:0.##} kg  ·  {origin}", Styles.Dim);
            y += 28;
            Styles.TypeChip(new Rect(x, y, 90, 24), m.Type1);
            if (m.Type2 != MonType.None) Styles.TypeChip(new Rect(x + 96, y, 90, 24), m.Type2);
            GUI.Label(new Rect(x + 200, y, 120, 24), "Nickname:", Styles.Small);
            _renameBuffer = GUI.TextField(new Rect(x + 280, y, 200, 24), _renameBuffer ?? "", 16, Styles.Field);
            y += 34;

            if (_detailTab == 1) { DrawMoveEditor(new Rect(x, y, w, d.yMax - y - 14), m); return; }
            if (_detailTab == 2) { DrawItemTab(new Rect(x, y, w, d.yMax - y - 14), m); return; }

            GUI.Label(new Rect(x, y, 60, 20), "HP", Styles.Small);
            Styles.HpBar(new Rect(x + 40, y + 5, w - 160, 12), m.CurrentHp / (float)m.MaxHp);
            GUI.Label(new Rect(x + w - 110, y, 110, 22), $"{m.CurrentHp}/{m.MaxHp}", Styles.SmallRight);
            y += 24;
            GUI.Label(new Rect(x, y, 60, 20), "EXP", Styles.Small);
            Styles.Bar(new Rect(x + 40, y + 6, w - 160, 8), m.ExpProgress, new Color(0.35f, 0.7f, 1f));
            GUI.Label(new Rect(x + w - 110, y, 110, 22), m.Level >= 100 ? "MAX" : $"{m.ExpToNext} to next", Styles.SmallRight);
            y += 32;

            var nat = Natures.All[m.Nature];
            float c0 = x, c1 = x + 110, c2 = x + 175, c3 = x + 280, c4 = x + 350;
            GUI.Label(new Rect(c0, y, 100, 22), "Stat", Styles.SmallBold);
            GUI.Label(new Rect(c1, y, 60, 22), "Value", Styles.SmallBold);
            GUI.Label(new Rect(c2, y, 100, 22), "Base", Styles.SmallBold);
            GUI.Label(new Rect(c3, y, 60, 22), "IV", Styles.SmallBold);
            GUI.Label(new Rect(c4, y, 200, 22), "Judge", Styles.SmallBold);

            // held item, in the free space right of the stat table
            var itemBox = new Rect(x + w - 168, y, 168, 104);
            Styles.Fill(itemBox, new Color(1f, 1f, 1f, 0.05f));
            GUI.Label(new Rect(itemBox.x + 10, itemBox.y + 6, itemBox.width - 20, 20), "Held item", Styles.Dim);
            var heldInfo = m.ItemInfo;
            GUI.Label(new Rect(itemBox.x + 10, itemBox.y + 26, itemBox.width - 20, 24),
                heldInfo != null ? $"<b><color=#ffd84a>{heldInfo.Name}</color></b>" : "<color=#9aa6bd>None</color>", Styles.Small);
            if (GUI.Button(new Rect(itemBox.x + 10, itemBox.yMax - 40, itemBox.width - 20, 30), heldInfo != null ? "Change" : "Give an item", Styles.Button))
            { _detailTab = 2; _moveSlot = -1; }
            y += 24;
            for (int s = 0; s < 6; s++)
            {
                string label = Stat.Long[s];
                if (nat.up == s) label = $"<color=#ff8a8a>{label} ▲</color>";
                else if (nat.down == s) label = $"<color=#8ab8ff>{label} ▼</color>";
                GUI.Label(new Rect(c0, y, 110, 22), label, Styles.Small);
                GUI.Label(new Rect(c1, y, 60, 22), (s == Stat.HP ? m.MaxHp : m.CalcStat(s)).ToString(), Styles.SmallBold);
                Styles.Bar(new Rect(c2, y + 7, 95, 8), m.BaseStats[s] / 160f, new Color(0.9f, 0.75f, 0.35f));
                string ivCol = m.IVs[s] >= 31 ? "#ffd84a" : m.IVs[s] >= 26 ? "#9fff9f" : m.IVs[s] <= 5 ? "#ff8a8a" : "#ffffff";
                GUI.Label(new Rect(c3, y, 60, 22), $"<color={ivCol}>{m.IVs[s]}</color>", Styles.Small);
                GUI.Label(new Rect(c4, y, 200, 22), FishMon.StatJudge(m.IVs[s]), Styles.Dim);
                y += 22;
            }
            var nextEvo = Evolutions.Next(m);
            string evoText = nextEvo != null ? $"Evolves into {nextEvo.Name} at Lv. {nextEvo.Level}" : "Fully evolved";
            GUI.Label(new Rect(x, y + 2, w, 22), $"IV total {m.IVTotal}/186 — {m.IVJudge()}   ·   Bosses defeated: {m.BossesDefeated}   ·   {evoText}", Styles.Dim);
            y += 32;

            var ab = m.AbilityInfo;
            if (ab != null)
            {
                string col = Abilities.RarityColor(ab.Rarity);
                string soon = ab.Implemented ? "" : "  <color=#9aa6bd>(coming soon)</color>";
                GUI.Label(new Rect(x, y, w - 330, 24), $"Ability: <b><color={col}>{ab.Name}</color></b>  <color={col}><size=13>{ab.Rarity}</size></color>{soon}", Styles.Small);
                GUI.Label(new Rect(x, y + 22, w - 330, 22), m.AbilityOn ? ab.Desc : "<color=#ff9a9a>Switched off.</color> " + ab.Desc, Styles.Dim);
                if (GUI.Button(new Rect(x + w - 320, y + 4, 100, 30), m.AbilityOn ? "<color=#9fffb0>ON</color>" : "<color=#ff9a9a>OFF</color>", Styles.Button))
                {
                    m.AbilityOn = !m.AbilityOn;
                    FishMonStorage.SaveSoon();
                }
                if (GUI.Button(new Rect(x + w - 210, y + 4, 210, 30), CostLabel("Reroll ability", Plugin.AbilityRerollCost.Value), Styles.Button))
                    RerollAbility(m);
                y += 50;
            }

            GUI.Label(new Rect(x, y, w, 22), m.CustomMoves ? "Moves <color=#9aa6bd>(picked by you, change them in the Moves tab)</color>"
                                                           : "Moves <color=#9aa6bd>(auto, pick your own in the Moves tab)</color>", Styles.SmallBold);
            y += 24;
            var moves = m.GetMoves();
            for (int i = 0; i < moves.Count; i++)
            {
                var mv = moves[i];
                var r = new Rect(x + (i % 2) * (w / 2f), y + (i / 2) * 34, w / 2f - 8, 30);
                Styles.Fill(r, TypeChart.ColorOf(mv.Type) * new Color(1, 1, 1, 0.55f));
                GUI.Label(new Rect(r.x + 8, r.y, r.width - 16, r.height), $"<b>{mv.Name}</b>  <size=13>{mv.Type} · {mv.Category} · Pow {mv.Power} · Acc {mv.Accuracy}</size>", Styles.Small);
            }

            float bx = x, bw = 150, bh = 34;
            y = d.yMax - bh - 14;
            bool inParty = FishMonStorage.Party.Contains(m);
            if (inParty)
            {
                bool isOut = Active && Active.Mon == m;
                if (m.Fainted)
                {
                    if (GUI.Button(new Rect(bx, y, bw, bh), CostLabel("Revive", Plugin.ReviveCost.Value), Styles.Button)) Revive(m);
                }
                else if (GUI.Button(new Rect(bx, y, bw, bh), isOut ? "Recall" : "Send Out", Styles.Button))
                {
                    if (isOut) Recall(true); else SendOut(m);
                }
                GUI.enabled = FishMonStorage.Party.IndexOf(m) != 0;
                if (GUI.Button(new Rect(bx + bw + 10, y, bw, bh), "Make Lead", Styles.Button)) FishMonStorage.MakeLead(m);
                GUI.enabled = FishMonStorage.Party.Count > 1;
                if (GUI.Button(new Rect(bx + (bw + 10) * 2, y, bw, bh), "Send to Box", Styles.Button)) FishMonStorage.MoveToBox(m);
                GUI.enabled = true;
            }
            else
            {
                GUI.enabled = FishMonStorage.Party.Count < Plugin.MaxPartySize.Value;
                if (GUI.Button(new Rect(bx, y, bw, bh), "Add to Party", Styles.Button)) FishMonStorage.MoveToParty(m);
                GUI.enabled = true;
                if (m.Fainted && GUI.Button(new Rect(bx + bw + 10, y, bw, bh), CostLabel("Revive", Plugin.ReviveCost.Value), Styles.Button)) Revive(m);
            }
            if (_confirmRelease == m)
            {
                if (GUI.Button(new Rect(d.xMax - 20 - bw * 2 - 10, y - bh - 8, bw, bh), "Yes, release", Styles.DangerButton))
                {
                    var kept = m.ItemInfo;
                    ShowToast(kept != null ? $"Bye bye, {m.DisplayName}! Its {kept.Name} went back in your bag." : $"Bye bye, {m.DisplayName}!");
                    if (kept != null) { m.Item = HeldItem.None; FishMonStorage.AddToBag(kept.Id); }
                    FishMonStorage.Release(m);
                    _selected = FishMonStorage.Party.FirstOrDefault() ?? FishMonStorage.Box.FirstOrDefault();
                    _confirmRelease = null;
                }
                if (GUI.Button(new Rect(d.xMax - 20 - bw, y - bh - 8, bw, bh), "Cancel", Styles.Button)) _confirmRelease = null;
            }
            else if (GUI.Button(new Rect(d.xMax - 20 - 110, y, 110, bh), "Release", Styles.DangerButton))
            {
                _confirmRelease = m;
            }
        }

        // moves tab

        private int _detailTab;          // 0 = summary, 1 = moves, 2 = held item
        private int _moveSlot = -1;      // selected move slot in the Moves tab
        private Vector2 _moveScroll;

        private void DrawMoveEditor(Rect r, FishMon m)
        {
            float x = r.x, y = r.y, w = r.width;
            m.PruneMoves();
            var current = m.GetMoves();

            string mode = m.CustomMoves
                ? "<b>Custom moveset.</b> It only uses the moves you've picked."
                : "<b>Auto.</b> It always uses its 4 strongest moves. Change one to pick your own.";
            GUI.Label(new Rect(x, y, w - 170, 26), mode, Styles.Small);
            if (m.CustomMoves && GUI.Button(new Rect(x + w - 160, y - 2, 160, 30), "Back to Auto", Styles.Button))
            {
                m.KnownMoves = null; _moveSlot = -1; MovesChanged(m);
                ShowToast($"{m.DisplayName} will pick its own moves again.");
                return;
            }
            y += 34;

            for (int i = 0; i < 4; i++)
            {
                var sr = new Rect(x + (i % 2) * (w / 2f), y + (i / 2) * 50, w / 2f - 8, 44);
                bool sel = _moveSlot == i;
                if (i < current.Count)
                {
                    var mv = current[i];
                    Styles.Fill(sr, TypeChart.ColorOf(mv.Type) * new Color(1, 1, 1, 0.55f));
                    GUI.Label(new Rect(sr.x + 8, sr.y + 1, sr.width - 16, 22), $"<b>{mv.Name}</b>", Styles.Small);
                    GUI.Label(new Rect(sr.x + 8, sr.y + 21, sr.width - 16, 20), $"<size=12>{mv.Type} · {mv.Category} · Pow {mv.Power} · Acc {mv.Accuracy}</size>", Styles.Small);
                }
                else
                {
                    Styles.Fill(sr, new Color(1f, 1f, 1f, 0.05f));
                    GUI.Label(sr, "<color=#7f8aa0>(empty slot)</color>", Styles.Center);
                }
                if (sel) Styles.Border(sr, Color.white, 2f);
                if (GUI.Button(sr, GUIContent.none, GUIStyle.none)) _moveSlot = sel ? -1 : i;
            }
            y += 104;

            string hint = _moveSlot < 0 ? "Click a slot above, then click a move below to put it there."
                : _moveSlot < current.Count ? $"Click a move below to replace <b>{current[_moveSlot].Name}</b>."
                : "Click a move below to fill this slot.";
            GUI.Label(new Rect(x, y, w - 130, 24), hint, Styles.Dim);
            if (_moveSlot >= 0 && _moveSlot < current.Count && current.Count > 1 &&
                GUI.Button(new Rect(x + w - 120, y - 3, 120, 28), "Forget", Styles.DangerButton))
            {
                m.BeginCustomMoves();
                string gone = current[_moveSlot].Name;
                m.KnownMoves.Remove(gone);
                _moveSlot = -1;
                MovesChanged(m);
                ShowToast($"{m.DisplayName} forgot {gone}.");
                return;
            }
            y += 30;

            var pool = Moves.PoolFor(m.Type1, m.Type2);
            var listR = new Rect(x, y, w, r.yMax - y);
            Styles.Fill(listR, new Color(0f, 0f, 0f, 0.25f));
            const float rowH = 30f;
            var view = new Rect(0, 0, listR.width - 20, pool.Count * (rowH + 2) + 4);
            _moveScroll = GUI.BeginScrollView(listR, _moveScroll, view);
            for (int i = 0; i < pool.Count; i++)
            {
                var mv = pool[i];
                var rr = new Rect(4, i * (rowH + 2) + 2, view.width - 8, rowH);
                bool can = mv.LearnLevel <= m.Level;
                bool known = current.Contains(mv);
                Styles.Fill(rr, known ? new Color(0.3f, 0.5f, 0.8f, 0.35f) : new Color(1f, 1f, 1f, can ? 0.06f : 0.02f));
                Styles.TypeChip(new Rect(rr.x + 4, rr.y + 5, 74, 20), mv.Type);
                string col = can ? "#ffffff" : "#6f7890";
                GUI.Label(new Rect(rr.x + 86, rr.y, 140, rowH), $"<color={col}><b>{mv.Name}</b></color>", Styles.Small);

                var traits = new List<string>();
                if (mv.Type == m.Type1 || mv.Type == m.Type2) traits.Add("same type +50%");
                string t = Moves.Traits(mv);
                if (t.Length > 0) traits.Add(t);
                string tr = traits.Count > 0 ? " · " + string.Join(" · ", traits) : "";
                GUI.Label(new Rect(rr.x + 226, rr.y, rr.width - 316, rowH), $"<color={col}><size=12>{mv.Category} · Pow {mv.Power} · Acc {mv.Accuracy}{tr}</size></color>", Styles.Small);

                string right = known ? "<color=#9fffb0>known</color>" : can ? "<color=#9aa6bd>learn</color>" : $"<color=#ff9a7a>Lv. {mv.LearnLevel}</color>";
                GUI.Label(new Rect(rr.xMax - 84, rr.y, 80, rowH), right, Styles.SmallRight);
                if (can && !known && GUI.Button(rr, GUIContent.none, GUIStyle.none)) TeachMove(m, mv);
            }
            GUI.EndScrollView();
        }

        private void TeachMove(FishMon m, Move mv)
        {
            m.BeginCustomMoves();
            var km = m.KnownMoves;
            if (_moveSlot >= 0 && _moveSlot < km.Count)
            {
                string old = km[_moveSlot];
                km[_moveSlot] = mv.Name;
                ShowToast($"1, 2 and... Poof! {m.DisplayName} forgot {old} and learned {mv.Name}!", new Color(0.7f, 0.85f, 1f));
            }
            else if (km.Count < 4)
            {
                km.Add(mv.Name);
                ShowToast($"{m.DisplayName} learned {mv.Name}!", new Color(0.7f, 0.85f, 1f));
            }
            else
            {
                ShowToast("All 4 move slots are full. Click the slot you want to replace first.");
                return;
            }
            _moveSlot = -1;
            MovesChanged(m);
        }

        private static void MovesChanged(FishMon m)
        {
            FishMonStorage.Save();
            if (Active && Active.Mon == m) Active.RefreshMoves();
        }

        // fishdex

        private bool _showingDex;
        private int _dexSel;
        private bool _dexDrip;
        private Vector2 _dexScroll, _dexEntryScroll;

        private void DrawDex(Rect win, float h)
        {
            var all = Fishdex.All;
            var listRect = new Rect(win.x + 16, win.y + 62, 330, h - 78);
            Styles.Fill(listRect, Styles.PanelDark);
            if (all.Count == 0)
            {
                GUI.Label(new Rect(listRect.x + 12, listRect.y + 12, listRect.width - 24, 200), "The Fishdex fills in once you're in a game.", Styles.BodyWrap);
                return;
            }

            const float rowH = 40f;
            var view = new Rect(0, 0, listRect.width - 20, all.Count * (rowH + 4) + 4);
            _dexScroll = GUI.BeginScrollView(listRect, _dexScroll, view);
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                var e = Fishdex.Get(c.ID);
                bool seen = e != null && e.Seen, caught = e != null && e.Caught > 0;
                var r = new Rect(6, i * (rowH + 4) + 4, view.width - 8, rowH);
                bool sel = i == _dexSel;
                Styles.Fill(r, sel ? new Color(0.25f, 0.45f, 0.7f, 0.9f) : new Color(1f, 1f, 1f, seen ? 0.06f : 0.03f));
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) _dexSel = i;
                GUI.Label(new Rect(r.x + 8, r.y, 52, rowH), $"<color=#9aa6bd>#{i + 1:000}</color>", Styles.Small);
                string name = seen ? Species.SafeName(c) : "<color=#6f7890>???</color>";
                GUI.Label(new Rect(r.x + 60, r.y, r.width - 150, rowH), name, seen ? Styles.BodyBold : Styles.Body);
                string status;
                if (c.BossType != BossType.None) status = (e != null && e.Defeated > 0) ? "<color=#ff9a7a>BOSS ✓</color>" : seen ? "<color=#ff9a7a>BOSS</color>" : "";
                else if (caught) status = (e.DripCaught ? "<color=#ffd84a>★</color> " : "") + "<color=#9fffb0>CAUGHT</color>";
                else status = seen ? "<color=#9aa6bd>seen</color>" : "";
                GUI.Label(new Rect(r.xMax - 96, r.y, 90, rowH), status, Styles.SmallRight);
            }
            GUI.EndScrollView();

            var d = new Rect(listRect.xMax + 16, win.y + 62, win.xMax - listRect.xMax - 32, h - 78);
            Styles.Fill(d, Styles.PanelDark);
            _dexSel = Mathf.Clamp(_dexSel, 0, all.Count - 1);
            DrawDexEntry(d, all[_dexSel], _dexSel + 1);
            GUI.Label(new Rect(d.x + 20, d.yMax - 30, d.width - 40, 24),
                $"Seen {Fishdex.SeenCount}  ·  Caught {Fishdex.CaughtCount} of {Fishdex.Total} catchable creatures", Styles.Dim);
        }

        private void DrawDexEntry(Rect d, Creature c, int num)
        {
            float x = d.x + 20, y = d.y + 14, w = d.width - 40;
            var e = Fishdex.Get(c.ID);
            bool seen = e != null && e.Seen, caught = e != null && e.Caught > 0;
            bool boss = c.BossType != BossType.None;

            if (!seen)
            {
                GUI.Label(new Rect(x, y, w, 36), $"#{num:000}  ???", Styles.Title);
                GUI.Label(new Rect(x, y + 50, w, 200),
                    "You haven't seen this creature yet.\n\nA creature goes in the Fishdex when you get close to one, hook one, or fight one. " +
                    "Anything you've already killed in the game's own journal counts as seen.", Styles.BodyWrap);
                return;
            }

            var info = Fishdex.InfoFor(c);
            GUI.Label(new Rect(x, y, w - 160, 36), $"#{num:000}  {info.Name}", Styles.Title);

            // spinning 3D model on the right (drag to turn it)
            var pr = new Rect(d.xMax - 250, d.y + 56, 230, 230);
            Styles.Fill(pr, new Color(1f, 1f, 1f, 0.04f));
            bool dripView = _dexDrip && e.DripCaught;
            ModelPreview.Draw(pr, c.ID, dripView);
            if (e.DripCaught && GUI.Button(new Rect(pr.x + 6, pr.yMax - 30, 90, 24), dripView ? "<color=#ffd84a>★ Drip</color>" : "Normal", Styles.Button)) _dexDrip = !_dexDrip;
            float wl = w - 250; // text column width left of the model
            GUI.Label(new Rect(x + w - 160, y + 4, 160, 30), boss ? "<color=#ff9a7a>BOSS</color>" : caught ? "<color=#9fffb0>CAUGHT</color>" : "<color=#9aa6bd>SEEN</color>", Styles.TitleRight);
            y += 44;

            if (boss)
            {
                GUI.Label(new Rect(x, y, wl, 24), $"Worth ${info.Worth}  ·  Bosses can't be caught.", Styles.Dim);
                y += 30;
                GUI.Label(new Rect(x, y, wl, 24), $"Defeated {e.Defeated} time{(e.Defeated == 1 ? "" : "s")} while you were there.", Styles.Body);
                return;
            }

            Styles.TypeChip(new Rect(x, y, 90, 24), info.Type1);
            if (info.Type2 != MonType.None) Styles.TypeChip(new Rect(x + 96, y, 90, 24), info.Type2);
            y += 30;
            GUI.Label(new Rect(x, y, wl, 22), $"Worth ${info.Worth}  ·  about {info.Weight:0.##} kg", Styles.Dim);
            if (c.Bird) { y += 22; GUI.Label(new Rect(x, y, wl, 22), "<color=#ff9a7a>Birds can't be caught.</color>", Styles.Dim); }
            y += 30;

            var chain = Evolutions.ChainFor(info.Name);
            string evo = info.Name;
            foreach (var s in chain)
            {
                var gained = s.NewType2 != MonType.None ? s.NewType2 : s.NewType1;
                evo += $"  →  {s.Name} <size=12><color=#9aa6bd>(Lv. {s.Level}{(gained != MonType.None ? ", +" + gained : "")})</color></size>";
            }
            GUI.Label(new Rect(x, y, wl, 22), "Evolution", Styles.SmallBold);
            y += 22;
            GUI.Label(new Rect(x, y, wl, 66), evo.Replace("  →  ", "\n→ "), Styles.SmallWrap);
            y += 72;

            if (!caught)
            {
                GUI.Label(new Rect(x, y, wl, 80), "Catch one to see its base stats and the moves it learns.", Styles.BodyWrap);
                return;
            }

            GUI.Label(new Rect(x, y, 200, 22), "Base stats", Styles.SmallBold);
            y += 24;
            for (int s = 0; s < 6; s++)
            {
                GUI.Label(new Rect(x, y, 120, 22), Stat.Long[s], Styles.Small);
                GUI.Label(new Rect(x + 120, y, 50, 22), info.BaseStats[s].ToString(), Styles.SmallBold);
                Styles.Bar(new Rect(x + 170, y + 7, Mathf.Min(200f, wl - 180f), 8), info.BaseStats[s] / 160f, new Color(0.9f, 0.75f, 0.35f));
                y += 22;
            }
            GUI.Label(new Rect(x, y, 300, 22), $"Total {info.BaseStats.Sum()}", Styles.Dim);
            y += 28;

            int owned = FishMonStorage.Party.Concat(FishMonStorage.Box).Count(m => m.SpeciesId == c.ID);
            GUI.Label(new Rect(x, y, w, 22),
                $"Caught {e.Caught}×  ·  Best IVs {Mathf.Max(0, e.BestIVs)}/186  ·  Highest Lv. {e.BestLevel}" +
                (e.DripCaught ? "  ·  <color=#ffd84a>★ Drip caught</color>" : ""), Styles.Small);
            y += 22;
            GUI.Label(new Rect(x, y, w, 22), $"First caught {e.FirstCaught}  ·  You own {owned} right now", Styles.Dim);
            y += 30;

            GUI.Label(new Rect(x, y, w, 22), "Moves it can learn", Styles.SmallBold);
            y += 24;
            var pool = Fishdex.MovePool(info);
            var area = new Rect(x, y, w, d.yMax - 40 - y);
            float cellW = (w - 20) / 3f;
            int rows = Mathf.CeilToInt(pool.Count / 3f);
            var view = new Rect(0, 0, w - 20, rows * 28);
            _dexEntryScroll = GUI.BeginScrollView(area, _dexEntryScroll, view);
            for (int i = 0; i < pool.Count; i++)
            {
                var mv = pool[i];
                var cr = new Rect((i % 3) * cellW, (i / 3) * 28, cellW - 6, 24);
                Styles.Fill(cr, TypeChart.ColorOf(mv.Type) * new Color(1, 1, 1, 0.45f));
                GUI.Label(new Rect(cr.x + 6, cr.y, cr.width - 8, cr.height), $"<size=12><color=#d0d8e8>Lv {mv.LearnLevel}</color></size>  <b>{mv.Name}</b>", Styles.Small);
            }
            GUI.EndScrollView();
        }

        private void CommitRename()
        {
            if (_renameTarget != null && _renameBuffer != null && _renameBuffer.Trim() != _renameTarget.Nickname)
            {
                _renameTarget.Nickname = _renameBuffer.Trim();
                FishMonStorage.Save();
            }
            _renameTarget = null;
        }

        private static readonly Color Bad = new Color(1f, 0.6f, 0.6f);

        // spend the team's money. The host removes it directly; guests go through the game's bait-shop purchase
        // (the only server call that accepts a custom price), which also hands them 1 cheap bait
        private static bool TryPay(int cost, string what)
        {
            if (cost <= 0) return true;
            var mm = MoneyManager.Instance;
            var p = Player.LocalPlayer;
            if (!mm || !p) { ShowToast("You can't buy anything right now.", Bad); return false; }
            if (!MoneyManager.CanAfford(cost)) { ShowToast($"You need ${cost} to {what}. You have ${MoneyManager.Money}.", Bad); return false; }
            try
            {
                if (mm.IsServerInitialized) MoneyManager.RemoveMoney(cost, p);
                else if (Server.Instance && GameInfo.AllBaits.Count > 1) Server.Instance.BuyBait(p, 1, cost);
                else { ShowToast("Couldn't reach the host to pay.", Bad); return false; }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("FishMon payment failed: " + e);
                ShowToast("Payment failed.", Bad);
                return false;
            }
            return true;
        }

        private void HealAll(int cost)
        {
            var all = FishMonStorage.Party.Concat(FishMonStorage.Box).ToList();
            int fainted = all.Count(m => m.Fainted);
            if (all.Where(m => !m.Fainted).All(m => m.CurrentHp >= m.MaxHp))
            {
                ShowToast(fainted > 0 ? $"Everyone else is at full health. Fainted FishMon need a Revive." : "Everyone is already at full health!");
                return;
            }
            if (!TryPay(cost, "heal your FishMon")) return;
            FishMonStorage.HealAll();
            ShowToast(fainted > 0 ? $"Healed! ({fainted} fainted FishMon still need a Revive.)" : "Your FishMon are fighting fit!", new Color(0.6f, 1f, 0.7f));
        }

        private void Revive(FishMon m)
        {
            if (!m.Fainted) return;
            if (!TryPay(Plugin.ReviveCost.Value, $"revive {m.DisplayName}")) return;
            m.Revive(Plugin.ReviveHpPercent.Value / 100f);
            FishMonStorage.Save();
            ShowToast($"{m.DisplayName} was revived! ({m.CurrentHp}/{m.MaxHp} HP)", new Color(0.6f, 1f, 0.7f));
            try { AudioManager.PlayGlobalClip("Confetti"); } catch { }
        }

        private void RerollAbility(FishMon m)
        {
            if (!TryPay(Plugin.AbilityRerollCost.Value, $"reroll {m.DisplayName}'s ability")) return;
            var old = m.AbilityInfo;
            m.Ability = Abilities.Roll(new System.Random(), m.Shiny, m.Ability);
            m.AbilityOn = true;
            FishMonStorage.Save();
            var ab = m.AbilityInfo;
            ShowToast($"{m.DisplayName}'s ability changed from {old?.Name ?? "nothing"} to <color={Abilities.RarityColor(ab.Rarity)}>{ab.Name} ({ab.Rarity})</color>!", new Color(0.8f, 0.9f, 1f), 5f);
        }

        private static string CostLabel(string label, int cost) => cost > 0 ? $"{label} (${cost})" : label;
    }
}
