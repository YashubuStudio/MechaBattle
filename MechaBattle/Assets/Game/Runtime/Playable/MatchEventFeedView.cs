using System.Text;
using MechaBattle.Battlefield;
using MechaBattle.Simulation;
using UnityEngine;
namespace MechaBattle.Playable
{
    // User request 2026-10-16: notify the player about whole-team match events (their own
    // main base under attack), distinct from BattleFeedbackView's "my own shots landed"
    // feedback. Human-UI only - never fed into any BOT decision input.
    internal sealed class MatchEventFeedView
    {
        private const int Slots = 4;
        private const float DisplaySeconds = 4f;
        private readonly string[] messages = new string[Slots];
        private readonly float[] until = new float[Slots];
        private long sequence;
        private int previousTeam = -1;
        public void Reset() { for (int i = 0; i < Slots; i++) { until[i] = 0; messages[i] = null; } sequence = 0; previousTeam = -1; }
        public string Update(BattleSimulation world, int humanTeam, MapDefinition map)
        {
            // A side switch (or first call) must not replay the whole event history as fresh notices.
            if (humanTeam != previousTeam) { previousTeam = humanTeam; sequence = world.CombatEventSequence; }
            float now = Time.unscaledTime;
            long first = System.Math.Max(sequence + 1, world.CombatEventSequence - world.CombatEventCapacity + 1);
            for (long n = first; n <= world.CombatEventSequence; n++)
            {
                if (!world.TryGetCombatEvent(n, out var e) || e.Kind != CombatEventKind.CoreHit) continue;
                int baseIndex = -1;
                for (int i = 0; i < map.mainBases.Length; i++)
                    if (Mathf.Approximately(map.mainBases[i].x, e.X) && Mathf.Approximately(map.mainBases[i].z, e.Z)) { baseIndex = i; break; }
                if (baseIndex < 0 || map.mainBases[baseIndex].team != humanTeam) continue;
                Push(map.mainBases[baseIndex].id + " 本拠点が攻撃されています！", now);
            }
            sequence = world.CombatEventSequence;
            var builder = new StringBuilder();
            for (int i = 0; i < Slots; i++)
                if (now < until[i]) { if (builder.Length > 0) builder.Append('\n'); builder.Append(messages[i]); }
            return builder.ToString();
        }
        private void Push(string message, float now)
        {
            // De-dupe: refresh the existing slot's timer instead of stacking repeats of the same notice.
            for (int i = 0; i < Slots; i++) if (messages[i] == message) { until[i] = now + DisplaySeconds; return; }
            int slot = 0; float oldest = until[0];
            for (int i = 1; i < Slots; i++) if (until[i] < oldest) { oldest = until[i]; slot = i; }
            messages[slot] = message; until[slot] = now + DisplaySeconds;
        }
    }
}
