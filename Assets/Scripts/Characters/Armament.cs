using System;
using BrumaPx.Data;
using UnityEngine;

namespace BrumaPx.Characters
{
    /// <summary>
    /// What a character holds: one weapon (light or heavy) and, with a light weapon, a shield.
    /// For now it only drives the look; HeroCombat does not read the numbers of the WeaponDef yet.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Armament : MonoBehaviour
    {
        [SerializeField] WeaponDef _weapon;
        [SerializeField] WeaponDef _shield;

        public event Action Changed;

        public WeaponDef Weapon { get { return _weapon; } }

        /// <summary>The shield in use. A heavy weapon takes both hands, so there is none with it.</summary>
        public WeaponDef Shield { get { return _weapon != null && _weapon.TwoHanded ? null : _shield; } }

        public void SetWeapon(WeaponDef weapon)
        {
            if (weapon != null && weapon.Class == WeaponClass.Escudo) { SetShield(weapon); return; }
            if (_weapon == weapon) return;
            _weapon = weapon;
            if (Changed != null) Changed();
        }

        public void SetShield(WeaponDef shield)
        {
            if (shield != null && shield.Class != WeaponClass.Escudo) return;
            if (_shield == shield) return;
            _shield = shield;
            if (Changed != null) Changed();
        }

        void OnValidate()
        {
            if (Application.isPlaying && Changed != null) Changed();
        }
    }
}
