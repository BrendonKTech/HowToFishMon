using UnityEngine;

namespace HowToFishMon
{
    // another player's FishMon. Only a look-alike: it copies the model, glides to the positions that player's
    // game sends, and plays the attack effects. All the fighting (and the damage to bosses) happens on its
    // owner's machine
    public class RemoteCompanion : MonoBehaviour
    {
        public MonInfoMsg Info { get; private set; }
        public int OwnerId => Info.Owner;
        public float HpFrac { get; private set; } = 1f;
        public float HalfHeight { get; private set; } = 0.3f;
        public float LastHeard { get; private set; }
        public StatusState Status { get; } = new StatusState();
        private float _zzzAt;

        private Transform _body;
        private TrailRenderer _trail;
        private Vector3 _target, _vel;
        private float _targetYaw, _yaw;
        private byte _state;
        private bool _inWater;
        private float _lungeStart = -10f;
        private bool _gotPos;

        public static RemoteCompanion Spawn(MonInfoMsg info, Vector3 pos, bool poof)
        {
            var go = new GameObject("FishMon_Remote_" + info.Name);
            var rc = go.AddComponent<RemoteCompanion>();
            rc.Init(info, pos, poof);
            return rc;
        }

        // same FishMon as before (only the level / nickname may have changed)?
        public bool Matches(MonInfoMsg m) => m.SpeciesId == Info.SpeciesId && m.Shiny == Info.Shiny && m.Stage == Info.Stage;

        public void Refresh(MonInfoMsg m)
        {
            Info = m;
            LastHeard = Time.unscaledTime;
        }

        private void Init(MonInfoMsg info, Vector3 pos, bool poof)
        {
            Info = info;
            LastHeard = Time.unscaledTime;

            // a throwaway FishMon just to build the same model the owner sees
            var look = new FishMon
            {
                SpeciesId = info.SpeciesId,
                Shiny = info.Shiny,
                Stage = info.Stage,
                Level = info.Level,
                Type1 = (MonType)info.Type1,
                Type2 = (MonType)info.Type2,
                SpeciesName = info.Name,
            };

            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            Companion.BuildModel(look, _body, out float halfHeight, out _, out int prefabLayer);
            HalfHeight = halfHeight;
            int layer = Companion.PickVisibleLayer(prefabLayer);
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

            transform.position = _target = pos;
            _yaw = _targetYaw = transform.eulerAngles.y;

            var col = TypeChart.ColorOf(look.Type1);
            _trail = Fx.AddTrail(gameObject, col, Mathf.Max(0.15f, HalfHeight * 1.2f));
            if (_trail) { _trail.gameObject.layer = layer; _trail.emitting = false; }
            if (info.Stage >= 2)
            {
                var aura = new GameObject("Aura");
                aura.transform.SetParent(transform, false);
                aura.transform.localPosition = Vector3.up * HalfHeight;
                var l = aura.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = col;
                l.range = 2.5f + HalfHeight * 3f;
                l.intensity = 1.5f;
                l.shadows = LightShadows.None;
            }
            if (poof) Poof();
            Plugin.Log.LogInfo($"Showing another player's FishMon: {info.Name} (id {info.SpeciesId}, Lv {info.Level}, owner {info.Owner})");
        }

        public void OnPos(MonPosMsg m)
        {
            LastHeard = Time.unscaledTime;
            // a big jump (it teleported to its owner) snaps instead of sliding across the map
            if (!_gotPos || Vector3.Distance(transform.position, m.Pos) > 30f)
            {
                transform.position = m.Pos;
                _vel = Vector3.zero;
            }
            _gotPos = true;
            _target = m.Pos;
            _targetYaw = m.Yaw;
            if (m.State == Companion.LungeState && _state != Companion.LungeState) _lungeStart = Time.time;
            _state = m.State;
            _inWater = m.InWater;
            HpFrac = m.HpFrac;
        }

        public void SetStatus(StatusCond cond, float seconds)
        {
            LastHeard = Time.unscaledTime;
            bool isNew = cond != StatusCond.None && !Statuses.Is(Status, cond);
            if (cond == StatusCond.None || seconds <= 0f) { Status.Clear(); return; }
            Status.Cond = cond;
            Status.Until = Time.time + seconds;
            if (isNew && Plugin.AttackEffects.Value) Fx.Burst(transform.position, Statuses.ColorOf(cond), 8, 1f);
        }

        public void OnAttack(MonAttackMsg m)
        {
            LastHeard = Time.unscaledTime;
            var move = Moves.Find(m.Move);
            if (move == null) return;
            Vector3 head = transform.position + Vector3.up * (HalfHeight + 0.6f);

            switch (m.Kind)
            {
                case 0: // launch
                    FishMonController.AddFloat(head, move.Name + "!", TypeChart.ColorOf(move.Type));
                    Fx.Launch(move, m.Pos, m.Dir, m.InWater);
                    if (_trail && Plugin.AttackEffects.Value)
                    {
                        var c = TypeChart.ColorOf(move.Type);
                        _trail.startColor = new Color(c.r, c.g, c.b, 0.9f);
                        _trail.endColor = new Color(c.r, c.g, c.b, 0f);
                        _trail.Clear();
                        _trail.emitting = true;
                    }
                    _lungeStart = Time.time;
                    break;
                case 1: // hit
                    if (_trail) _trail.emitting = false;
                    if (Plugin.AttackEffects.Value) Fx.Impact(move, m.Pos, m.Dir, m.Eff, m.Crit);
                    else Fx.Sound("FishPunchHit", m.Pos, 0.6f);
                    string extra = m.Eff >= 2f ? "Super effective!" : m.Eff < 1f ? "Not very effective..." : "";
                    if (m.Crit) extra = "Critical hit! " + extra;
                    if (extra.Length > 0) FishMonController.AddFloat(m.Pos + Vector3.up * 0.5f, extra, m.Eff >= 2f ? new Color(0.7f, 1f, 0.5f) : new Color(1f, 0.85f, 0.5f));
                    break;
                case 3: // a spread move hitting one of the extra foes
                    if (Plugin.AttackEffects.Value) Fx.Burst(m.Pos, TypeChart.ColorOf(move.Type), 6, 0.8f);
                    break;
                default: // missed / no effect
                    if (_trail) _trail.emitting = false;
                    FishMonController.AddFloat(m.Pos, m.Eff <= 0f ? "No effect..." : "Missed!", Color.gray);
                    break;
            }
        }

        public void Despawn(bool effects)
        {
            if (effects) Poof();
            Destroy(gameObject);
        }

        private void Poof()
        {
            Fx.Sound("Poof", transform.position, 0.5f, AudioDistance.Short);
            Fx.Vfx("WaterSplash", transform.position);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // smooth toward the last position we heard, a little faster while it's leaping
            float smooth = _state == Companion.LungeState || _state == Companion.RecoverState ? 0.05f : 0.09f;
            transform.position = Vector3.SmoothDamp(transform.position, _target, ref _vel, smooth);
            _yaw = Mathf.LerpAngle(_yaw, _targetYaw, 1f - Mathf.Exp(-dt * 12f));
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

            if (_trail && _trail.emitting && _state != Companion.LungeState && Time.time - _lungeStart > 1.2f) _trail.emitting = false;
            if (Statuses.Is(Status, StatusCond.Sleep) && Time.time >= _zzzAt)
            {
                _zzzAt = Time.time + 1.5f;
                FishMonController.AddFloat(transform.position + Vector3.up * (HalfHeight + 0.8f), "Zzz", Statuses.ColorOf(StatusCond.Sleep), 1.2f);
            }
            AnimateBody(dt);
        }

        private void AnimateBody(float dt)
        {
            if (!_body) return;
            float t = Time.time;
            Quaternion q;
            if (_state == Companion.LungeState)
            {
                float k = Mathf.Clamp01((t - _lungeStart) / 0.6f);
                q = Quaternion.Euler(Mathf.Lerp(-30f, 35f, k), 0f, k * 360f);
            }
            else if (_inWater)
                q = Quaternion.Euler(0f, Mathf.Sin(t * 7f) * 12f, 0f);
            else
                q = Quaternion.Euler(0f, Mathf.Sin(t * 11f) * 10f, Mathf.Sin(t * 9f) * 18f);
            _body.localRotation = Quaternion.Slerp(_body.localRotation, q, 1f - Mathf.Exp(-dt * 12f));
        }
    }
}
