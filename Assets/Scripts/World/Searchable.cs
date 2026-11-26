using System.Collections.Generic;
using BrumaPx.Data;
using BrumaPx.Player;
using BrumaPx.UI;
using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>
    /// Furniture that can be searched with the interact key: a chest, a wardrobe, a shelf, barrels, crates.
    /// What it holds is rolled from a loot table (MapWorld.RollLoot) with a seed taken from where it stands, so the
    /// same piece always holds the same; once emptied it stays empty for the rest of the session, even when its
    /// chunk is built again. What does not fit in the bag stays inside for later. A small window shows the result.
    /// </summary>
    public sealed class Searchable : MonoBehaviour
    {
        [Tooltip("Title of the loot window.")]
        public string displayName = "Arcon";
        [Tooltip("What the prompt says after 'Revisar'.")]
        public string what = "el arcon";
        [Tooltip("Loot table: cofre, armario, estante, despensa, botica, medico, barril, cajon, canasto.")]
        public string table = "barril";
        public float radius = 1.4f;

        sealed class Left
        {
            public List<KeyValuePair<ItemDef, int>> items = new List<KeyValuePair<ItemDef, int>>();
            public ArmorDef armor;
        }

        static readonly List<Searchable> Active = new List<Searchable>();
        static readonly HashSet<string> Emptied = new HashSet<string>();
        static readonly Dictionary<string, Left> Leftovers = new Dictionary<string, Left>();
        static int _frame = -1;
        static int _targetFrame = -10;

        /// <summary>The hero stands next to something to search (this frame or the last): other interactions wait.</summary>
        public static bool HasTarget { get { return _targetFrame >= Time.frameCount - 1; } }

        string Key
        {
            get
            {
                Vector3 p = transform.position;
                return table + ":" + Mathf.RoundToInt(p.x * 4f) + "," + Mathf.RoundToInt(p.z * 4f);
            }
        }

        public bool IsEmptied { get { return Emptied.Contains(Key); } }

        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        void Update()
        {
            // one of them picks the nearest piece for everybody, once a frame
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            var hero = DioramaHeroCache.Hero;
            if (hero == null) return;
            Searchable best = null;
            float bd = float.MaxValue;
            for (int i = 0; i < Active.Count; i++)
            {
                var s = Active[i];
                if (s == null || s.IsEmptied) continue;
                Vector3 d = s.transform.position - hero.position;
                d.y = 0f;
                float dist = d.sqrMagnitude;
                if (dist < s.radius * s.radius && dist < bd) { bd = dist; best = s; }
            }
            if (best == null) return;
            _targetFrame = Time.frameCount;
            MapLoot.SetPrompt("[Interactuar] Revisar " + best.what);
            if (DioramaInput.InteractPressed()) best.Search(hero);
        }

        void Search(Transform hero)
        {
            string key = Key;
            Left content;
            if (!Leftovers.TryGetValue(key, out content))
            {
                content = new Left();
                MapWorld.RollLoot(table, key.GetHashCode(), content.items, out content.armor);
            }

            var inv = hero.GetComponent<Inventory>();
            var eq = hero.GetComponent<Equipment>();
            var shown = new List<LootWindow.Entry>();
            var left = new Left();

            if (content.armor != null)
            {
                bool ok = eq != null && eq.Add(content.armor);
                shown.Add(new LootWindow.Entry(content.armor.name, new Color32(0x6B, 0x2A, 0x24, 255), 1, !ok));
                if (!ok) left.armor = content.armor;
            }
            foreach (var kv in content.items)
            {
                if (kv.Key == null || kv.Value <= 0) continue;
                int notFit = inv != null ? inv.Add(kv.Key, kv.Value) : kv.Value;
                int got = kv.Value - notFit;
                if (got > 0) shown.Add(new LootWindow.Entry(kv.Key.DisplayName, kv.Key.Tint, got, false));
                if (notFit > 0)
                {
                    shown.Add(new LootWindow.Entry(kv.Key.DisplayName, kv.Key.Tint, notFit, true));
                    left.items.Add(new KeyValuePair<ItemDef, int>(kv.Key, notFit));
                }
            }

            if (left.armor == null && left.items.Count == 0)
            {
                Emptied.Add(key);
                Leftovers.Remove(key);
            }
            else Leftovers[key] = left;

            LootWindow.Show(displayName, shown, transform.position);
        }
    }
}
