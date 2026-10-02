using UnityEngine;

namespace Wolfenstein.Editor
{
    // The five monster bodies, after the concept sheet in Reference: a hunched feral ghoul, a hulking spiked brute, a horned
    // demon, a floating armoured eye and a towering horned elite. Each is modelled with the mesh kit at life size, facing +Z
    // with its feet at the origin, on the rig the monster view animates. None stands taller than 2.6 m even with its arms
    // raised, which keeps it under the ceiling beams and lamps of the lowest rooms.
    public static partial class TestLevelBuilder
    {
        private static readonly float[] Sides = { -1f, 1f };

        private static string SideName(float side, string part) => (side < 0f ? "Left " : "Right ") + part;

        // ---- Stalker: the feral --------------------------------------------------------------------------------------------

        private static MonsterMotion StalkerMotion() => new MonsterMotion
        {
            StrideLength = 1.6f, LegSwing = 36f, ArmSwing = 30f, RunLean = 16f, BobHeight = 0.07f, StrikeLean = 24f,
            WindupArmPitch = -145f, WindupLean = -14f, BodyShift = 0.18f, HeadTurn = 35f, JawGape = 32f,
            SwayAngle = 0f, SwayRate = 1f, HoverBobRate = 1f,
            FallDirection = 1f, LyingHeight = 0.2f, Blood = new Color(0.34f, 0.03f, 0.02f)
        };

        // A hunched ghoul of raw red flesh running low on bent-back legs, its pale head thrust forward below its shoulders,
        // pointed ears laid back, a gaping maw of fangs and burning red eyes, and long arms ending in hooked claws that
        // hang almost to the floor.
        private static void BuildStalkerModel(MonsterRig rig)
        {
            const string monster = "Stalker";
            Material eyes = GetOrCreateGlowMaterial("Feral_Eyes", new Color(1f, 0.06f, 0.02f) * 2.2f);

            foreach (float side in Sides)
            {
                Transform leg = Pivot(rig.Model, SideName(side, "Leg"), new Vector3(side * 0.17f, 0.84f, -0.06f));
                var kit = new MeshKit(side < 0f ? 11 : 12);
                kit.Blob(new Vector3(0f, -0.16f, 0.07f), new Vector3(0.12f, 0.21f, 0.13f), RedFlesh, new Vector3(-25f, 0f, 0f));
                kit.Limb(new Vector3(0f, -0.34f, 0.17f), new Vector3(0f, -0.66f, -0.07f), 0.08f, 0.05f, RedFlesh);
                kit.Blob(new Vector3(0f, -0.45f, 0.08f), new Vector3(0.075f, 0.13f, 0.075f), TanFlesh, new Vector3(38f, 0f, 0f));
                kit.Limb(new Vector3(0f, -0.66f, -0.07f), new Vector3(0f, -0.8f, 0.1f), 0.055f, 0.045f, RedFlesh);
                kit.Spike(new Vector3(0f, -0.66f, -0.08f), new Vector3(0f, -0.72f, -0.19f), 0.025f, ClawTone);
                for (int toe = -1; toe <= 1; toe++)
                {
                    kit.Hook(new Vector3(toe * 0.035f, -0.8f, 0.11f), new Vector3(toe * 0.05f, -0.8f, 0.2f),
                        new Vector3(toe * 0.06f, -0.84f, 0.27f), 0.024f, ClawTone);
                }

                Skin(leg, monster, leg.name, kit);
                if (side < 0f)
                    rig.LeftLeg = leg;
                else
                    rig.RightLeg = leg;
            }

            Transform torso = Pivot(rig.Model, "Torso", new Vector3(0f, 0.84f, 0f));
            rig.Torso = torso;
            var body = new MeshKit(13);
            body.Blob(new Vector3(0f, 0.04f, 0f), new Vector3(0.19f, 0.13f, 0.15f), RedFlesh);
            body.Blob(new Vector3(0f, -0.07f, 0.09f), new Vector3(0.15f, 0.1f, 0.07f), BloodTone);
            body.Blob(new Vector3(0f, 0.22f, 0.06f), new Vector3(0.17f, 0.16f, 0.14f), RedFlesh, new Vector3(20f, 0f, 0f));
            body.Blob(new Vector3(0f, 0.45f, 0.2f), new Vector3(0.3f, 0.21f, 0.22f), RedFlesh, new Vector3(35f, 0f, 0f), 0.12f);
            for (int i = 0; i < 3; i++)
            {
                body.Blob(new Vector3(0f, 0.18f + i * 0.09f, 0.18f + i * 0.03f), new Vector3(0.09f, 0.045f, 0.05f), TanFlesh,
                    new Vector3(20f, 0f, 0f), 0.05f);
            }

            body.Blob(new Vector3(0f, 0.5f, 0.02f), new Vector3(0.23f, 0.17f, 0.18f), DarkHide, new Vector3(30f, 0f, 0f));
            for (int i = 0; i < 4; i++)
                body.Spike(new Vector3(0f, 0.32f + i * 0.1f, -0.1f - i * 0.02f), new Vector3(0f, 0.39f + i * 0.1f, -0.21f - i * 0.02f), 0.03f, BoneTone);
            foreach (float side in Sides)
                body.Blob(new Vector3(side * 0.22f, 0.6f, 0.2f), new Vector3(0.15f, 0.13f, 0.14f), RedFlesh);
            body.Limb(new Vector3(0f, 0.58f, 0.3f), new Vector3(0f, 0.62f, 0.44f), 0.09f, 0.08f, RedFlesh);
            Skin(torso, monster, "Torso", body);

            Transform head = Pivot(torso, "Head", new Vector3(0f, 0.64f, 0.47f));
            rig.Head = head;
            var skull = new MeshKit(14);
            skull.Blob(new Vector3(0f, 0.05f, 0.01f), new Vector3(0.14f, 0.13f, 0.16f), TanFlesh);
            skull.Blob(new Vector3(0f, 0.1f, 0.11f), new Vector3(0.13f, 0.04f, 0.06f), RedFlesh, new Vector3(-10f, 0f, 0f));
            skull.Blob(new Vector3(0f, 0f, 0.14f), new Vector3(0.11f, 0.07f, 0.11f), TanFlesh);
            skull.Blob(new Vector3(0f, 0.03f, 0.245f), new Vector3(0.028f, 0.018f, 0.014f), BlackTone, default, 0f, 4, 6);
            foreach (float x in new[] { -0.075f, -0.04f, 0.04f, 0.075f })
                skull.Spike(new Vector3(x, -0.04f, 0.2f), new Vector3(x * 0.9f, -0.12f, 0.21f), 0.013f, TeethTone);
            foreach (float side in Sides)
                skull.Spike(new Vector3(side * 0.12f, 0.1f, -0.02f), new Vector3(side * 0.27f, 0.21f, -0.13f), 0.045f, TanFlesh);
            Skin(head, monster, "Head", skull);
            foreach (float side in Sides)
            {
                Glowing(rig, CreateModelPart(head, "Eye", PrimitiveType.Sphere, new Vector3(side * 0.056f, 0.07f, 0.15f),
                    new Vector3(0.05f, 0.04f, 0.035f), Quaternion.identity, eyes));
            }

            Transform jaw = Pivot(head, "Jaw", new Vector3(0f, -0.03f, 0.05f), new Vector3(6f, 0f, 0f));
            rig.Jaw = jaw;
            var mouth = new MeshKit(15);
            mouth.Blob(new Vector3(0f, -0.035f, 0.1f), new Vector3(0.1f, 0.04f, 0.11f), TanFlesh);
            mouth.Blob(new Vector3(0f, 0f, 0.1f), new Vector3(0.08f, 0.025f, 0.085f), MouthTone);
            foreach (float x in new[] { -0.07f, -0.035f, 0.035f, 0.07f })
                mouth.Spike(new Vector3(x, -0.01f, 0.17f), new Vector3(x * 0.9f, 0.06f, 0.18f), 0.012f, TeethTone);
            Skin(jaw, monster, "Jaw", mouth);

            foreach (float side in Sides)
            {
                Transform arm = Pivot(torso, SideName(side, "Arm"), new Vector3(side * 0.31f, 0.53f, 0.27f), new Vector3(-25f, 0f, side * 12f));
                var kit = new MeshKit(side < 0f ? 16 : 17);
                kit.Blob(new Vector3(0f, -0.02f, 0f), new Vector3(0.12f, 0.1f, 0.12f), RedFlesh);
                kit.Limb(Vector3.zero, new Vector3(0f, -0.36f, 0.02f), 0.09f, 0.075f, RedFlesh);
                kit.Blob(new Vector3(0f, -0.17f, 0.03f), new Vector3(0.085f, 0.15f, 0.09f), TanFlesh);
                kit.Limb(new Vector3(0f, -0.36f, 0.02f), new Vector3(0f, -0.72f, 0.08f), 0.08f, 0.065f, RedFlesh);
                kit.Blob(new Vector3(0f, -0.5f, 0.05f), new Vector3(0.08f, 0.14f, 0.08f), RedFlesh, new Vector3(-8f, 0f, 0f));
                kit.Blob(new Vector3(0f, -0.78f, 0.1f), new Vector3(0.09f, 0.06f, 0.1f), RedFlesh);
                for (int claw = -1; claw <= 1; claw++)
                {
                    kit.Hook(new Vector3(claw * 0.05f, -0.8f, 0.15f), new Vector3(claw * 0.06f, -0.92f, 0.28f),
                        new Vector3(claw * 0.07f, -1.08f, 0.3f), 0.03f, ClawTone);
                }

                kit.Hook(new Vector3(side * -0.08f, -0.76f, 0.08f), new Vector3(side * -0.12f, -0.84f, 0.16f),
                    new Vector3(side * -0.1f, -0.94f, 0.22f), 0.022f, ClawTone);
                Skin(arm, monster, arm.name, kit);
                if (side < 0f)
                    rig.LeftArm = arm;
                else
                    rig.RightArm = arm;
            }
        }

        // ---- Brute ---------------------------------------------------------------------------------------------------------

        private static MonsterMotion BruteMotion() => new MonsterMotion
        {
            StrideLength = 2.3f, LegSwing = 22f, ArmSwing = 16f, RunLean = 6f, BobHeight = 0.09f, StrikeLean = 20f,
            WindupArmPitch = -112f, WindupLean = -8f, BodyShift = 0.06f, HeadTurn = 25f, JawGape = 24f,
            SwayAngle = 0f, SwayRate = 1f, HoverBobRate = 1f,
            FallDirection = -1f, LyingHeight = 0.42f, Blood = new Color(0.3f, 0.03f, 0.02f)
        };

        // A mountain of red and tan muscle on short thick legs: a chest and shoulders far wider than it is tall, bone spikes
        // bursting from its shoulders and back under riveted iron plates, a small fanged head sunk low between them with
        // glowing eyes, and huge fists in spiked iron bracers.
        private static void BuildBruteModel(MonsterRig rig)
        {
            const string monster = "Brute";
            Material eyes = GetOrCreateGlowMaterial("Brute_Eyes", new Color(1f, 0.12f, 0.02f) * 2.2f);

            foreach (float side in Sides)
            {
                Transform leg = Pivot(rig.Model, SideName(side, "Leg"), new Vector3(side * 0.3f, 0.95f, -0.02f));
                var kit = new MeshKit(side < 0f ? 21 : 22);
                kit.Blob(new Vector3(0f, -0.22f, 0.05f), new Vector3(0.22f, 0.28f, 0.24f), RedFlesh, new Vector3(-10f, 0f, 0f));
                kit.Blob(new Vector3(0f, -0.48f, 0.1f), new Vector3(0.16f, 0.14f, 0.16f), TanFlesh);
                kit.Limb(new Vector3(0f, -0.5f, 0.1f), new Vector3(0f, -0.84f, 0.02f), 0.17f, 0.14f, RedFlesh);
                kit.Blob(new Vector3(0f, -0.64f, 0.02f), new Vector3(0.16f, 0.17f, 0.16f), RedFlesh);
                kit.Limb(new Vector3(0f, -0.76f, 0.02f), new Vector3(0f, -0.84f, 0.02f), 0.165f, 0.165f, IronTone, 8);
                kit.Blob(new Vector3(0f, -0.89f, 0.1f), new Vector3(0.18f, 0.07f, 0.27f), RedFlesh);
                for (int toe = -1; toe <= 1; toe++)
                    kit.Spike(new Vector3(toe * 0.08f, -0.9f, 0.32f), new Vector3(toe * 0.09f, -0.93f, 0.42f), 0.03f, ClawTone);
                Skin(leg, monster, leg.name, kit);
                if (side < 0f)
                    rig.LeftLeg = leg;
                else
                    rig.RightLeg = leg;
            }

            Transform torso = Pivot(rig.Model, "Torso", new Vector3(0f, 0.95f, 0f));
            rig.Torso = torso;
            var body = new MeshKit(23);
            body.Blob(new Vector3(0f, 0.05f, 0f), new Vector3(0.36f, 0.2f, 0.28f), RedFlesh);
            body.Blob(new Vector3(0f, -0.08f, 0.2f), new Vector3(0.24f, 0.18f, 0.07f), BloodTone);
            body.Blob(new Vector3(0f, 0.32f, 0.12f), new Vector3(0.44f, 0.34f, 0.36f), RedFlesh);
            body.Blob(new Vector3(0f, 0.72f, 0.12f), new Vector3(0.6f, 0.38f, 0.42f), RedFlesh, new Vector3(15f, 0f, 0f), 0.12f);
            foreach (float side in Sides)
            {
                body.Blob(new Vector3(side * 0.24f, 0.74f, 0.4f), new Vector3(0.24f, 0.18f, 0.14f), TanFlesh);
                body.Blob(new Vector3(side * 0.1f, 0.3f, 0.44f), new Vector3(0.09f, 0.08f, 0.06f), TanFlesh, default, 0.05f);
                body.Blob(new Vector3(side * 0.1f, 0.45f, 0.44f), new Vector3(0.09f, 0.07f, 0.06f), TanFlesh, default, 0.05f);
                body.Blob(new Vector3(side * 0.54f, 0.92f, 0.02f), new Vector3(0.32f, 0.3f, 0.34f), RedFlesh);
                body.Slab(new Vector3(side * 0.54f, 1.14f, 0.08f), new Vector3(0.32f, 0.06f, 0.42f), IronTone, new Vector3(0f, 0f, side * -25f));
                body.Spike(new Vector3(side * 0.6f, 1.12f, 0.05f), new Vector3(side * 0.7f, 1.38f, 0.02f), 0.06f, BoneTone);
                body.Spike(new Vector3(side * 0.74f, 1.04f, -0.05f), new Vector3(side * 0.88f, 1.18f, -0.12f), 0.055f, BoneTone);
                body.Spike(new Vector3(side * 0.52f, 1.1f, -0.2f), new Vector3(side * 0.56f, 1.3f, -0.34f), 0.05f, BoneTone);
                body.Spike(new Vector3(side * 0.74f, 0.92f, 0.18f), new Vector3(side * 0.86f, 0.98f, 0.3f), 0.045f, BoneTone);
            }

            body.Blob(new Vector3(0f, 0.98f, -0.22f), new Vector3(0.5f, 0.34f, 0.32f), DarkHide);
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * 0.16f;
                body.Spike(new Vector3(x, 1.12f - Mathf.Abs(x) * 0.3f, -0.36f), new Vector3(x * 1.3f, 1.32f - Mathf.Abs(x) * 0.3f, -0.5f), 0.05f, BoneTone);
            }

            Skin(torso, monster, "Torso", body);

            Transform head = Pivot(torso, "Head", new Vector3(0f, 0.98f, 0.44f));
            rig.Head = head;
            var skull = new MeshKit(24);
            skull.Blob(new Vector3(0f, 0.06f, 0.02f), new Vector3(0.2f, 0.18f, 0.2f), TanFlesh);
            skull.Blob(new Vector3(0f, 0.13f, 0.14f), new Vector3(0.19f, 0.05f, 0.07f), RedFlesh);
            skull.Blob(new Vector3(0f, 0f, 0.17f), new Vector3(0.14f, 0.08f, 0.1f), TanFlesh);
            foreach (float side in Sides)
                skull.Spike(new Vector3(side * 0.17f, 0.12f, 0f), new Vector3(side * 0.3f, 0.25f, -0.07f), 0.05f, BoneTone);
            foreach (float x in new[] { -0.1f, -0.06f, -0.02f, 0.02f, 0.06f, 0.1f })
                skull.Spike(new Vector3(x, -0.05f, 0.24f), new Vector3(x * 0.95f, -0.14f, 0.25f), 0.018f, TeethTone);
            Skin(head, monster, "Head", skull);
            foreach (float side in Sides)
            {
                Glowing(rig, CreateModelPart(head, "Eye", PrimitiveType.Sphere, new Vector3(side * 0.075f, 0.08f, 0.18f),
                    new Vector3(0.06f, 0.04f, 0.04f), Quaternion.Euler(0f, 0f, side * -15f), eyes));
            }

            Transform jaw = Pivot(head, "Jaw", new Vector3(0f, -0.06f, 0.05f), new Vector3(10f, 0f, 0f));
            rig.Jaw = jaw;
            var mouth = new MeshKit(25);
            mouth.Blob(new Vector3(0f, -0.05f, 0.13f), new Vector3(0.17f, 0.06f, 0.15f), TanFlesh);
            mouth.Blob(new Vector3(0f, 0f, 0.13f), new Vector3(0.13f, 0.035f, 0.11f), MouthTone);
            foreach (float side in Sides)
                mouth.Spike(new Vector3(side * 0.1f, -0.02f, 0.24f), new Vector3(side * 0.12f, 0.13f, 0.26f), 0.03f, TeethTone);
            foreach (float x in new[] { -0.05f, 0f, 0.05f })
                mouth.Spike(new Vector3(x, -0.02f, 0.26f), new Vector3(x, 0.05f, 0.27f), 0.015f, TeethTone);
            Skin(jaw, monster, "Jaw", mouth);

            foreach (float side in Sides)
            {
                Transform arm = Pivot(torso, SideName(side, "Arm"), new Vector3(side * 0.66f, 0.86f, 0.1f), new Vector3(-10f, 0f, side * 3f));
                var kit = new MeshKit(side < 0f ? 26 : 27);
                kit.Blob(new Vector3(0f, -0.26f, 0f), new Vector3(0.22f, 0.32f, 0.23f), RedFlesh);
                kit.Blob(new Vector3(0f, -0.55f, 0.03f), new Vector3(0.17f, 0.15f, 0.17f), TanFlesh);
                kit.Limb(new Vector3(0f, -0.55f, 0.04f), new Vector3(0f, -0.92f, 0.12f), 0.2f, 0.17f, RedFlesh);
                kit.Limb(new Vector3(0f, -0.68f, 0.07f), new Vector3(0f, -0.92f, 0.12f), 0.22f, 0.2f, IronTone, 8);
                foreach (Vector3 outward in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                {
                    Vector3 root = new Vector3(0f, -0.8f, 0.1f) + outward * 0.2f;
                    kit.Spike(root, root + outward * 0.1f + Vector3.up * 0.03f, 0.04f, BoneTone);
                }

                kit.Blob(new Vector3(0f, -1.08f, 0.16f), new Vector3(0.2f, 0.18f, 0.21f), TanFlesh);
                foreach (float x in new[] { -0.1f, 0f, 0.1f })
                    kit.Spike(new Vector3(x, -1.08f, 0.35f), new Vector3(x, -1.08f, 0.45f), 0.035f, ClawTone);
                Skin(arm, monster, arm.name, kit);
                if (side < 0f)
                    rig.LeftArm = arm;
                else
                    rig.RightArm = arm;
            }
        }

        // ---- Demon ---------------------------------------------------------------------------------------------------------

        private static MonsterMotion DemonMotion() => new MonsterMotion
        {
            StrideLength = 1.9f, LegSwing = 34f, ArmSwing = 24f, RunLean = 12f, BobHeight = 0.06f, StrikeLean = 22f,
            WindupArmPitch = -150f, WindupLean = -14f, BodyShift = 0.08f, HeadTurn = 35f, JawGape = 28f,
            SwayAngle = 0f, SwayRate = 1f, HoverBobRate = 1f,
            FallDirection = -1f, LyingHeight = 0.2f, Blood = new Color(0.45f, 0.05f, 0.02f)
        };

        // A lean red devil on bent-back legs: great ridged horns sweeping out and up from its brow, an angry bull-like
        // muzzle with a mouth that glows like a furnace, spines on its shoulders and clawed hands, one of which conjures
        // the fireballs it throws.
        private static void BuildDemonModel(MonsterRig rig)
        {
            const string monster = "Demon";
            Material eyes = GetOrCreateGlowMaterial("Demon_Eyes", new Color(1f, 0.32f, 0.04f) * 2.4f);
            Material maw = GetOrCreateGlowMaterial("Demon_Maw", new Color(1f, 0.35f, 0.05f) * 2.5f);
            Material fireball = GetOrCreateGlowMaterial("Demon_Fireball", new Color(1f, 0.3f, 0.05f) * 4f);

            foreach (float side in Sides)
            {
                Transform leg = Pivot(rig.Model, SideName(side, "Leg"), new Vector3(side * 0.16f, 0.92f, -0.04f));
                var kit = new MeshKit(side < 0f ? 31 : 32);
                kit.Blob(new Vector3(0f, -0.18f, 0.08f), new Vector3(0.145f, 0.24f, 0.155f), DemonSkin, new Vector3(-28f, 0f, 0f));
                kit.Limb(new Vector3(0f, -0.38f, 0.18f), new Vector3(0f, -0.72f, -0.08f), 0.085f, 0.06f, DemonSkin);
                kit.Blob(new Vector3(0f, -0.5f, 0.08f), new Vector3(0.095f, 0.16f, 0.095f), DemonSkin, new Vector3(38f, 0f, 0f));
                kit.Limb(new Vector3(0f, -0.72f, -0.08f), new Vector3(0f, -0.88f, 0.08f), 0.055f, 0.05f, DemonSkin);
                for (int toe = -1; toe <= 1; toe++)
                {
                    kit.Hook(new Vector3(toe * 0.035f, -0.88f, 0.09f), new Vector3(toe * 0.05f, -0.89f, 0.17f),
                        new Vector3(toe * 0.06f, -0.92f, 0.23f), 0.022f, ClawTone);
                }

                Skin(leg, monster, leg.name, kit);
                if (side < 0f)
                    rig.LeftLeg = leg;
                else
                    rig.RightLeg = leg;
            }

            Transform torso = Pivot(rig.Model, "Torso", new Vector3(0f, 0.92f, 0f));
            rig.Torso = torso;
            var body = new MeshKit(33);
            body.Blob(new Vector3(0f, 0.04f, 0f), new Vector3(0.18f, 0.12f, 0.13f), DemonSkin);
            body.Blob(new Vector3(0f, -0.06f, 0.09f), new Vector3(0.14f, 0.09f, 0.06f), DarkHide);
            body.Blob(new Vector3(0f, 0.22f, 0.04f), new Vector3(0.19f, 0.17f, 0.15f), DemonSkin);
            foreach (float side in Sides)
            {
                for (int i = 0; i < 3; i++)
                    body.Blob(new Vector3(side * 0.06f, 0.16f + i * 0.1f, 0.14f), new Vector3(0.055f, 0.045f, 0.04f), RedFlesh, default, 0.05f);
                body.Blob(new Vector3(side * 0.14f, 0.52f, 0.24f), new Vector3(0.16f, 0.12f, 0.09f), DemonSkin);
                body.Blob(new Vector3(side * 0.33f, 0.64f, 0.08f), new Vector3(0.18f, 0.16f, 0.18f), DemonSkin);
                body.Blob(new Vector3(side * 0.18f, 0.68f, 0f), new Vector3(0.15f, 0.1f, 0.13f), RedFlesh);
                body.Spike(new Vector3(side * 0.34f, 0.74f, 0.04f), new Vector3(side * 0.4f, 0.95f, -0.02f), 0.035f, BoneTone);
                body.Spike(new Vector3(side * 0.42f, 0.7f, 0f), new Vector3(side * 0.57f, 0.84f, -0.06f), 0.03f, BoneTone);
                body.Spike(new Vector3(side * 0.26f, 0.76f, -0.06f), new Vector3(side * 0.28f, 0.94f, -0.16f), 0.03f, BoneTone);
            }

            body.Blob(new Vector3(0f, 0.5f, 0.1f), new Vector3(0.37f, 0.25f, 0.23f), DemonSkin, new Vector3(15f, 0f, 0f), 0.1f);
            for (int i = 0; i < 3; i++)
                body.Spike(new Vector3(0f, 0.34f + i * 0.12f, -0.1f), new Vector3(0f, 0.4f + i * 0.12f, -0.2f), 0.025f, BoneTone);
            body.Limb(new Vector3(0f, 0.66f, 0.12f), new Vector3(0f, 0.74f, 0.2f), 0.08f, 0.075f, DemonSkin);
            Skin(torso, monster, "Torso", body);

            Transform head = Pivot(torso, "Head", new Vector3(0f, 0.76f, 0.2f));
            rig.Head = head;
            var skull = new MeshKit(34);
            skull.Blob(new Vector3(0f, 0.07f, 0f), new Vector3(0.15f, 0.16f, 0.15f), DemonSkin);
            foreach (float side in Sides)
            {
                skull.Blob(new Vector3(side * 0.06f, 0.13f, 0.11f), new Vector3(0.08f, 0.035f, 0.05f), RedFlesh, new Vector3(0f, 0f, side * -20f), 0.05f);
                skull.Spike(new Vector3(side * 0.13f, 0f, 0.05f), new Vector3(side * 0.21f, -0.05f, -0.01f), 0.03f, DemonSkin);
                skull.Hook(new Vector3(side * 0.1f, 0.16f, 0f), new Vector3(side * 0.32f, 0.2f, -0.06f), new Vector3(side * 0.52f, 0.34f, -0.02f),
                    new Vector3(side * 0.46f, 0.6f, 0.1f), 0.075f, BoneTone, 9, 7, 0.12f);
            }

            skull.Blob(new Vector3(0f, 0.02f, 0.14f), new Vector3(0.085f, 0.07f, 0.1f), DemonSkin);
            skull.Blob(new Vector3(0f, 0.04f, 0.23f), new Vector3(0.045f, 0.025f, 0.02f), BlackTone, default, 0f, 4, 6);
            foreach (float x in new[] { -0.06f, -0.025f, 0.025f, 0.06f })
                skull.Spike(new Vector3(x, -0.02f, 0.2f), new Vector3(x * 0.9f, -0.08f, 0.21f), 0.012f, TeethTone);
            Skin(head, monster, "Head", skull);
            foreach (float side in Sides)
            {
                Glowing(rig, CreateModelPart(head, "Eye", PrimitiveType.Sphere, new Vector3(side * 0.055f, 0.09f, 0.13f),
                    new Vector3(0.05f, 0.035f, 0.035f), Quaternion.Euler(0f, 0f, side * -18f), eyes));
            }

            Glowing(rig, CreateModelPart(head, "Maw", PrimitiveType.Sphere, new Vector3(0f, -0.03f, 0.13f), new Vector3(0.12f, 0.05f, 0.12f),
                Quaternion.identity, maw));

            Transform jaw = Pivot(head, "Jaw", new Vector3(0f, -0.03f, 0.04f), new Vector3(10f, 0f, 0f));
            rig.Jaw = jaw;
            var mouth = new MeshKit(35);
            mouth.Blob(new Vector3(0f, -0.04f, 0.12f), new Vector3(0.09f, 0.04f, 0.1f), DemonSkin);
            foreach (float x in new[] { -0.06f, -0.025f, 0.025f, 0.06f })
                mouth.Spike(new Vector3(x, -0.01f, 0.18f), new Vector3(x * 0.9f, 0.05f, 0.19f), 0.012f, TeethTone);
            Skin(jaw, monster, "Jaw", mouth);

            foreach (float side in Sides)
            {
                Transform arm = Pivot(torso, SideName(side, "Arm"), new Vector3(side * 0.42f, 0.6f, 0.1f), new Vector3(-18f, 0f, side * 12f));
                var kit = new MeshKit(side < 0f ? 36 : 37);
                kit.Blob(new Vector3(0f, -0.02f, 0f), new Vector3(0.14f, 0.12f, 0.14f), DemonSkin);
                kit.Limb(Vector3.zero, new Vector3(0f, -0.34f, 0.02f), 0.1f, 0.08f, DemonSkin);
                kit.Blob(new Vector3(0f, -0.16f, 0.03f), new Vector3(0.105f, 0.16f, 0.105f), RedFlesh);
                kit.Limb(new Vector3(0f, -0.34f, 0.02f), new Vector3(0f, -0.66f, 0.06f), 0.085f, 0.07f, DemonSkin);
                kit.Blob(new Vector3(0f, -0.48f, 0.04f), new Vector3(0.09f, 0.15f, 0.09f), DemonSkin, new Vector3(-6f, 0f, 0f));
                kit.Spike(new Vector3(0f, -0.34f, -0.05f), new Vector3(0f, -0.29f, -0.17f), 0.03f, BoneTone);
                kit.Blob(new Vector3(0f, -0.72f, 0.08f), new Vector3(0.095f, 0.06f, 0.1f), DemonSkin);
                foreach (float x in new[] { -0.05f, -0.017f, 0.017f, 0.05f })
                {
                    kit.Hook(new Vector3(x, -0.74f, 0.13f), new Vector3(x * 1.2f, -0.84f, 0.22f), new Vector3(x * 1.3f, -0.96f, 0.22f),
                        0.02f, ClawTone);
                }

                Skin(arm, monster, arm.name, kit);
                if (side < 0f)
                {
                    rig.LeftArm = arm;
                    continue;
                }

                rig.RightArm = arm;
                rig.Muzzle = Pivot(arm, "Muzzle", new Vector3(0f, -0.82f, 0.14f));
                rig.ChargeGlow = CreateModelPart(arm, "Charge", PrimitiveType.Sphere, new Vector3(0f, -0.84f, 0.14f), Vector3.one * 0.26f,
                    Quaternion.identity, fireball).transform;
            }
        }

        // ---- Spitter: the floating eye -------------------------------------------------------------------------------------

        private static MonsterMotion SpitterMotion() => new MonsterMotion
        {
            StrideLength = 2f, LegSwing = 0f, ArmSwing = 0f, RunLean = 10f, BobHeight = 0f, StrikeLean = 0f,
            WindupArmPitch = -150f, WindupLean = 0f, HeadTurn = 14f, JawGape = 0f, HoverBob = 0.08f, HoverBobRate = 0.9f, SwayAngle = 14f,
            SwayRate = 0.9f, FallDirection = 0f, DeathDrop = 1.05f, LyingHeight = 0f, Blood = new Color(0.32f, 0.02f, 0.02f)
        };

        // No body at all: an armoured orb floating at head height around one huge burning eye with a slit pupil, ringed by
        // teeth, crowned with horned spikes and dripping blood, trailing bladed tentacles that curl beneath it. It fires
        // from the eye.
        private static void BuildSpitterModel(MonsterRig rig)
        {
            const string monster = "Spitter";
            Material eyeGlow = GetOrCreateGlowMaterial("Spitter_Eye", new Color(1f, 0.14f, 0.02f) * 2.2f);
            Material charge = GetOrCreateGlowMaterial("Spitter_EyeCharge", new Color(1f, 0.3f, 0.05f) * 3.2f);

            Transform torso = Pivot(rig.Model, "Torso", new Vector3(0f, 1.55f, 0f));
            rig.Torso = torso;
            var shell = new MeshKit(41);
            shell.Blob(new Vector3(0f, 0f, -0.05f), new Vector3(0.5f, 0.48f, 0.46f), ShellTone, default, 0.07f, 8, 12);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f;
                Vector3 around = Quaternion.Euler(0f, 0f, angle) * Vector3.up;
                shell.Slab(around * 0.37f + Vector3.forward * 0.28f, new Vector3(0.17f, 0.07f, 0.12f), ShellTone, new Vector3(0f, 0f, angle));
                shell.Spike(around * 0.3f + Vector3.forward * 0.38f, around * 0.21f + Vector3.forward * 0.44f, 0.022f, TeethTone);
            }

            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f + 22.5f;
                Vector3 around = Quaternion.Euler(0f, 0f, angle) * Vector3.up;
                shell.Slab(around * 0.42f - Vector3.forward * 0.12f, new Vector3(0.22f, 0.08f, 0.26f), ShellTone,
                    new Vector3(0f, 0f, angle));
            }

            shell.Spike(new Vector3(0f, 0.42f, -0.02f), new Vector3(0f, 0.8f, 0f), 0.075f, ClawTone);
            foreach (float side in Sides)
            {
                shell.Spike(new Vector3(side * 0.3f, 0.34f, 0.1f), new Vector3(side * 0.52f, 0.6f, 0.14f), 0.055f, ClawTone);
                shell.Spike(new Vector3(side * 0.4f, 0.18f, -0.22f), new Vector3(side * 0.64f, 0.36f, -0.4f), 0.05f, ClawTone);
                shell.Spike(new Vector3(side * 0.44f, -0.06f, 0.12f), new Vector3(side * 0.72f, -0.02f, 0.2f), 0.045f, ClawTone);
            }

            for (int i = 0; i < 6; i++)
            {
                float x = -0.25f + i * 0.1f;
                var root = new Vector3(x, -0.3f - Mathf.Abs(x) * 0.2f, 0.28f);
                shell.Spike(root, root + Vector3.down * (0.22f + 0.1f * (i % 3)) + Vector3.forward * 0.01f, 0.028f, BloodTone);
            }

            Skin(torso, monster, "Shell", shell);

            // The eye turns within its socket to follow its target, carrying the charge and the point bolts leave from.
            Transform eye = Pivot(torso, "Eye", Vector3.zero);
            rig.Head = eye;
            var socket = new MeshKit(42);
            socket.Blob(new Vector3(0f, 0f, 0.27f), new Vector3(0.3f, 0.3f, 0.14f), MouthTone, default, 0.04f);
            socket.Slab(new Vector3(0f, 0f, 0.455f), new Vector3(0.05f, 0.3f, 0.02f), BlackTone);
            Skin(eye, monster, "Socket", socket);
            Glowing(rig, CreateModelPart(eye, "Eye Glow", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.33f), new Vector3(0.44f, 0.44f, 0.22f),
                Quaternion.identity, eyeGlow));
            rig.Muzzle = Pivot(eye, "Muzzle", new Vector3(0f, 0f, 0.6f));
            rig.ChargeGlow = CreateModelPart(eye, "Charge", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.56f), Vector3.one * 0.34f,
                Quaternion.identity, charge).transform;

            float[] angles = { -150f, -90f, -30f, 30f, 90f, 150f };
            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 outward = Quaternion.Euler(0f, angles[i], 0f) * Vector3.forward;
                bool front = Mathf.Abs(angles[i]) < 60f;
                Transform tentacle = Pivot(torso, $"Tentacle {i + 1}", outward * 0.3f + Vector3.down * 0.3f);
                var kit = new MeshKit(43 + i);
                float reach = front ? 0.3f : 0.22f;
                Vector3[] path =
                {
                    Vector3.zero,
                    outward * (reach * 0.5f) + Vector3.down * 0.28f,
                    outward * reach + Vector3.down * 0.56f,
                    outward * (reach * 0.8f) + Vector3.down * 0.8f,
                };
                kit.Tube(path, new[] { 0.08f, 0.065f, 0.05f, 0.036f }, RedFlesh, 6, 0.08f);
                kit.Spike(path[1] + outward * 0.05f, path[1] + outward * 0.16f + Vector3.up * 0.04f, 0.025f, ClawTone);
                kit.Spike(path[2] + outward * 0.04f, path[2] + outward * 0.13f + Vector3.up * 0.03f, 0.02f, ClawTone);
                kit.Hook(path[3], path[3] + Vector3.down * 0.12f + outward * 0.05f, path[3] + Vector3.down * 0.2f - outward * 0.1f,
                    front ? 0.04f : 0.032f, ClawTone);
                Skin(tentacle, monster, tentacle.name, kit);
                rig.Sway.Add(tentacle);
            }
        }

        // ---- Revenant: the elite -------------------------------------------------------------------------------------------

        private static MonsterMotion RevenantMotion() => new MonsterMotion
        {
            StrideLength = 2.2f, LegSwing = 28f, ArmSwing = 18f, RunLean = 10f, BobHeight = 0.07f, StrikeLean = 20f,
            WindupArmPitch = -84f, WindupLean = -3f, BodyShift = 0.06f, HeadTurn = 25f, JawGape = 24f,
            SwayAngle = 10f, SwayRate = 0.8f, HoverBobRate = 1f,
            FallDirection = 1f, LyingHeight = 0.45f, Blood = new Color(0.55f, 0.12f, 0.02f)
        };

        // The largest thing in the fortress: a hulk of charcoal hide and blood-red muscle on bent-back legs, great ridged
        // horns curling from its head around a central crest, spiked pauldron masses and a spined back, a furnace glowing
        // in its jaws and chest, a spiked tail lashing behind, and taloned hands, one of which hurls the fire it spits.
        private static void BuildRevenantModel(MonsterRig rig)
        {
            const string monster = "Revenant";
            Material glow = GetOrCreateGlowMaterial("Revenant_Glow", new Color(1f, 0.08f, 0.02f) * 2f);
            Material furnace = GetOrCreateGlowMaterial("Revenant_Furnace", new Color(1f, 0.22f, 0.03f) * 2.2f);
            Material fireball = GetOrCreateGlowMaterial("Revenant_Fireball", new Color(1f, 0.28f, 0.05f) * 4f);

            foreach (float side in Sides)
            {
                Transform leg = Pivot(rig.Model, SideName(side, "Leg"), new Vector3(side * 0.34f, 1f, -0.06f));
                var kit = new MeshKit(side < 0f ? 51 : 52);
                kit.Blob(new Vector3(0f, -0.2f, 0.06f), new Vector3(0.22f, 0.3f, 0.24f), RedFlesh, new Vector3(-20f, 0f, 0f));
                kit.Slab(new Vector3(0f, -0.2f, 0.28f), new Vector3(0.24f, 0.3f, 0.06f), CharcoalTone, new Vector3(-20f, 0f, 0f));
                kit.Spike(new Vector3(0f, -0.42f, 0.22f), new Vector3(0f, -0.38f, 0.42f), 0.06f, BoneTone);
                kit.Limb(new Vector3(0f, -0.44f, 0.16f), new Vector3(0f, -0.78f, -0.1f), 0.15f, 0.11f, CharcoalTone);
                kit.Blob(new Vector3(0f, -0.58f, 0.04f), new Vector3(0.13f, 0.17f, 0.13f), CharcoalTone, new Vector3(35f, 0f, 0f));
                kit.Limb(new Vector3(0f, -0.78f, -0.1f), new Vector3(0f, -0.94f, 0.1f), 0.1f, 0.09f, CharcoalTone);
                for (int toe = -1; toe <= 1; toe++)
                {
                    kit.Hook(new Vector3(toe * 0.06f, -0.94f, 0.12f), new Vector3(toe * 0.08f, -0.95f, 0.26f),
                        new Vector3(toe * 0.1f, -1f, 0.36f), 0.04f, ClawTone);
                }

                Skin(leg, monster, leg.name, kit);
                if (side < 0f)
                    rig.LeftLeg = leg;
                else
                    rig.RightLeg = leg;
            }

            Transform torso = Pivot(rig.Model, "Torso", new Vector3(0f, 1f, 0f));
            rig.Torso = torso;
            var body = new MeshKit(53);
            body.Blob(new Vector3(0f, 0.05f, 0f), new Vector3(0.38f, 0.22f, 0.3f), CharcoalTone);
            body.Slab(new Vector3(0f, -0.08f, 0.26f), new Vector3(0.3f, 0.3f, 0.05f), CharcoalTone, new Vector3(8f, 0f, 0f));
            body.Blob(new Vector3(0f, 0.34f, 0.1f), new Vector3(0.46f, 0.34f, 0.36f), RedFlesh);
            body.Blob(new Vector3(0f, 0.72f, 0.1f), new Vector3(0.64f, 0.4f, 0.44f), CharcoalTone, new Vector3(12f, 0f, 0f), 0.12f);
            body.Blob(new Vector3(0f, 1.02f, -0.24f), new Vector3(0.52f, 0.36f, 0.34f), CharcoalTone);
            foreach (float side in Sides)
            {
                body.Blob(new Vector3(side * 0.56f, 0.96f, 0.02f), new Vector3(0.33f, 0.3f, 0.34f), CharcoalTone);
                body.Spike(new Vector3(side * 0.62f, 1.18f, 0.05f), new Vector3(side * 0.72f, 1.46f, 0f), 0.07f, BoneTone);
                body.Spike(new Vector3(side * 0.78f, 1.06f, -0.06f), new Vector3(side * 0.92f, 1.22f, -0.12f), 0.06f, BoneTone);
                body.Spike(new Vector3(side * 0.6f, 1.14f, -0.22f), new Vector3(side * 0.64f, 1.36f, -0.38f), 0.055f, BoneTone);
                body.Spike(new Vector3(side * 0.8f, 0.92f, 0.16f), new Vector3(side * 0.92f, 0.98f, 0.3f), 0.05f, BoneTone);
            }

            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2f) * 0.14f;
                body.Spike(new Vector3(x, 1.18f - Mathf.Abs(x) * 0.4f, -0.4f), new Vector3(x * 1.2f, 1.4f - Mathf.Abs(x) * 0.4f, -0.56f), 0.055f, BoneTone);
            }

            Skin(torso, monster, "Torso", body);
            Glowing(rig, CreateModelPart(torso, "Chest Fire", PrimitiveType.Sphere, new Vector3(0f, 0.64f, 0.5f), new Vector3(0.28f, 0.12f, 0.06f),
                Quaternion.identity, furnace));

            Transform tail = Pivot(torso, "Tail", new Vector3(0f, 0.12f, -0.28f));
            var tailKit = new MeshKit(54);
            Vector3[] tailPath = { Vector3.zero, new Vector3(0f, -0.18f, -0.3f), new Vector3(0f, -0.46f, -0.56f), new Vector3(0f, -0.76f, -0.72f) };
            tailKit.Tube(tailPath, new[] { 0.14f, 0.1f, 0.06f, 0.03f }, RedFlesh, 7, 0.06f);
            for (int i = 1; i < 3; i++)
                tailKit.Spike(tailPath[i] + Vector3.up * 0.06f, tailPath[i] + new Vector3(0f, 0.2f, -0.08f), 0.035f, BoneTone);
            tailKit.Hook(tailPath[3], tailPath[3] + new Vector3(0f, -0.1f, -0.08f), tailPath[3] + new Vector3(0f, -0.08f, -0.24f), 0.045f, ClawTone);
            Skin(tail, monster, "Tail", tailKit);
            rig.Sway.Add(tail);

            Transform head = Pivot(torso, "Head", new Vector3(0f, 1f, 0.44f));
            rig.Head = head;
            var skull = new MeshKit(55);
            skull.Blob(new Vector3(0f, 0.06f, 0.02f), new Vector3(0.22f, 0.19f, 0.22f), CharcoalTone);
            skull.Blob(new Vector3(0f, 0f, 0.16f), new Vector3(0.17f, 0.12f, 0.12f), RedFlesh);
            skull.Blob(new Vector3(0f, 0.12f, 0.15f), new Vector3(0.2f, 0.05f, 0.08f), CharcoalTone);
            skull.Hook(new Vector3(0f, 0.2f, 0.06f), new Vector3(0f, 0.34f, 0.1f), new Vector3(0f, 0.48f, 0.2f), 0.07f, BoneTone, 5, 6, 0.1f);
            foreach (float side in Sides)
            {
                skull.Hook(new Vector3(side * 0.16f, 0.12f, 0f), new Vector3(side * 0.4f, 0.14f, -0.1f), new Vector3(side * 0.56f, 0.34f, -0.04f),
                    new Vector3(side * 0.46f, 0.54f, 0.14f), 0.09f, BoneTone, 9, 7, 0.12f);
            }

            foreach (float x in new[] { -0.1f, -0.06f, -0.02f, 0.02f, 0.06f, 0.1f })
                skull.Spike(new Vector3(x, -0.03f, 0.25f), new Vector3(x * 0.95f, -0.13f, 0.26f), 0.02f, TeethTone);
            Skin(head, monster, "Head", skull);
            foreach (float side in Sides)
            {
                Glowing(rig, CreateModelPart(head, "Eye", PrimitiveType.Sphere, new Vector3(side * 0.08f, 0.1f, 0.2f),
                    new Vector3(0.06f, 0.035f, 0.035f), Quaternion.Euler(0f, 0f, side * -15f), glow));
            }

            Glowing(rig, CreateModelPart(head, "Furnace", PrimitiveType.Sphere, new Vector3(0f, -0.05f, 0.17f), new Vector3(0.18f, 0.08f, 0.14f),
                Quaternion.identity, furnace));

            Transform jaw = Pivot(head, "Jaw", new Vector3(0f, -0.07f, 0.06f), new Vector3(12f, 0f, 0f));
            rig.Jaw = jaw;
            var mouth = new MeshKit(56);
            mouth.Blob(new Vector3(0f, -0.06f, 0.14f), new Vector3(0.18f, 0.06f, 0.16f), CharcoalTone);
            foreach (float side in Sides)
                mouth.Spike(new Vector3(side * 0.12f, -0.02f, 0.26f), new Vector3(side * 0.15f, 0.12f, 0.29f), 0.028f, TeethTone);
            foreach (float x in new[] { -0.06f, -0.02f, 0.02f, 0.06f })
                mouth.Spike(new Vector3(x, -0.02f, 0.27f), new Vector3(x, 0.06f, 0.28f), 0.016f, TeethTone);
            Skin(jaw, monster, "Jaw", mouth);

            foreach (float side in Sides)
            {
                Transform arm = Pivot(torso, SideName(side, "Arm"), new Vector3(side * 0.72f, 0.88f, 0.08f), new Vector3(-12f, 0f, side * 3f));
                var kit = new MeshKit(side < 0f ? 57 : 58);
                kit.Blob(new Vector3(0f, -0.26f, 0f), new Vector3(0.22f, 0.32f, 0.22f), RedFlesh);
                kit.Spike(new Vector3(0f, -0.52f, -0.12f), new Vector3(0f, -0.46f, -0.34f), 0.05f, BoneTone);
                kit.Limb(new Vector3(0f, -0.52f, 0.02f), new Vector3(0f, -0.9f, 0.1f), 0.19f, 0.16f, CharcoalTone);
                foreach (Vector3 outward in new[] { Vector3.right, Vector3.left, Vector3.back })
                {
                    Vector3 root = new Vector3(0f, -0.72f, 0.06f) + outward * 0.17f;
                    kit.Spike(root, root + outward * 0.1f + Vector3.up * 0.04f, 0.04f, BoneTone);
                }

                kit.Blob(new Vector3(0f, -0.98f, 0.14f), new Vector3(0.16f, 0.1f, 0.18f), RedFlesh);
                foreach (float x in new[] { -0.09f, -0.03f, 0.03f, 0.09f })
                {
                    kit.Hook(new Vector3(x, -1f, 0.24f), new Vector3(x * 1.2f, -1.12f, 0.36f), new Vector3(x * 1.3f, -1.26f, 0.36f),
                        0.035f, ClawTone);
                }

                Skin(arm, monster, arm.name, kit);
                if (side < 0f)
                {
                    rig.LeftArm = arm;
                    continue;
                }

                rig.RightArm = arm;
                rig.Muzzle = Pivot(arm, "Muzzle", new Vector3(0f, -1.05f, 0.22f));
                rig.ChargeGlow = CreateModelPart(arm, "Charge", PrimitiveType.Sphere, new Vector3(0f, -1.05f, 0.24f), Vector3.one * 0.34f,
                    Quaternion.identity, fireball).transform;
            }
        }
    }
}
