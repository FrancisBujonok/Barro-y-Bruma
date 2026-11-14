using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>
    /// A cloud of Bruma that does not last: it is a BrumaArea whose density fades out, with a flat dark stain and a
    /// few wisps that shrink with it. Left behind by the bloated infected when they burst.
    /// </summary>
    public sealed class BrumaCloud : MonoBehaviour
    {
        BrumaArea _area;
        Transform _visual;
        float _life, _age, _density;

        public void Begin(BrumaArea area, float seconds, Material material, float radius, float density)
        {
            _area = area;
            _life = Mathf.Max(1f, seconds);
            _density = density;

            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            Part(PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), new Vector3(radius * 1.6f, 0.01f, radius * 1.6f), material);
            for (int i = 0; i < 5; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                float d = Random.value * radius * 0.55f;
                float s = 0.45f + Random.value * 0.5f;
                Part(PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * d, 0.12f + Random.value * 0.15f, Mathf.Sin(a) * d),
                    new Vector3(s, 0.2f, s * 0.8f), material);
            }
        }

        void Part(PrimitiveType type, Vector3 pos, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(_visual, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        void Update()
        {
            _age += Time.deltaTime;
            float t = _age / _life;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            float fade = t < 0.4f ? 1f : 1f - (t - 0.4f) / 0.6f;
            if (_area != null) _area.SetDensity(_density * fade);
            if (_visual != null)
            {
                float k = Mathf.Max(0.2f, fade);
                _visual.localScale = new Vector3(k, 1f, k);
            }
        }
    }
}
