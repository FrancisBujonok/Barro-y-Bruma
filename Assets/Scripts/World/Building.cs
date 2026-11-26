using System;
using System.Collections.Generic;
using BrumaPx.Rendering;
using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>
    /// A building made in Blender (see tools/blender/edificio_kit.py and Assets/Editor/BuildingImporter.cs).
    /// While the hero is inside its footprint the roof and the walls that face the camera dissolve (dithered), so
    /// the interior can be seen; a little of that also happens when the hero walks behind it. An optional
    /// basement lives in the same spot and is shown by <see cref="Underground"/> when the hero goes down the hatch.
    /// </summary>
    public sealed class Building : MonoBehaviour
    {
        [Serializable]
        public class Piece
        {
            public Renderer renderer;
            [Tooltip("roof, cut (a wall: dissolves when it faces the camera), door, static")]
            public string role = "static";
            [Tooltip("Outward normal in local space (walls and door).")]
            public Vector3 normal;
        }

        public string id = "";
        [Tooltip("Inside of the walls on the ground, local x/z: centre and half size.")]
        public Vector2 footprintCenter;
        public Vector2 footprintHalf = new Vector2(2.8f, 2f);
        public Piece[] pieces = new Piece[0];
        public GameObject cabinRoot;
        public GameObject basementRoot;
        [Tooltip("Where someone who lives in the basement stands (the woodcutter).")]
        public Transform occupantAnchor;
        public BuildingDoor door;
        public BuildingHatch hatch;
        [Tooltip("A room hidden off the basement (shown by a BuildingWardrobe). While the hero is in it, the pieces with role secret_cut (the wall between it and the basement) and the pushed-aside wardrobe dissolve.")]
        public GameObject secretRoot;
        public Vector2 secretCenter;
        public Vector2 secretHalf;
        public BuildingWardrobe wardrobe;
        [Tooltip("How far behind the building (along the camera view) the hero makes it see-through.")]
        public float behindReach = 3.2f;
        [Range(0f, 1f)] public float behindCut = 0.8f;

        public static readonly List<Building> All = new List<Building>();
        static readonly int CutId = Shader.PropertyToID("_Cut");

        MaterialPropertyBlock _block;
        float _cut;
        Transform _camera;

        /// <summary>True while the hero stands inside the walls (ground floor).</summary>
        public bool HeroInside { get; private set; }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            // Obstacles are axis-aligned boxes in world space: swap their sides when the building is turned 90 degrees.
            float yaw = transform.eulerAngles.y;
            if (Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)) > 0.7f)
                foreach (var o in GetComponentsInChildren<WorldObstacle>(true))
                    if (o.kind == WorldObstacle.Kind.Box) o.halfSize = new Vector2(o.halfSize.y, o.halfSize.x);
            if (basementRoot != null) basementRoot.SetActive(false);
            if (secretRoot != null) secretRoot.SetActive(false);
        }

        /// <summary>Is a world point inside the walls of this building?</summary>
        public bool Contains(Vector3 world, float margin = 0f)
        {
            Vector3 l = transform.InverseTransformPoint(world);
            return Mathf.Abs(l.x - footprintCenter.x) < footprintHalf.x + margin
                && Mathf.Abs(l.z - footprintCenter.y) < footprintHalf.y + margin;
        }

        Vector3 CameraForward()
        {
            if (_camera == null)
            {
                var rig = FindFirstObjectByType<PixelDioramaRig>();
                if (rig != null && rig.sceneCamera != null) _camera = rig.sceneCamera.transform;
            }
            Vector3 f = _camera != null ? _camera.forward : Quaternion.Euler(0f, 45f, 0f) * Vector3.forward;
            f.y = 0f;
            return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
        }

        void Update()
        {
            var hero = DioramaHeroCache.Hero;
            if (hero == null) return;
            if (Underground.Current == this)
            {
                Vector3 l = transform.InverseTransformPoint(hero.position);
                bool inSecret = secretRoot != null && secretRoot.activeInHierarchy
                    && Mathf.Abs(l.x - secretCenter.x) < secretHalf.x && Mathf.Abs(l.z - secretCenter.y) < secretHalf.y;
                _secretCut = Mathf.MoveTowards(_secretCut, inSecret ? 1f : 0f, Time.deltaTime * 3.5f);
                ApplySecret();
                return;
            }

            Vector3 fwd = CameraForward();
            HeroInside = Contains(hero.position);
            float target = 0f;
            bool behind = !HeroInside && CoversHero(hero.position, fwd);
            if (HeroInside) target = 1f;
            else if (behind) target = behindCut;     // behind the building, seen from the camera: see-through
            _cut = Mathf.MoveTowards(_cut, target, Time.deltaTime * 3.5f);
            // a chimney stands outside the walls: it only fades when it would hide the hero
            _chimneyCut = Mathf.MoveTowards(_chimneyCut, behind ? behindCut : 0f, Time.deltaTime * 3.5f);
            Apply(fwd);
        }

        float _secretCut;
        float _chimneyCut;

        // Box around the walls and the roof (world space), measured once from their renderers.
        Bounds _shell;
        bool _hasShell;

        void MeasureShell()
        {
            _hasShell = false;
            for (int i = 0; i < pieces.Length; i++)
            {
                var p = pieces[i];
                if (p.renderer == null || (p.role != "roof" && p.role != "cut" && p.role != "chimney")) continue;
                if (!_hasShell) { _shell = p.renderer.bounds; _hasShell = true; }
                else _shell.Encapsulate(p.renderer.bounds);
            }
        }

        // Does the building hide the hero from the camera? Works on the screen of the orthographic camera, with the
        // whole height of the walls and roof: a point `along` metres further away and `h` up shows at
        // v = along * sin(pitch) + h * cos(pitch). The hero has to be past the front corner of the building.
        bool CoversHero(Vector3 heroPos, Vector3 fwd)
        {
            if (!_hasShell) MeasureShell();
            if (!_hasShell) return false;
            float sinP = 0.5f, cosP = 0.866f;
            if (_camera != null) { sinP = Mathf.Clamp(-_camera.forward.y, 0.05f, 1f); cosP = Mathf.Sqrt(1f - sinP * sinP); }
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
            float u0 = float.MaxValue, u1 = float.MinValue, v0 = float.MaxValue, v1 = float.MinValue;
            Vector3 mn = _shell.min, mx = _shell.max;
            float baseY = transform.position.y;
            for (int k = 0; k < 4; k++)
            {
                var c = new Vector3((k & 1) == 0 ? mn.x : mx.x, 0f, (k & 2) == 0 ? mn.z : mx.z);
                float along = Vector3.Dot(c, fwd), u = Vector3.Dot(c, right);
                u0 = Mathf.Min(u0, u); u1 = Mathf.Max(u1, u);
                v0 = Mathf.Min(v0, along * sinP);
                v1 = Mathf.Max(v1, along * sinP + (mx.y - baseY) * cosP);
            }
            var h = new Vector3(heroPos.x, 0f, heroPos.z);
            float ha = Vector3.Dot(h, fwd), hu = Vector3.Dot(h, right);
            if (hu + 0.45f < u0 || hu - 0.45f > u1) return false;
            // the front edge of the building in the hero's screen column: the hero has to be behind it
            float front = float.MaxValue;
            var q = new Vector3[] { new Vector3(mn.x, 0f, mn.z), new Vector3(mx.x, 0f, mn.z), new Vector3(mx.x, 0f, mx.z), new Vector3(mn.x, 0f, mx.z) };
            float hc = Mathf.Clamp(hu, u0 + 0.01f, u1 - 0.01f);
            for (int k = 0; k < 4; k++)
            {
                Vector3 p0 = q[k], p1 = q[(k + 1) & 3];
                float pu0 = Vector3.Dot(p0, right), pu1 = Vector3.Dot(p1, right);
                if ((hc - pu0) * (hc - pu1) > 0f || Mathf.Abs(pu1 - pu0) < 1e-4f) continue;
                float tt = (hc - pu0) / (pu1 - pu0);
                front = Mathf.Min(front, Vector3.Dot(Vector3.Lerp(p0, p1, tt), fwd));
            }
            if (front == float.MaxValue || ha < front + 0.3f) return false;    // in front of the building
            float hv0 = ha * sinP, hv1 = hv0 + 2.7f * cosP;
            return hv1 > v0 && hv0 < v1;
        }

        void ApplySecret()
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                var p = pieces[i];
                if (p.renderer == null || (p.role != "secret_cut" && p.role != "wardrobe")) continue;
                p.renderer.GetPropertyBlock(_block);
                _block.SetFloat(CutId, _secretCut);
                p.renderer.SetPropertyBlock(_block);
            }
        }

        void Apply(Vector3 fwd)
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                var p = pieces[i];
                if (p.renderer == null) continue;
                float c = 0f;
                if (p.role == "roof") c = _cut;
                else if (p.role == "chimney") c = _chimneyCut;
                else if (p.role == "cut" || p.role == "door")
                {
                    Vector3 n = transform.TransformDirection(p.normal);
                    n.y = 0f;
                    if (Vector3.Dot(n, fwd) < -0.2f) c = _cut;
                }
                p.renderer.GetPropertyBlock(_block);
                _block.SetFloat(CutId, c);
                p.renderer.SetPropertyBlock(_block);
            }
        }

        /// <summary>Shows the basement instead of the ground floor (Underground calls this).</summary>
        public void ShowBasement(bool on)
        {
            if (cabinRoot != null) cabinRoot.SetActive(!on);
            if (basementRoot != null) basementRoot.SetActive(on);
            if (!on) { _cut = 1f; _secretCut = 0f; Apply(CameraForward()); }
        }

        /// <summary>The building whose basement anchor is closest to a point (within a radius), or null.</summary>
        public static Building WithBasementNear(Vector3 p, float radius)
        {
            Building best = null;
            float bd = radius * radius;
            foreach (var b in All)
            {
                if (b.basementRoot == null || b.occupantAnchor == null) continue;
                Vector3 d = b.transform.position - p;
                d.y = 0f;
                if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = b; }
            }
            return best;
        }
    }
}
