using BrumaPx.Combat;
using BrumaPx.Data;
using BrumaPx.Dialogue;
using BrumaPx.World;
using UnityEngine;

namespace BrumaPx.Player
{
    /// <summary>
    /// Walks the hero over the X/Z ground, relative to the screen (up arrow = up the screen), with simple circle/box
    /// collisions, an 8-direction facing, a stepped walk cycle that stays on the pixel grid, and NPC interaction.
    /// HeroCombat can lock the movement and slow it down, and uses the collision and facing helpers.
    /// </summary>
    public sealed class DioramaHero : MonoBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 3.2f;
        public float runSpeed = 5.4f;
        public float radius = 0.32f;
        public float worldLimit = 32f;
        public float turnSpeed = 900f;

        [Header("Noise")]
        [Tooltip("How far enemies can hear a walking hero.")]
        public float walkNoise = 2.2f;
        [Tooltip("How far enemies can hear a running hero.")]
        public float runNoise = 6.5f;

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

        /// <summary>When true the hero ignores movement input (attacks, rolls, being hit).</summary>
        public bool MovementLocked { get; set; }

        /// <summary>Multiplies walk and run speed (1 = normal). Used by the guard.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        public bool IsMoving { get { return _moving; } }
        /// <summary>Moving with the run key held.</summary>
        public bool IsRunning { get { return _moving && _running; } }

        Vector3 _bodyRest, _legLRest, _legRRest;
        float _clock;
        float _targetYaw;
        bool _moving;
        bool _running;
        HeroBody _bodyFx;

        void Awake()
        {
            if (body != null) _bodyRest = body.localPosition;
            if (legL != null) _legLRest = legL.localPosition;
            if (legR != null) _legRRest = legR.localPosition;
            _targetYaw = transform.eulerAngles.y;
            _bodyFx = GetComponent<HeroBody>();
            if (prompt != null) prompt.gameObject.SetActive(false);
        }

        void Update()
        {
            TalkableNpc npc = TalkableNpc.Nearest(transform.position);
            bool talking = dialogue != null && dialogue.IsOpen;

            _moving = false;
            if (!talking && !MovementLocked) Move();
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

        /// <summary>The direction the arrows ask for, on the ground plane (zero when no key is held).</summary>
        public Vector3 InputDirection()
        {
            Vector2 input = DioramaInput.Move();
            if (input.sqrMagnitude < 0.01f) return Vector3.zero;
            input.Normalize();

            // Input is relative to the screen: rotate it by the camera yaw onto the ground plane.
            float camYaw = sceneCamera != null ? sceneCamera.transform.eulerAngles.y : 45f;
            return Quaternion.Euler(0f, camYaw, 0f) * new Vector3(input.x, 0f, input.y);
        }

        /// <summary>Pushes a ground position (x, z) out of obstacles and keeps it inside the world.</summary>
        public Vector2 ResolveGround(Vector2 p, float r)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                var all = WorldObstacle.All;
                for (int i = 0; i < all.Count; i++) p = all[i].Resolve(p, r);
            }
            p = MapGrid.Resolve(p, r);
            p.x = Mathf.Clamp(p.x, -worldLimit, worldLimit);
            p.y = Mathf.Clamp(p.y, -worldLimit, worldLimit);
            return p;
        }

        void Move()
        {
            Vector3 dir = InputDirection();
            if (dir.sqrMagnitude < 0.01f) return;

            _running = DioramaInput.Run();
            float speed = (_running ? runSpeed : walkSpeed) * SpeedMultiplier * BodySpeed(_running);
            Vector3 pos = transform.position + dir * (speed * Time.deltaTime);

            Vector2 p = ResolveGround(new Vector2(pos.x, pos.z), radius);
            transform.position = new Vector3(p.x, 0f, p.y);

            _targetYaw = Mathf.Round(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg / 45f) * 45f;
            _moving = true;
            NoiseBus.Emit(transform.position, _running ? runNoise : walkNoise);
        }

        // The legs lose speed as the Bruma takes them (running also loses what walking loses).
        float BodySpeed(bool running)
        {
            if (_bodyFx == null) return 1f;
            float m = _bodyFx.Mult(BodyStat.WalkSpeed);
            return running ? m * _bodyFx.Mult(BodyStat.RunSpeed) : m;
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

        /// <summary>Turns towards the arrows being held (does nothing when none is).</summary>
        public void FaceInput()
        {
            Vector3 d = InputDirection();
            if (d.sqrMagnitude < 0.01f) return;
            _targetYaw = Mathf.Round(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg / 45f) * 45f;
        }

        /// <summary>Finishes the turn at once, so an attack starts facing the right way.</summary>
        public void SnapFacing()
        {
            transform.rotation = Quaternion.Euler(0f, _targetYaw, 0f);
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
