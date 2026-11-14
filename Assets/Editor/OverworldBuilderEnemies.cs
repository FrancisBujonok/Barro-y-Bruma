using BrumaPx.Combat;
using UnityEditor;
using UnityEngine;

namespace BrumaPx.EditorTools
{
    /// <summary>
    /// Fourth part of the overworld builder: the five new enemies (the bloated one, the hound, the crossbowman, the
    /// fog ambusher and the taken one). Profiles are only created when missing, so hand tuning survives a rebuild.
    /// All numbers that make each one different live in the profile; the models are placeholder blocks.
    /// </summary>
    public static partial class OverworldBuilder
    {
        static EnemyProfile MakeEnemy(string file, string label, float hp, float radius, float walk, float chase,
            float sight, float minLight, float fov, float hearing, float lose,
            float range, float damage, float startup, float active, float recovery, float arc, float noise, float bruma,
            float staggerAt, float staggerSeconds, float knock, System.Action<SerializedObject> extra)
        {
            return GetProfile(file, delegate (SerializedObject so)
            {
                so.FindProperty("_displayName").stringValue = label;
                SetF(so, "_maxHealth", hp);
                SetF(so, "_radius", radius);
                SetF(so, "_walkSpeed", walk);
                SetF(so, "_chaseSpeed", chase);
                SetF(so, "_sightRange", sight);
                SetF(so, "_minLightFactor", minLight);
                SetF(so, "_fieldOfView", fov);
                SetF(so, "_hearingMultiplier", hearing);
                SetF(so, "_loseTrackSeconds", lose);
                SetF(so, "_attackRange", range);
                SetF(so, "_attackDamage", damage);
                SetF(so, "_attackStartup", startup);
                SetF(so, "_attackActive", active);
                SetF(so, "_attackRecovery", recovery);
                SetF(so, "_attackArcDegrees", arc);
                SetF(so, "_attackNoise", noise);
                SetF(so, "_brumaOnHit", bruma);
                SetF(so, "_staggerThreshold", staggerAt);
                SetF(so, "_staggerSeconds", staggerSeconds);
                SetF(so, "_knockbackScale", knock);
                if (extra != null) extra(so);
            });
        }

        static void BuildMoreEnemies(int ppu, Material flash)
        {
            // Slow and heavy; when it dies it bursts into a cloud of Bruma that lingers.
            var bloat = MakeEnemy("Hinchado", "Hinchado", 80f, 0.55f, 0.8f, 1.5f, 6f, 0.3f, 110f, 1.0f, 5f,
                1.5f, 18f, 0.9f, 0.2f, 1.4f, 120f, 3f, 10f, 30f, 0.5f, 0.4f,
                delegate (SerializedObject so)
                {
                    SetF(so, "_deathCloudRadius", 2.8f);
                    SetF(so, "_deathCloudSeconds", 25f);
                    SetF(so, "_deathCloudDensity", 1f);
                });

            // Fast, weak, hears from far away, and the whole pack comes when one of them sees you.
            var hound = MakeEnemy("Sabueso", "Sabueso de Bruma", 14f, 0.3f, 1.8f, 4.4f, 8f, 0.6f, 180f, 2.2f, 7f,
                1.0f, 7f, 0.22f, 0.1f, 0.35f, 80f, 2f, 2f, 8f, 0.5f, 1.8f,
                delegate (SerializedObject so) { SetF(so, "_packRadius", 8f); });

            // Shoots from afar, backs away when you get close, and a parry staggers it.
            var crossbow = MakeEnemy("Ballestero", "Saqueador con ballesta", 35f, 0.35f, 1.3f, 2.8f, 11f, 0.5f, 140f, 1.0f, 7f,
                9f, 16f, 0.9f, 0.1f, 1.2f, 40f, 6f, 0f, 15f, 0.5f, 1.0f,
                delegate (SerializedObject so)
                {
                    SetF(so, "_projectileSpeed", 11f);
                    SetF(so, "_keepDistance", 4.5f);
                });

            // Waits hidden at the edge of what you see and springs when you come close.
            var ambusher = MakeEnemy("Emboscador", "Emboscador de la niebla", 40f, 0.35f, 1.4f, 3.0f, 6f, 0.4f, 360f, 0.6f, 6f,
                1.3f, 20f, 0.25f, 0.12f, 0.8f, 100f, 3f, 4f, 12f, 0.5f, 1.0f,
                delegate (SerializedObject so) { SetF(so, "_ambushRange", 4.5f); });

            // A person the Bruma took: tougher than an infected one, and a patch of Bruma that walks.
            var taken = MakeEnemy("Tomado", "Tomado", 70f, 0.4f, 1.0f, 2.4f, 8f, 0.35f, 130f, 1.4f, 8f,
                1.3f, 16f, 0.6f, 0.15f, 0.8f, 100f, 3f, 12f, 24f, 0.5f, 0.7f,
                delegate (SerializedObject so)
                {
                    SetF(so, "_auraRadius", 2.2f);
                    SetF(so, "_auraDensity", 0.6f);
                });

            BuildBloat("Hinchado A", new Vector3(-14f, 0f, -6f), 70f, bloat, ppu, flash);

            BuildHound("Sabueso A", new Vector3(-8f, 0f, 14f), 160f, hound, ppu, flash);
            BuildHound("Sabueso B", new Vector3(-9.3f, 0f, 15.1f), 150f, hound, ppu, flash);
            BuildHound("Sabueso C", new Vector3(-6.8f, 0f, 15.5f), 170f, hound, ppu, flash);

            BuildCrossbow("Ballestero A", new Vector3(14f, 0f, 10f), 220f, crossbow, ppu, flash);
            BuildAmbusher("Emboscador A", new Vector3(22f, 0f, 4f), 270f, ambusher, ppu, flash);

            BuildTaken("Tomado A", new Vector3(-20f, 0f, -16f), 40f, taken, ppu, flash);
        }

        // Pale and swollen, with black sores on the belly.
        static void BuildBloat(string name, Vector3 pos, float yaw, EnemyProfile profile, int ppu, Material flash)
        {
            var g = Group(name, pos, yaw);
            var body = new GameObject("Body").transform;
            body.SetParent(g, false);
            Prim(body, PrimitiveType.Sphere, "Belly", new Vector3(0f, 0.95f, 0.04f), new Vector3(1.15f, 1.05f, 1.0f), Vector3.zero, "RockLight");
            Prim(body, PrimitiveType.Sphere, "Chest", new Vector3(0f, 1.5f, 0.1f), new Vector3(0.7f, 0.5f, 0.55f), Vector3.zero, "RockLight");
            Prim(body, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.82f, 0.18f), new Vector3(0.3f, 0.32f, 0.3f), Vector3.zero, "HeroScarf");
            Prim(body, PrimitiveType.Cube, "Eye L", new Vector3(-0.06f, 1.85f, 0.32f), new Vector3(0.06f, 0.05f, 0.03f), Vector3.zero, "Glow", false);
            Prim(body, PrimitiveType.Cube, "Eye R", new Vector3(0.06f, 1.85f, 0.32f), new Vector3(0.06f, 0.05f, 0.03f), Vector3.zero, "Glow", false);
            Prim(body, PrimitiveType.Sphere, "Sore 1", new Vector3(-0.3f, 1.05f, 0.5f), new Vector3(0.2f, 0.2f, 0.12f), Vector3.zero, "Black", false);
            Prim(body, PrimitiveType.Sphere, "Sore 2", new Vector3(0.28f, 0.85f, 0.52f), new Vector3(0.24f, 0.24f, 0.12f), Vector3.zero, "Black", false);
            Prim(body, PrimitiveType.Sphere, "Sore 3", new Vector3(0.05f, 1.25f, 0.48f), new Vector3(0.16f, 0.16f, 0.1f), Vector3.zero, "Black", false);

            var arms = new GameObject("Arms").transform;
            arms.SetParent(body, false);
            arms.localPosition = new Vector3(0f, 1.4f, 0.15f);
            Prim(arms, PrimitiveType.Cube, "Arm L", new Vector3(-0.58f, -0.4f, 0.1f), new Vector3(0.16f, 0.75f, 0.16f), new Vector3(0f, 0f, 8f), "Bark");
            Prim(arms, PrimitiveType.Cube, "Arm R", new Vector3(0.58f, -0.4f, 0.1f), new Vector3(0.16f, 0.75f, 0.16f), new Vector3(0f, 0f, -8f), "Bark");

            var legL = Prim(g, PrimitiveType.Cube, "Leg L", new Vector3(-0.22f, 0.3f, 0f), new Vector3(0.26f, 0.6f, 0.28f), Vector3.zero, "Bark");
            var legR = Prim(g, PrimitiveType.Cube, "Leg R", new Vector3(0.22f, 0.3f, 0f), new Vector3(0.26f, 0.6f, 0.28f), Vector3.zero, "Bark");
            FinishEnemy(g, body, legL.transform, legR.transform, arms, profile, ppu, flash);
        }

        // Low on four legs (two slabs, one per side); the head is the pivot that dips when it bites.
        static void BuildHound(string name, Vector3 pos, float yaw, EnemyProfile profile, int ppu, Material flash)
        {
            var g = Group(name, pos, yaw);
            var body = new GameObject("Body").transform;
            body.SetParent(g, false);
            Prim(body, PrimitiveType.Cube, "Torso", new Vector3(0f, 0.52f, 0f), new Vector3(0.38f, 0.36f, 0.85f), Vector3.zero, "Bark");
            Prim(body, PrimitiveType.Cube, "Haunch", new Vector3(0f, 0.56f, -0.35f), new Vector3(0.42f, 0.42f, 0.4f), Vector3.zero, "Bark");
            Prim(body, PrimitiveType.Cube, "Tail", new Vector3(0f, 0.64f, -0.72f), new Vector3(0.08f, 0.08f, 0.4f), new Vector3(-25f, 0f, 0f), "Bark");

            var head = new GameObject("Head").transform;
            head.SetParent(body, false);
            head.localPosition = new Vector3(0f, 0.62f, 0.42f);
            Prim(head, PrimitiveType.Cube, "Skull", new Vector3(0f, 0f, 0.22f), new Vector3(0.26f, 0.24f, 0.36f), Vector3.zero, "RockLight");
            Prim(head, PrimitiveType.Cube, "Jaw", new Vector3(0f, -0.1f, 0.3f), new Vector3(0.2f, 0.07f, 0.28f), Vector3.zero, "HeroScarf");
            Prim(head, PrimitiveType.Cube, "Eye L", new Vector3(-0.08f, 0.06f, 0.37f), new Vector3(0.05f, 0.04f, 0.03f), Vector3.zero, "Glow", false);
            Prim(head, PrimitiveType.Cube, "Eye R", new Vector3(0.08f, 0.06f, 0.37f), new Vector3(0.05f, 0.04f, 0.03f), Vector3.zero, "Glow", false);

            var legL = Prim(g, PrimitiveType.Cube, "Legs L", new Vector3(-0.16f, 0.2f, 0f), new Vector3(0.11f, 0.4f, 0.78f), Vector3.zero, "Bark");
            var legR = Prim(g, PrimitiveType.Cube, "Legs R", new Vector3(0.16f, 0.2f, 0f), new Vector3(0.11f, 0.4f, 0.78f), Vector3.zero, "Bark");
            FinishEnemy(g, body, legL.transform, legR.transform, head, profile, ppu, flash);
        }

        // A hooded man with a crossbow held out in front when he aims.
        static void BuildCrossbow(string name, Vector3 pos, float yaw, EnemyProfile profile, int ppu, Material flash)
        {
            var g = Group(name, pos, yaw);
            var body = new GameObject("Body").transform;
            body.SetParent(g, false);
            Prim(body, PrimitiveType.Cylinder, "Cloak", new Vector3(0f, 0.85f, 0f), new Vector3(0.58f, 0.62f, 0.58f), Vector3.zero, "Bark");
            Prim(body, PrimitiveType.Sphere, "Shoulders", new Vector3(0f, 1.4f, 0f), new Vector3(0.68f, 0.38f, 0.52f), Vector3.zero, "Bark");
            Prim(body, PrimitiveType.Sphere, "Hood", new Vector3(0f, 1.68f, 0f), new Vector3(0.5f, 0.54f, 0.5f), Vector3.zero, "Black");
            Prim(body, PrimitiveType.Sphere, "Face", new Vector3(0f, 1.64f, 0.13f), new Vector3(0.3f, 0.3f, 0.26f), Vector3.zero, "HeroScarf");
            Prim(body, PrimitiveType.Cube, "Eye L", new Vector3(-0.07f, 1.67f, 0.26f), new Vector3(0.06f, 0.05f, 0.03f), Vector3.zero, "Glow", false);
            Prim(body, PrimitiveType.Cube, "Eye R", new Vector3(0.07f, 1.67f, 0.26f), new Vector3(0.06f, 0.05f, 0.03f), Vector3.zero, "Glow", false);
            Prim(body, PrimitiveType.Cube, "Arm L", new Vector3(-0.4f, 1.0f, 0.04f), new Vector3(0.14f, 0.62f, 0.14f), new Vector3(0f, 0f, 8f), "Bark");
            Prim(body, PrimitiveType.Cube, "Quiver", new Vector3(-0.12f, 1.1f, -0.34f), new Vector3(0.14f, 0.55f, 0.14f), new Vector3(0f, 0f, 14f), "WoodDark");

            var arms = new GameObject("Weapon Arm").transform;
            arms.SetParent(body, false);
            arms.localPosition = new Vector3(0.4f, 1.34f, 0.05f);
            Prim(arms, PrimitiveType.Cube, "Arm R", new Vector3(0f, -0.3f, 0.08f), new Vector3(0.14f, 0.62f, 0.14f), new Vector3(-20f, 0f, 0f), "Bark");
            Prim(arms, PrimitiveType.Cube, "Stock", new Vector3(0f, -0.55f, 0.42f), new Vector3(0.1f, 0.1f, 0.8f), Vector3.zero, "Wood");
            Prim(arms, PrimitiveType.Cube, "Limbs", new Vector3(0f, -0.55f, 0.78f), new Vector3(0.75f, 0.06f, 0.07f), Vector3.zero, "WoodDark");

            var legL = Prim(g, PrimitiveType.Cube, "Leg L", new Vector3(-0.14f, 0.38f, 0f), new Vector3(0.2f, 0.76f, 0.22f), Vector3.zero, "HeroPants");
            var legR = Prim(g, PrimitiveType.Cube, "Leg R", new Vector3(0.14f, 0.38f, 0f), new Vector3(0.2f, 0.76f, 0.22f), Vector3.zero, "HeroPants");
            FinishEnemy(g, body, legL.transform, legR.transform, arms, profile, ppu, flash);
            g.GetComponent<EnemyBrain>().projectileMaterial = M("Wood");
        }

        // Tall, thin and dark, with long arms: you only see it once it is already on you.
        static void BuildAmbusher(string name, Vector3 pos, float yaw, EnemyProfile profile, int ppu, Material flash)
        {
            var g = Group(name, pos, yaw);
            var body = new GameObject("Body").transform;
            body.SetParent(g, false);
            Prim(body, PrimitiveType.Cylinder, "Torso", new Vector3(0f, 0.95f, 0f), new Vector3(0.4f, 0.55f, 0.34f), Vector3.zero, "Black");
            Prim(body, PrimitiveType.Sphere, "Shoulders", new Vector3(0f, 1.5f, 0f), new Vector3(0.55f, 0.3f, 0.4f), Vector3.zero, "Black");
            Prim(body, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.78f, 0.05f), new Vector3(0.3f, 0.34f, 0.3f), Vector3.zero, "RockLight");
            Prim(body, PrimitiveType.Cube, "Eye L", new Vector3(-0.07f, 1.8f, 0.2f), new Vector3(0.08f, 0.06f, 0.03f), Vector3.zero, "Glow", false);
            Prim(body, PrimitiveType.Cube, "Eye R", new Vector3(0.07f, 1.8f, 0.2f), new Vector3(0.08f, 0.06f, 0.03f), Vector3.zero, "Glow", false);

            var arms = new GameObject("Arms").transform;
            arms.SetParent(body, false);
            arms.localPosition = new Vector3(0f, 1.42f, 0.05f);
            Prim(arms, PrimitiveType.Cube, "Arm L", new Vector3(-0.34f, -0.55f, 0.1f), new Vector3(0.1f, 1.1f, 0.1f), new Vector3(0f, 0f, 5f), "Black");
            Prim(arms, PrimitiveType.Cube, "Arm R", new Vector3(0.34f, -0.55f, 0.1f), new Vector3(0.1f, 1.1f, 0.1f), new Vector3(0f, 0f, -5f), "Black");

            var legL = Prim(g, PrimitiveType.Cube, "Leg L", new Vector3(-0.12f, 0.45f, 0f), new Vector3(0.12f, 0.9f, 0.14f), Vector3.zero, "Black");
            var legR = Prim(g, PrimitiveType.Cube, "Leg R", new Vector3(0.12f, 0.45f, 0f), new Vector3(0.12f, 0.9f, 0.14f), Vector3.zero, "Black");
            FinishEnemy(g, body, legL.transform, legR.transform, arms, profile, ppu, flash);
        }

        // An infected person in rags, bigger, with a dark stain under it that marks its aura of Bruma.
        static void BuildTaken(string name, Vector3 pos, float yaw, EnemyProfile profile, int ppu, Material flash)
        {
            var g = Group(name, pos, yaw);
            Prim(g, PrimitiveType.Cylinder, "Aura", new Vector3(0f, 0.02f, 0f), new Vector3(3.6f, 0.01f, 3.6f), Vector3.zero, "Black", false);

            var body = new GameObject("Body").transform;
            body.SetParent(g, false);
            Prim(body, PrimitiveType.Cylinder, "Torso", new Vector3(0f, 1.0f, 0.06f), new Vector3(0.58f, 0.52f, 0.44f), new Vector3(16f, 0f, 0f), "HeroCloakDark");
            Prim(body, PrimitiveType.Cylinder, "Rags", new Vector3(0f, 0.62f, 0.02f), new Vector3(0.68f, 0.3f, 0.52f), Vector3.zero, "HeroCloakDark");
            Prim(body, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.65f, 0.2f), new Vector3(0.36f, 0.38f, 0.36f), Vector3.zero, "HeroScarf");
            Prim(body, PrimitiveType.Cube, "Eye L", new Vector3(-0.08f, 1.68f, 0.38f), new Vector3(0.07f, 0.06f, 0.03f), Vector3.zero, "Glow", false);
            Prim(body, PrimitiveType.Cube, "Eye R", new Vector3(0.08f, 1.68f, 0.38f), new Vector3(0.07f, 0.06f, 0.03f), Vector3.zero, "Glow", false);

            var arms = new GameObject("Arms").transform;
            arms.SetParent(body, false);
            arms.localPosition = new Vector3(0f, 1.32f, 0.1f);
            Prim(arms, PrimitiveType.Cube, "Arm L", new Vector3(-0.4f, -0.45f, 0.1f), new Vector3(0.15f, 0.9f, 0.15f), new Vector3(0f, 0f, 6f), "RockLight");
            Prim(arms, PrimitiveType.Cube, "Arm R", new Vector3(0.4f, -0.45f, 0.1f), new Vector3(0.15f, 0.9f, 0.15f), new Vector3(0f, 0f, -6f), "RockLight");

            var legL = Prim(g, PrimitiveType.Cube, "Leg L", new Vector3(-0.16f, 0.36f, 0f), new Vector3(0.2f, 0.72f, 0.22f), Vector3.zero, "HeroPants");
            var legR = Prim(g, PrimitiveType.Cube, "Leg R", new Vector3(0.16f, 0.36f, 0f), new Vector3(0.2f, 0.72f, 0.22f), Vector3.zero, "HeroPants");
            FinishEnemy(g, body, legL.transform, legR.transform, arms, profile, ppu, flash);
            g.GetComponent<EnemyTraits>().npcId = "tomado_demo";
        }
    }
}
