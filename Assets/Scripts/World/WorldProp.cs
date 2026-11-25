using BrumaPx.Rendering;
using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>
    /// A loose prop made in Blender (the big rocks). Knows the size of its foot, so the map can line it up along a
    /// cliff edge, and turns half see-through while the hero is behind it, seen from the camera.
    /// </summary>
    public sealed class WorldProp : MonoBehaviour
    {
        [Tooltip("Half size of the foot along its main axis and across it (prefab scale 1).")]
        public float halfLength = 1f, halfWidth = 1f;
        [Tooltip("Yaw of the main axis of the foot, from local +x.")]
        public float axisYaw;
        public float height = 1.5f;
        [Range(0f, 1f)] public float behindCut = 0.75f;

        static readonly int CutId = Shader.PropertyToID("_Cut");
        static Transform _camera;
        MaterialPropertyBlock _block;
        Renderer[] _renderers;
        float _cut;

        /// <summary>Distance from the centre to the edge of the foot in a world direction (flat), with the prop's scale.</summary>
        public float Reach(Vector3 worldDir)
        {
            Vector3 axis = transform.rotation * (Quaternion.Euler(0f, axisYaw, 0f) * Vector3.right);
            axis.y = 0f;
            worldDir.y = 0f;
            if (axis.sqrMagnitude < 1e-6f || worldDir.sqrMagnitude < 1e-6f) return halfLength * transform.lossyScale.x;
            float c = Vector3.Dot(axis.normalized, worldDir.normalized);
            float s = Mathf.Sqrt(Mathf.Max(0f, 1f - c * c));
            float a = halfLength * c, b = halfWidth * s;
            return Mathf.Sqrt(a * a + b * b) * transform.lossyScale.x;
        }

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
        }

        void Update()
        {
            var hero = DioramaHeroCache.Hero;
            if (hero == null) return;
            float k = transform.lossyScale.x;
            Vector3 d = hero.position - transform.position;
            d.y = 0f;
            float far = (halfLength + height * 1.2f) * k + 1f;
            float target = 0f;
            if (d.sqrMagnitude < far * far)
            {
                Vector3 fwd = CameraForward();
                Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
                float along = Vector3.Dot(d, fwd);
                float side = Mathf.Abs(Vector3.Dot(d, right));
                // behind the rock and inside the part of the screen it covers (taller rocks hide more ground)
                if (along > 0f && along < Reach(fwd) + height * k * 1.1f && side < Reach(right) + 0.35f) target = behindCut;
            }
            if (_cut == target) return;
            _cut = Mathf.MoveTowards(_cut, target, Time.deltaTime * 3f);
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetFloat(CutId, _cut);
                r.SetPropertyBlock(_block);
            }
        }

        static Vector3 CameraForward()
        {
            if (_camera == null)
            {
                var rig = FindFirstObjectByType<PixelDioramaRig>();
                if (rig != null && rig.sceneCamera != null) _camera = rig.sceneCamera.transform;
            }
            Vector3 f = _camera != null ? _camera.forward : Quaternion.Euler(0f, 45f, 0f) * Vector3.forward;
            f.y = 0f;
            return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
        }
    }
}
