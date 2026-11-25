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
        [Tooltip("Enemy: speed (units per second) at which the walk clip plays at its own pace; faster runs speed it up.")]
        public float walkSpeedRef = 2.4f;

        [Header("NPC")]
        [Tooltip("An NPC turns towards the hero when he is this close.")]
        public float lookAtHeroRange = 7f;
        [Tooltip("Off for an NPC that cannot turn (chained to a wall): it always faces fixedYaw.")]
        public bool faceHero = true;
        [Tooltip("World yaw (degrees) of an NPC that does not turn. 0 = north, 90 = east, 180 = south.")]
        public float fixedYaw = 180f;

        [Tooltip("Clip played when the hero comes within greetRange (and not closer than greetMin): looking up, holding out a remedy, waving, the guard's stop. Plays once and holds its last frame.")]
        public string greetClip = "";
        public float greetRange = 5f;
        public float greetMin = 0f;
        [Tooltip("When the greet clip ends, loop its frames from this one on (waving). -1 = hold the last frame, -2 = go back to idle (the greeting is not repeated until the hero leaves).")]
        public int greetLoopFrom = -1;

        [Tooltip("Clip that plays on its own now and then while the hero is within ambientRange (the woodcutter's Bruma fit).")]
        public string ambientClip = "";
        public Vector2 ambientEvery = new Vector2(18f, 35f);
        public float ambientRange = 12f;

        [Tooltip("A warm lamp carried by the NPC (the healer's lantern).")]
        public bool lamp;
        public Vector3 lampOffset = new Vector3(0f, 1.4f, 0f);
        public float lampRadius = 4f;
        public Color lampColor = new Color(1f, 0.62f, 0.3f, 1f);
        [Range(0f, 2f)] public float lampIntensity = 0.9f;

        [Tooltip("Extra box obstacle (what is drawn in the sprite, like the woodcutter's wall): world offset x, z from the NPC and half size. Zero = none.")]
        public Vector2 obstacleOffset;
        public Vector2 obstacleHalf;

        public static ActorSpriteConfig Load(string id) { return Resources.Load<ActorSpriteConfig>(Folder + id); }
    }
}
