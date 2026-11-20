using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BrumaPx.Characters
{
    /// <summary>
    /// Draws one layered character in the diorama: a single quad that the PPSprite shader turns towards the camera,
    /// snaps to the render-pixel grid and lights like everything else. It keeps the composed texture, the current
    /// animation frame and the state of the body zones (wounds and Bruma). Who decides what is worn and which clip
    /// plays lives elsewhere (HeroSpriteDriver for the hero).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DioramaSprite : MonoBehaviour
    {
        public const int MaxZones = 16;

        public CharacterLayerSet set;
        public Material material;
        [Tooltip("Second material: the same picture laid on the ground as a shadow.")]
        public Material shadowMaterial;
        [Tooltip("View index. 4 views: 0 up (away), 1 left, 2 down (towards the camera), 3 right. 8 views: 0 down, 1 down-right, 2 right, 4 up, 6 left.")]
        public int direction = 2;

        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int ZoneTexId = Shader.PropertyToID("_ZoneTex");
        static readonly int GridId = Shader.PropertyToID("_Grid");
        static readonly int FeetId = Shader.PropertyToID("_Feet");
        static readonly int FrameId = Shader.PropertyToID("_Frame");
        static readonly int WoundId = Shader.PropertyToID("_ZoneWound");
        static readonly int BrumaId = Shader.PropertyToID("_ZoneBruma");

        static Mesh _quad;

        MeshRenderer _renderer;
        MaterialPropertyBlock _block;
        Texture _texture;
        string _key;
        int _clip = -1;
        int _held = -1;
        float _time, _speed = 1f;
        readonly float[] _wound = new float[MaxZones];
        readonly float[] _bruma = new float[MaxZones];

        public string ClipName { get { return _clip >= 0 && set != null ? set.clips[_clip].name : string.Empty; } }
        /// <summary>True when a clip that does not loop has reached its last frame.</summary>
        public bool Finished
        {
            get
            {
                if (_clip < 0 || set == null) return false;
                var c = set.clips[_clip];
                return !c.loop && _time * c.fps >= c.frames - 1;
            }
        }
        public string TextureKey { get { return _key; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _quad = null; }

        void Awake()
        {
            var filter = gameObject.GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = Quad();
            _renderer = gameObject.GetComponent<MeshRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _block = new MaterialPropertyBlock();
        }

        void OnDestroy()
        {
            CharacterCompositor.Release(_key);
            _key = null;
        }

        /// <summary>Changes what the character wears. Cheap when the same stack is already built.</summary>
        public void SetLayers(List<CharacterLayerSet.Layer> layers)
        {
            if (set == null) return;
            string key = CharacterCompositor.KeyOf(set, layers);
            if (key == _key && _texture != null) return;
            string old = _key;
            _texture = CharacterCompositor.Acquire(set, layers, out _key);
            CharacterCompositor.Release(old);
            ApplyMaterials();
        }

        /// <summary>Plays a clip by name. Asking for the clip that is already playing does not restart it.</summary>
        public void Play(string clipName, float speed, bool restart)
        {
            if (set == null) return;
            int i = set.ClipIndex(clipName);
            if (i < 0) return;
            _speed = speed;
            _held = -1;
            if (i == _clip && !restart) return;
            _clip = i;
            _time = 0f;
        }

        /// <summary>Shows one frame of a clip and stays there (the caller drives the animation, e.g. an attack timer).</summary>
        public void Hold(string clipName, int frame)
        {
            if (set == null) return;
            int i = set.ClipIndex(clipName);
            if (i < 0) return;
            _clip = i;
            _held = Mathf.Clamp(frame, 0, set.clips[i].frames - 1);
        }

        public int FramesOf(string clipName)
        {
            int i = set != null ? set.ClipIndex(clipName) : -1;
            return i < 0 ? 0 : set.clips[i].frames;
        }

        public bool HasClip(string clipName) { return set != null && set.ClipIndex(clipName) >= 0; }

        /// <summary>For sets whose clips bring their own sheets: nothing to compose, just turn the materials on.</summary>
        public void UseClipSheets()
        {
            CharacterCompositor.Release(_key);
            _key = null;
            _texture = null;
            ApplyMaterials();
        }

        /// <summary>Wound per zone: 0 none, 1 open, 2 cleaned, 3 bandaged. Bruma per zone: 0 to 1.</summary>
        public void SetZone(int zoneNumber, float wound, float bruma01)
        {
            if (zoneNumber < 1 || zoneNumber >= MaxZones) return;
            _wound[zoneNumber] = wound;
            _bruma[zoneNumber] = Mathf.Clamp01(bruma01);
        }

        void ApplyMaterials()
        {
            if (_renderer == null) return;
            if (material != null && shadowMaterial != null) _renderer.sharedMaterials = new[] { material, shadowMaterial };
            else if (material != null) _renderer.sharedMaterials = new[] { material };
        }

        void LateUpdate()
        {
            if (set == null || _renderer == null) return;
            if (_clip < 0) Play(set.clips.Count > 0 ? set.clips[0].name : string.Empty, 1f, true);
            if (_clip < 0) return;

            var clip = set.clips[_clip];
            bool own = clip.sheet != null;
            if (!own && _texture == null) return;
            int frame;
            if (_held >= 0) frame = _held;
            else
            {
                _time += Time.deltaTime * _speed;
                frame = (int)(_time * clip.fps);
                frame = clip.loop ? frame % Mathf.Max(1, clip.frames) : Mathf.Min(frame, clip.frames - 1);
            }
            int views = Mathf.Max(1, clip.directions);
            int row = own ? (views > 1 ? Mathf.Clamp(direction, 0, views - 1) : 0) : set.RowOf(_clip, direction);

            _renderer.GetPropertyBlock(_block);
            if (own)
            {
                _block.SetTexture(MainTexId, clip.sheet);
                _block.SetVector(GridId, new Vector4(clip.frames, views, clip.size.x, clip.size.y));
                _block.SetVector(FeetId, new Vector4(clip.feet.x, clip.feet.y, 0f, 0f));
            }
            else
            {
                _block.SetTexture(MainTexId, _texture);
                if (set.zoneMap != null) _block.SetTexture(ZoneTexId, set.zoneMap);
                _block.SetVector(GridId, new Vector4(set.columns, set.rows, set.cell, 0f));
                _block.SetVector(FeetId, new Vector4(set.feet.x, set.feet.y, 0f, 0f));
            }
            _block.SetVector(FrameId, new Vector4(frame, row, 0f, 0f));
            _block.SetFloatArray(WoundId, _wound);
            _block.SetFloatArray(BrumaId, _bruma);
            _renderer.SetPropertyBlock(_block);
        }

        // A unit quad (0..1). The shader places it; the bounds are generous so it is never culled by mistake.
        static Mesh Quad()
        {
            if (_quad != null) return _quad;
            _quad = new Mesh { name = "DioramaSpriteQuad", hideFlags = HideFlags.HideAndDontSave };
            _quad.vertices = new[] { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(1f, 1f, 0f), new Vector3(0f, 1f, 0f) };
            _quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _quad.bounds = new Bounds(Vector3.zero, new Vector3(40f, 40f, 40f));
            return _quad;
        }
    }
}
