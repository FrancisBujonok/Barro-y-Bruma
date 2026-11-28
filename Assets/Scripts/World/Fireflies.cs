using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>
    /// Fireflies at night over the forest, the swamp and the banks. One pool for the whole map that keeps them
    /// around the hero: each one hovers a while near the spot it was born, blinks, and is born again elsewhere
    /// close to him. Every firefly is two camera-facing quads drawn with PPPuff in glow mode (no mist over them):
    /// a two-pixel core and a sparse halo. Out by day, in rain and in caves.
    /// </summary>
    public sealed class Fireflies : MonoBehaviour
    {
        public Material material;
        public int count = 26;
        public float radius = 10f;

        sealed class Fly
        {
            public Transform core, halo;
            public MeshRenderer rc, rh;
            public Vector3 home;
            public float age, life, seed, period, height;
            public bool alive;
        }

        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int SeedId = Shader.PropertyToID("_Seed");
        static readonly int RingId = Shader.PropertyToID("_Ring");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int Color2Id = Shader.PropertyToID("_Color2");

        Fly[] _pool;
        Mesh _quad;
        MaterialPropertyBlock _mpb;
        Transform _cam;
        BrumaPx.Atmosphere.DayNightWeather _sky;
        float _strength;            // 0 by day, 1 at full night (eased, so they come out at dusk)
        float _spawnTimer;

        Mesh Quad()
        {
            if (_quad != null) return _quad;
            _quad = new Mesh { name = "Firefly Quad" };
            _quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            _quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _quad.RecalculateBounds();
            return _quad;
        }

        MeshRenderer Part(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        void Build()
        {
            _mpb = new MaterialPropertyBlock();
            _pool = new Fly[count];
            for (int i = 0; i < count; i++)
            {
                var root = new GameObject("Firefly " + i).transform;
                root.SetParent(transform, false);
                var f = new Fly();
                f.rh = Part("Halo", root);
                f.rc = Part("Core", root);
                f.halo = f.rh.transform;
                f.core = f.rc.transform;
                f.halo.localScale = Vector3.one * 0.42f;
                f.core.localScale = Vector3.one * 0.13f;
                Paint(f.rh, 0.3f, true, i * 3.1f);
                Paint(f.rc, 1f, false, i * 3.1f);
                root.gameObject.SetActive(false);
                _pool[i] = f;
            }
        }

        void Paint(MeshRenderer r, float fade, bool halo, float seed)
        {
            r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(FadeId, fade);
            _mpb.SetFloat(SeedId, seed);
            _mpb.SetFloat(RingId, 0f);
            _mpb.SetFloat(GlowId, 1f);
            if (halo)
            {
                _mpb.SetColor(ColorId, new Color32(0x4E, 0x5A, 0x2E, 255));
                _mpb.SetColor(Color2Id, new Color32(0x6F, 0x7A, 0x3A, 255));
            }
            else
            {
                _mpb.SetColor(ColorId, new Color32(0xB8, 0xC8, 0x5A, 255));
                _mpb.SetColor(Color2Id, new Color32(0xE8, 0xF0, 0x9A, 255));
            }
            r.SetPropertyBlock(_mpb);
        }

        bool NightOut()
        {
            if (_sky == null) _sky = FindFirstObjectByType<BrumaPx.Atmosphere.DayNightWeather>();
            if (_sky == null) return false;
            if (_sky.weather == BrumaPx.Atmosphere.WeatherType.Rain || _sky.weather == BrumaPx.Atmosphere.WeatherType.Storm) return false;
            if (BrumaPx.Atmosphere.DayNightWeather.CaveDarknessTarget > 0.1f) return false;
            return _sky.LampStrength > 0.35f;
        }

        static bool GoodGround(float x, float z)
        {
            byte id = MapGrid.IdAtWorld(x, z);
            if (id == 1 || id == 3) return true;
            if (id != 0 && id != 2) return false;
            // the edge of water: plains and dead forest only by the banks
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * 0.5f;
                byte n = MapGrid.IdAtWorld(x + Mathf.Cos(a) * 3f, z + Mathf.Sin(a) * 3f);
                if (n == 4) return true;
            }
            return false;
        }

        void Update()
        {
            if (material == null) return;
            if (_pool == null) Build();
            var hero = DioramaHeroCache.Hero;
            if (hero == null) return;
            if (_cam == null)
            {
                var rig = FindFirstObjectByType<BrumaPx.Rendering.PixelDioramaRig>();
                if (rig != null && rig.sceneCamera != null) _cam = rig.sceneCamera.transform;
            }
            float dt = Time.deltaTime;
            _strength = Mathf.MoveTowards(_strength, NightOut() ? 1f : 0f, dt * 0.25f);
            int want = Mathf.RoundToInt(count * _strength);

            int alive = 0;
            for (int i = 0; i < _pool.Length; i++) if (_pool[i].alive) alive++;
            _spawnTimer -= dt;
            if (alive < want && _spawnTimer <= 0f)
            {
                _spawnTimer = 0.15f;
                Born(hero.position);
            }

            Quaternion face = _cam != null ? _cam.rotation : Quaternion.Euler(30f, 45f, 0f);
            Vector3 hp = hero.position;
            for (int i = 0; i < _pool.Length; i++)
            {
                var f = _pool[i];
                if (!f.alive) continue;
                f.age += dt;
                Vector3 off = f.home - hp;
                off.y = 0f;
                // too far from the hero, too many for the hour, or simply old: it goes out
                bool gone = f.age >= f.life || off.sqrMagnitude > (radius + 4f) * (radius + 4f) || (alive > want && i % 3 == 0);
                if (gone)
                {
                    f.alive = false;
                    f.core.parent.gameObject.SetActive(false);
                    alive--;
                    continue;
                }
                // slow wandering loops around home, bobbing up and down
                float t = f.age + f.seed;
                Vector3 p = f.home + new Vector3(Mathf.Sin(t * 0.43f) * 0.9f + Mathf.Sin(t * 1.3f) * 0.2f,
                                                 f.height + Mathf.Sin(t * 0.9f) * 0.25f,
                                                 Mathf.Cos(t * 0.37f) * 0.9f + Mathf.Cos(t * 1.1f) * 0.2f);
                var root = f.core.parent;
                root.position = p;
                f.core.rotation = face;
                f.halo.rotation = face;
                // blinking: on most of the time, a soft dark beat; fades in and out at birth and death
                float ph = Mathf.Repeat(t / f.period, 1f);
                float blink = ph < 0.62f ? 1f : (ph < 0.72f ? 0.45f : 0f);
                float life = Mathf.Min(1f, f.age / 0.8f, (f.life - f.age) / 0.8f);
                float b = blink * life;
                f.rc.enabled = b > 0.3f;
                f.rh.enabled = b > 0.6f;
            }
        }

        void Born(Vector3 hero)
        {
            Fly f = null;
            for (int i = 0; i < _pool.Length; i++) if (!_pool[i].alive) { f = _pool[i]; break; }
            if (f == null) return;
            for (int tries = 0; tries < 6; tries++)
            {
                Vector2 c = Random.insideUnitCircle * radius;
                float x = hero.x + c.x, z = hero.z + c.y;
                if (c.sqrMagnitude < 2.5f || !GoodGround(x, z)) continue;
                f.home = new Vector3(x, 0f, z);
                f.alive = true;
                f.age = 0f;
                f.life = Random.Range(7f, 14f);
                f.seed = Random.value * 40f;
                f.period = Random.Range(1.8f, 3.4f);
                f.height = Random.Range(0.35f, 1.5f);
                f.core.parent.gameObject.SetActive(true);
                return;
            }
        }
    }
}
