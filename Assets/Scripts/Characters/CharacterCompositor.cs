using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace BrumaPx.Characters
{
    /// <summary>
    /// Stacks layer sheets into one texture. It runs when the gear changes, never per frame. Characters that wear
    /// the same things share the same texture (enemies of one kind, guards of one town).
    /// Layers whose order is <see cref="CoverFrom"/> or more (armour, weapons, capes, scarves) are marked in the
    /// alpha channel so wounds and Bruma are not painted over them.
    /// </summary>
    public static class CharacterCompositor
    {
        /// <summary>First layer order that covers the body. Body, head and under-clothes stay below it.</summary>
        public const int CoverFrom = 30;

        sealed class Entry
        {
            public RenderTexture texture;
            public int users;
        }

        static readonly Dictionary<string, Entry> Cache = new Dictionary<string, Entry>();
        static readonly List<CharacterLayerSet.Layer> Sorted = new List<CharacterLayerSet.Layer>();
        static readonly int CoverId = Shader.PropertyToID("_Cover");
        static Material _blit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Cache.Clear();
            _blit = null;
        }

        /// <summary>Identity of a stack of layers: same key, same picture.</summary>
        public static string KeyOf(CharacterLayerSet set, List<CharacterLayerSet.Layer> layers)
        {
            Sort(layers);
            var sb = new StringBuilder(set != null ? set.name : "null");
            for (int i = 0; i < Sorted.Count; i++) sb.Append('|').Append(Sorted[i].id);
            return sb.ToString();
        }

        /// <summary>Returns the composed texture for these layers, building it if nobody has it yet.</summary>
        public static Texture Acquire(CharacterLayerSet set, List<CharacterLayerSet.Layer> layers, out string key)
        {
            key = KeyOf(set, layers);
            Entry e;
            if (Cache.TryGetValue(key, out e) && e.texture != null)
            {
                e.users++;
                return e.texture;
            }

            int w = set.columns * set.cell, h = set.rows * set.cell;
            var rt = new RenderTexture(new RenderTextureDescriptor(w, h, GraphicsFormat.R8G8B8A8_SRGB, 0))
            {
                name = "Character " + key,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                hideFlags = HideFlags.HideAndDontSave
            };
            rt.Create();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
            RenderTexture.active = prev;

            if (_blit == null)
            {
                var sh = Shader.Find("Hidden/BrumaPx/LayerBlit");
                if (sh != null) _blit = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            }
            if (_blit != null)
                for (int i = 0; i < Sorted.Count; i++)
                {
                    _blit.SetFloat(CoverId, Sorted[i].order >= CoverFrom ? 1f : 0f);
                    Graphics.Blit(Sorted[i].sheet, rt, _blit);
                }

            Cache[key] = new Entry { texture = rt, users = 1 };
            return rt;
        }

        /// <summary>Gives a texture back. It is freed when nobody uses it.</summary>
        public static void Release(string key)
        {
            Entry e;
            if (string.IsNullOrEmpty(key) || !Cache.TryGetValue(key, out e)) return;
            e.users--;
            if (e.users > 0) return;
            if (e.texture != null)
            {
                e.texture.Release();
                Object.Destroy(e.texture);
            }
            Cache.Remove(key);
        }

        static void Sort(List<CharacterLayerSet.Layer> layers)
        {
            Sorted.Clear();
            for (int i = 0; i < layers.Count; i++)
                if (layers[i] != null && layers[i].sheet != null && !Sorted.Contains(layers[i])) Sorted.Add(layers[i]);
            // Stable insertion sort: layers with the same order keep the order of the set.
            for (int i = 1; i < Sorted.Count; i++)
            {
                var x = Sorted[i];
                int j = i - 1;
                while (j >= 0 && Sorted[j].order > x.order) { Sorted[j + 1] = Sorted[j]; j--; }
                Sorted[j + 1] = x;
            }
        }
    }
}
