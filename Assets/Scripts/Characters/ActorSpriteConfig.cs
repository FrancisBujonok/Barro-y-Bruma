using UnityEngine;

namespace BrumaPx.Characters
{
    /// <summary>
    /// Sprite of a character that is not the hero (an NPC, a boss). It lives in Assets/Resources/Actors/ so MapWorld
    /// can find it by name when it spawns a marker; without it the old primitive look stays.
    /// </summary>
    [CreateAssetMenu(menuName = "Barro y Bruma/Actor Sprite", fileName = "ActorSprite")]
    public sealed class ActorSpriteConfig : ScriptableObject
    {
        public const string Folder = "Actors/";

        public CharacterLayerSet set;
        public Material material;
        public Material shadowMaterial;

        [Header("Boss")]
        [Tooltip("Below this share of its life the boss switches to its second-phase clips (p2idle, crawl2, bite2).")]
        [Range(0f, 1f)] public float phase2Below = 0.5f;
        [Tooltip("Speed (units per second) above which it counts as moving.")]
        public float moveThreshold = 0.25f;

        [Header("NPC")]
        [Tooltip("An NPC turns towards the hero when he is this close.")]
        public float lookAtHeroRange = 7f;

        public static ActorSpriteConfig Load(string id) { return Resources.Load<ActorSpriteConfig>(Folder + id); }
    }
}
