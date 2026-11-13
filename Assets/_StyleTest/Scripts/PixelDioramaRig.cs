using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace BrumaPx.StyleTest
{
    /// <summary>
    /// Renders the 3D world with a fixed 30-degree orthographic camera into a small render texture and shows it
    /// with nearest-neighbour filtering at an integer scale. The camera follows the focus in whole render pixels,
    /// so nothing shimmers while it moves. Also publishes the palette, light and island radius the StyleTest
    /// shaders read as globals.
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

        [Header("Palette (sRGB)")]
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

        RenderTexture _target;

        public void Rebuild() { Apply(); }

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
            Vector4 island = cam == portraitCamera
                ? new Vector4(1e6f, 1e6f, 0f, 0f)
                : new Vector4(islandRadius.x, islandRadius.y, 0f, 0f);
            Shader.SetGlobalVector(IslandId, island);
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
            sceneCamera.backgroundColor = fogColor;
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
            Vector3 shadowDir = right * shadowLength + towardCamera * shadowTowardCamera;
            Vector3 towardLight = (Vector3.up - shadowDir).normalized;

            Shader.SetGlobalVector(LightDirId, towardLight);
            Shader.SetGlobalVector(CoolId, (Vector4)coolShade.linear);
            Shader.SetGlobalVector(WarmId, (Vector4)warmLight.linear);
            Shader.SetGlobalVector(FogId, (Vector4)fogColor.linear);
            Shader.SetGlobalVector(ShadowTintId, (Vector4)shadowTint.linear);
            Shader.SetGlobalVector(FocusId, focus != null ? focus.position : Vector3.zero);
            Shader.SetGlobalVector(CamRightId, sceneCamera.transform.right);
            Shader.SetGlobalVector(CamUpId, sceneCamera.transform.up);
            Shader.SetGlobalVector(PpuId, new Vector4(pixelsPerUnit, 0f, 0f, 0f));
            Shader.SetGlobalVector(IslandId, new Vector4(islandRadius.x, islandRadius.y, 0f, 0f));
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
