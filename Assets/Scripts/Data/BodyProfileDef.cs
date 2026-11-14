using System;
using UnityEngine;

namespace BrumaPx.Data
{
    /// <summary>What a zone of the body can take away from the hero as the Bruma spreads in it.</summary>
    public enum BodyStat
    {
        VisionRange,
        MaxHealth,
        StaminaRegen,
        MeleeDamage,
        HeavyDamage,
        AttackRecovery,
        ParryWindow,
        GuardLeak,
        ItemSpeed,
        WalkSpeed,
        RunSpeed,
        DodgeDistance,
        DodgeCost
    }

    /// <summary>How far the Bruma has gone in one zone (the cut-offs live in BrumaTuning).</summary>
    public enum BrumaStage { Clean, Exposed, Infected, Taken }

    public static class BodyStatInfo
    {
        public const int Count = 13;

        public static string Label(BodyStat stat)
        {
            switch (stat)
            {
                case BodyStat.VisionRange: return "Visión";
                case BodyStat.MaxHealth: return "Vida máxima";
                case BodyStat.StaminaRegen: return "Recuperar aguante";
                case BodyStat.MeleeDamage: return "Daño de golpe";
                case BodyStat.HeavyDamage: return "Daño del golpe pesado";
                case BodyStat.AttackRecovery: return "Tiempo tras golpear";
                case BodyStat.ParryWindow: return "Margen del parry";
                case BodyStat.GuardLeak: return "Daño que pasa la guardia";
                case BodyStat.ItemSpeed: return "Tiempo para usar objetos";
                case BodyStat.WalkSpeed: return "Caminar";
                case BodyStat.RunSpeed: return "Correr";
                case BodyStat.DodgeDistance: return "Distancia de esquiva";
                case BodyStat.DodgeCost: return "Costo de la esquiva";
                default: return stat.ToString();
            }
        }

        /// <summary>True when a bigger number hurts the hero (recovery time, damage that passes the guard...).</summary>
        public static bool HigherIsWorse(BodyStat stat)
        {
            return stat == BodyStat.AttackRecovery || stat == BodyStat.GuardLeak
                || stat == BodyStat.ItemSpeed || stat == BodyStat.DodgeCost;
        }

        public static string StageName(BrumaStage stage)
        {
            switch (stage)
            {
                case BrumaStage.Exposed: return "Expuesta";
                case BrumaStage.Infected: return "Infectada";
                case BrumaStage.Taken: return "Tomada";
                default: return "Limpia";
            }
        }
    }

    /// <summary>
    /// One effect of a zone. It starts when the zone reaches startsAt (0 to 99) and grows in a straight line up to
    /// atFull when the zone is at 100. A zone can have several, so a worse infection costs more things.
    /// atFull is a change of the stat: -0.25 means 25% less, +0.3 means 30% more.
    /// </summary>
    [Serializable]
    public sealed class ZoneEffect
    {
        public BodyStat stat;
        [Range(0f, 99f)] public float startsAt;
        public float atFull = -0.2f;
    }

    /// <summary>One zone of the body window.</summary>
    [Serializable]
    public sealed class BodyZone
    {
        public string id = "zona";
        public string displayName = "Zona";
        [Tooltip("Centre of the zone on the silhouette (0 to 1, y up).")]
        public Vector2 center = new Vector2(0.5f, 0.5f);
        [Tooltip("Size of the zone on the silhouette (0 to 1 of the silhouette area).")]
        public Vector2 extent = new Vector2(0.1f, 0.1f);
        [Tooltip("How much the air reaches this zone (0 = covered, 1 = bare).")]
        [Range(0f, 1f)] public float airWeight = 0.5f;
        [Tooltip("Chance weight of a blow landing here.")]
        [Min(0f)] public float hitWeight = 0.07f;
        [Tooltip("Protection from clothes and gear (0 to 1). No gear changes it yet.")]
        [Range(0f, 1f)] public float cover;
        [Tooltip("Left arm side: a blow taken behind the guard tends to land here.")]
        public bool guardSide;
        public ZoneEffect[] effects = new ZoneEffect[0];
    }

    /// <summary>All the zones of the hero's body. Create from the menu "Barro y Bruma/Body Profile".</summary>
    [CreateAssetMenu(menuName = "Barro y Bruma/Body Profile", fileName = "BodyProfile")]
    public sealed class BodyProfileDef : ScriptableObject
    {
        [SerializeField] BodyZone[] _zones = new BodyZone[0];

        public BodyZone[] Zones { get { return _zones; } }

        public int IndexOf(string id)
        {
            for (int i = 0; i < _zones.Length; i++)
                if (_zones[i] != null && _zones[i].id == id) return i;
            return -1;
        }
    }
}
