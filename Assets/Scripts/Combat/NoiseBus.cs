using System;
using UnityEngine;

namespace BrumaPx.Combat
{
    /// <summary>
    /// Sounds in the world. The hero (steps, rolls, swings) and the fights call Emit; enemies listen and decide
    /// whether the sound reached them.
    /// </summary>
    public static class NoiseBus
    {
        /// <summary>World position of the sound and how far it carries.</summary>
        public static event Action<Vector3, float> Noise;

        public static void Emit(Vector3 position, float radius)
        {
            if (radius <= 0f) return;
            Action<Vector3, float> handler = Noise;
            if (handler != null) handler(position, radius);
        }

        // Keeps listeners from leaking between play sessions when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetListeners()
        {
            Noise = null;
        }
    }
}
