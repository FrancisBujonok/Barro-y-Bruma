using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BrumaPx.Atmosphere;
using BrumaPx.Combat;
using BrumaPx.World;
using UnityEditor;
using UnityEngine;

namespace BrumaPx.EditorTools
{
    /// <summary>
    /// Turns the buildings exported from Blender (tools/blender/edificio_kit.py) into Unity assets. Each folder in
    /// Assets/Art/Buildings/&lt;Name&gt;/ holds building.json (meshes by part, plus doors, hatch, obstacles, lights) and
    /// atlas.png (the baked palette texture). The result is Assets/Resources/Buildings/&lt;Name&gt;.prefab, which
    /// MapWorld spawns by name.
    /// </summary>
    public static class BuildingImporter
    {
        const string Root = "Assets/Art/Buildings";
        const string PrefabDir = "Assets/Resources/Buildings";
        const string ShadowMat = "Assets/Art/Materials/Diorama/PP_Shadow.mat";
        // the characters read about 1.4 times taller than a real person; buildings are scaled to match
        const float DefaultScale = 1.4f;
        static float S = 1f;

        [MenuItem("BrumaPx/Buildings/Import Blender Buildings")]
        public static void ImportAll() { Debug.Log(ImportAndReport()); }

        public static string ImportAndReport()
        {
            var log = new StringBuilder();
            if (!Directory.Exists(Root)) return "No hay " + Root;
            foreach (var dir in Directory.GetDirectories(Root))
            {
                string json = Path.Combine(dir, "building.json").Replace('\\', '/');
                if (!File.Exists(json)) continue;
                log.AppendLine(Import(dir.Replace('\\', '/')));
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return log.ToString();
        }

        // ------------------------------------------------------------------------------------------ one building

        static string Import(string dir)
        {
            var data = (Dictionary<string, object>)Json.Parse(File.ReadAllText(dir + "/building.json"));
            string name = (string)data["name"];
            S = data.ContainsKey("scale") ? (float)(double)data["scale"] : DefaultScale;
            bool props = data.ContainsKey("kind") && (string)data["kind"] == "props";

            // texture: point-sampled, uncompressed, no mipmaps
            string texPath = dir + "/atlas.png";
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            ti.textureType = TextureImporterType.Default;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.sRGBTexture = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = 2048;
            ti.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            var shader = Shader.Find("BrumaPx/Diorama/PPToonTex");
            var mat = Mat(dir + "/" + name + ".mat", shader, tex, false);
            // how much the lit side turns to the warm light (leaves keep their green with less)
            mat.SetFloat("_WarmMix", data.ContainsKey("warm") ? (float)(double)data["warm"] : 0.3f);
            mat.SetFloat("_CoolMix", data.ContainsKey("cool") ? (float)(double)data["cool"] : 0.4f);
            var glow = Mat(dir + "/" + name + "_glow.mat", shader, tex, true);
            var shadow = AssetDatabase.LoadAssetAtPath<Material>(ShadowMat);

            // meshes, all inside one asset
            string meshPath = dir + "/" + name + "_meshes.asset";
            if (AssetDatabase.LoadMainAssetAtPath(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);
            var parts = (Dictionary<string, object>)data["parts"];
            var meshes = new Dictionary<string, Mesh>();
            bool first = true;
            foreach (var kv in parts)
            {
                var m = BuildMesh(kv.Key, (Dictionary<string, object>)kv.Value);
                if (first) { AssetDatabase.CreateAsset(m, meshPath); first = false; }
                else AssetDatabase.AddObjectToAsset(m, meshPath);
                meshes[kv.Key] = m;
            }

            if (props) return ImportProps(name, data, parts, meshes, mat, shadow, tex);

            // the prefab
            var root = new GameObject(name);
            var building = root.AddComponent<Building>();
            building.id = name;
            var fp = Floats(data["footprint"]);
            building.footprintCenter = new Vector2(fp[0], fp[1]) * S;
            building.footprintHalf = new Vector2(fp[2], fp[3]) * S;

            var groups = new Dictionary<string, GameObject>();
            System.Func<string, GameObject> Group = null;
            Group = g =>
            {
                GameObject go;
                if (groups.TryGetValue(g, out go)) return go;
                go = new GameObject(g == "cabin" ? "Cabin" : (g == "basement" ? "Basement" : (g == "secret" ? "Secret" : g)));
                go.transform.SetParent(g == "secret" ? Group("basement").transform : root.transform, false);
                groups[g] = go;
                return go;
            };

            var pieces = new List<Building.Piece>();
            var bars = new List<GameObject>();
            BuildingDoor door = null;
            BuildingWardrobe wardrobe = null;
            GameObject doorBroken = null, hatchLid = null;
            foreach (var kv in parts)
            {
                var p = (Dictionary<string, object>)kv.Value;
                string role = Str(p, "role", "static");
                string group = Str(p, "group", "cabin");
                var pivot = V3(p["pivot"]) * S;
                bool isGlow = p.ContainsKey("glow") && (bool)p["glow"];
                bool castShadow = !isGlow && (!p.ContainsKey("shadow") || (bool)p["shadow"]);
                var parent = Group(group).transform;

                GameObject holder = null;
                if (role == "door")
                {
                    holder = new GameObject("Door");
                    holder.transform.SetParent(parent, false);
                    holder.transform.localPosition = pivot;
                    door = holder.AddComponent<BuildingDoor>();
                }
                if (role == "wardrobe")
                {
                    holder = new GameObject("Wardrobe");
                    holder.transform.SetParent(parent, false);
                    holder.transform.localPosition = pivot;
                    wardrobe = holder.AddComponent<BuildingWardrobe>();
                }
                var go = new GameObject(kv.Key);
                if (role == "door_bar") bars.Add(go);
                go.transform.SetParent(holder != null ? holder.transform : parent, false);
                go.transform.localPosition = holder != null ? Vector3.zero : pivot;
                go.AddComponent<MeshFilter>().sharedMesh = meshes[kv.Key];
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = isGlow ? new[] { glow } : (castShadow && shadow != null ? new[] { mat, shadow } : new[] { mat });
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                if (role == "door") door.leaf = go.transform;
                if (role == "door_broken") doorBroken = go;
                if (role == "hatch") hatchLid = go;
                if (role == "secret_cut" || role == "wardrobe")
                    pieces.Add(new Building.Piece { renderer = mr, role = role, normal = Vector3.zero });
                if (role == "roof" || role == "cut" || role == "door" || role == "door_bar")
                    pieces.Add(new Building.Piece { renderer = mr, role = role == "door_bar" ? "door" : role, normal = p.ContainsKey("normal") ? V3(p["normal"]) : Vector3.zero });
            }
            building.pieces = pieces.ToArray();
            building.cabinRoot = groups.ContainsKey("cabin") ? groups["cabin"] : null;
            building.basementRoot = groups.ContainsKey("basement") ? groups["basement"] : null;
            building.secretRoot = groups.ContainsKey("secret") ? groups["secret"] : null;
            if (data.ContainsKey("secret"))
            {
                var sf = Floats(((Dictionary<string, object>)data["secret"])["footprint"]);
                building.secretCenter = new Vector2(sf[0], sf[1]) * S;
                building.secretHalf = new Vector2(sf[2], sf[3]) * S;
            }
            if (wardrobe != null && data.ContainsKey("wardrobe"))
            {
                var w = (Dictionary<string, object>)data["wardrobe"];
                wardrobe.building = building;
                wardrobe.slide = V3(w["slide"]) * S;
                wardrobe.promptPoint = V3(w["prompt"]) * S;
                var wb = Floats(w["box"]);
                Box(wardrobe.transform, new[] { 0f, 0f, wb[0] * S, wb[1] * S }, "Obstacle", true);
                building.wardrobe = wardrobe;
            }

            // obstacles
            if (data.ContainsKey("obstacles"))
                foreach (var kv in (Dictionary<string, object>)data["obstacles"])
                {
                    int k = 0;
                    foreach (var b in (List<object>)kv.Value)
                        Box(Group(kv.Key).transform, Floats(b), "Obstacle " + (k++));
                }

            // door: blocker, the target the hero hits, where the prompt shows
            if (door != null && data.ContainsKey("door"))
            {
                var d = (Dictionary<string, object>)data["door"];
                var c = Floats(d["closed"]);
                Vector3 piv = door.transform.localPosition;
                door.blocker = Box(door.transform, new[] { c[0] * S - piv.x, c[1] * S - piv.z, c[2] * S, c[3] * S }, "Blocker", true);
                door.locked = d.ContainsKey("locked") && (bool)d["locked"];
                door.openYaw = Num(d, "openYaw", 100f);
                door.promptPoint = V3(d["prompt"]) * S - piv;
                door.broken = doorBroken;
                // boards nailed across the door: they ride on the leaf and show only while it is locked
                foreach (var b in bars)
                {
                    var world = b.transform.position;
                    b.transform.SetParent(door.leaf, true);
                    door.bars = b;
                }
                var target = new GameObject("Target");
                target.transform.SetParent(door.transform, false);
                target.transform.localPosition = new Vector3(c[0] * S - piv.x, 0f, c[1] * S - piv.z);
                var h = target.AddComponent<Health>();
                h.team = 1;
                h.max = Num(d, "health", 60f);
                door.health = h;
                building.door = door;
            }

            // hatch: its logic lives on the root so it keeps working while the ground floor is hidden
            if (data.ContainsKey("hatch") && hatchLid != null)
            {
                var d = (Dictionary<string, object>)data["hatch"];
                var logic = new GameObject("Hatch");
                logic.transform.SetParent(root.transform, false);
                var hatch = logic.AddComponent<BuildingHatch>();
                hatch.building = building;
                hatch.lid = hatchLid.transform;
                hatch.openPitch = Num(d, "openPitch", 100f);
                hatch.center = V3(d["center"]) * S;
                hatch.returnPoint = V3(d["return"]) * S;
                hatch.arrivePoint = V3(d["arrive"]) * S;
                hatch.upPoint = V3(d["up"]) * S;
                building.hatch = hatch;
            }

            // lamps
            if (data.ContainsKey("lights"))
                foreach (var o in (List<object>)data["lights"])
                {
                    var d = (Dictionary<string, object>)o;
                    var go = new GameObject("Light");
                    go.transform.SetParent(Group(Str(d, "group", "cabin")).transform, false);
                    go.transform.localPosition = V3(d["pos"]) * S;
                    var l = go.AddComponent<PixelLight>();
                    l.offset = Vector3.zero;
                    l.radius = Num(d, "radius", 4f) * S;
                    var col = Floats(d["color"]);
                    l.color = new Color(col[0], col[1], col[2], 1f);
                    l.intensity = Num(d, "intensity", 1f);
                    l.flicker = Num(d, "flicker", 0.2f);
                }

            // anchors
            if (data.ContainsKey("anchors"))
            {
                var a = (Dictionary<string, object>)data["anchors"];
                if (a.ContainsKey("occupant") && building.basementRoot != null)
                {
                    var go = new GameObject("Occupant");
                    string og = a.ContainsKey("occupantGroup") ? (string)a["occupantGroup"] : "basement";
                    go.transform.SetParent(Group(og).transform, false);
                    go.transform.localPosition = V3(a["occupant"]) * S;
                    go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    building.occupantAnchor = go.transform;
                }
            }

            if (!Directory.Exists(PrefabDir)) Directory.CreateDirectory(PrefabDir);
            string prefabPath = PrefabDir + "/" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            int tris = 0;
            foreach (var m in meshes.Values) tris += m.triangles.Length / 3;
            return name + ": " + meshes.Count + " partes, " + tris + " triangulos, escala " + S + ", atlas " + tex.width + "x" + tex.height + " -> " + prefabPath;
        }

        // ------------------------------------------------------------------------------------------ props

        /// <summary>
        /// A set of loose props (the rocks): every part becomes its own prefab in Resources/Props, with its pivot at
        /// the centre of its foot and a hitbox of one to three circles fitted to that foot (from the JSON).
        /// </summary>
        static string ImportProps(string set, Dictionary<string, object> data, Dictionary<string, object> parts,
                                  Dictionary<string, Mesh> meshes, Material mat, Material shadow, Texture2D tex)
        {
            const string dir = "Assets/Resources/Props";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var info = data.ContainsKey("props") ? (Dictionary<string, object>)data["props"] : new Dictionary<string, object>();
            var log = new StringBuilder(set + ":");
            foreach (var kv in parts)
            {
                var root = new GameObject(kv.Key);
                var body = new GameObject("Mesh");
                body.transform.SetParent(root.transform, false);
                body.AddComponent<MeshFilter>().sharedMesh = meshes[kv.Key];
                var mr = body.AddComponent<MeshRenderer>();
                mr.sharedMaterials = shadow != null ? new[] { mat, shadow } : new[] { mat };
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                var prop = root.AddComponent<WorldProp>();
                int circles = 0;
                if (info.ContainsKey(kv.Key))
                {
                    var p = (Dictionary<string, object>)info[kv.Key];
                    var half = Floats(p["half"]);
                    prop.halfLength = half[0] * S;
                    prop.halfWidth = half[1] * S;
                    prop.axisYaw = -Num(p, "axis", 0f);          // Blender's angle is counter-clockwise from +x
                    prop.height = Num(p, "height", 1f) * S;
                    if (p.ContainsKey("canopy"))
                    {
                        var cn = Floats(p["canopy"]);
                        prop.canopyRadius = cn[0] * S;
                        prop.canopyLow = cn[1] * S;
                        prop.canopyHigh = cn[2] * S;
                    }
                    foreach (var o in (List<object>)p["obstacles"])
                    {
                        var c = Floats(o);
                        var go = new GameObject("Hit " + circles++);
                        go.transform.SetParent(root.transform, false);
                        go.transform.localPosition = new Vector3(c[0] * S, 0f, c[1] * S);
                        var ob = go.AddComponent<WorldObstacle>();
                        ob.kind = WorldObstacle.Kind.Circle;
                        ob.radius = c[2] * S;
                    }
                }
                string path = dir + "/" + kv.Key + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                log.Append(" " + kv.Key + " (" + meshes[kv.Key].triangles.Length / 3 + " tri, " + circles + " circulos)");
            }
            return log.ToString() + ", atlas " + tex.width + "x" + tex.height + " -> " + dir;
        }

        static Material Mat(string path, Shader shader, Texture2D tex, bool emissive)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else m.shader = shader;
            m.SetTexture("_MainTex", tex);
            m.SetFloat("_Levels", 4f);
            m.SetFloat("_Ambient", 0.3f);
            m.SetFloat("_Dither", 0.85f);
            m.SetFloat("_Emissive", emissive ? 1f : 0f);
            m.SetFloat("_OutlineTexels", emissive ? 0f : 1f);
            m.SetColor("_OutlineColor", new Color(0.08f, 0.07f, 0.06f, 1f));
            m.SetFloat("_Cut", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Mesh BuildMesh(string name, Dictionary<string, object> p)
        {
            var v = Floats(p["v"]);
            var n = Floats(p["n"]);
            var uv = Floats(p["uv"]);
            var c = Floats(p["c"]);
            var t = (List<object>)p["t"];
            int count = v.Length / 3;
            var verts = new Vector3[count];
            var norms = new Vector3[count];
            var uvs = new Vector2[count];
            var cols = new Color[count];
            for (int i = 0; i < count; i++)
            {
                verts[i] = new Vector3(v[i * 3], v[i * 3 + 1], v[i * 3 + 2]) * S;
                norms[i] = new Vector3(n[i * 3], n[i * 3 + 1], n[i * 3 + 2]);
                uvs[i] = new Vector2(uv[i * 2], uv[i * 2 + 1]);
                cols[i] = new Color(c[i * 3], c[i * 3 + 1], c[i * 3 + 2], 1f);
            }
            var tris = new int[t.Count];
            for (int i = 0; i < t.Count; i++) tris[i] = (int)(double)t[i];
            var m = new Mesh { name = name };
            if (count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.vertices = verts;
            m.normals = norms;
            m.uv = uvs;
            m.colors = cols;
            m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        static WorldObstacle Box(Transform parent, float[] b, string name, bool scaled = false)
        {
            float k = scaled ? 1f : S;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(b[0] * k, 0f, b[1] * k);
            var o = go.AddComponent<WorldObstacle>();
            o.kind = WorldObstacle.Kind.Box;
            o.halfSize = new Vector2(b[2] * k, b[3] * k);
            return o;
        }

        static float[] Floats(object o)
        {
            var l = (List<object>)o;
            var r = new float[l.Count];
            for (int i = 0; i < l.Count; i++) r[i] = (float)(double)l[i];
            return r;
        }

        static Vector3 V3(object o) { var f = Floats(o); return new Vector3(f[0], f[1], f[2]); }
        static string Str(Dictionary<string, object> d, string k, string def) { return d.ContainsKey(k) ? (string)d[k] : def; }
        static float Num(Dictionary<string, object> d, string k, float def) { return d.ContainsKey(k) ? (float)(double)d[k] : def; }

        // ------------------------------------------------------------------------------------------ tiny JSON reader

        static class Json
        {
            public static object Parse(string s) { int i = 0; return Value(s, ref i); }

            static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

            static object Value(string s, ref int i)
            {
                Ws(s, ref i);
                char c = s[i];
                if (c == '{')
                {
                    var d = new Dictionary<string, object>();
                    i++;
                    Ws(s, ref i);
                    if (s[i] == '}') { i++; return d; }
                    while (true)
                    {
                        Ws(s, ref i);
                        string k = Str(s, ref i);
                        Ws(s, ref i);
                        i++;                                   // ':'
                        d[k] = Value(s, ref i);
                        Ws(s, ref i);
                        if (s[i] == ',') { i++; continue; }
                        i++;                                   // '}'
                        return d;
                    }
                }
                if (c == '[')
                {
                    var l = new List<object>();
                    i++;
                    Ws(s, ref i);
                    if (s[i] == ']') { i++; return l; }
                    while (true)
                    {
                        l.Add(Value(s, ref i));
                        Ws(s, ref i);
                        if (s[i] == ',') { i++; continue; }
                        i++;                                   // ']'
                        return l;
                    }
                }
                if (c == '"') return Str(s, ref i);
                if (s.Substring(i, 4) == "true") { i += 4; return true; }
                if (s.Substring(i, 5) == "false") { i += 5; return false; }
                if (s.Substring(i, 4) == "null") { i += 4; return null; }
                int start = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
            }

            static string Str(string s, ref int i)
            {
                var sb = new StringBuilder();
                i++;                                           // opening quote
                while (s[i] != '"')
                {
                    if (s[i] == '\\')
                    {
                        i++;
                        char e = s[i];
                        if (e == 'u') { sb.Append((char)int.Parse(s.Substring(i + 1, 4), NumberStyles.HexNumber)); i += 4; }
                        else sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e);
                    }
                    else sb.Append(s[i]);
                    i++;
                }
                i++;
                return sb.ToString();
            }
        }
    }
}
