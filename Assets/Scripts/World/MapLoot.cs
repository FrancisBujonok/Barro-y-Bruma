using BrumaPx.Data;
using BrumaPx.Player;
using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>
    /// Something lying on the ground that the hero picks up with the interact key: a chest, herbs, a bandage or a
    /// piece of clothing. Prototype visuals; the contents come from the map markers.
    /// </summary>
    public sealed class MapLoot : MonoBehaviour
    {
        public string title = "Cofre";
        public ItemDef[] items;
        public int[] counts;
        public ArmorDef armor;
        public float radius = 1.7f;
        public GameObject visual;

        static MapLoot _near;
        static int _nearFrame = -1;
        bool _taken;

        void Update()
        {
            if (_taken) return;
            var hero = DioramaHeroCache.Hero;
            if (hero == null) return;
            Vector3 d = transform.position - hero.position;
            d.y = 0f;
            if (d.sqrMagnitude > radius * radius) return;

            _near = this;
            _nearFrame = Time.frameCount;

            if (DioramaInput.InteractPressed()) Take(hero);
        }

        void Take(Transform hero)
        {
            string got = string.Empty;
            var inv = hero.GetComponent<Inventory>();
            var eq = hero.GetComponent<Equipment>();

            if (armor != null && eq != null && eq.Add(armor)) got += armor.name + ", ";
            if (items != null && inv != null)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i] == null) continue;
                    int n = counts != null && i < counts.Length ? counts[i] : 1;
                    int left = inv.Add(items[i], n);
                    if (n - left > 0) got += items[i].DisplayName + " x" + (n - left) + ", ";
                }
            }

            if (got.Length == 0) { MapWorld.Banner("No te cabe nada mas", 2f); return; }
            _taken = true;
            if (visual != null) visual.SetActive(false);
            MapWorld.Banner(title + ": " + got.TrimEnd(',', ' '), 3f);
        }

        /// <summary>The prompt line for the loot the hero stands next to (empty when none).</summary>
        public static string Prompt()
        {
            if (_near == null || _nearFrame < Time.frameCount - 1 || _near._taken) return string.Empty;
            return "[Interactuar] " + _near.title;
        }
    }

    /// <summary>Finds the hero once; props do not need a reference wired in the scene.</summary>
    public static class DioramaHeroCache
    {
        static Transform _hero;

        public static Transform Hero
        {
            get
            {
                if (_hero == null)
                {
                    var h = Object.FindFirstObjectByType<DioramaHero>();
                    if (h != null) _hero = h.transform;
                }
                return _hero;
            }
        }
    }
}
