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
            if (Underground.Current == this) return;

            Vector3 fwd = CameraForward();
            HeroInside = Contains(hero.position);
            float target = 0f;
            if (HeroInside) target = 1f;
            else
            {
                // behind the building, seen from the camera: half see-through so the hero is not lost
                Vector3 c = transform.TransformPoint(new Vector3(footprintCenter.x, 0f, footprintCenter.y));
                Vector3 d = hero.position - c;
                d.y = 0f;
                float along = Vector3.Dot(d, fwd);
                Vector3 side = d - fwd * along;
                float width = Mathf.Max(footprintHalf.x, footprintHalf.y) + 0.8f;
                if (along > 0f && along < Mathf.Max(footprintHalf.x, footprintHalf.y) + behindReach && side.magnitude < width)
                    target = behindCut;
            }
            _cut = Mathf.MoveTowards(_cut, target, Time.deltaTime * 3.5f);
            Apply(fwd);
        }

        void Apply(Vector3 fwd)
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                var p = pieces[i];
                if (p.renderer == null) continue;
                float c = 0f;
                if (p.role == "roof") c = _cut;
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
            if (!on) { _cut = 1f; Apply(CameraForward()); }
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
