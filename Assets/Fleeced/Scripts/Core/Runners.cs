using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    /// Lambs visibly coming OUT (M12): a runner is a little cluster of lambs that hops from a gate lane's door or a breeding hut
    /// into the pen, flies in an arc from the Sheep Launcher's upper lanes or drops from the Singularity Gate's cloud, lands in a
    /// puff of dust and melts into the herd's carpet. It is only a picture: never a sheep, only ever inside a pen, from a pool of
    /// 64 (40 of them for the gate) that never allocates once it is full.
    /// The visible-agent budget: named sheep (at most 300), dying and ghost sheep, and runners stay at or below 400 renderers at
    /// any population. A runner needs a free place under it, and dead sheep's renderers are hidden past it. Only renderers'
    /// enabled flags change: when a dead sheep leaves the list is decided exactly as before, so the simulation never sees it.
    /// Visual only: landing points come from fxRng, never from the simulation's rng.
    public partial class Game
    {
        class Runner { public SpriteRenderer sr; public Vector2 a, b; public float t, delay, dur, arc; public int minOrder; public bool gate, on, landed; }
        readonly List<Runner> runners = new List<Runner>();
        GameObject runnerRoot;
        int runnersOn, gateRunnersOn;
        int namedNow, aliveShown, ghostBudget, ghostsShown, agentsFrame = -1; // counted once a frame by PlanAgents (and the frame it ran)

        public int RunnersActive { get { return runnersOn; } }

        /// sends `lambs` lambs from `from` to `to` in `dur` seconds after `delay`, `arc` tiles high at the top. For the first half of
        /// its flight it draws at least at `minOrder` (in front of the door it leaves by). False (and nothing made) when the start is
        /// off screen or the pool, the gate's share of it or the agent budget is full.
        bool Run(Vector2 from, Vector2 to, double lambs, int strain, float arc, float dur, float delay, bool gate, int minOrder = int.MinValue)
        {
            if (cam == null || !IsVisible(from)) return false;
            if (runnersOn >= ScaleMath.RunnerCap || (gate && gateRunnersOn >= ScaleMath.GateRunnerCap) || namedNow + runnersOn >= ScaleMath.AgentCap) return false;
            // after this frame's sheep visuals (hut runners): the ghosts already drawn count too. Before them (gate runners) PlanAgents
            // still counts the runner and leaves the ghosts what is left.
            if (agentsFrame == Time.frameCount && aliveShown + Mathf.Min(ghostsShown, ghostBudget) + runnersOn >= ScaleMath.AgentCap) return false;
            Runner r = null;
            foreach (var x in runners) if (!x.on) { r = x; break; }
            if (r == null)
            {
                if (runners.Count >= ScaleMath.RunnerCap) return false;
                if (runnerRoot == null) { runnerRoot = new GameObject("Runners"); runnerRoot.transform.SetParent(worldRoot.transform); }
                var go = new GameObject("runner");
                go.transform.SetParent(runnerRoot.transform);
                go.transform.localScale = new Vector3(0.62f, 0.62f, 1); // a lamb's size
                r = new Runner { sr = go.AddComponent<SpriteRenderer>() };
                runners.Add(r);
            }
            r.a = from; r.b = to; r.t = 0; r.delay = delay; r.dur = Mathf.Max(0.05f, dur); r.arc = arc; r.minOrder = minOrder; r.gate = gate; r.on = true; r.landed = false;
            r.sr.sprite = HerdArt.Tail(ScaleMath.ClusterTier(lambs), 1, strain);
            r.sr.flipX = to.x < from.x;
            r.sr.color = Color.white;
            r.sr.enabled = delay <= 0;
            r.sr.transform.position = new Vector3(from.x, from.y, 0);
            r.sr.sortingOrder = Mathf.Max(SortY(from.y) + 2, minOrder);
            r.sr.gameObject.SetActive(true);
            runnersOn++; if (gate) gateRunnersOn++;
#if FLEECED_DEV
            DevHooks.RunnersSpawned++;
#endif
            return true;
        }

        /// every frame, after the sheep (UpdateVisuals): the runners fly, land and fade
        void UpdateRunners(float rdt)
        {
            if (runnersOn == 0) return;
            foreach (var r in runners)
            {
                if (!r.on) continue;
                if (r.delay > 0) { r.delay -= rdt; if (r.delay > 0) continue; r.sr.enabled = true; }
                r.t += rdt;
                float k = Mathf.Clamp01(r.t / r.dur);
                Vector2 ground = Vector2.Lerp(r.a, r.b, k);
                Vector2 p = ground + Vector2.up * (r.arc * 4f * k * (1f - k));
                r.sr.transform.position = new Vector3(p.x, p.y, 0);
                r.sr.sortingOrder = Mathf.Max(SortY(ground.y) + 2, r.minOrder); // in front of its door the whole way (the gate's upper lanes arc high over it)
                if (k >= 1f && !r.landed) { r.landed = true; SpawnParticles(r.b, new Color(0.6f, 0.5f, 0.38f), 3, 1.1f); }
                // after landing it melts into the carpet over a fifth of its flight
                float fade = r.t > r.dur ? 1f - (r.t - r.dur) / (r.dur * 0.25f) : 1f;
                if (fade <= 0) { r.on = false; r.sr.gameObject.SetActive(false); runnersOn--; if (r.gate) gateRunnersOn--; continue; }
                if (fade < 1f) r.sr.color = new Color(1, 1, 1, fade);
            }
        }

        /// once a frame, before the sheep visuals: what the agent budget leaves for dying and ghost sheep
        void PlanAgents()
        {
            int named = 0, alive = 0;
            foreach (var s in sheep)
            {
                if (s.dead) continue;
                named++;
                if (s.go != null && s.act != Act.Hidden && s.act != Act.InMachine) alive++;
            }
            namedNow = named; aliveShown = alive;
            ghostBudget = Mathf.Max(0, ScaleMath.AgentCap - alive - runnersOn);
            ghostsShown = 0;
            agentsFrame = Time.frameCount;
        }

        /// a dying or ghost sheep's renderers show while the agent budget has room for them
        void BudgetGhost(Sheep s)
        {
            bool vis = ghostsShown++ < ghostBudget;
            if (s.sr.enabled != vis) s.sr.enabled = vis;
            if (s.shadow != null && s.shadow.enabled != vis) s.shadow.enabled = vis;
        }

#if FLEECED_DEV
        /// creatures drawn now: sheep with a GameObject whose renderer shows, and the runners (tests)
        public int DevVisibleAgents
        {
            get
            {
                int n = runnersOn;
                foreach (var s in sheep) if (s.go != null && s.go.activeInHierarchy && s.sr.enabled) n++;
                return n;
            }
        }
        public int DevRunnersActive { get { return runnersOn; } }
#endif
    }
}
