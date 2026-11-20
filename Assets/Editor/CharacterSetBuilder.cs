using System;
using System.Collections.Generic;
using System.IO;
using BrumaPx.Characters;
using BrumaPx.Data;
using UnityEditor;
using UnityEngine;

namespace BrumaPx.EditorTools
{
    /// <summary>
    /// Builds everything the sprite hero needs from the sheets in Assets/Characters/Hero: the layer set, the two
    /// materials, three weapons to test with and the config in Resources. Run it again after replacing or adding
    /// sheets: a PNG that is not listed in _meta.json is added too (order 50, or the number after "@" in its name,
    /// e.g. "capucha_medico@40.png").
    /// Menu: BrumaPx / Characters / Rebuild Hero Layer Set.
    /// </summary>
    public static class CharacterSetBuilder
    {
        const string SheetFolder = "Assets/Characters/Hero";
        const string DataFolder = "Assets/Data/Personajes";
        const string WeaponFolder = "Assets/Data/Armas";
        const string MaterialFolder = "Assets/Art/Materials/Diorama";

        [Serializable] class MetaLayer { public string id; public int z; }
        [Serializable] class MetaAnim { public string name; public int frames; public int dirs; }
        [Serializable] class MetaClip { public string name; public int frames; public int w; public int h; public float feetX; public float feetY; public int hitFrom; public int hitTo; }
        [Serializable]
        class Meta
        {
            public int cell = 128, cols = 9, rows = 13, views = 4;
            public MetaClip[] clips = new MetaClip[0];
            public MetaAnim[] anims = new MetaAnim[0];
            public MetaLayer[] layers = new MetaLayer[0];
            public string[] zones = new string[0];
        }

        [MenuItem("BrumaPx/Characters/Rebuild Hero Layer Set")]
        public static void Rebuild()
        {
            Debug.Log(RebuildAndReport());
        }

        public static string RebuildAndReport()
        {
            var log = new System.Text.StringBuilder();
            Directory.CreateDirectory(DataFolder);
            Directory.CreateDirectory(WeaponFolder);
            Directory.CreateDirectory("Assets/Resources");
            AssetDatabase.Refresh();

            var meta = new Meta();
            string metaPath = SheetFolder + "/_meta.json";
            if (File.Exists(metaPath)) JsonUtility.FromJsonOverwrite(File.ReadAllText(metaPath), meta);
            var order = new Dictionary<string, int>();
            foreach (var l in meta.layers) order[l.id] = l.z;

            var set = LoadOrCreate<CharacterLayerSet>(DataFolder + "/HeroLayers.asset");
            set.cell = meta.cell;
            set.columns = meta.cols;
            set.rows = meta.rows;
            set.feet = new Vector2(meta.cell * 0.5f, (meta.cell - 64) * 0.5f + 4f);
            set.zoneIds = meta.zones;
            set.zoneMap = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetFolder + "/_zonas.png");
            set.baseLayers = new[] { "cuerpo", "cabeza", "zapatos", "calzas", "camisa", "pelo" };
            set.hiddenUnderHeadgear = new[] { "pelo" };

            set.layers.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SheetFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                if (file.StartsWith("_")) continue;
                string id = file;
                int z = 50;
                int at = file.IndexOf('@');
                if (at > 0)
                {
                    id = file.Substring(0, at);
                    int.TryParse(file.Substring(at + 1), out z);
                }
                else if (order.ContainsKey(id)) z = order[id];
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex.width != set.columns * set.cell || tex.height != set.rows * set.cell)
                    log.AppendLine("AVISO: " + file + " mide " + tex.width + "x" + tex.height + ", se esperaba " + (set.columns * set.cell) + "x" + (set.rows * set.cell));
                set.layers.Add(new CharacterLayerSet.Layer { id = id, sheet = tex, order = z });
            }
            set.layers.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : string.CompareOrdinal(a.id, b.id));

            set.clips.Clear();
            foreach (var a in meta.anims)
            {
                bool idle = a.name == "idle", hurt = a.name == "hurt", slash = a.name == "slash";
                set.clips.Add(new CharacterLayerSet.Clip
                {
                    name = a.name, frames = a.frames, directions = a.dirs,
                    fps = idle ? 2f : (slash ? 10f : 8f), loop = !(hurt || slash)
                });
            }
            EditorUtility.SetDirty(set);
            log.AppendLine("Capas: " + set.layers.Count + ", clips: " + set.clips.Count + ", zonas: " + set.zoneIds.Length + ", mapa de zonas: " + (set.zoneMap != null));

            var mat = LoadOrCreateMaterial(MaterialFolder + "/PP_Sprite.mat", "BrumaPx/Diorama/PPSprite", log);
            var shadow = LoadOrCreateMaterial(MaterialFolder + "/PP_SpriteShadow.mat", "BrumaPx/Diorama/PPSpriteShadow", log);
            // Keeps the hidden compositor shader in builds.
            LoadOrCreateMaterial("Assets/Resources/LayerBlit.mat", "Hidden/BrumaPx/LayerBlit", log);

            var sword = Weapon("EspadaDeMano", "arma_ligera", "Espada de mano", "Rápida y corta. Deja la otra mano libre para un escudo.", WeaponClass.Ligera, 1f, 1f, 1f, 1f, 1f);
            var axe = Weapon("HachaDeGuerra", "arma_pesada", "Hacha de guerra", "A dos manos. Pega fuerte, tarda y cansa.", WeaponClass.Pesada, 1.6f, 1.5f, 1.4f, 1.15f, 1f);
            var shield = Weapon("EscudoDeMadera", "escudo", "Escudo de madera", "Deja pasar menos daño en la guardia. Solo con arma ligera.", WeaponClass.Escudo, 0f, 1f, 1f, 1f, 0.6f);

            var config = LoadOrCreate<HeroSpriteConfig>("Assets/Resources/HeroSpriteConfig.asset");
            config.layers = set;
            if (config.blenderLayers == null)
                config.blenderLayers = AssetDatabase.LoadAssetAtPath<CharacterLayerSet>(DataFolder + "/HeroBlender.asset");
            config.material = mat;
            config.shadowMaterial = shadow;
            if (config.weapon == null) config.weapon = sword;
            config.testWeapons = new[] { sword, axe };
            config.testShield = shield;
            EditorUtility.SetDirty(config);

            AssetDatabase.SaveAssets();
            log.AppendLine("Config: Assets/Resources/HeroSpriteConfig.asset, useSprite=" + config.useSprite);
            return log.ToString();
        }

        // ---- Characters rendered in Blender ----------------------------------------------------------------

        [MenuItem("BrumaPx/Characters/Rebuild Blender Sets (Hero, Medico, Rey Sapo)")]
        public static void RebuildBlender()
        {
            Debug.Log(RebuildBlenderAndReport());
        }

        /// <summary>
        /// Builds the sets of the characters rendered in Blender: the hero (Assets/Characters/HeroBlender, one grid,
        /// 8 views, zone map), the plague doctor (Assets/Characters/MedicoPeste) and the Rey Sapo
        /// (Assets/Characters/ReySapo, one cropped sheet per clip). Also writes the actor configs in
        /// Resources/Actors and points the hero config at the Blender hero.
        /// </summary>
        public static string RebuildBlenderAndReport()
        {
            var log = new System.Text.StringBuilder();
            Directory.CreateDirectory(DataFolder);
            Directory.CreateDirectory("Assets/Resources/Actors");
            AssetDatabase.Refresh();

            var mat = LoadOrCreateMaterial(MaterialFolder + "/PP_Sprite.mat", "BrumaPx/Diorama/PPSprite", log);
            var shadow = LoadOrCreateMaterial(MaterialFolder + "/PP_SpriteShadow.mat", "BrumaPx/Diorama/PPSpriteShadow", log);
            LoadOrCreateMaterial("Assets/Resources/LayerBlit.mat", "Hidden/BrumaPx/LayerBlit", log);

            // The hero has one cropped sheet per clip (with its zone map), like the Rey Sapo.
            var hero = ClipSheetSet("Assets/Characters/HeroBlender", DataFolder + "/HeroBlender.asset", log);
            if (hero != null) AddReverse(hero, "sheathe", "draw");
            if (hero != null)
            {
                var config = LoadOrCreate<HeroSpriteConfig>("Assets/Resources/HeroSpriteConfig.asset");
                config.blenderLayers = hero;
                config.blenderHero = true;
                if (config.material == null) config.material = mat;
                if (config.shadowMaterial == null) config.shadowMaterial = shadow;
                EditorUtility.SetDirty(config);
            }

            var medico = GridSet("Assets/Characters/MedicoPeste", DataFolder + "/MedicoPeste.asset", log);
            if (medico != null) Actor("MedicoPeste", medico, mat, shadow);

            var sapo = ClipSheetSet("Assets/Characters/ReySapo", DataFolder + "/ReySapo.asset", log);
            if (sapo != null) Actor("ReySapo", sapo, mat, shadow);

            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        static Meta ReadMeta(string folder, System.Text.StringBuilder log)
        {
            string path = folder + "/_meta.json";
            if (!File.Exists(path)) { log.AppendLine("FALTA " + path); return null; }
            var meta = new Meta();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(path), meta);
            return meta;
        }

        // Fps and looping by clip name, shared by every Blender character.
        static void Timing(string name, out float fps, out bool loop)
        {
            loop = true;
            fps = 8f;
            switch (name)
            {
                case "idle": case "p2idle": case "idle_s": fps = 5f; break;
                case "walk": case "walk_s": fps = 9f; break;
                case "sprint": case "sprint_s": fps = 14f; break;
                case "guard_walk": fps = 7f; break;
                case "guard": case "block": case "parry": loop = false; break;
                case "roll": case "roll_s": case "hurt_s": case "sheathe": case "draw": loop = false; break;
                case "light1": case "light2": case "light3": case "heavy1": case "heavy2": case "heavy3": loop = false; fps = 10f; break;
                case "crawl": fps = 7f; break;
                case "crawl2": fps = 9f; break;
                case "slash": fps = 10f; loop = false; break;
                case "bite": case "bite2": fps = 10f; loop = false; break;
                case "hop": case "tongue": fps = 10f; loop = false; break;
                case "death": fps = 8f; loop = false; break;
                case "hurt": loop = false; break;
            }
        }

        // One shared grid, every clip one row per view (the hero, the doctor).
        static CharacterLayerSet GridSet(string folder, string assetPath, System.Text.StringBuilder log)
        {
            var meta = ReadMeta(folder, log);
            if (meta == null) return null;
            var body = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/cuerpo.png");
            if (body == null) { log.AppendLine("FALTA " + folder + "/cuerpo.png"); return null; }

            var set = LoadOrCreate<CharacterLayerSet>(assetPath);
            set.views = meta.views;
            set.cell = meta.cell;
            set.columns = meta.cols;
            set.rows = meta.rows;
            // Blender cells: feet centred, 36 texels up from the bottom of a 128 cell.
            set.feet = new Vector2(meta.cell * 0.5f, meta.cell * 36f / 128f);
            set.zoneIds = meta.zones;
            set.zoneMap = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/_zonas.png");
            set.baseLayers = new[] { "cuerpo" };
            set.hiddenUnderHeadgear = new string[0];
            set.layers.Clear();
            set.layers.Add(new CharacterLayerSet.Layer { id = "cuerpo", sheet = body, order = 10 });
            if (body.width != set.columns * set.cell || body.height != set.rows * set.cell)
                log.AppendLine("AVISO: " + folder + "/cuerpo.png mide " + body.width + "x" + body.height + ", se esperaba " + (set.columns * set.cell) + "x" + (set.rows * set.cell));

            set.clips.Clear();
            foreach (var a in meta.anims)
            {
                float fps; bool loop;
                Timing(a.name, out fps, out loop);
                set.clips.Add(new CharacterLayerSet.Clip { name = a.name, frames = a.frames, directions = a.dirs > 0 ? a.dirs : meta.views, fps = fps, loop = loop });
            }
            EditorUtility.SetDirty(set);
            log.AppendLine(Path.GetFileName(folder) + ": " + set.clips.Count + " clips, " + set.views + " vistas, zonas: " + (set.zoneMap != null));
            return set;
        }

        // One cropped sheet per clip (big creatures).
        static CharacterLayerSet ClipSheetSet(string folder, string assetPath, System.Text.StringBuilder log)
        {
            var meta = ReadMeta(folder, log);
            if (meta == null) return null;
            var set = LoadOrCreate<CharacterLayerSet>(assetPath);
            set.views = meta.views;
            set.layers.Clear();
            set.baseLayers = new string[0];
            set.hiddenUnderHeadgear = new string[0];
            set.zoneIds = meta.zones != null ? meta.zones : new string[0];
            set.zoneMap = null;
            set.clips.Clear();
            int texels = 0;
            foreach (var c in meta.clips)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + c.name + ".png");
                if (tex == null) { log.AppendLine("FALTA " + folder + "/" + c.name + ".png"); continue; }
                if (tex.width != c.frames * c.w || tex.height != meta.views * c.h)
                    log.AppendLine("AVISO: " + c.name + ".png mide " + tex.width + "x" + tex.height + ", se esperaba " + (c.frames * c.w) + "x" + (meta.views * c.h));
                float fps; bool loop;
                Timing(c.name, out fps, out loop);
                set.clips.Add(new CharacterLayerSet.Clip
                {
                    name = c.name, frames = c.frames, directions = meta.views, fps = fps, loop = loop,
                    sheet = tex, size = new Vector2Int(c.w, c.h), feet = new Vector2(c.feetX, c.feetY),
                    zoneSheet = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/_zonas_" + c.name + ".png"),
                    hitFrom = c.hitFrom, hitTo = c.hitTo
                });
                texels += tex.width * tex.height;
            }
            EditorUtility.SetDirty(set);
            log.AppendLine(Path.GetFileName(folder) + ": " + set.clips.Count + " clips con hoja propia, " + (texels * 4 / (1024 * 1024)) + " MB sin comprimir");
            return set;
        }

        // A clip that plays another one backwards (drawing the sword is putting it away in reverse).
        static void AddReverse(CharacterLayerSet set, string source, string name)
        {
            int i = set.ClipIndex(source);
            if (i < 0) return;
            var c = set.clips[i];
            set.clips.Add(new CharacterLayerSet.Clip
            {
                name = name, frames = c.frames, directions = c.directions, fps = c.fps, loop = false,
                sheet = c.sheet, size = c.size, feet = c.feet, zoneSheet = c.zoneSheet, reverse = true
            });
            EditorUtility.SetDirty(set);
        }

        static void Actor(string id, CharacterLayerSet set, Material mat, Material shadow)
        {
            var a = LoadOrCreate<ActorSpriteConfig>("Assets/Resources/" + ActorSpriteConfig.Folder + id + ".asset");
            a.set = set;
            a.material = mat;
            a.shadowMaterial = shadow;
            EditorUtility.SetDirty(a);
        }

        static WeaponDef Weapon(string file, string id, string label, string text, WeaponClass cls, float damage, float stamina, float swing, float range, float leak)
        {
            string path = WeaponFolder + "/" + file + ".asset";
            var w = AssetDatabase.LoadAssetAtPath<WeaponDef>(path);
            if (w != null) return w;
            w = ScriptableObject.CreateInstance<WeaponDef>();
            var so = new SerializedObject(w);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_displayName").stringValue = label;
            so.FindProperty("_description").stringValue = text;
            so.FindProperty("_class").enumValueIndex = (int)cls;
            so.FindProperty("_damage").floatValue = damage;
            so.FindProperty("_staminaCost").floatValue = stamina;
            so.FindProperty("_swingTime").floatValue = swing;
            so.FindProperty("_range").floatValue = range;
            so.FindProperty("_guardLeak").floatValue = leak;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(w, path);
            return w;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static Material LoadOrCreateMaterial(string path, string shaderName, System.Text.StringBuilder log)
        {
            var sh = Shader.Find(shaderName);
            if (sh == null) { log.AppendLine("FALTA el shader " + shaderName); return null; }
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != sh) m.shader = sh;
            int n = ShaderUtil.GetShaderMessageCount(sh);
            if (n > 0)
                foreach (var msg in ShaderUtil.GetShaderMessages(sh)) log.AppendLine("  [" + msg.severity + "] " + shaderName + ": " + msg.message + " (" + msg.line + ")");
            return m;
        }
    }
}
