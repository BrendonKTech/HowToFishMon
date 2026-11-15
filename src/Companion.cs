using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace HowToFishMon
{
    // a FishMon out in the world. It runs on your machine only (no physics); other players with the mod see a
    // copy of it through Net. Its hits deal real damage to the boss through the game's normal hit
    // path. Follows you around (flops on land, swims on water) and leaps at bosses on its own
    public partial class Companion : MonoBehaviour
    {
        private enum State { Follow, Engage, Lunge, Recover }

        public FishMon Mon { get; private set; }
        public bool Engaged => _state != State.Follow;
        public BossMon Target => _boss;
        public float HalfHeight { get; private set; } = 0.3f;
        public bool InWater => _inWater;

        // what other players' games need to animate their copy of it
        public const byte LungeState = (byte)State.Lunge, RecoverState = (byte)State.Recover;
        public byte StateByte => (byte)_state;

        // the Player component's own transform never moves; the body that walks around is Player.Transform
        public static Vector3 PlayerPos(Player p)
        {
            if (!p) return Vector3.zero;
            if (p.Transform) return p.Transform.position;
            if (p.Rigidbody) return p.Rigidbody.position;
            return p.transform.position;
        }

        public static float PlayerYaw(Player p)
        {
            if (!p) return 0f;
            if (p.CamObject) return p.CamObject.eulerAngles.y;
            return p.CurPlayerRot.eulerAngles.y;
        }

        public static Vector3 PlayerForward(Player p) => Quaternion.Euler(0f, PlayerYaw(p), 0f) * Vector3.forward;

        // commands a creature you told it to attack. Overrides automatic targeting.
        public Creature OrderedTarget { get; private set; }
        // holding position: doesn't follow you or pick its own fights
        public bool Staying { get; private set; }
        private Vector3 _stayPos;

        public void OrderAttack(Creature c)
        {
            OrderedTarget = c;
            _wildTarget = null;
            if (_state == State.Engage) _state = State.Follow; // re-pick target next frame
        }

        public void OrderStay()
        {
            Staying = true;
            _stayPos = transform.position;
            OrderedTarget = null;
        }

        public void OrderFollow()
        {
            Staying = false;
            OrderedTarget = null;
        }

        // carry orders over when the model is rebuilt (evolution)
        public void CopyOrdersFrom(Companion other)
        {
            if (!other) return;
            OrderedTarget = other.OrderedTarget;
            Staying = other.Staying;
            _stayPos = other._stayPos;
        }

        private State _state = State.Follow;
        private Transform _body;
        private TrailRenderer _trail;
        private float _yaw;
        private float _hopPhase;
        private bool _inWater;

        // fight
        private BossMon _boss;
        private float _bossRadius = 2f;
        private float _attackCooldown = 1f;
        private float _bossAttackTimer = 3f;
        private Move _pendingMove;
        private List<Move> _moves;
        private float _expCarry;

        // lunge / recover arc
        private Vector3 _p0, _p1;
        private float _arcT, _arcDur, _arcHeight;

        public static Companion Spawn(FishMon mon, Vector3 nearPos)
        {
            var go = new GameObject("FishMon_" + mon.DisplayName);
            var c = go.AddComponent<Companion>();
            c.Init(mon, nearPos);
            return c;
        }

        private void Init(FishMon mon, Vector3 pos)
        {
            Mon = mon;
            _moves = mon.GetMoves();
            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            BuildModel(mon, _body, out float halfHeight, out int parts, out int prefabLayer);
            HalfHeight = halfHeight;
            int layer = PickVisibleLayer(prefabLayer);
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

            transform.position = new Vector3(pos.x, SurfaceY(pos, out _inWater) + HalfHeight, pos.z);
            _yaw = Player.LocalPlayer ? PlayerYaw(Player.LocalPlayer) : 0f;
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            Effects(transform.position, "Poof", true);

            _trail = Fx.AddTrail(gameObject, TypeChart.ColorOf(mon.Type1), Mathf.Max(0.15f, HalfHeight * 1.2f));
            if (_trail) _trail.gameObject.layer = layer;
            if (mon.Stage >= 2)
            {
                // fully evolved FishMon get a soft glow in their main type colour
                var aura = new GameObject("Aura");
                aura.transform.SetParent(transform, false);
                aura.transform.localPosition = Vector3.up * HalfHeight;
                var l = aura.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = TypeChart.ColorOf(mon.Type1);
                l.range = 2.5f + HalfHeight * 3f;
                l.intensity = 1.5f;
                l.shadows = LightShadows.None;
            }
            Plugin.Log.LogInfo($"Sent out {mon.DisplayName} ({mon.SpeciesName}, id {mon.SpeciesId}): " +
                               (parts > 0 ? parts + " model parts" : "stand-in capsule") +
                               $", layer {layer} ({LayerMask.LayerToName(layer)}), halfHeight {HalfHeight:0.00}, at {transform.position}, inWater {_inWater}, requested {pos}");
        }

        public void RefreshMoves() => _moves = Mon.GetMoves();

        public void Despawn(bool effects = true)
        {
            if (effects) Effects(transform.position, "Poof", true);
            Destroy(gameObject);
        }

        // model

        internal static void BuildModel(FishMon mon, Transform parent, out float halfHeight, out int parts, out int prefabLayer)
        {
            var model = new GameObject("Model").transform;
            model.SetParent(parent, false);
            bool built = false;
            parts = 0;
            prefabLayer = -1;

            try { parts = CopyPrefabVisuals(mon.SpeciesId, mon.Shiny, model, out prefabLayer); built = parts > 0; }
            catch (System.Exception e) { Plugin.Log.LogWarning("Couldn't copy fish model, using a stand-in: " + e.Message); }

            if (!built)
            {
                // stand-in: a coloured capsule lying along +Z
                var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.Destroy(cap.GetComponent<Collider>());
                cap.transform.SetParent(model, false);
                cap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                cap.transform.localScale = new Vector3(0.35f, 0.4f, 0.3f);
                var r = cap.GetComponent<Renderer>();
                r.material.color = TypeChart.ColorOf(mon.Type1);
            }

            // center the model on the pivot and size it so small fish are still visible
            var bounds = LocalBounds(model);
            float longest = Mathf.Max(0.05f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
            float wanted = Mathf.Clamp(longest * 1.6f, 0.8f, 3.2f) * Mathf.Max(0.1f, Plugin.CompanionScale.Value)
                           * (1f + mon.Level / 250f) * (1f + 0.3f * mon.Stage); // evolved forms are bigger
            float scale = wanted / longest;
            model.localScale = Vector3.one * scale;
            model.localPosition = -bounds.center * scale;
            halfHeight = Mathf.Max(0.15f, bounds.extents.y * scale);
        }

        // copies the meshes of a creature's prefab under model. Returns how many parts were copied.
        internal static int CopyPrefabVisuals(byte speciesId, bool shiny, Transform model, out int layer)
        {
            layer = -1;
            var prefab = GameInfo.IDToItem(speciesId);
            if (!prefab) { Plugin.Log.LogWarning($"No prefab found for item id {speciesId}"); return 0; }
            var root = prefab.transform;
            var tr = Traverse.Create(prefab);

            Transform enableShiny = tr.Field<GameObject>("_enableOnShiny").Value?.transform;
            Transform disableShiny = tr.Field<GameObject>("_disableOnShiny").Value?.transform;
            Transform inHand = tr.Field<GameObject>("_inHandHolder").Value?.transform;
            Transform handR = tr.Field<Transform>("_handModelRight").Value;
            Transform handL = tr.Field<Transform>("_handModelLeft").Value;
            Mesh plainMesh = tr.Field<Mesh>("_mesh").Value;
            Mesh dripMesh = tr.Field<Mesh>("_dripMesh").Value;

            // only use the highest-detail LOD
            HashSet<Renderer> skipLods = new HashSet<Renderer>();
            foreach (var lg in prefab.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = lg.GetLODs();
                for (int i = 1; i < lods.Length; i++)
                    foreach (var r in lods[i].renderers) if (r) skipLods.Add(r);
            }

            bool Under(Transform t, Transform parentT) => parentT && (t == parentT || t.IsChildOf(parentT));

            bool ActiveInPrefab(Transform t)
            {
                for (var x = t; x && x != root.parent; x = x.parent)
                {
                    if (x.gameObject.activeSelf) continue;
                    // the shiny/default variant objects get switched at runtime, so judge those ourselves
                    if (x == enableShiny || x == disableShiny) continue;
                    return false;
                }
                return true;
            }

            int count = 0;
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || skipLods.Contains(r)) continue;
                Mesh mesh = null;
                if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
                else if (r is MeshRenderer) mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
                if (!mesh) continue;

                var t = r.transform;
                if (Under(t, inHand) || Under(t, handR) || Under(t, handL)) continue;
                if (shiny ? Under(t, disableShiny) : Under(t, enableShiny)) continue;
                if (!ActiveInPrefab(t)) continue;
                if (shiny && dripMesh && mesh == plainMesh) mesh = dripMesh;

                var part = new GameObject(r.name);
                part.transform.SetParent(model, false);
                Matrix4x4 rel = root.worldToLocalMatrix * t.localToWorldMatrix;
                part.transform.localPosition = rel.GetColumn(3);
                part.transform.localRotation = rel.rotation;
                part.transform.localScale = rel.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = part.AddComponent<MeshRenderer>();
                mr.sharedMaterials = r.sharedMaterials;
                if (layer < 0) layer = r.gameObject.layer;
                count++;
            }
            return count;
        }

        internal static Bounds LocalBounds(Transform model)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh) continue;
                var mb = mf.sharedMesh.bounds;
                var m = Matrix4x4.TRS(mf.transform.localPosition, mf.transform.localRotation, mf.transform.localScale);
                if (mf.transform.parent != model) m = model.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(corner);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return any ? b : new Bounds(Vector3.zero, Vector3.one * 0.5f);
        }

        // world helpers

        // a layer the player's camera actually draws (prefer the fish's own layer)
        internal static int PickVisibleLayer(int preferred)
        {
            var p = Player.LocalPlayer;
            Camera cam = p && p.CurCam ? p.CurCam : Camera.main;
            if (!cam) return preferred >= 0 ? preferred : 0;
            int mask = cam.cullingMask;
            if (preferred >= 0 && (mask & (1 << preferred)) != 0) return preferred;
            if ((mask & 1) != 0) return 0;
            for (int i = 0; i < 32; i++) if ((mask & (1 << i)) != 0) return i;
            return 0;
        }

        private static float SurfaceY(Vector3 pos, out bool water)
        {
            float waterY = -10000f;
            try { waterY = WaterManager.WaterHeight; } catch { }
            float groundY = -10000f;
            bool hitGround = false;
            var origin = new Vector3(pos.x, pos.y + 25f, pos.z);
            if (Physics.Raycast(origin, Vector3.down, out var hit, 120f, GameInfo.LevelLayer, QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
                hitGround = true;
            }
            else
            {
                // level layer missed (some ground may be on other layers): try everything except players / loose objects
                float best = -10000f;
                foreach (var h in Physics.RaycastAll(origin, Vector3.down, 120f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (h.collider.GetComponentInParent<Player>()) continue;
                    if (h.rigidbody && !h.rigidbody.isKinematic) continue;
                    if (h.point.y > best) best = h.point.y;
                }
                if (best > -9999f) { groundY = best; hitGround = true; }
            }
            if (!hitGround)
            {
                // over open water (no sea floor collider) or the void: use the water, else the player's height
                if (waterY > -9999f) { water = true; return waterY - 0.1f; }
                water = false;
                var p = Player.LocalPlayer;
                return p ? PlayerPos(p).y : pos.y;
            }
            water = waterY > groundY;
            return water ? waterY - 0.1f : groundY;
        }

        private static void Effects(Vector3 pos, string sound, bool splash)
        {
            try { AudioManager.PlayClipAt(sound, pos, true, AudioDistance.Short, 0.5f); } catch { }
            if (splash) { try { VFXManager.Play("WaterSplash", pos, Vector3.zero); } catch { } }
        }

        // update

        private void Update()
        {
            var player = Player.LocalPlayer;
            if (!player || Mon == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            TickGuard(player);
            UpdateTarget(player);
            TickMedic(player, dt);
            TickTreasure(player, dt);
            if (TickStatus()) return; // fainted from burn / poison

            // asleep or frozen: stays put (but finishes a leap it already started)
            bool stuck = Statuses.CantAct(Mon.Status) && (_state == State.Follow || _state == State.Engage);
            if (!stuck)
                switch (_state)
                {
                    case State.Follow: DoFollow(player, dt); break;
                    case State.Engage: DoEngage(dt); break;
                    case State.Lunge: DoArc(dt, true); break;
                    case State.Recover: DoArc(dt, false); break;
                }

            if (_state == State.Engage || _state == State.Lunge || _state == State.Recover)
                BossCounterAttack(dt);

            AnimateBody(dt);
        }

        private float _scanTimer;
        private Creature _wildTarget;
        private bool _wildKillRewarded;

        private static bool IsHostile(Creature c) =>
            c && !c.IsDead && !c.IsDestroying && !c.Bird && c.BossType == BossType.None && (c is Crab || c is AttackingFish);

        private static bool IsValidOrder(Creature c) => c && !c.IsDead && !c.IsDestroying && !c.Holder;

        private Creature FindTarget(Player player)
        {
            Vector3 pp = PlayerPos(player);

            // 1) your orders come first
            if (OrderedTarget)
            {
                if (IsValidOrder(OrderedTarget) && Vector3.Distance(pp, OrderedTarget.transform.position) <= 120f)
                    return OrderedTarget;
                OrderedTarget = null;
            }
            // 2) when told to stay, it only fights what you point at
            if (Staying) return null;

            var boss = BossManager.Boss;
            // skip the boss while it's immortal (piranha / pufferfish / whale phases) so it goes for the adds instead
            if (boss && !boss.IsDead && !boss.IsDestroying && !BossManager.IsImmortal &&
                Vector3.Distance(pp, boss.transform.position) <= Plugin.AggroRange.Value)
                return boss;

            if (!Plugin.FightWildCreatures.Value) return null;
            float range = Plugin.WildAggroRange.Value;

            // keep chasing the current wild target a little further than we would pick a new one
            if (IsHostile(_wildTarget) && Vector3.Distance(pp, _wildTarget.transform.position) <= range * 1.5f)
                return _wildTarget;

            _scanTimer -= Time.deltaTime;
            if (_scanTimer > 0f) return null;
            _scanTimer = 0.5f;

            Creature best = null;
            float bestD = range;
            foreach (var item in ItemManager.Items.Values)
            {
                var c = item ? item.Creature : null;
                if (!IsHostile(c) || c.AttachedRod || c.Holder) continue;
                float d = Vector3.Distance(pp, c.transform.position);
                if (d < bestD) { bestD = d; best = c; }
            }
            _wildTarget = best;
            return best;
        }

        private void UpdateTarget(Player player)
        {
            // a wild foe we were fighting just died -> kill exp
            if (_boss != null && !_boss.IsBoss && _boss.Creature && _boss.Creature.IsDead && !_wildKillRewarded)
            {
                _wildKillRewarded = true;
                int exp = Mathf.Max(1, Mathf.RoundToInt(_boss.FullExp * 0.5f));
                FishMonController.AddFloat(transform.position + Vector3.up, $"{_boss.Name} defeated! +{exp} exp", new Color(1f, 0.85f, 0.4f));
                FishMonController.GiveExp(Mon, exp, new[] { 0, 1, 0, 0, 0, 0 });
                FishMonStorage.Save();
            }

            var target = FindTarget(player);
            if (!target)
            {
                if (_boss != null && _state != State.Lunge) { _boss = null; _state = State.Follow; }
                return;
            }
            if (_boss == null || _boss.Creature != target)
            {
                var boss = target;
                _boss = BossMon.From(boss);
                _wildKillRewarded = false;
                Fishdex.MarkSeen(boss);
                Plugin.Log.LogInfo($"{Mon.DisplayName} targets {_boss.Name} ({(_boss.IsBoss ? "boss" : "wild")}, Lv {_boss.Level}, HP {_boss.RealHp}/{_boss.RealMaxHp})");
                var rends = boss.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    var bb = rends[0].bounds;
                    foreach (var r in rends) bb.Encapsulate(r.bounds);
                    _bossRadius = Mathf.Clamp(Mathf.Max(bb.extents.x, bb.extents.z), 0.5f, 25f);
                }
                _attackCooldown = 0.6f;
                _bossAttackTimer = 2.5f + Combat.Random01() * 2f;
                if (_boss.IsBoss) FishMonController.Participants.Add(Mon);
                FishMonController.AddFloat(transform.position + Vector3.up, $"{Mon.DisplayName} charges in!", new Color(0.6f, 0.85f, 1f));
            }
            if (_state == State.Follow) _state = State.Engage;
        }

        private float SpeedMult => (Mon.Has(Ability.Speedster) ? Abilities.SpeedsterSpeed : 1f) *
                                   (Statuses.Is(Mon.Status, StatusCond.Paralysis) ? Statuses.ParaSpeed : 1f);
        private float MoveSpeed => (4f + Mon.CalcStat(Stat.Spe) / 25f) * SpeedMult;

        private void DoFollow(Player player, float dt)
        {
            Vector3 fwd = PlayerForward(player);
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            if (!Staying && TickMerchant(player, dt)) return;
            if (!Staying && Plugin.RoamAroundYou.Value) { DoRoam(player, fwd, dt); return; }
            Vector3 goal = Staying ? _stayPos : PlayerPos(player) - fwd * 2.2f + right * 1.5f;

            Vector3 flat = goal - transform.position; flat.y = 0f;
            float dist = flat.magnitude;
            if (Staying)
            {
                MoveFlat(flat, dist, MoveSpeed, 0.4f, dt);
                return;
            }
            if (dist > 35f)
            {
                Effects(transform.position, "Poof", true);
                transform.position = new Vector3(goal.x, SurfaceY(goal, out _inWater) + HalfHeight, goal.z);
                Effects(transform.position, "Poof", true);
                return;
            }
            MoveFlat(flat, dist, dist > 8f ? MoveSpeed * 2f : MoveSpeed, 0.6f, dt);
            if (dist < 0.6f) FaceYaw(PlayerYaw(player), dt * 2f);
        }

        // roaming: wander between random spots near the player, mostly in front of the camera
        private Vector3 _roamGoal;
        private bool _hasRoamGoal;
        private float _roamIdle;

        private void DoRoam(Player player, Vector3 fwd, float dt)
        {
            Vector3 me = PlayerPos(player);
            float radius = Mathf.Max(2.5f, Plugin.RoamRadius.Value);
            if (Mon.Has(Ability.Guard)) radius = Mathf.Min(radius, Abilities.GuardRoamRadius); // a Guard sticks close
            Vector3 fromPlayer = transform.position - me; fromPlayer.y = 0f;
            float distToPlayer = fromPlayer.magnitude;

            // way too far (fell behind, player teleported): pop over next to them
            if (distToPlayer > 35f + radius)
            {
                Vector3 to = PickRoamSpot(me, fwd, radius);
                Effects(transform.position, "Poof", true);
                transform.position = new Vector3(to.x, SurfaceY(to, out _inWater) + HalfHeight, to.z);
                Effects(transform.position, "Poof", true);
                _hasRoamGoal = false;
                return;
            }

            Vector3 goalFromPlayer = _roamGoal - me; goalFromPlayer.y = 0f;
            bool goalTooFar = goalFromPlayer.magnitude > radius + 1.5f;  // player walked away from it
            bool tooFar = distToPlayer > radius + 3f;
            if (!_hasRoamGoal || goalTooFar || (_roamIdle <= 0f && ReachedRoamGoal()))
            {
                _roamGoal = PickRoamSpot(me, fwd, radius);
                _hasRoamGoal = true;
                _roamIdle = Random.Range(1.5f, 4.5f);
            }

            Vector3 flat = _roamGoal - transform.position; flat.y = 0f;
            float dist = flat.magnitude;
            // stroll when close, hurry to catch up when the player has moved off
            float speed = tooFar ? MoveSpeed * 2f : (dist > 6f ? MoveSpeed : MoveSpeed * 0.55f);
            MoveFlat(flat, dist, speed, 0.5f, dt);

            if (dist <= 0.5f)
            {
                _roamIdle -= dt;
                // idle: look over at the player
                Vector3 look = me - transform.position; look.y = 0f;
                if (look.sqrMagnitude > 0.01f) FaceYaw(Quaternion.LookRotation(look).eulerAngles.y, dt * 2f);
            }
        }

        private bool ReachedRoamGoal()
        {
            Vector3 d = _roamGoal - transform.position; d.y = 0f;
            return d.magnitude <= 0.6f;
        }

        // random spot 2m..radius from the player; usually within the camera's view, sometimes anywhere
        private Vector3 PickRoamSpot(Vector3 me, Vector3 fwd, float radius)
        {
            float baseYaw = Quaternion.LookRotation(fwd).eulerAngles.y;
            float yaw = Random.value < 0.8f ? baseYaw + Random.Range(-55f, 55f) : Random.Range(0f, 360f);
            float r = Random.Range(Mathf.Min(2f, radius * 0.5f), radius);
            return me + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * r;
        }

        private void DoEngage(float dt)
        {
            if (_boss == null || _boss.Dead) { _state = State.Follow; return; }
            Vector3 center = _boss.Center;
            Vector3 away = transform.position - center; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
            Vector3 spot = center + away.normalized * (_bossRadius + 3f);
            Vector3 flat = spot - transform.position; flat.y = 0f;
            MoveFlat(flat, flat.magnitude, MoveSpeed * 1.3f, 0.8f, dt);

            Vector3 look = center - transform.position; look.y = 0f;
            if (flat.magnitude < 1.5f && look.sqrMagnitude > 0.01f) FaceYaw(Quaternion.LookRotation(look).eulerAngles.y, dt * 6f);

            _attackCooldown -= dt;
            float toBoss = Vector3.Distance(transform.position, center) - _bossRadius;
            if (_attackCooldown <= 0f && toBoss < 9f)
            {
                if (Statuses.Is(Mon.Status, StatusCond.Paralysis) && Combat.Random01() < Statuses.ParaSkip)
                {
                    _attackCooldown = 1.5f;
                    FishMonController.AddFloat(transform.position + Vector3.up * (HalfHeight + 0.6f), $"{Mon.DisplayName} is paralyzed! It can't move!", Statuses.ColorOf(StatusCond.Paralysis));
                }
                else StartLunge();
            }
        }

        private void MoveFlat(Vector3 flat, float dist, float speed, float stopDist, float dt)
        {
            Vector3 pos = transform.position;
            bool moving = dist > stopDist;
            if (moving)
            {
                Vector3 step = flat.normalized * Mathf.Min(dist, speed * dt);
                pos += step;
                FaceYaw(Quaternion.LookRotation(flat).eulerAngles.y, dt * 8f);
                _hopPhase += dt * (_inWater ? 6f : 9f);
            }
            float surf = SurfaceY(pos, out _inWater) + HalfHeight;
            float hop = 0f;
            if (!_inWater && moving) hop = Mathf.Abs(Mathf.Sin(_hopPhase)) * 0.45f;
            if (_inWater) hop = Mathf.Sin(Time.time * 2.2f) * 0.06f;
            pos.y = Mathf.Lerp(pos.y, surf + hop, 1f - Mathf.Exp(-dt * 14f));
            transform.position = pos;
        }

        private void FaceYaw(float target, float t)
        {
            _yaw = Mathf.LerpAngle(_yaw, target, Mathf.Clamp01(t));
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        private void AnimateBody(float dt)
        {
            if (!_body) return;
            float t = Time.time;
            Quaternion q;
            if (_state == State.Lunge)
            {
                // nose-dive into the target with a barrel roll
                float pitch = Mathf.Lerp(-30f, 35f, _arcT);
                q = Quaternion.Euler(pitch, 0f, _arcT * 360f);
            }
            else if (_inWater)
                q = Quaternion.Euler(0f, Mathf.Sin(t * 7f) * 12f, 0f);
            else
                q = Quaternion.Euler(0f, Mathf.Sin(t * 11f) * 10f, Mathf.Sin(t * 9f) * 18f); // flopping
            _body.localRotation = Quaternion.Slerp(_body.localRotation, q, 1f - Mathf.Exp(-dt * 12f));
        }

        // attacking

        private void StartLunge()
        {
            _pendingMove = Combat.PickMove(_moves, Mon.Type1, Mon.Type2, _boss.Type1, _boss.Type2, SpreadTargets(_boss.Center).Count);
            Vector3 center = _boss.Center;
            Vector3 fromBoss = transform.position - center;
            // aim at the near side of the boss, a bit inside its body
            Vector3 hitPoint = center + Vector3.ClampMagnitude(fromBoss, _bossRadius * 0.5f);
            _p0 = transform.position;
            _p1 = hitPoint;
            float dist = Vector3.Distance(_p0, _p1);
            _arcDur = Mathf.Clamp(dist / 16f, 0.3f, 1.0f);
            _arcHeight = Mathf.Clamp(dist * 0.35f, 0.8f, 5f);
            _arcT = 0f;
            _state = State.Lunge;
            FishMonController.AddFloat(transform.position + Vector3.up * (HalfHeight + 0.6f), _pendingMove.Name + "!", TypeChart.ColorOf(_pendingMove.Type));
            if (_inWater) Effects(transform.position, "FishCaught_Splash", true);
            Fx.Launch(_pendingMove, transform.position, (_p1 - _p0).normalized, _inWater);
            Net.SendAttack(0, _pendingMove, transform.position, (_p1 - _p0).normalized, 1f, false, _inWater);
            if (_trail && Plugin.AttackEffects.Value)
            {
                var c = TypeChart.ColorOf(_pendingMove.Type);
                _trail.startColor = new Color(c.r, c.g, c.b, 0.9f);
                _trail.endColor = new Color(c.r, c.g, c.b, 0f);
                _trail.Clear();
                _trail.emitting = true;
            }
        }

        private static bool _impactErrorLogged;

        private void DoArc(float dt, bool lunge)
        {
            _arcT += dt / _arcDur;
            float t = Mathf.Clamp01(_arcT);
            Vector3 p = Vector3.Lerp(_p0, _p1, t) + Vector3.up * (_arcHeight * 4f * t * (1f - t));
            Vector3 vel = p - transform.position;
            transform.position = p;
            vel.y = 0f;
            if (vel.sqrMagnitude > 0.0001f) FaceYaw(Quaternion.LookRotation(lunge ? vel : -vel).eulerAngles.y, dt * 12f);
            if (t < 1f) return;

            if (lunge)
            {
                // if the hit blows up (game update changed something) still finish the leap,
                // otherwise the fish gets stuck here forever
                try { Impact(); }
                catch (System.Exception e)
                {
                    if (!_impactErrorLogged) Plugin.Log.LogError("FishMon attack failed, the game probably updated: " + e.GetBaseException());
                    _impactErrorLogged = true;
                }
                if (_trail) _trail.emitting = false;
                Vector3 back = _p0 - _p1; back.y = 0f;
                if (back.sqrMagnitude < 0.01f) back = -transform.forward;
                Vector3 land = _p1 + back.normalized * Mathf.Max(3f, _bossRadius * 0.6f + 2f);
                land.y = SurfaceY(land, out _) + HalfHeight;
                _p0 = transform.position;
                _p1 = land;
                _arcDur = 0.55f;
                _arcHeight = 1.5f;
                _arcT = 0f;
                _state = State.Recover;
            }
            else
            {
                _state = _boss != null && !_boss.Dead ? State.Engage : State.Follow;
                float spe = Mon.CalcStat(Stat.Spe);
                _attackCooldown = Mathf.Clamp(3.2f - spe * 0.015f, 0.8f, 3.2f) / Mathf.Max(0.1f, Plugin.AttackSpeedMultiplier.Value)
                                  * (0.85f + Combat.Random01() * 0.3f) / SpeedMult;
                if (_inWater) Effects(transform.position, "FishCaught_Splash", true);
            }
        }

        private void Impact()
        {
            if (_boss == null || _boss.Dead || _pendingMove == null) return;
            Vector3 at = transform.position;
            Vector3 dir = (_p1 - _p0).normalized;

            var others = _pendingMove.Spread ? SpreadTargets(_boss.Center) : new List<Creature>();
            float spreadMult = others.Count > 0 ? 0.75f : 1f;
            if (others.Count > 0) HitSpread(others, spreadMult, dir);

            // the game only makes actual bosses immortal (e.g. the piranha boss while its babies are out), not the babies
            if (_boss.Creature.BossType != BossType.None && BossManager.IsImmortal) { FishMonController.AddFloat(at, "Immune!", Color.gray); return; }
            var h = Combat.FishHitsBoss(Mon, _boss, _pendingMove, spreadMult);
            if (h.Missed) { FishMonController.AddFloat(at, "Missed!", Color.gray); Net.SendAttack(2, _pendingMove, at, dir, 1f, false, _inWater); return; }
            if (h.Eff <= 0f) { FishMonController.AddFloat(at, "No effect...", Color.gray); Net.SendAttack(2, _pendingMove, at, dir, 0f, false, _inWater); return; }

            int real = Mathf.Min(Combat.ToRealDamage(h.Damage, _boss), Mathf.Max(1, _boss.RealHp));
            Combat.DealRealDamage(_boss, real, at, dir);
            Plugin.Log.LogInfo($"{Mon.DisplayName} used {_pendingMove.Name} on {_boss.Name}: {h.Damage} battle dmg -> {real} real dmg (x{h.Eff}), target HP before {_boss.RealHp}/{_boss.RealMaxHp}");
            if (Plugin.AttackEffects.Value) Fx.Impact(_pendingMove, at, dir, h.Eff, h.Crit);
            else { try { AudioManager.PlayClipAt("FishPunchHit", at, true, AudioDistance.Medium, 0.6f); } catch { } }
            Net.SendAttack(1, _pendingMove, at, dir, h.Eff, h.Crit, _inWater);

            InflictOnFoe(_boss, _pendingMove);

            string extra = h.Eff >= 2f ? "Super effective!" : h.Eff < 1f ? "Not very effective..." : "";
            if (h.Crit) extra = "Critical hit! " + extra;
            if (extra.Length > 0) FishMonController.AddFloat(at + Vector3.up * 0.5f, extra, h.Eff >= 2f ? new Color(0.7f, 1f, 0.5f) : new Color(1f, 0.85f, 0.5f));

            float drain = _pendingMove.DrainPercent + (Mon.Has(Ability.Leech) ? Abilities.LeechHeal : 0f);
            if (drain > 0f)
            {
                int heal = Mathf.Max(1, Mathf.RoundToInt(h.Damage * drain));
                Mon.CurrentHp = Mathf.Min(Mon.MaxHp, Mon.CurrentHp + heal);
                FishMonController.AddFloat(transform.position + Vector3.up, "+" + heal, new Color(0.5f, 1f, 0.5f));
            }

            // exp for the damage dealt (the kill bonus is handed out when the boss dies)
            _expCarry += _boss.FullExp * 0.5f * real / (float)_boss.RealMaxHp;
            int exp = Mathf.FloorToInt(_expCarry);
            if (exp > 0)
            {
                _expCarry -= exp;
                FishMonController.GiveExp(Mon, exp, null);
            }
        }

        private const float SpreadRadius = 5f;
        private const int MaxSpreadTargets = 8;

        // other hostile creatures close to the target (not the target itself, not bosses)
        private List<Creature> SpreadTargets(Vector3 center)
        {
            var list = new List<Creature>();
            var main = _boss != null ? _boss.Creature : null;
            foreach (var item in ItemManager.Items.Values)
            {
                var c = item ? item.Creature : null;
                if (c == main || !IsHostile(c) || c.AttachedRod || c.Holder) continue;
                if (Vector3.Distance(center, c.transform.position) <= SpreadRadius) list.Add(c);
                if (list.Count >= MaxSpreadTargets) break;
            }
            return list;
        }

        private void HitSpread(List<Creature> targets, float mult, Vector3 dir)
        {
            var move = _pendingMove;
            int hits = 0;
            foreach (var c in targets)
            {
                if (!c || c.IsDead) continue;
                var foe = BossMon.From(c);
                var h = Combat.FishHitsBoss(Mon, foe, move, mult);
                if (h.Missed || h.Eff <= 0f) continue;

                Vector3 pos = foe.Center;
                int real = Mathf.Min(Combat.ToRealDamage(h.Damage, foe), Mathf.Max(1, foe.RealHp));
                bool kills = foe.RealHp - real <= 0;
                Combat.DealRealDamage(foe, real, pos, dir);
                if (Plugin.AttackEffects.Value) Fx.Burst(pos, TypeChart.ColorOf(move.Type), 6, 0.8f);
                Net.SendAttack(3, move, pos, dir, h.Eff, h.Crit, false);
                Fishdex.MarkSeen(c);
                hits++;
                if (!kills) InflictOnFoe(foe, move);
                if (Mon.Has(Ability.Leech)) Mon.CurrentHp = Mathf.Min(Mon.MaxHp, Mon.CurrentHp + Mathf.Max(1, Mathf.RoundToInt(h.Damage * Abilities.LeechHeal)));

                _expCarry += foe.FullExp * 0.5f * real / (float)foe.RealMaxHp;
                if (kills)
                {
                    int killExp = Mathf.Max(1, Mathf.RoundToInt(foe.FullExp * 0.5f));
                    FishMonController.AddFloat(pos + Vector3.up, $"{foe.Name} defeated! +{killExp} exp", new Color(1f, 0.85f, 0.4f));
                    FishMonController.GiveExp(Mon, killExp, new[] { 0, 1, 0, 0, 0, 0 });
                }
            }
            if (hits > 0)
            {
                FishMonController.AddFloat(transform.position + Vector3.up * (HalfHeight + 1f), $"Hit {hits + 1} foes!", TypeChart.ColorOf(move.Type));
                Plugin.Log.LogInfo($"{Mon.DisplayName}'s {move.Name} also hit {hits} other foe(s)");
            }
        }

        private void BossCounterAttack(float dt)
        {
            if (_boss == null || _boss.Dead) return;
            // only things that actually fight (bosses, crabs, attacking fish) hit back
            var c = _boss.Creature;
            if (!_boss.IsBoss && !(c is Crab || c is AttackingFish)) return;
            float toBoss = Vector3.Distance(transform.position, _boss.Center) - _bossRadius;
            if (toBoss > 12f) return;
            _bossAttackTimer -= dt;
            if (_bossAttackTimer > 0f) return;
            _bossAttackTimer = 2.5f + Combat.Random01() * 2.5f;

            // the foe's own status can stop it hitting back
            var foeStatus = Statuses.FoePeek(c);
            Vector3 foeTop = _boss.Center + Vector3.up * 1.5f;
            if (Statuses.CantAct(foeStatus))
            {
                FishMonController.AddFloat(foeTop, Statuses.Is(foeStatus, StatusCond.Sleep) ? $"{_boss.Name} is fast asleep. Zzz" : $"{_boss.Name} is frozen solid!", Statuses.ColorOf(foeStatus.Cond));
                return;
            }
            if (Statuses.Is(foeStatus, StatusCond.Paralysis) && Combat.Random01() < Statuses.ParaSkip)
            {
                FishMonController.AddFloat(foeTop, $"{_boss.Name} is paralyzed! It can't move!", Statuses.ColorOf(StatusCond.Paralysis));
                return;
            }

            var move = Combat.PickMove(_boss.Moves, _boss.Type1, _boss.Type2, Mon.Type1, Mon.Type2);
            var h = Combat.BossHitsFish(_boss, Mon, move);
            Vector3 head = transform.position + Vector3.up * (HalfHeight + 0.4f);
            if (h.Missed) { FishMonController.AddFloat(head, $"Dodged {move.Name}!", new Color(0.7f, 0.9f, 1f)); return; }
            if (h.Eff <= 0f) { FishMonController.AddFloat(head, $"{move.Name}: no effect", Color.gray); return; }

            Mon.CurrentHp = Mathf.Max(0, Mon.CurrentHp - h.Damage);
            FishMonController.AddFloat(head, $"{_boss.Name} used {move.Name}!  -{h.Damage}", new Color(1f, 0.45f, 0.4f));
            if (!Mon.Fainted)
            {
                if (Statuses.TryThaw(Mon.Status, move))
                    FishMonController.AddFloat(head + Vector3.up * 0.5f, Mon.DisplayName + Statuses.Cured(StatusCond.Freeze), Statuses.ColorOf(StatusCond.Freeze));
                else if (Statuses.TryInflict(Mon.Status, move, Mon.Type1, Mon.Type2, false))
                    FishMonController.ShowToast($"{Mon.DisplayName} {Statuses.Inflicted(Mon.Status.Cond)}", Statuses.ColorOf(Mon.Status.Cond), 3f);
            }
            try { AudioManager.PlayClipAt("FishPunchHit", transform.position, true, AudioDistance.Short, 0.5f); } catch { }

            if (Mon.Fainted) FishMonController.OnCompanionFainted(this);
            else if (_state == State.Engage)
            {
                // knocked back a few metres
                Vector3 back = transform.position - _boss.Center; back.y = 0f;
                if (back.sqrMagnitude < 0.01f) back = -transform.forward;
                _p0 = transform.position;
                _p1 = transform.position + back.normalized * 3f;
                _p1.y = SurfaceY(_p1, out _) + HalfHeight;
                _arcDur = 0.45f; _arcHeight = 1.2f; _arcT = 0f;
                _state = State.Recover;
            }
        }
    }
}
