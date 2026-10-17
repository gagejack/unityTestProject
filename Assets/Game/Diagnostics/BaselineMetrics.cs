using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Diagnostics
{
    /// <summary>Turns a recorded sequence into feel numbers. Definitions are listed in docs/FEEL_SPEC.md.</summary>
    public static class BaselineMetrics
    {
        const int MoveKeys = (int)(HeldKeys.W | HeldKeys.A | HeldKeys.S | HeldKeys.D);
        public const int LeftMouseBit = 1 << 16, RightMouseBit = 1 << 17;

        public static Dictionary<string, float> Compute(ActionSequence sequence, List<FrameRow> rows)
        {
            var m = new Dictionary<string, float>();
            if (rows.Count < 3) return m;
            int unfocused = rows.Count(r => !r.focused);
            if (unfocused > 0) m["INVALID_unfocused_frames"] = unfocused;
            switch (sequence.kind)
            {
                case SequenceKind.Movement: Movement(sequence, rows, m); break;
                case SequenceKind.Jump: Jump(rows, m); break;
                case SequenceKind.Weapon: Weapon(rows, m); break;
                case SequenceKind.Unarmed: Unarmed(rows, m); break;
                case SequenceKind.Mob: Mob(rows, m); break;
            }
            return m;
        }

        static Vector3 Velocity(List<FrameRow> rows, int i)
        {
            if (i <= 0) return Vector3.zero;
            var d = rows[i].position - rows[i - 1].position;
            d.y = 0f;
            return d / Mathf.Max(1e-5f, rows[i].dt);
        }

        static List<int> StepFrames(List<FrameRow> rows, int step) =>
            Enumerable.Range(0, rows.Count).Where(i => rows[i].step == step).ToList();

        static void Movement(ActionSequence sequence, List<FrameRow> rows, Dictionary<string, float> m)
        {
            int first = rows.FindIndex(r => (r.input & MoveKeys) != 0);
            if (first >= 0)
            {
                int moved = Find(rows, first, i => (Velocity(rows, i).magnitude * rows[i].dt) > 0.001f);
                int posed = Find(rows, first, i => rows[i].poseWeight > 0.05f);
                if (moved >= 0) m["input_to_motion_frames"] = moved - first;
                if (posed >= 0) m["input_to_walk_pose_frames"] = posed - first;
            }
            for (int s = 0; s < sequence.steps.Length; s++)
            {
                var frames = StepFrames(rows, s);
                if (frames.Count < 4) continue;
                string label = string.IsNullOrEmpty(sequence.steps[s].label) ? "step" + s : sequence.steps[s].label;
                int tail = Mathf.Max(2, frames.Count / 4);
                var plateau = frames.Skip(frames.Count - tail).Select(i => Velocity(rows, i)).Aggregate(Vector3.zero, (a, b) => a + b) / tail;
                float tolerance = Mathf.Max(0.05f, plateau.magnitude * 0.05f);
                int settled = frames.Count - 1;
                for (int k = frames.Count - 1; k >= 0; k--)
                {
                    if ((Velocity(rows, frames[k]) - plateau).magnitude > tolerance) break;
                    settled = k;
                }
                m[label + "_speed"] = plateau.magnitude;
                m[label + "_settle_s"] = settled * rows[frames[0]].dt;
                m[label + "_distance_m"] = frames.Skip(1).Sum(i => Velocity(rows, i).magnitude * rows[i].dt);
                // Frames the capsule was not grounded although no jump was pressed (slopes, steps).
                m[label + "_airborne_frames"] = frames.Skip(1).Count(i => !rows[i].grounded);
                bool moving = plateau.magnitude > 0.5f;
                if (moving)
                {
                    var steady = frames.Skip(frames.Count * 2 / 5).ToList();
                    m[label + "_foot_slide_pct"] = FootSlide(rows, steady, r => r.leftContact, r => r.rightContact, r => r.position, true);
                    // The original ankle-bone measure, kept so results stay comparable with the first baseline.
                    m[label + "_ankle_slide_pct"] = FootSlide(rows, steady, r => r.leftFoot, r => r.rightFoot, r => r.position, false);
                    m[label + "_arm_out_deg"] = steady.Average(i => (rows[i].armOutLeft + rows[i].armOutRight) * 0.5f);
                    m[label + "_hand_jitter_mm"] = Jitter(rows, steady);
                    if (rows[steady[0]].armed)
                    {
                        var gaps = steady.Where(i => rows[i].supportGap >= 0f).Select(i => rows[i].supportGap).ToList();
                        if (gaps.Count > 0) m[label + "_support_hand_gap_cm"] = gaps.Average() * 100f;
                    }
                }
            }
        }

        static void Jump(List<FrameRow> rows, Dictionary<string, float> m)
        {
            int press = rows.FindIndex(r => (r.input & (int)HeldKeys.Space) != 0);
            if (press < 0) return;
            // The press row already includes the jump frame's rise, so measure from the frame before.
            float startY = rows[Mathf.Max(0, press - 1)].position.y;
            int leave = Find(rows, press, i => !rows[i].grounded);
            m["jump_apex_m"] = rows.Skip(press).Max(r => r.position.y) - startY;
            int dive = rows.FindIndex(r => r.diving);
            if (dive >= 0)
            {
                m["dive_started_s"] = rows[dive].time - rows[press].time;
                int slide = Find(rows, dive, i => rows[i].sliding);
                if (slide >= 0)
                {
                    int end = Find(rows, slide, i => !rows[i].sliding);
                    m["dive_slide_s"] = (end >= 0 ? rows[end].time : rows[rows.Count - 1].time) - rows[slide].time;
                }
                m["dive_distance_m"] = Vector3.ProjectOnPlane(rows[rows.Count - 1].position - rows[press].position, Vector3.up).magnitude;
            }
            if (leave >= 0)
            {
                m["input_to_takeoff_frames"] = leave - press;
                int land = Find(rows, leave, i => rows[i].grounded);
                if (land >= 0) m["airtime_s"] = rows[land].time - rows[leave].time;
            }
        }

        static void Weapon(List<FrameRow> rows, Dictionary<string, float> m)
        {
            var shots = Enumerable.Range(0, rows.Count).Where(i => rows[i].shot).ToList();
            m["shots"] = shots.Count;
            int press = rows.FindIndex(r => (r.input & LeftMouseBit) != 0);
            if (press >= 0 && shots.Count > 0)
            {
                m["input_to_shot_frames"] = shots[0] - press;
                float pitch = rows[shots[0] - 1 >= 0 ? shots[0] - 1 : 0].cameraPitch;
                int kick = Find(rows, shots[0], i => Mathf.Abs(rows[i].cameraPitch - pitch) > 0.05f);
                if (kick >= 0) m["shot_to_camera_kick_frames"] = kick - shots[0];
            }
            if (shots.Count > 1)
            {
                float interval = (rows[shots[shots.Count - 1]].time - rows[shots[0]].time) / (shots.Count - 1);
                m["shot_interval_s"] = interval;
                m["rounds_per_minute"] = 60f / Mathf.Max(1e-4f, interval);
            }
            m["damage_total"] = rows.Sum(r => r.damage);
            m["hits"] = rows.Sum(r => r.damageCount);
            var firing = Enumerable.Range(1, rows.Count - 1).Where(i => (rows[i].input & LeftMouseBit) != 0).ToList();
            if (firing.Count > 4)
            {
                var steady = firing.Skip(firing.Count / 3).ToList();
                m["speed_while_firing"] = steady.Average(i => Velocity(rows, i).magnitude);
                m["hand_jitter_mm"] = Jitter(rows, steady);
                var aim = steady.Where(i => rows[i].gunAimError >= 0f && rows[i].aimWeight > 0.99f).ToList();
                if (aim.Count > 0) m["gun_aim_error_deg"] = aim.Average(i => rows[i].gunAimError);
                var gaps = steady.Where(i => rows[i].supportGap >= 0f).ToList();
                if (gaps.Count > 0) m["support_hand_gap_cm"] = gaps.Average(i => rows[i].supportGap) * 100f;
            }
        }

        static void Unarmed(List<FrameRow> rows, Dictionary<string, float> m)
        {
            var clicks = Enumerable.Range(1, rows.Count - 1).Where(i => (rows[i].input & LeftMouseBit) != 0 && (rows[i - 1].input & LeftMouseBit) == 0).ToList();
            m["clicks"] = clicks.Count;
            if (clicks.Count == 0) return;
            int started = Find(rows, clicks[0], i => !string.IsNullOrEmpty(rows[i].fight));
            if (started >= 0) m["click_to_attack_frames"] = started - clicks[0];
            int hit = Find(rows, clicks[0], i => rows[i].damageCount > 0);
            if (hit >= 0) m["click_to_first_hit_s"] = rows[hit].time - rows[clicks[0]].time;
            m["hits"] = rows.Sum(r => r.damageCount);
            m["damage_total"] = rows.Sum(r => r.damage);
            m["attacks_started"] = Enumerable.Range(1, rows.Count - 1).Count(i => rows[i].fight != rows[i - 1].fight && !string.IsNullOrEmpty(rows[i].fight));
            var attacking = Enumerable.Range(1, rows.Count - 1).Where(i => rows[i].fightWeight > 0.5f).ToList();
            if (attacking.Count > 0) m["speed_while_attacking"] = attacking.Average(i => Velocity(rows, i).magnitude);
        }

        static void Mob(List<FrameRow> rows, Dictionary<string, float> m)
        {
            int firstAttack = rows.FindIndex(r => r.mobAttacks > 0);
            if (firstAttack >= 0) m["time_to_first_attack_s"] = rows[firstAttack].time - rows[0].time;
            m["attacks"] = rows[rows.Count - 1].mobAttacks;
            var moving = new List<int>();
            for (int i = 1; i < rows.Count; i++)
            {
                var d = rows[i].mobPosition - rows[i - 1].mobPosition; d.y = 0f;
                if (d.magnitude / Mathf.Max(1e-5f, rows[i].dt) > 0.3f) moving.Add(i);
            }
            if (moving.Count > 10)
            {
                m["chase_speed"] = moving.Average(i => { var d = rows[i].mobPosition - rows[i - 1].mobPosition; d.y = 0f; return d.magnitude / rows[i].dt; });
                m["foot_slide_pct"] = FootSlide(rows, moving, r => r.mobLeftFoot, r => r.mobRightFoot, r => r.mobPosition, false);
            }
        }

        static int Find(List<FrameRow> rows, int from, System.Func<int, bool> test)
        {
            for (int i = Mathf.Max(0, from); i < rows.Count; i++) if (test(i)) return i;
            return -1;
        }

        // Foot slide as a percentage of body travel: while a foot is planted, how far it travels compared with the body.
        // 0 = the foot stays planted, 100 = it skates along with the body.
        // Player (visibleContact): the lowest corner of the lower-leg block, planted while within 2 cm of the lowest
        // contact of either foot in the window, i.e. what a viewer sees touching the floor.
        // Mobs: the ankle bone, planted while within 3 cm of that foot's own lowest point (the original measure; mob
        // ankles sit at different heights, so a shared floor would miss one foot's contacts).
        static float FootSlide(List<FrameRow> rows, List<int> frames, System.Func<FrameRow, Vector3> left,
            System.Func<FrameRow, Vector3> right, System.Func<FrameRow, Vector3> body, bool visibleContact)
        {
            float sharedFloor = float.MaxValue;
            foreach (int i in frames) sharedFloor = Mathf.Min(sharedFloor, Mathf.Min(left(rows[i]).y, right(rows[i]).y));
            float footTravel = 0f, bodyTravel = 0f;
            foreach (var foot in new[] { left, right })
            {
                float floor = visibleContact ? sharedFloor : frames.Min(i => foot(rows[i]).y);
                float planted = visibleContact ? 0.02f : 0.03f;
                for (int k = 1; k < frames.Count; k++)
                {
                    int i = frames[k], prev = frames[k - 1];
                    if (i != prev + 1 || foot(rows[i]).y - floor > planted || foot(rows[prev]).y - floor > planted) continue;
                    var f = foot(rows[i]) - foot(rows[prev]); f.y = 0f;
                    var b = body(rows[i]) - body(rows[prev]); b.y = 0f;
                    footTravel += f.magnitude;
                    bodyTravel += b.magnitude;
                }
            }
            return bodyTravel > 0.01f ? footTravel / bodyTravel * 100f : 0f;
        }

        // Root-mean-square second difference of the right hand in body space: high-frequency shake per frame.
        static float Jitter(List<FrameRow> rows, List<int> frames)
        {
            double sum = 0; int n = 0;
            for (int k = 2; k < frames.Count; k++)
            {
                int a = frames[k - 2], b = frames[k - 1], c = frames[k];
                if (c != b + 1 || b != a + 1) continue;
                var second = rows[c].rightHandLocal - 2f * rows[b].rightHandLocal + rows[a].rightHandLocal;
                sum += second.sqrMagnitude; n++;
            }
            return n > 0 ? (float)System.Math.Sqrt(sum / n) * 1000f : 0f;
        }
    }
}
