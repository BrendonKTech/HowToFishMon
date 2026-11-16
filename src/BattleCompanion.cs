using UnityEngine;

namespace HowToFishMon
{
    // our FishMon in a battle against another player's FishMon (see Battle.cs). It lines up on its side of the
    // arena during the countdown, then circles the other FishMon and leaps at it. Our game works out the
    // damage we deal and sends it over; the damage we take comes in through TakePvpHit
    public partial class Companion
    {
        private const float PvpSpacing = 3.5f;   // how far from the other FishMon it waits between leaps
        private const float PvpReach = 9f;       // leaps from up to this far away

        private bool _pvp;
        public bool InPvp => _pvp;

        public void BeginPvp()
        {
            _pvp = true;
            _boss = null;
            _wildTarget = null;
            OrderedTarget = null;
            _hasRoamGoal = false;
            _moves = Mon.GetMoves();
            _attackCooldown = 0.6f + Combat.Random01() * 0.8f;
            if (_trail) _trail.emitting = false;
            if (_state != State.Lunge && _state != State.Recover) _state = State.Engage;
        }

        public void EndPvp()
        {
            _pvp = false;
            _boss = null;
            _pendingMove = null;
            _hasRoamGoal = false;
            _state = State.Follow;
            if (_trail) _trail.emitting = false;
        }

        private void UpdatePvp(float dt)
        {
            if (TickStatus()) return; // fainted from burn / poison: Battle.Lose (see OnCompanionFainted)
            if (!_pvp) return;
            TickItem(dt);

            switch (_state)
            {
                case State.Lunge: DoArc(dt, true); break;
                case State.Recover: DoArc(dt, false); break;
                default:
                    _state = State.Engage;
                    if (!Statuses.CantAct(Mon.Status)) DoPvpEngage(dt); // asleep / frozen: stays put
                    break;
            }
            AnimateBody(dt);
        }

        private void DoPvpEngage(float dt)
        {
            var foe = Battle.OpponentBody;
            bool fighting = Battle.State == Battle.Phase.Fighting && foe;
            Vector3 arena = Battle.Arena;
            Vector3 goal, look;

            if (!fighting)
            {
                // countdown (or their FishMon hasn't shown up yet): line up on our side
                goal = Battle.HomeSpot;
                look = foe ? foe.transform.position : arena;
            }
            else
            {
                // hang about a leap away from it, without leaving the arena
                Vector3 fp = foe.transform.position;
                Vector3 away = transform.position - fp; away.y = 0f;
                if (away.sqrMagnitude < 0.25f) away = Battle.OurSide;
                goal = fp + away.normalized * PvpSpacing;
                Vector3 off = goal - arena; off.y = 0f;
                if (off.magnitude > Battle.ArenaRadius) goal = arena + off.normalized * Battle.ArenaRadius;
                look = fp;
            }

            Vector3 flat = goal - transform.position; flat.y = 0f;
            float dist = flat.magnitude;
            if (dist > 40f)
            {
                // way off (it was far from the arena when the battle started): pop over
                Effects(transform.position, "Poof", true);
                transform.position = new Vector3(goal.x, SurfaceY(goal, out _inWater) + HalfHeight, goal.z);
                Effects(transform.position, "Poof", true);
                return;
            }
            MoveFlat(flat, dist, MoveSpeed * (dist > 8f ? 2f : 1.2f), 0.6f, dt);
            Vector3 l = look - transform.position; l.y = 0f;
            if (dist < 1.2f && l.sqrMagnitude > 0.01f) FaceYaw(Quaternion.LookRotation(l).eulerAngles.y, dt * 6f);

            if (!fighting) return;
            _attackCooldown -= dt;
            if (_attackCooldown > 0f || l.magnitude > PvpReach) return;
            if (Statuses.Is(Mon.Status, StatusCond.Paralysis) && Combat.Random01() < Statuses.ParaSkip)
            {
                _attackCooldown = 1.5f;
                FishMonController.AddFloat(transform.position + Vector3.up * (HalfHeight + 0.6f), $"{Mon.DisplayName} is paralyzed! It can't move!", Statuses.ColorOf(StatusCond.Paralysis));
                return;
            }

            var opp = Battle.Opponent;
            if (opp == null) return;
            _pendingMove = Combat.PickMove(_moves, Mon.Type1, Mon.Type2, opp.Type1, opp.Type2);
            _bossRadius = Mathf.Max(0.5f, foe.HalfHeight);
            LeapAt(foe.transform.position);
        }

        // the end of a leap at the other FishMon
        private void PvpImpact()
        {
            var opp = Battle.Opponent;
            var move = _pendingMove;
            if (opp == null || move == null || Battle.State != Battle.Phase.Fighting) return;
            Vector3 at = transform.position;
            Vector3 dir = (_p1 - _p0).normalized;

            var h = Combat.FishHitsFish(Mon, opp, move);
            if (h.Missed) { FishMonController.AddFloat(at, "Missed!", Color.gray); Net.SendAttack(2, move, at, dir, 1f, false, _inWater); return; }
            if (h.Eff <= 0f) { FishMonController.AddFloat(at, "No effect...", Color.gray); Net.SendAttack(2, move, at, dir, 0f, false, _inWater); return; }

            if (Plugin.AttackEffects.Value) Fx.Impact(move, at, dir, h.Eff, h.Crit);
            else { try { AudioManager.PlayClipAt("FishPunchHit", at, true, AudioDistance.Medium, 0.6f); } catch { } }
            Net.SendAttack(1, move, at, dir, h.Eff, h.Crit, _inWater);
            Battle.SendHit(move, h);

            FishMonController.AddFloat(at + Vector3.up * 0.2f, "-" + h.Damage, new Color(1f, 0.9f, 0.6f));
            string extra = h.Eff >= 2f ? "Super effective!" : h.Eff < 1f ? "Not very effective..." : "";
            if (h.Crit) extra = "Critical hit! " + extra;
            if (extra.Length > 0) FishMonController.AddFloat(at + Vector3.up * 0.7f, extra, h.Eff >= 2f ? new Color(0.7f, 1f, 0.5f) : new Color(1f, 0.85f, 0.5f));

            float drain = move.DrainPercent + (Mon.Has(Ability.Leech) ? Abilities.LeechHeal : 0f);
            if (drain > 0f)
            {
                int heal = Mathf.Max(1, Mathf.RoundToInt(h.Damage * drain));
                Mon.CurrentHp = Mathf.Min(Mon.MaxHp, Mon.CurrentHp + heal);
                FishMonController.AddFloat(transform.position + Vector3.up, "+" + heal, new Color(0.5f, 1f, 0.5f));
            }
        }

        // the other player's FishMon hit ours (their game worked out the damage)
        public void TakePvpHit(Move move, int dmg, float eff, bool crit)
        {
            if (!_pvp || Mon.Fainted || dmg <= 0) return;
            Vector3 head = transform.position + Vector3.up * (HalfHeight + 0.4f);
            string who = Battle.Opponent != null ? Battle.Opponent.DisplayName : "The other FishMon";

            // Focus Sash: a knockout hit from full HP leaves it hanging on with 1 HP
            if (Mon.Holds(HeldItem.FocusSash) && Mon.CurrentHp >= Mon.MaxHp && dmg >= Mon.CurrentHp && Mon.MaxHp > 1)
            {
                dmg = Mon.CurrentHp - 1;
                UseUpItem("hung on using its Focus Sash!");
            }
            Mon.CurrentHp = Mathf.Max(0, Mon.CurrentHp - dmg);
            FishMonController.AddFloat(head, $"{who} used {move.Name}!  -{dmg}", new Color(1f, 0.45f, 0.4f));
            try { AudioManager.PlayClipAt("FishPunchHit", transform.position, true, AudioDistance.Short, 0.5f); } catch { }

            if (Mon.Fainted)
            {
                if (Plugin.AttackEffects.Value) Fx.Burst(transform.position, new Color(1f, 0.5f, 0.5f), 12, 1.2f);
                Battle.Lose();
                return;
            }

            if (Statuses.TryThaw(Mon.Status, move))
                FishMonController.AddFloat(head + Vector3.up * 0.5f, Mon.DisplayName + Statuses.Cured(StatusCond.Freeze), Statuses.ColorOf(StatusCond.Freeze));
            else if (Statuses.TryInflict(Mon.Status, move, Mon.Type1, Mon.Type2, false))
                FishMonController.ShowToast($"{Mon.DisplayName} {Statuses.Inflicted(Mon.Status.Cond)}", Statuses.ColorOf(Mon.Status.Cond), 3f);

            if (_state == State.Engage)
            {
                // knocked back a couple of metres
                var foe = Battle.OpponentBody;
                Vector3 back = foe ? transform.position - foe.transform.position : Battle.OurSide;
                back.y = 0f;
                if (back.sqrMagnitude < 0.01f) back = -transform.forward;
                _p0 = transform.position;
                _p1 = transform.position + back.normalized * 2.5f;
                _p1.y = SurfaceY(_p1, out _) + HalfHeight;
                _arcDur = 0.45f; _arcHeight = 1.2f; _arcT = 0f;
                _state = State.Recover;
            }
        }
    }
}
