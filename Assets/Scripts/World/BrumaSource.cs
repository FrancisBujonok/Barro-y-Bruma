using System.Collections.Generic;
using BrumaPx.Data;
using BrumaPx.Player;
using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>
    /// A thing in the world that spreads the Bruma by contact, not by air. Two kinds:
    /// Water: standing in it doses some zones every second (a tainted pool soaks the knees and calves).
    /// Corpse: pressing E searches it once; that dirties the hands, and may give an item.
    /// The zones are named by id from the body profile.
    /// </summary>
    public sealed class BrumaSource : MonoBehaviour
    {
        public enum Kind { Water, Corpse }

        public static readonly List<BrumaSource> All = new List<BrumaSource>();

        [SerializeField] string displayName = "Foco de contagio";
        [SerializeField] Kind kind = Kind.Water;
        [SerializeField, Min(0.3f)] float radius = 1.4f;
        [Tooltip("Ids of the body zones that receive the Bruma.")]
        [SerializeField] string[] zoneIds = new string[0];
        [Tooltip("Water: Bruma per second to each zone. Corpse: Bruma once to each zone.")]
        [SerializeField, Min(0f)] float dose = 0.5f;
        [Tooltip("Water: resistance lost per second. Corpse: resistance lost once.")]
        [SerializeField, Min(0f)] float resistCost = 1f;
        [Tooltip("Corpse only: what the hero finds.")]
        [SerializeField] ItemDef loot;
        [SerializeField, Min(1)] int lootCount = 1;

        bool _searched, _near;
        int[] _zones;

        public string DisplayName { get { return displayName; } }
        public bool Searched { get { return _searched; } }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }

        bool IsInside(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.sqrMagnitude <= radius * radius;
        }

        void ResolveZones(HeroBody body)
        {
            if (_zones != null) return;
            _zones = new int[zoneIds.Length];
            for (int i = 0; i < zoneIds.Length; i++) _zones[i] = body.IndexOf(zoneIds[i]);
        }

        void Update()
        {
            HeroBody body = HeroBody.Instance;
            if (body == null) return;

            bool inside = IsInside(body.transform.position);
            if (!inside)
            {
                _near = false;
                return;
            }

            if (kind == Kind.Water)
            {
                ResolveZones(body);
                float dt = Mathf.Min(Time.deltaTime, 0.05f);
                body.Absorb(resistCost * dt);
                for (int i = 0; i < _zones.Length; i++) body.Dose(_zones[i], dose * dt);
                if (!_near) body.Say(displayName + ": te moja las piernas");
            }
            else if (!_searched)
            {
                if (!_near) body.Say("E: registrar el cuerpo (te ensucias las manos)");
                if (!Inventory.MenuOpen && DioramaInput.InteractPressed()) TrySearch();
            }
            _near = true;
        }

        /// <summary>Searches the body (corpse only, once, and only when the hero is next to it).</summary>
        public bool TrySearch()
        {
            HeroBody body = HeroBody.Instance;
            if (kind != Kind.Corpse || _searched || body == null || !IsInside(body.transform.position)) return false;
            _searched = true;
            ResolveZones(body);
            body.Absorb(resistCost);
            for (int i = 0; i < _zones.Length; i++) body.Dose(_zones[i], dose);

            string found = string.Empty;
            if (loot != null)
            {
                var inv = body.GetComponent<Inventory>();
                if (inv != null && inv.Add(loot, lootCount) == 0) found = " Encuentras: " + loot.DisplayName;
            }
            body.Say("Registras el cuerpo." + found);
            return true;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = kind == Kind.Water ? new Color(0.4f, 0.55f, 0.65f, 0.8f) : new Color(0.6f, 0.4f, 0.4f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
