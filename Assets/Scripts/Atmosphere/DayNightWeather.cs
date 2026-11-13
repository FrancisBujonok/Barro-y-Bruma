using BrumaPx.Player;
using BrumaPx.Rendering;
using UnityEngine;
using UnityEngine.UI;

namespace BrumaPx.Atmosphere
{
    public enum WeatherType { Clear, Fog, Rain, Storm }

    /// <summary>
    /// Day/night cycle and simple weather. Every frame it works out the palette, shadows, tone, mist, rain, wetness
    /// and the nearest lamp lights, and hands them to the PixelDioramaRig, which publishes them to the shaders.
    /// Runs in the editor too (without advancing time), so timeOfDay and weather can be scrubbed in the inspector.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class DayNightWeather : MonoBehaviour
    {
        [Header("Links")]
        public PixelDioramaRig rig;
        [Tooltip("Whatever the lamp lights are chosen around (the hero).")]
        public Transform focus;
        public Text hud;

        [Header("Time")]
        [Range(0f, 24f)] public float timeOfDay = 16f;
        [Tooltip("Real minutes a full day takes.")]
        public float dayLengthMinutes = 12f;
        public bool runTime = true;

        [Header("Weather")]
        public WeatherType weather = WeatherType.Clear;
        public bool autoWeather = true;
        [Tooltip("Seconds a weather change takes to blend in.")]
        public float transitionSeconds = 6f;
        [Tooltip("How long a weather lasts before the next one is rolled (seconds, min and max).")]
        public Vector2 autoDuration = new Vector2(90f, 240f);

        // ------------------------------------------------------------------------------------------ day keys

        struct Key
        {
            public float hour;
            public Color fog, cool, warm, shadow;
            public Vector3 tone;
            public float lamp, angle, length;
        }

        static Color C(uint hex)
        {
            return new Color32((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);
        }

        static Key K(float hour, uint fog, uint cool, uint warm, uint shadow, float tr, float tg, float tb, float lamp, float angle, float length)
        {
            return new Key { hour = hour, fog = C(fog), cool = C(cool), warm = C(warm), shadow = C(shadow), tone = new Vector3(tr, tg, tb), lamp = lamp, angle = angle, length = length };
        }

        // The 11:00-16:00 keys are the look the style test was approved with (fog C4BFB2, shadows 8C8FA3, length 1.3).
        // angle: where the shadows point, in degrees from screen-left (180) through toward-camera (90) to screen-right (0).
        static readonly Key[] Keys =
        {
            K(0.0f,  0x151B26, 0x090C12, 0x3E4F6B, 0xE4E7F2, 0.30f, 0.38f, 0.62f, 1.0f, 172f, 1.6f),
            K(4.5f,  0x151B26, 0x090C12, 0x3E4F6B, 0xE4E7F2, 0.30f, 0.38f, 0.62f, 1.0f, 172f, 1.6f),
            K(6.2f,  0x8C8FA0, 0x2A2F3A, 0xD9A58A, 0x8E8A9E, 0.86f, 0.82f, 0.90f, 0.5f, 172f, 2.2f),
            K(8.5f,  0xB9BCB6, 0x2F3436, 0xD2C49C, 0x8C8FA3, 0.97f, 0.97f, 0.97f, 0.0f, 120f, 1.6f),
            K(11.0f, 0xC4BFB2, 0x2F3436, 0xCDBB94, 0x8C8FA3, 1.00f, 1.00f, 1.00f, 0.0f, 80f, 0.95f),
            K(13.0f, 0xC4BFB2, 0x2F3436, 0xCDBB94, 0x8C8FA3, 1.00f, 1.00f, 1.00f, 0.0f, 62f, 0.8f),
            K(15.0f, 0xC4BFB2, 0x2F3436, 0xCDBB94, 0x8C8FA3, 1.00f, 1.00f, 1.00f, 0.0f, 28f, 1.05f),
            K(16.0f, 0xC4BFB2, 0x2F3436, 0xCDBB94, 0x8C8FA3, 1.00f, 1.00f, 1.00f, 0.0f, 5.27f, 1.3f),
            K(18.3f, 0xA98B78, 0x3A2F3C, 0xE0A25F, 0x8A7488, 1.00f, 0.82f, 0.70f, 0.4f, 5.27f, 2.4f),
            K(19.8f, 0x4E4A5C, 0x1B1A28, 0xA06A5A, 0xA59FB5, 0.70f, 0.62f, 0.78f, 0.9f, 5.27f, 2.0f),
            K(21.5f, 0x151B26, 0x090C12, 0x3E4F6B, 0xE4E7F2, 0.30f, 0.38f, 0.62f, 1.0f, 5.27f, 1.6f),
            K(24.0f, 0x151B26, 0x090C12, 0x3E4F6B, 0xE4E7F2, 0.30f, 0.38f, 0.62f, 1.0f, 172f, 1.6f)
        };

        // ------------------------------------------------------------------------------------------ weather

        struct Preset
        {
            public float mist, shift, rain, wet, grey;
            public Vector3 tone;
            public Vector2 island;
            public bool lightning;
        }

        // island: how far the hero sees. Thicker weather shortens it, which also means fewer chunks are loaded.
        static readonly Preset[] Presets =
        {
            new Preset { mist = 1.0f, shift = 0.00f,  rain = 0.0f, wet = 0f,   grey = 0.00f, tone = new Vector3(1f, 1f, 1f),          island = new Vector2(8.5f, 8.0f), lightning = false },
            new Preset { mist = 1.7f, shift = -0.14f, rain = 0.0f, wet = 0.15f, grey = 0.35f, tone = new Vector3(0.96f, 0.97f, 0.99f), island = new Vector2(6.0f, 5.6f), lightning = false },
            new Preset { mist = 1.25f, shift = -0.05f, rain = 1.0f, wet = 1f,   grey = 0.45f, tone = new Vector3(0.86f, 0.90f, 0.96f), island = new Vector2(7.8f, 7.3f), lightning = false },
            new Preset { mist = 1.5f, shift = -0.10f, rain = 1.9f, wet = 1f,   grey = 0.60f, tone = new Vector3(0.68f, 0.74f, 0.86f), island = new Vector2(6.8f, 6.3f), lightning = true }
        };

        static readonly float[] AutoWeights = { 0.45f, 0.20f, 0.25f, 0.10f };
        static readonly string[] WeatherNames = { "Despejado", "Niebla", "Lluvia", "Tormenta" };

        // current (blended) weather values
        float _mist = 1f, _shift, _rain, _wet, _grey;
        Vector3 _tone = Vector3.one;
        Vector2 _island = new Vector2(8.5f, 8f);
        bool _inited;

        float _autoTimer;
        float _lightningTimer = 6f;
        float _flashTime;

        readonly AtmosphereState _state = new AtmosphereState();
        readonly float[] _lightDist = new float[AtmosphereState.MaxLights];

void OnEnable()
        {
            _inited = false;
            _autoTimer = Random.Range(autoDuration.x, autoDuration.y);
            Tick(0f);
        }
        void OnValidate() { if (isActiveAndEnabled) { _inited = false; Tick(0f); } }

        void Update()
        {
            float dt = Application.isPlaying ? Time.deltaTime : 0f;
            if (Application.isPlaying) HandleDebugKeys();
            Tick(dt);
        }

        void HandleDebugKeys()
        {
            if (DioramaInput.WeatherPressed())
            {
                weather = (WeatherType)(((int)weather + 1) % Presets.Length);
                autoWeather = false;
            }
            if (DioramaInput.HourPressed()) timeOfDay = Mathf.Repeat(timeOfDay + 1f, 24f);
            if (DioramaInput.PausePressed()) runTime = !runTime;
        }

        void Tick(float dt)
        {
            if (rig == null) return;

            if (dt > 0f)
            {
                if (runTime) timeOfDay = Mathf.Repeat(timeOfDay + dt * 24f / (Mathf.Max(0.1f, dayLengthMinutes) * 60f), 24f);

                if (autoWeather)
                {
                    _autoTimer -= dt;
                    if (_autoTimer <= 0f)
                    {
                        weather = RollWeather();
                        _autoTimer = Random.Range(autoDuration.x, autoDuration.y);
                    }
                }
            }

            // Blend the weather values towards the target preset (snap on the first frame and in the editor).
            Preset p = Presets[(int)weather];
            float k = (_inited && dt > 0f) ? 1f - Mathf.Exp(-dt * 3f / Mathf.Max(0.1f, transitionSeconds)) : 1f;
            _mist = Mathf.Lerp(_mist, p.mist, k);
            _shift = Mathf.Lerp(_shift, p.shift, k);
            _rain = Mathf.Lerp(_rain, p.rain, k);
            _wet = Mathf.Lerp(_wet, p.wet, k);
            _grey = Mathf.Lerp(_grey, p.grey, k);
            _tone = Vector3.Lerp(_tone, p.tone, k);
            _island = Vector2.Lerp(_island, p.island, k);
            _inited = true;

            float flash = UpdateLightning(dt, p.lightning);

            Apply(flash);
            if (hud != null) hud.text = FormatClock() + "   " + WeatherNames[(int)weather];
        }

        WeatherType RollWeather()
        {
            float r = Random.value, acc = 0f;
            for (int i = 0; i < AutoWeights.Length; i++)
            {
                acc += AutoWeights[i];
                if (r <= acc) return (WeatherType)i;
            }
            return WeatherType.Clear;
        }

        // Storms flash now and then: two quick pulses that brighten everything for a moment.
        float UpdateLightning(float dt, bool storm)
        {
            if (dt <= 0f) return 0f;
            if (storm)
            {
                _lightningTimer -= dt;
                if (_lightningTimer <= 0f)
                {
                    _flashTime = 0.3f;
                    _lightningTimer = Random.Range(5f, 12f);
                }
            }
            if (_flashTime <= 0f) return 0f;
            _flashTime -= dt;
            return ((int)(_flashTime * 16f) % 2 == 0) ? 1f : 0.4f;
        }

        void Apply(float flash)
        {
            Key key = Sample(timeOfDay);
            AtmosphereState s = _state;

            float grey = Mathf.Lerp(0f, 1f, _grey);
            s.fog = Greyed(key.fog, grey);
            s.cool = Greyed(key.cool, grey * 0.5f);
            s.warm = Greyed(key.warm, grey);
            s.shadowTint = key.shadow;

            Vector3 tone = new Vector3(key.tone.x * _tone.x, key.tone.y * _tone.y, key.tone.z * _tone.z);
            if (flash > 0f)
            {
                tone += Vector3.one * (0.45f * flash);
                s.fog = Color.Lerp(s.fog, Color.white, 0.25f * flash);
            }
            s.tone = new Vector4(tone.x, tone.y, tone.z, 1f);
            s.shadowLength = key.length;
            s.shadowAngle = key.angle;
            s.weather = new Vector4(_mist, _shift, _rain, _wet);
            s.island = _island;

            // Heavy weather turns the lamps on a little even by day.
            float avg = (tone.x + tone.y + tone.z) / 3f;
            float lamp = Mathf.Clamp01(key.lamp + (1f - Mathf.Min(avg, 1f)) * 0.8f);
            CollectLights(s, lamp);

            rig.SetAtmosphere(s);
            rig.Rebuild();
        }

        static Color Greyed(Color c, float amount)
        {
            float g = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            return Color.Lerp(c, new Color(g, g, g, 1f), amount);
        }

        Key Sample(float t)
        {
            int i = 0;
            while (i < Keys.Length - 2 && t >= Keys[i + 1].hour) i++;
            Key a = Keys[i], b = Keys[i + 1];
            float f = Mathf.InverseLerp(a.hour, b.hour, t);
            f = f * f * (3f - 2f * f);
            return new Key
            {
                hour = t,
                fog = Color.Lerp(a.fog, b.fog, f),
                cool = Color.Lerp(a.cool, b.cool, f),
                warm = Color.Lerp(a.warm, b.warm, f),
                shadow = Color.Lerp(a.shadow, b.shadow, f),
                tone = Vector3.Lerp(a.tone, b.tone, f),
                lamp = Mathf.Lerp(a.lamp, b.lamp, f),
                angle = Mathf.Lerp(a.angle, b.angle, f),
                length = Mathf.Lerp(a.length, b.length, f)
            };
        }

        void CollectLights(AtmosphereState s, float lamp)
        {
            int max = AtmosphereState.MaxLights;
            Vector3 f = focus != null ? focus.position : Vector3.zero;
            int n = 0;

            if (lamp > 0.001f)
            {
                var all = PixelLight.All;
                for (int li = 0; li < all.Count; li++)
                {
                    PixelLight l = all[li];
                    if (l == null || !l.isActiveAndEnabled) continue;
                    Vector3 pos = l.WorldPosition;
                    float sq = (pos - f).sqrMagnitude;
                    float reach = l.radius + 18f;
                    if (sq > reach * reach) continue;
                    if (n == max && sq >= _lightDist[max - 1]) continue;

                    int i = n < max ? n : max - 1;
                    while (i > 0 && _lightDist[i - 1] > sq)
                    {
                        _lightDist[i] = _lightDist[i - 1];
                        s.lightPos[i] = s.lightPos[i - 1];
                        s.lightCol[i] = s.lightCol[i - 1];
                        i--;
                    }
                    Color c = l.color * (l.intensity * l.Flicker() * lamp);
                    _lightDist[i] = sq;
                    s.lightPos[i] = new Vector4(pos.x, pos.y, pos.z, l.radius);
                    s.lightCol[i] = new Vector4(c.r, c.g, c.b, 0f);
                    if (n < max) n++;
                }
            }
            s.lightCount = n;
        }

        string FormatClock()
        {
            int h = Mathf.FloorToInt(timeOfDay) % 24;
            int m = Mathf.FloorToInt((timeOfDay - Mathf.Floor(timeOfDay)) * 60f);
            return h.ToString("00") + ":" + m.ToString("00");
        }
    }
}
