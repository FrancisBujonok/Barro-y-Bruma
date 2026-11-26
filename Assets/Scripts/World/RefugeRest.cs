using BrumaPx.Combat;
using BrumaPx.Player;
using UnityEngine;

namespace BrumaPx.World
{
    /// <summary>
    /// What makes a refuge a place to rest: standing inside it the hero can press interact to recover health and
    /// stamina, and the spot becomes the place where the hero comes back if they fall. The Bruma never reaches a
    /// refuge (HeroBody already treats it as clean air), so resting here is safe from it.
    /// </summary>
    [RequireComponent(typeof(Refuge))]
    public sealed class RefugeRest : MonoBehaviour
    {
        Refuge _refuge;
        float _cooldown;

        void Awake() { _refuge = GetComponent<Refuge>(); }

        void Update()
        {
            var hero = DioramaHeroCache.Hero;
            if (hero == null || _refuge == null) return;
            if (Refuge.At(hero.position) != _refuge) return;
            // furniture to search right next to the hero (inside the healer's hut) comes first
            if (Searchable.HasTarget) return;

            var health = hero.GetComponent<Health>();
            if (health == null || health.IsDead) return;

            MapLoot.SetPrompt("[Interactuar] Descansar en " + _refuge.DisplayName);
            if (Time.unscaledTime < _cooldown || !DioramaInput.InteractPressed()) return;
            _cooldown = Time.unscaledTime + 1.5f;

            health.Heal(health.max);
            var combat = hero.GetComponent<HeroCombat>();
            if (combat != null)
            {
                combat.RestoreStamina(9999f);
                combat.SetCheckpoint(hero.position, hero.rotation);
            }
            MapWorld.Banner("Descansas en " + _refuge.DisplayName + ". Si caes, volveras aqui.", 4f);
        }
    }
}
