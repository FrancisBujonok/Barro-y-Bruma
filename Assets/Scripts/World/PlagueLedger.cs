using System;
using System.Collections.Generic;
using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>How the plague stands in one person who is not the hero.</summary>
    public enum NpcPlagueState { Healthy, Exposed, Sick, Dead, Cured }

    /// <summary>
    /// The book of who is sick. Key NPCs are recorded here by id; their state moves by chapter and by what the hero
    /// does (treat, isolate, infect, ignore), and the endings read it. No NPC uses it yet: it is the place they will
    /// plug into when chapter one exists. A person who is not in the book is Healthy.
    /// </summary>
    public static class PlagueLedger
    {
        static readonly Dictionary<string, NpcPlagueState> States = new Dictionary<string, NpcPlagueState>();

        public static event Action<string, NpcPlagueState> Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            States.Clear();
            Changed = null;
        }

        public static NpcPlagueState Get(string id)
        {
            NpcPlagueState s;
            return !string.IsNullOrEmpty(id) && States.TryGetValue(id, out s) ? s : NpcPlagueState.Healthy;
        }

        public static void Set(string id, NpcPlagueState state)
        {
            if (string.IsNullOrEmpty(id) || Get(id) == state) return;
            States[id] = state;
            if (Changed != null) Changed(id, state);
        }

        /// <summary>Healthy, then Exposed, then Sick, then Dead. A cured or dead person stays as they are.</summary>
        public static NpcPlagueState Worsen(string id)
        {
            NpcPlagueState now = Get(id);
            NpcPlagueState next = now;
            if (now == NpcPlagueState.Healthy) next = NpcPlagueState.Exposed;
            else if (now == NpcPlagueState.Exposed) next = NpcPlagueState.Sick;
            else if (now == NpcPlagueState.Sick) next = NpcPlagueState.Dead;
            Set(id, next);
            return next;
        }

        public static int Count(NpcPlagueState state)
        {
            int n = 0;
            foreach (var kv in States) if (kv.Value == state) n++;
            return n;
        }
    }
}
