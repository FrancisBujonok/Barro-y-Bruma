using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>
    /// Smoke out of a chimney, or bubbles and a little steam over a cauldron: a small pool of camera-facing puffs
    /// drawn with PPPuff (dithered, on the world pixel lattice). Sleeps when the hero is far.
    /// </summary>
    public sealed class PuffEmitter : MonoBehaviour
    {
        public enum Kind { Smoke, Bubbles }

        public Kind kind = Kind.Smoke;
        public Material material;
        [Tooltip("Bubbles: radius of the surface they come up on.")]
        public float radius = 0.2f;
        public float wakeDistance = 34f;

        sealed class Puff
        {
            public Transform t;
            public MeshRenderer r;
            public float age, life, size0, size1, seed;
            public Vector3 vel;
            public bool alive, bubble;
        }

        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int SeedId = Shader.PropertyToID("_Seed");
        static readonly int RingId = Shader.PropertyToID("_Ring");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int Color2Id = Shader.PropertyToID("_Color2");
        static Mesh _quad;
        static Transform _cam;

        Puff[] _pool;
        float _timer, _steamTimer;
        MaterialPropertyBlock _mpb;

        static Mesh Quad()
        {
            if (_quad != null) return _quad;
            _quad = new Mesh { name = "Puff Quad" };
            _quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            _quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _quad.RecalculateBounds();
            return _quad;
        }

        void Build()
        {
            _mpb = new MaterialPropertyBlock();
            int n = kind == Kind.Smoke ? 9 : 10;
            _pool = new Puff[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Puff " + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = Quad();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                _pool[i] = new Puff { t = go.transform, r = r };
            }
        }

        void Update()
        {
            if (material == null) return;
            if (_pool == null) Build();
            var hero = DioramaHeroCache.Hero;
            if (hero != null && (hero.position - transform.position).sqrMagnitude > wakeDistance * wakeDistance) return;
            if (_cam == null)
            {
                var rig = FindFirstObjectByType<BrumaPx.Rendering.PixelDioramaRig>();
                if (rig != null && rig.sceneCamera != null) _cam = rig.sceneCamera.transform;
            }
            float dt = Time.deltaTime;
            _timer -= dt;
            if (_timer <= 0f)
            {
                _timer = kind == Kind.Smoke ? Random.Range(0.45f, 0.75f) : Random.Range(0.18f, 0.4f);
                Spawn(false);
            }
            if (kind == Kind.Bubbles)
            {
                _steamTimer -= dt;
                if (_steamTimer <= 0f) { _steamTimer = Random.Range(0.9f, 1.6f); Spawn(true); }
            }
            Quaternion face = _cam != null ? _cam.rotation : Quaternion.Euler(30f, 45f, 0f);
            for (int i = 0; i < _pool.Length; i++)
            {
                var p = _pool[i];
                if (!p.alive) continue;
                p.age += dt;
                float t = p.age / p.life;
                if (t >= 1f) { p.alive = false; p.t.gameObject.SetActive(false); continue; }
                if (!p.bubble)
                {
                    // rises, slows down, leans with the wind and wobbles
                    p.vel.y = Mathf.Lerp(p.vel.y, 0.25f, dt * 0.6f);
                    // the wind carries it to the right of the screen (east-south-east), not away from the camera
                    p.vel.x = 0.3f + Mathf.Sin(p.age * 1.7f + p.seed) * 0.12f;
                    p.vel.z = -0.22f + Mathf.Cos(p.age * 1.3f + p.seed) * 0.08f;
                }
                p.t.position += p.vel * dt;
                float size = Mathf.Lerp(p.size0, p.size1, Mathf.Sqrt(t));
                p.t.localScale = new Vector3(size, size, size);
                p.t.rotation = face;
                // full while it rises, then thins out over the last part of its life
                float fade = p.bubble ? (t < 0.8f ? 1f : 0f) : (t < 0.45f ? 1f : 1f - (t - 0.45f) / 0.55f);
                p.r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(FadeId, fade);
                p.r.SetPropertyBlock(_mpb);
            }
        }

        void Spawn(bool steam)
        {
            Puff p = null;
            for (int i = 0; i < _pool.Length; i++) if (!_pool[i].alive) { p = _pool[i]; break; }
            if (p == null) return;
            p.alive = true;
            p.age = 0f;
            p.seed = Random.value * 50f;
            p.t.gameObject.SetActive(true);
            p.r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(SeedId, p.seed);
            if (kind == Kind.Smoke)
            {
                p.bubble = false;
                p.life = Random.Range(3.6f, 4.8f);
                p.size0 = 0.45f;
                p.size1 = Random.Range(1.3f, 1.7f);
                p.vel = new Vector3(0.2f, Random.Range(0.6f, 0.8f), -0.15f);
                p.t.position = transform.position + new Vector3(Random.Range(-0.08f, 0.08f), 0f, Random.Range(-0.08f, 0.08f));
                _mpb.SetFloat(RingId, 0f);
                // darker than the mist, so it reads against it
                _mpb.SetColor(ColorId, new Color32(0x3A, 0x36, 0x31, 255));
                _mpb.SetColor(Color2Id, new Color32(0x77, 0x73, 0x6A, 255));
            }
            else if (steam)
            {
                p.bubble = false;
                p.life = Random.Range(1.4f, 2.0f);
                p.size0 = 0.25f;
                p.size1 = 0.75f;
                p.vel = new Vector3(0f, 0.55f, 0f);
                p.t.position = transform.position + RandomOnSurface() * 0.6f + Vector3.up * 0.05f;
                _mpb.SetFloat(RingId, 0f);
                _mpb.SetColor(ColorId, new Color32(0x4F, 0x5C, 0x40, 255));
                _mpb.SetColor(Color2Id, new Color32(0x9A, 0x96, 0x8C, 255));
            }
            else
            {
                // a bubble swells on the brew and pops
                p.bubble = true;
                p.life = Random.Range(0.45f, 0.8f);
                // big enough to read as a few pixels at the game's zoom (about 20 px per metre)
                p.size0 = 0.06f;
                p.size1 = Random.Range(0.16f, 0.24f);
                p.vel = Vector3.zero;
                p.t.position = transform.position + RandomOnSurface() + Vector3.up * 0.03f;
                _mpb.SetFloat(RingId, Random.value < 0.6f ? 1f : 0f);
                _mpb.SetColor(ColorId, new Color32(0x7E, 0x77, 0x52, 255));
                _mpb.SetColor(Color2Id, new Color32(0xC4, 0xBF, 0xB2, 255));
            }
            p.r.SetPropertyBlock(_mpb);
        }

        Vector3 RandomOnSurface()
        {
            Vector2 c = Random.insideUnitCircle * radius;
            return new Vector3(c.x, 0f, c.y);
        }
    }
}
