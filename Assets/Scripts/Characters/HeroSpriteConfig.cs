using BrumaPx.Data;
using UnityEngine;

namespace BrumaPx.Characters
{
    /// <summary>
    /// Switch and settings of the sprite hero. It lives in Assets/Resources/HeroSpriteConfig.asset so it is found
    /// without touching any scene: with <see cref="useSprite"/> off, the game shows the old primitive hero.
    /// </summary>
    [CreateAssetMenu(menuName = "Barro y Bruma/Hero Sprite Config", fileName = "HeroSpriteConfig")]
    public sealed class HeroSpriteConfig : ScriptableObject
    {
        public const string ResourcePath = "HeroSpriteConfig";

        [Tooltip("Off = the old hero made of primitives.")]
        public bool useSprite = true;
        [Tooltip("On = the hero rendered in Blender (8 views). Off = the LPC layers (4 views, armour visible).")]
        public bool blenderHero = true;
        public CharacterLayerSet blenderLayers;
        [Tooltip("The LPC layer set (armour and weapons as layers).")]
        public CharacterLayerSet layers;
        public Material material;
        public Material shadowMaterial;

        [Header("What he holds at the start")]
        public WeaponDef weapon;
        public WeaponDef shield;

        [Header("Test keys (temporary): F9 next weapon, F10 shield on/off")]
        public bool testKeys = true;
        public WeaponDef[] testWeapons = new WeaponDef[0];
        public WeaponDef testShield;

        [Header("Animation speed")]
        public float lightAttackSpeed = 1.6f;
        public float heavyAttackSpeed = 0.9f;
        public float runSpeed = 1.5f;

        /// <summary>The layer set in use.</summary>
        public CharacterLayerSet Active { get { return blenderHero && blenderLayers != null ? blenderLayers : layers; } }
    }
}
