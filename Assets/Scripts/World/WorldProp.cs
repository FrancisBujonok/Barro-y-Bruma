using BrumaPx.Rendering;
using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>
    /// A loose prop made in Blender (the big rocks). Knows the size of its foot, so the map can line it up along a
    /// cliff edge, and turns half see-through while it hides the hero from the camera (a tree by its crown too).
    /// </summary>
    public sealed class WorldProp : MonoBehaviour
    {
        [Tooltip("Half size of the foot along its main axis and across it (prefab scale 1).")]
        public float halfLength = 1f, halfWidth = 1f;
        [Tooltip("Yaw of the main axis of the foot, from local +x.")]
        public float axisYaw;
        [Tooltip("Height of the body (a rock, or the trunk below the crown).")]
        public float height = 1.5f;
        [Tooltip("Crown of a tree: radius, lowest and highest leaves. Radius 0 = no crown.")]
        public float canopyRadius, canopyLow, canopyHigh;
        [Range(0f, 1f)] public float behindCut = 0.75f;

        const float HeroHeight = 2.7f, HeroHalfWidth = 0.45f;

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
            float top = Mathf.Max(height, canopyHigh) * k;
            float far = (Mathf.Max(halfLength, canopyRadius) * k) + top * 1.8f + 2f;
            float target = 0f;
            if (d.sqrMagnitude < far * far && Covers(d, k)) target = behindCut;
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

        // Does the prop hide the hero from the camera? Works on the screen of the orthographic camera: a point
        // `along` metres further from the camera and `h` metres up shows at v = along * sin(pitch) + h * cos(pitch).
        bool Covers(Vector3 d, float k)
        {
            Vector3 f3 = CameraForward3();
            Vector3 fwd = new Vector3(f3.x, 0f, f3.z).normalized;
            float sinP = Mathf.Clamp(-f3.y, 0.05f, 1f), cosP = Mathf.Sqrt(1f - sinP * sinP);
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
            float along = Vector3.Dot(d, fwd);
            float side = Mathf.Abs(Vector3.Dot(d, right));
            float h0 = along * sinP, h1 = h0 + HeroHeight * cosP;            // the hero on the screen

            // the body (a rock, a trunk): the hero has to be behind it
            float rf = Reach(fwd), rr = Reach(right);
            if (along > 0f && side < rr + HeroHalfWidth)
            {
                float b0 = -rf * sinP, b1 = rf * sinP + height * k * cosP;
                if (h0 < b1 && h1 > b0) return true;
            }
            // the crown: high up, so it hides the hero from just in front of the trunk to well behind it
            if (canopyRadius > 0f)
            {
                float rc = canopyRadius * k;
                if (side < rc + HeroHalfWidth && along > -rc)
                {
                    float c0 = -rc * sinP + canopyLow * k * cosP, c1 = rc * sinP + canopyHigh * k * cosP;
                    if (h0 < c1 && h1 > c0) return true;
                }
            }
            return false;
        }

        static Vector3 CameraForward3()
        {
            if (_camera == null)
            {
                var rig = FindFirstObjectByType<PixelDioramaRig>();
                if (rig != null && rig.sceneCamera != null) _camera = rig.sceneCamera.transform;
            }
            return _camera != null ? _camera.forward : new Vector3(0.612f, -0.5f, 0.612f);
        }

    }
}
