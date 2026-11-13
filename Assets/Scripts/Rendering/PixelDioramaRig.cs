using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace BrumaPx.Rendering
{
    /// <summary>
    /// Everything the shaders need to know about the time of day and the weather. Produced by DayNightWeather and
    /// handed to PixelDioramaRig, which publishes it as shader globals.
    /// </summary>
    public sealed class AtmosphereState
    {
        public const int MaxLights = 8;

        public Color fog, cool, warm, shadowTint;
        /// <summary>Linear multiplier for every lit surface: 1 by day, dark blue at night.</summary>
        public Vector4 tone = Vector4.one;
        public float shadowLength = 1.3f;
        /// <summary>Where shadows point: 180 = screen-left, 90 = toward the camera, 0 = screen-right.</summary>
        public float shadowAngle = 5.27f;
        /// <summary>x mist density multiplier, y mist threshold shift, z rain amount, w wetness.</summary>
        public Vector4 weather = new Vector4(1f, 0f, 0f, 0f);
        /// <summary>Radius of the visible island around the hero.</summary>
        public Vector2 island = new Vector2(8.5f, 8f);
        public readonly Vector4[] lightPos = new Vector4[MaxLights];
        public readonly Vector4[] lightCol = new Vector4[MaxLights];
        public int lightCount;
    }

    /// <summary>
    /// Renders the 3D world with a fixed 30-degree orthographic camera into a small render texture and shows it
    /// with nearest-neighbour filtering at an integer scale. The camera follows the focus in whole render pixels,
    /// so nothing shimmers while it moves. Also publishes the palette, light, atmosphere and island radius the
    /// diorama shaders read as globals.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PixelDioramaRig : MonoBehaviour
    {
        [Header("Render target")]
        public Camera sceneCamera;
        public RawImage output;
        public int width = 480;
        public int height = 270;
        public int pixelsPerUnit = 20;

        [Header("Camera")]
        public Transform focus;
        public float pitch = 30f;
        public float yaw = 45f;
        [Tooltip("Moved to the focus position every frame (ground and mist planes), keeping their own height.")]
        public Transform[] followers;

        [Header("Visible island (the fog edge)")]
        public Vector2 islandRadius = new Vector2(8.5f, 8.0f);
        [Tooltip("Rendered with the island clip disabled (it lives far away from the world).")]
        public Camera portraitCamera;
        [Tooltip("Its background colour follows the fog, so the bars around the picture match the scene.")]
        public Camera displayCamera;

        [Header("Palette (sRGB) - used when no DayNightWeather drives the rig")]
        public Color fogColor = new Color32(0xC4, 0xBF, 0xB2, 255);
        public Color coolShade = new Color32(0x2F, 0x34, 0x36, 255);
        public Color warmLight = new Color32(0xCD, 0xBB, 0x94, 255);
        public Color shadowTint = new Color32(0x8C, 0x8F, 0xA3, 255);

        [Header("Light")]
        [Range(0.5f, 3f)] public float shadowLength = 1.3f;
        [Range(-0.5f, 0.5f)] public float shadowTowardCamera = 0.12f;

        static readonly int LightDirId = Shader.PropertyToID("_PP_LightDir");
        static readonly int CoolId = Shader.PropertyToID("_PP_Cool");
        static readonly int WarmId = Shader.PropertyToID("_PP_Warm");
        static readonly int FogId = Shader.PropertyToID("_PP_Fog");
        static readonly int ShadowTintId = Shader.PropertyToID("_PP_ShadowTint");
        static readonly int FocusId = Shader.PropertyToID("_PP_Focus");
        static readonly int CamRightId = Shader.PropertyToID("_PP_CamRight");
        static readonly int CamUpId = Shader.PropertyToID("_PP_CamUp");
        static readonly int PpuId = Shader.PropertyToID("_PP_PPU");
        static readonly int IslandId = Shader.PropertyToID("_PP_Island");
        static readonly int ToneId = Shader.PropertyToID("_PP_Tone");
        static readonly int WeatherId = Shader.PropertyToID("_PP_Weather");
        static readonly int LightCountId = Shader.PropertyToID("_PP_LightCount");
        static readonly int LightPosId = Shader.PropertyToID("_PP_LightPos");
        static readonly int LightColId = Shader.PropertyToID("_PP_LightCol");

        static readonly Vector4[] NoLights = new Vector4[AtmosphereState.MaxLights];

        RenderTexture _target;
        AtmosphereState _atmo;

        /// <summary>Hands the rig the current time-of-day / weather state. Pass null to go back to the inspector values.</summary>
        public void SetAtmosphere(AtmosphereState state) { _atmo = state; }

        public void Rebuild() { Apply(); }

        Vector2 Island { get { return _atmo != null ? _atmo.island : islandRadius; } }
        Color Fog { get { return _atmo != null ? _atmo.fog : fogColor; } }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            Apply();
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            ReleaseTarget();
        }

        void OnValidate() { if (isActiveAndEnabled) Apply(); }
        void LateUpdate() { Apply(); }

        // Globals are shared by every camera, so the portrait camera gets "no island" right before it renders.
        void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            bool portrait = cam == portraitCamera;
            Vector2 isl = Island;
            Shader.SetGlobalVector(IslandId, portrait ? new Vector4(1e6f, 1e6f, 0f, 0f) : new Vector4(isl.x, isl.y, 0f, 0f));
            // The portrait is lit like a picture on a wall: no night tone and no lamp pools.
            bool plain = portrait || _atmo == null;
            Shader.SetGlobalVector(ToneId, plain ? Vector4.one : _atmo.tone);
            Shader.SetGlobalVector(LightCountId, plain ? Vector4.zero : new Vector4(_atmo.lightCount, 0f, 0f, 0f));
        }

        void Apply()
        {
            if (sceneCamera == null) return;
            EnsureTarget();
            SetupCamera();
            PublishGlobals();
            MoveFollowers();
            FitOutput();
        }

        void EnsureTarget()
        {
            if (_target != null && (_target.width != width || _target.height != height)) ReleaseTarget();
            if (_target != null) return;

            var desc = new RenderTextureDescriptor(width, height, GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormat.D24_UNorm_S8_UInt, 1);
            _target = new RenderTexture(desc)
            {
                name = "PixelDioramaRT",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            _target.Create();
        }

        void ReleaseTarget()
        {
            if (sceneCamera != null && sceneCamera.targetTexture == _target) sceneCamera.targetTexture = null;
            if (_target != null)
            {
                _target.Release();
                DestroyImmediate(_target);
                _target = null;
            }
        }

        void SetupCamera()
        {
            var t = sceneCamera.transform;
            t.rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 target = focus != null ? focus.position : Vector3.zero;
            Vector3 pos = target - t.forward * 60f;

            // Snap the camera to the render-pixel grid (along its own right/up axes), measured from the world origin.
            // The shaders anchor dither and noise to the same lattice, so the world scrolls in whole pixels.
            float texel = 1f / pixelsPerUnit;
            Vector3 right = t.right, up = t.up;
            float x = Vector3.Dot(pos, right), y = Vector3.Dot(pos, up);
            pos += right * (Mathf.Round(x / texel) * texel - x) + up * (Mathf.Round(y / texel) * texel - y);
            t.position = pos;

            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = height / (2f * pixelsPerUnit);
            sceneCamera.nearClipPlane = 0.1f;
            sceneCamera.farClipPlane = 140f;
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = Fog;
            if (displayCamera != null) displayCamera.backgroundColor = Fog;
            sceneCamera.allowMSAA = false;
            sceneCamera.allowHDR = false;
            sceneCamera.targetTexture = _target;

            var data = sceneCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
        }

        void PublishGlobals()
        {
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            Vector3 towardCamera = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;

            Vector3 shadowDir;
            if (_atmo != null)
            {
                float a = _atmo.shadowAngle * Mathf.Deg2Rad;
                shadowDir = (right * Mathf.Cos(a) + towardCamera * Mathf.Sin(a)) * _atmo.shadowLength;
            }
            else
            {
                shadowDir = right * shadowLength + towardCamera * shadowTowardCamera;
            }
            Vector3 towardLight = (Vector3.up - shadowDir).normalized;

            Shader.SetGlobalVector(LightDirId, towardLight);
            Shader.SetGlobalVector(CoolId, (Vector4)(_atmo != null ? _atmo.cool : coolShade).linear);
            Shader.SetGlobalVector(WarmId, (Vector4)(_atmo != null ? _atmo.warm : warmLight).linear);
            Shader.SetGlobalVector(FogId, (Vector4)Fog.linear);
            Shader.SetGlobalVector(ShadowTintId, (Vector4)(_atmo != null ? _atmo.shadowTint : shadowTint).linear);
            Shader.SetGlobalVector(FocusId, focus != null ? focus.position : Vector3.zero);
            Shader.SetGlobalVector(CamRightId, sceneCamera.transform.right);
            Shader.SetGlobalVector(CamUpId, sceneCamera.transform.up);
            Shader.SetGlobalVector(PpuId, new Vector4(pixelsPerUnit, 0f, 0f, 0f));
            Vector2 isl = Island;
            Shader.SetGlobalVector(IslandId, new Vector4(isl.x, isl.y, 0f, 0f));

            if (_atmo != null)
            {
                Shader.SetGlobalVector(ToneId, _atmo.tone);
                Shader.SetGlobalVector(WeatherId, _atmo.weather);
                Shader.SetGlobalVector(LightCountId, new Vector4(_atmo.lightCount, 0f, 0f, 0f));
                Shader.SetGlobalVectorArray(LightPosId, _atmo.lightPos);
                Shader.SetGlobalVectorArray(LightColId, _atmo.lightCol);
            }
            else
            {
                Shader.SetGlobalVector(ToneId, Vector4.one);
                Shader.SetGlobalVector(WeatherId, new Vector4(1f, 0f, 0f, 0f));
                Shader.SetGlobalVector(LightCountId, Vector4.zero);
                Shader.SetGlobalVectorArray(LightPosId, NoLights);
                Shader.SetGlobalVectorArray(LightColId, NoLights);
            }
        }

        void MoveFollowers()
        {
            if (followers == null || focus == null) return;
            foreach (var f in followers)
            {
                if (f == null) continue;
                Vector3 p = f.position;
                p.x = focus.position.x;
                p.z = focus.position.z;
                f.position = p;
            }
        }

        void FitOutput()
        {
            if (output == null) return;
            int scale = Mathf.Max(1, Mathf.Min(Screen.width / width, Screen.height / height));
            var rt = output.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width * scale, height * scale);
            output.texture = _target;
            output.raycastTarget = false;
        }
    }
}
