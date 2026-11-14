using BrumaPx.Player;
using BrumaPx.World;
using UnityEngine;

namespace BrumaPx.Combat
{
    /// <summary>
    /// The extras of an enemy, read from its EnemyProfile: a Bruma aura that walks with it (the taken), a cloud when
    /// it dies (the bloated), hiding until the hero comes close (the fog ambusher), and the mark in the plague ledger
    /// when it used to be someone (a taken NPC). Every enemy carries this; with zeros in the profile it does nothing.
    /// </summary>
    [RequireComponent(typeof(EnemyBrain))]
    [DisallowMultipleComponent]
    public sealed class EnemyTraits : MonoBehaviour
    {
        [Tooltip("Dark material of the cloud this enemy leaves when it dies.")]
        public Material cloudMaterial;
        [Tooltip("Key NPC this enemy used to be (a taken one). When it dies the plague ledger marks that person as dead.")]
        public string npcId;

        EnemyBrain _brain;
        Health _health;
        Transform _hero;
        GameObject _aura;
        Renderer[] _renderers;
        bool _hidden;

        public bool Hidden { get { return _hidden; } }

        void Awake()
        {
            _brain = GetComponent<EnemyBrain>();
            _health = GetComponent<Health>();
            _health.Died += OnDied;
            _health.Damaged += OnDamaged;
            _brain.Respawned += OnRespawned;
        }

        void OnDestroy()
        {
            if (_health != null)
            {
                _health.Died -= OnDied;
                _health.Damaged -= OnDamaged;
            }
            if (_brain != null) _brain.Respawned -= OnRespawned;
        }

        void Start()
        {
            if (_brain.profile == null) return;
            var hero = FindFirstObjectByType<DioramaHero>();
            _hero = hero != null ? hero.transform : null;
            BuildAura();
            if (_brain.profile.AmbushRange > 0f) Hide();
        }

        void BuildAura()
        {
            EnemyProfile p = _brain.profile;
            if (p.AuraRadius <= 0f || _aura != null) return;
            _aura = new GameObject("Aura de Bruma");
            _aura.transform.SetParent(transform, false);
            _aura.AddComponent<BrumaArea>().Configure("Aura de Bruma", p.AuraRadius, p.AuraDensity);
        }

        void Update()
        {
            if (!_hidden || _hero == null) return;
            Vector3 d = _hero.position - transform.position;
            d.y = 0f;
            // A hero who sees farther spots the ambusher sooner; one who has lost sight to the Bruma, later.
            float range = _brain.profile.AmbushRange * Mathf.Max(0.5f, HeroBody.VisionScale);
            if (d.magnitude <= range) Reveal(true);
        }

        void Hide()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].enabled = false;
            _brain.Dormant = true;
            _hidden = true;
        }

        void Reveal(bool lunge)
        {
            if (!_hidden) return;
            _hidden = false;
            if (_renderers != null)
                for (int i = 0; i < _renderers.Length; i++)
                    if (_renderers[i] != null) _renderers[i].enabled = true;
            _brain.Dormant = false;
            if (lunge) _brain.Ambush();
        }

        void OnDamaged(float amount, Vector2 dir)
        {
            if (_hidden) Reveal(true);
        }

        void OnRespawned()
        {
            if (_brain.profile != null && _brain.profile.AmbushRange > 0f && !_hidden) Hide();
        }

        void OnDied()
        {
            Reveal(false);
            if (_aura != null) Destroy(_aura);

            EnemyProfile p = _brain.profile;
            if (p != null && p.DeathCloudRadius > 0f) SpawnCloud(p);
            if (!string.IsNullOrEmpty(npcId)) PlagueLedger.Set(npcId, NpcPlagueState.Dead);
        }

        void SpawnCloud(EnemyProfile p)
        {
            var go = new GameObject("Nube de Bruma");
            go.transform.position = new Vector3(transform.position.x, 0f, transform.position.z);
            var area = go.AddComponent<BrumaArea>();
            area.Configure("Nube de Bruma", p.DeathCloudRadius, p.DeathCloudDensity);
            go.AddComponent<BrumaCloud>().Begin(area, p.DeathCloudSeconds, cloudMaterial, p.DeathCloudRadius, p.DeathCloudDensity);
        }
    }
}
