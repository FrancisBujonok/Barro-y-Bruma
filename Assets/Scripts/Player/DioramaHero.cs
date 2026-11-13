using BrumaPx.Rendering;
using BrumaPx.World;
using BrumaPx.Player;
using BrumaPx.Dialogue;
using UnityEngine;

namespace BrumaPx.Player
{
    /// <summary>
    /// Walks the hero over the X/Z ground, relative to the screen (W = up the screen), with simple circle/box
    /// collisions, an 8-direction facing, a stepped walk cycle that stays on the pixel grid, and NPC interaction.
    /// </summary>
    public sealed class DioramaHero : MonoBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 3.2f;
        public float runSpeed = 5.4f;
        public float radius = 0.32f;
        public float worldLimit = 32f;
        public float turnSpeed = 900f;

        [Header("Rig")]
        public Camera sceneCamera;
        public Transform body;
        public Transform legL;
        public Transform legR;
        public int pixelsPerUnit = 20;
        public float stepsPerSecond = 7f;

        [Header("Interaction")]
        public DialogueBox dialogue;
        public RectTransform prompt;

        Vector3 _bodyRest, _legLRest, _legRRest;
        float _clock;
        float _targetYaw;
        bool _moving;
        bool _running;

        void Awake()
        {
            if (body != null) _bodyRest = body.localPosition;
            if (legL != null) _legLRest = legL.localPosition;
            if (legR != null) _legRRest = legR.localPosition;
            _targetYaw = transform.eulerAngles.y;
            if (prompt != null) prompt.gameObject.SetActive(false);
        }

        void Update()
        {
            TalkableNpc npc = TalkableNpc.Nearest(transform.position);
            bool talking = dialogue != null && dialogue.IsOpen;

            _moving = false;
            if (!talking) Move();
            Turn();
            Animate();
            UpdatePrompt(npc, talking);

            if (!talking && npc != null && dialogue != null
                && dialogue.ClosedFrame != Time.frameCount
                && DioramaInput.InteractPressed())
            {
                FaceTowards(npc.transform.position);
                npc.FaceTowards(transform.position);
                dialogue.Open(npc);
            }
        }

        void Move()
        {
            Vector2 input = DioramaInput.Move();
            if (input.sqrMagnitude < 0.01f) return;
            input.Normalize();

            // Input is relative to the screen: rotate it by the camera yaw onto the ground plane.
            float camYaw = sceneCamera != null ? sceneCamera.transform.eulerAngles.y : 45f;
            Vector3 dir = Quaternion.Euler(0f, camYaw, 0f) * new Vector3(input.x, 0f, input.y);

            _running = DioramaInput.Run();
            float speed = _running ? runSpeed : walkSpeed;
            Vector3 pos = transform.position + dir * (speed * Time.deltaTime);

            Vector2 p = new Vector2(pos.x, pos.z);
            for (int pass = 0; pass < 2; pass++)
            {
                var all = WorldObstacle.All;
                for (int i = 0; i < all.Count; i++) p = all[i].Resolve(p, radius);
            }
            p.x = Mathf.Clamp(p.x, -worldLimit, worldLimit);
            p.y = Mathf.Clamp(p.y, -worldLimit, worldLimit);
            transform.position = new Vector3(p.x, 0f, p.y);

            _targetYaw = Mathf.Round(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg / 45f) * 45f;
            _moving = true;
        }

        void Turn()
        {
            float yaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, _targetYaw, turnSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        public void FaceTowards(Vector3 target)
        {
            Vector3 d = target - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.001f) return;
            _targetYaw = Mathf.Round(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg / 45f) * 45f;
        }

        // Everything moves in whole render pixels: one pixel of height on screen is 1 / (ppu * cos 30) world units.
        void Animate()
        {
            if (body == null || legL == null || legR == null) return;
            float texel = 1f / (pixelsPerUnit * 0.8660254f);

            if (_moving)
            {
                _clock += Time.deltaTime * stepsPerSecond * (_running ? 1.4f : 1f);
                int step = (int)_clock;
                bool a = (step & 1) == 0;
                legL.localPosition = _legLRest + new Vector3(0f, a ? 0f : 0.07f, a ? 0.12f : -0.12f);
                legR.localPosition = _legRRest + new Vector3(0f, a ? 0.07f : 0f, a ? -0.12f : 0.12f);
                body.localPosition = _bodyRest + Vector3.up * ((step & 1) * texel);
            }
            else
            {
                _clock = 0f;
                legL.localPosition = _legLRest;
                legR.localPosition = _legRRest;
                int breath = (int)(Time.time * 2.2f) & 1;
                body.localPosition = _bodyRest + Vector3.up * (breath * texel);
            }
        }

void UpdatePrompt(TalkableNpc npc, bool talking)
        {
            if (prompt == null) return;
            bool show = npc != null && !talking && sceneCamera != null;
            if (prompt.gameObject.activeSelf != show) prompt.gameObject.SetActive(show);
            if (!show) return;

            Vector3 vp = sceneCamera.WorldToViewportPoint(npc.PromptPoint);
            Vector2 size = ((RectTransform)prompt.parent).rect.size;
            // The prompt lives on the diorama picture, so it grows with the integer upscale of the render texture.
            prompt.localScale = Vector3.one * Mathf.Max(1f, Mathf.Floor(size.y / 270f));
            prompt.anchoredPosition = new Vector2(Mathf.Round((vp.x - 0.5f) * size.x), Mathf.Round((vp.y - 0.5f) * size.y));
        }
    }
}
