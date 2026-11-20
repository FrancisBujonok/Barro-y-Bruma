using System.Collections.Generic;
using BrumaPx.Combat;
using BrumaPx.Data;
using BrumaPx.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BrumaPx.Characters
{
    /// <summary>
    /// Makes the hero a layered sprite. It reads what already exists (Equipment for the four armour slots, HeroBody
    /// for wounds and Bruma, DioramaHero and HeroCombat for what he is doing) plus the Armament for weapon and
    /// shield, and tells a DioramaSprite what to wear, where to look and which clip to play.
    /// It installs itself on the object called "Hero" when Resources/HeroSpriteConfig says so; no scene is edited.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroSpriteDriver : MonoBehaviour
    {
        HeroSpriteConfig _config;
        DioramaSprite _sprite;
        DioramaHero _hero;
        HeroCombat _combat;
        HeroBody _body;
        Health _health;
        Equipment _equipment;
        Armament _armament;
        int[] _zoneBodyIndex;
        int _testWeapon;
        bool _wasAttacking;
        readonly List<CharacterLayerSet.Layer> _layers = new List<CharacterLayerSet.Layer>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var config = Resources.Load<HeroSpriteConfig>(HeroSpriteConfig.ResourcePath);
            if (config == null || !config.useSprite || config.Active == null || config.material == null) return;
            var hero = Object.FindFirstObjectByType<DioramaHero>();
            if (hero == null || hero.GetComponent<HeroSpriteDriver>() != null) return;
            hero.gameObject.AddComponent<HeroSpriteDriver>()._config = config;
        }

        void Start()
        {
            if (_config == null) { enabled = false; return; }
            _hero = GetComponent<DioramaHero>();
            _combat = GetComponent<HeroCombat>();
            _body = GetComponent<HeroBody>();
            _health = GetComponent<Health>();
            _equipment = GetComponent<Equipment>();
            _armament = GetComponent<Armament>();
            if (_armament == null) _armament = gameObject.AddComponent<Armament>();
            if (_armament.Weapon == null) _armament.SetWeapon(_config.weapon);
            if (_armament.Shield == null) _armament.SetShield(_config.shield);

            // The old look stays in place (other scripts move those transforms); it just stops being drawn.
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;

            var go = new GameObject("Sprite");
            go.transform.SetParent(transform, false);
            _sprite = go.AddComponent<DioramaSprite>();
            _sprite.set = _config.Active;
            _sprite.material = _config.material;
            _sprite.shadowMaterial = _config.shadowMaterial;

            var ids = _config.Active.zoneIds;
            _zoneBodyIndex = new int[ids.Length];
            for (int i = 0; i < ids.Length; i++) _zoneBodyIndex[i] = _body != null ? _body.IndexOf(ids[i]) : -1;

            if (_equipment != null) _equipment.Changed += Rebuild;
            _armament.Changed += Rebuild;
            Rebuild();
        }

        void OnDestroy()
        {
            if (_equipment != null) _equipment.Changed -= Rebuild;
            if (_armament != null) _armament.Changed -= Rebuild;
        }

        /// <summary>Works out the stack of layers from what is worn and held.</summary>
        void Rebuild()
        {
            if (_sprite == null) return;
            var set = _config.Active;
            _layers.Clear();

            bool headgear = _equipment != null && _equipment.Worn(ArmorSlot.Cabeza) != null;
            for (int i = 0; i < set.baseLayers.Length; i++)
            {
                string id = set.baseLayers[i];
                if (headgear && System.Array.IndexOf(set.hiddenUnderHeadgear, id) >= 0) continue;
                var l = set.Find(id);
                if (l != null && l.sheet != null) _layers.Add(l);
            }
            if (_equipment != null)
                for (int s = 0; s < ArmorSlotInfo.Count; s++)
                {
                    ArmorDef piece = _equipment.Worn((ArmorSlot)s);
                    if (piece != null) set.CollectFor(piece.Id, _layers);
                }
            if (_armament.Weapon != null) set.CollectFor(_armament.Weapon.Id, _layers);
            if (_armament.Shield != null) set.CollectFor(_armament.Shield.Id, _layers);
            _sprite.SetLayers(_layers);
        }

        void Update()
        {
            if (_sprite == null) return;
            if (_config.testKeys) TestKeys();
            Face();
            Animate();
            Zones();
        }

        // The hero turns in steps of 45 degrees in the world. The Blender sheets have one view per step; the LPC
        // sheets have four, and diagonals use the side view.
        void Face()
        {
            float camYaw = _hero != null && _hero.sceneCamera != null ? _hero.sceneCamera.transform.eulerAngles.y : 45f;
            _sprite.direction = CharacterLayerSet.ViewFor(transform.eulerAngles.y - camYaw, _sprite.set.views);
        }

        void Animate()
        {
            bool dead = _health != null && _health.IsDead;
            bool attacking = _combat != null && _combat.IsAttacking;
            if (dead) _sprite.Play(_sprite.HasClip("death") ? "death" : "hurt", 1f, false);
            else if (attacking)
            {
                bool heavy = _armament.Weapon != null && _armament.Weapon.TwoHanded;
                _sprite.Play("slash", heavy ? _config.heavyAttackSpeed : _config.lightAttackSpeed, !_wasAttacking);
            }
            else if (_hero != null && _hero.IsMoving) _sprite.Play("walk", DioramaInput.Run() ? _config.runSpeed : 1f, false);
            else _sprite.Play("idle", 1f, false);
            _wasAttacking = attacking;
        }

        void Zones()
        {
            if (_body == null || _zoneBodyIndex == null || _body.Count == 0) return;
            for (int i = 0; i < _zoneBodyIndex.Length; i++)
            {
                int b = _zoneBodyIndex[i];
                if (b < 0) continue;
                _sprite.SetZone(i + 1, (int)_body.Wound(b), _body.Level(b) / 100f);
            }
        }

        void TestKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f9Key.wasPressedThisFrame && _config.testWeapons.Length > 0)
            {
                _testWeapon = (_testWeapon + 1) % (_config.testWeapons.Length + 1);
                _armament.SetWeapon(_testWeapon == 0 ? null : _config.testWeapons[_testWeapon - 1]);
            }
            if (kb.f10Key.wasPressedThisFrame)
                _armament.SetShield(_armament.Shield == null ? _config.testShield : null);
        }
    }
}
