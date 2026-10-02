using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wolfenstein.Audio;
using Wolfenstein.Combat;
using Wolfenstein.Monsters;
using Wolfenstein.Pickups;
using Wolfenstein.Waves;
using Wolfenstein.Weapons;
using Wolfenstein.World;

namespace Wolfenstein.Tests
{
    /// <summary>
    /// Phase 7 audio: the pooled audio manager and its volumes, and which sounds the game asks for as things happen, from
    /// the pistol, the player's footsteps and the items they collect to the monsters, the doors, the waves, the music and
    /// the ambience. Only the logic is tested, never how a clip sounds: sounds are checked by the cues requested from the
    /// audio manager and by how those cues are set up.
    /// </summary>
    public sealed class AudioTests : LevelTestFixture
    {
        private readonly List<(SoundCue cue, Vector3 position)> requested = new List<(SoundCue cue, Vector3 position)>();
        private AudioManager audio;
        private MonsterManager monsters;
        private WaveManager waves;

        [UnitySetUp]
        public IEnumerator FindAudio()
        {
            audio = Object.FindFirstObjectByType<AudioManager>();
            monsters = Object.FindFirstObjectByType<MonsterManager>();
            waves = Object.FindFirstObjectByType<WaveManager>();
            requested.Clear();
            audio.CueRequested += (cue, position) => requested.Add((cue, position));
            yield break;
        }

        [Test]
        public void AudioManager_PlaysEverythingThroughAFixedPool_WithCentralVolumes()
        {
            Assert.IsNotNull(audio, "The level has no audio manager.");
            Assert.That(audio.VoiceCount, Is.InRange(16, 32));
            foreach (AudioCategory category in System.Enum.GetValues(typeof(AudioCategory)))
                Assert.That(audio.Gain(category), Is.GreaterThan(0f).And.LessThanOrEqualTo(1f), category.ToString());

            // Effect voices, two music decks and three ambience beds: within the engine's real voices, and nothing more.
            AudioSource[] sources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            Assert.AreEqual(audio.VoiceCount + 5, sources.Length, "Audio sources should only belong to the audio system.");
            Assert.LessOrEqual(sources.Length, 32);
            Assert.IsTrue(monsters.Alive.All(monster => monster.GetComponentInChildren<AudioSource>(true) == null),
                "Monsters should play through the pooled voices, not sources of their own.");
        }

        [UnityTest]
        public IEnumerator CategoryVolumes_ScaleTheirOwnSoundsOnly()
        {
            SoundCue effect = Player.Weapon.Weapon.FireSound;
            SoundCue interfaceSound = Object.FindFirstObjectByType<WaveAudio>().WarningFor(WaveIntensity.Normal);
            SoundHandle effectHandle = audio.Play(effect);
            SoundHandle interfaceHandle = audio.Play(interfaceSound);
            yield return null;

            Assert.IsTrue(effectHandle.IsValid && interfaceHandle.IsValid, "Both sounds should have been given a voice.");
            Assert.Greater(audio.VolumeOf(effectHandle), 0f);
            Assert.Greater(audio.VolumeOf(interfaceHandle), 0f);

            audio.SetVolume(AudioCategory.Sfx, 0f);
            yield return null;
            Assert.AreEqual(0f, audio.VolumeOf(effectHandle), 0.0001f, "The effects volume did not silence an effect.");
            Assert.Greater(audio.VolumeOf(interfaceHandle), 0f, "The effects volume should not touch interface sounds.");

            audio.MasterVolume = 0f;
            yield return null;
            Assert.AreEqual(0f, audio.VolumeOf(interfaceHandle), 0.0001f, "The master volume did not silence everything.");
        }

        [UnityTest]
        public IEnumerator Pistol_PlaysItsShotAndStrike_AndClicksWhenEmpty()
        {
            WeaponDefinition pistol = Player.Weapon.Weapon;
            AssertHasSound(pistol.FireSound, "pistol shot");
            AssertHasSound(pistol.MechanismSound, "pistol mechanism");
            AssertHasSound(pistol.DryFireSound, "dry fire");

            var shots = new List<ShotResult>();
            Player.Weapon.Fired += shots.Add;
            yield return PullTrigger();

            Assert.AreEqual(1, shots.Count, "The pistol did not fire.");
            Assert.IsTrue(WasRequested(pistol.FireSound));
            Assert.IsTrue(WasRequested(pistol.MechanismSound));
            if (shots[0].HasHit)
            {
                SoundCue impact = Object.FindFirstObjectByType<PlayerAudio>().ImpactFor(shots[0]);
                AssertHasSound(impact, "bullet impact");
                Assert.IsTrue(WasRequested(impact, shots[0].Hit.point), "The shot's impact was not heard where it struck.");
            }

            Player.Weapon.Ammo.TryConsume(Player.Weapon.Ammo.Current);
            yield return new WaitForSeconds(pistol.FireInterval + 0.05f);
            requested.Clear();
            yield return PullTrigger();

            Assert.IsTrue(WasRequested(pistol.DryFireSound), "An empty pistol should click.");
            Assert.IsFalse(WasRequested(pistol.FireSound), "An empty pistol should not sound a shot.");
        }

        [UnityTest]
        public IEnumerator Footsteps_FollowTheFloorUnderfoot()
        {
            var playerAudio = Object.FindFirstObjectByType<PlayerAudio>();
            Assert.AreEqual(SurfaceKind.Carpet, playerAudio.FloorAt(World.CellToWorld(CellInZone('c'))), "The command rooms are carpeted.");
            Assert.AreEqual(SurfaceKind.Metal, playerAudio.FloorAt(World.CellToWorld(CellInZone('i'))), "The industrial halls have steel floors.");
            Assert.AreEqual(SurfaceKind.Grate, playerAudio.FloorAt(World.CellToWorld(CellInZone('p'))), "The pens have grating underfoot.");
            Assert.AreEqual(SurfaceKind.Metal, playerAudio.FloorAt(SpawnRoomDoor().transform.position), "A doorway is crossed on its track.");

            SurfaceKind[] surfaces = (SurfaceKind[])System.Enum.GetValues(typeof(SurfaceKind));
            List<SoundCue> sounds = surfaces.Select(playerAudio.FootstepOn).ToList();
            Assert.IsTrue(sounds.All(sound => sound != null && sound.HasClips), "Every kind of floor needs footsteps.");
            Assert.AreEqual(surfaces.Length, sounds.Distinct().Count(), "Each kind of floor should sound its own.");

            SoundCue expected = playerAudio.FootstepOn(playerAudio.SurfaceUnderfoot);
            yield return HoldKey(Keyboard.wKey, 1f);

            int steps = requested.Count(request => request.cue == expected);
            Assert.GreaterOrEqual(steps, 2, "Walking for a second should be heard as footsteps on the floor underfoot.");
            Assert.IsFalse(requested.Any(request => sounds.Contains(request.cue) && request.cue != expected),
                "Footsteps were heard on the wrong kind of floor.");
        }

        [UnityTest]
        public IEnumerator Items_PlayTheSoundOfWhatTheyGive()
        {
            var pickups = Object.FindFirstObjectByType<PickupManager>();
            Pickup[] items = Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None);
            Pickup health = items.First(item => item.Definition.Health > 0);
            Pickup ammo = items.First(item => item.Definition.Ammo > 0);
            Pickup treasure = items.First(item => item.Definition.Score > 0);
            var sounds = new[] { health.Definition.CollectSound, ammo.Definition.CollectSound, treasure.Definition.CollectSound };
            foreach (SoundCue sound in sounds)
                AssertHasSound(sound, "item");
            Assert.AreEqual(3, sounds.Distinct().Count(), "Health, ammo and treasure should each sound different.");

            // Each item is only taken when it is of use.
            PlayerHealth.TakeDamage(new DamageInfo(30, Player.transform.position, Vector3.zero, null));
            Player.Weapon.Ammo.TryConsume(10);
            yield return null;
            requested.Clear();

            foreach (Pickup item in new[] { health, ammo, treasure })
            {
                pickups.Spawn(item, Player.transform.position);
                Physics.SyncTransforms();
                yield return null;
                yield return null;
                Assert.IsTrue(WasRequested(item.Definition.CollectSound), $"Collecting {item.Definition.name} made no sound.");
            }
        }

        [Test]
        public void EveryMonster_HasAVoiceOfItsOwn_ThatFadesWithDistance()
        {
            List<MonsterDefinition> kinds = MonsterKinds();
            Assert.AreEqual(5, kinds.Count);
            foreach (MonsterDefinition kind in kinds)
            {
                CreatureSounds sounds = kind.Sounds;
                Assert.IsNotNull(sounds, $"{kind.DisplayName} has no sounds.");
                foreach (SoundCue cue in new[] { sounds.Hunt, sounds.Alert, sounds.Pain, sounds.Death })
                {
                    AssertHasSound(cue, kind.DisplayName);
                    Assert.IsTrue(cue.IsSpatial, $"{cue.name} should come from the monster.");
                    Assert.Less(cue.MinDistance, cue.MaxDistance);
                    Assert.LessOrEqual(cue.MaxDistance, 60f, $"{cue.name} carries too far.");
                    Assert.AreEqual(1f, cue.Rolloff.Evaluate(cue.MinDistance / cue.MaxDistance * 0.5f), 0.001f, "Full volume up close.");
                    Assert.Less(cue.Rolloff.Evaluate(0.6f), cue.Rolloff.Evaluate(0.3f), "It should grow quieter with distance.");
                    Assert.AreEqual(0f, cue.Rolloff.Evaluate(1f), 0.001f, $"{cue.name} should be silent at its furthest.");
                }

                if (kind.HasRanged)
                    AssertHasSound(sounds.RangedLaunch, $"{kind.DisplayName} launch");
                if (kind.HasMelee)
                    AssertHasSound(sounds.MeleeStrike, $"{kind.DisplayName} strike");
            }

            Assert.AreEqual(kinds.Count, kinds.Select(kind => kind.Sounds).Distinct().Count());
            Assert.AreEqual(kinds.Count, kinds.Select(kind => kind.Sounds.Hunt).Distinct().Count(), "No two monsters should growl alike.");
            Assert.AreEqual(kinds.Count, kinds.Select(kind => kind.Sounds.Death).Distinct().Count());

            MonsterDefinition spitter = kinds.Single(kind => kind.DisplayName == "SPITTER");
            MonsterDefinition brute = kinds.Single(kind => kind.DisplayName == "BRUTE");
            MonsterDefinition stalker = kinds.Single(kind => kind.DisplayName == "STALKER");
            Assert.IsNull(spitter.Sounds.Footstep, "The spitter floats, so it has no footsteps.");
            AssertHasSound(brute.Sounds.Slam, "brute slam");
            Assert.Greater(brute.Sounds.Footstep.MaxDistance, stalker.Sounds.Footstep.MaxDistance, "A brute's steps should carry further.");
        }

        [UnityTest]
        public IEnumerator Monster_IsHeardHurtAndDying_WhereItIs()
        {
            MonsterDefinition brute = MonsterKinds().Single(kind => kind.DisplayName == "BRUTE");
            Vector3 position = Player.transform.position + Player.transform.forward * 6f;
            MonsterController monster = monsters.Spawn(brute, position, 180f, MonsterModifiers.None);
            Assert.IsNotNull(monster);
            yield return WaitFor(() => monster.State == MonsterState.Chase, brute.SpawnDuration + 1f);

            requested.Clear();
            monster.Health.TakeDamage(new DamageInfo(1, monster.transform.position, Vector3.forward, Player.gameObject));
            Assert.IsTrue(WasRequested(brute.Sounds.Pain), "A hurt monster should cry out.");

            Vector3 deathPlace = monster.transform.position;
            monster.Health.TakeDamage(new DamageInfo(100000, deathPlace, Vector3.forward, Player.gameObject));
            Assert.IsTrue(WasRequested(brute.Sounds.Death, deathPlace), "Its death should be heard where it fell.");
            Assert.IsFalse(requested.Count(request => request.cue == brute.Sounds.Pain) > 1, "The killing blow is its death, not more pain.");
        }

        [Test]
        public void WaveIntensity_GrowsFromOrdinaryToMajorToEliteToFinal()
        {
            var waveAudio = Object.FindFirstObjectByType<WaveAudio>();
            IReadOnlyList<Wave> levelWaves = waves.Waves.Waves;
            var intensities = levelWaves.Select((wave, i) => WaveAudio.Classify(wave, i == levelWaves.Count - 1, waveAudio.MajorWaveSize)).ToList();

            CollectionAssert.AreEqual(
                new[] { WaveIntensity.Normal, WaveIntensity.Normal, WaveIntensity.Major, WaveIntensity.Major, WaveIntensity.Elite, WaveIntensity.Final },
                intensities);

            var warnings = new[] { WaveIntensity.Normal, WaveIntensity.Major, WaveIntensity.Elite, WaveIntensity.Final }.Select(waveAudio.WarningFor).ToList();
            foreach (SoundCue warning in warnings)
                AssertHasSound(warning, "wave warning");
            Assert.AreEqual(4, warnings.Distinct().Count(), "Each size of wave should have its own warning.");
        }

        [UnityTest]
        public IEnumerator Waves_AreAnnounced_Started_Beaten_AndCleared_WithSound()
        {
            var waveAudio = Object.FindFirstObjectByType<WaveAudio>();
            MonsterDefinition stalker = MonsterKinds().Single(kind => kind.DisplayName == "STALKER");
            PlacePlayer(ArenaCentre(), 0f);
            waves.Begin(WaveSet.Create(0.2f, 0.3f,
                new Wave(new[] { new WaveGroup(stalker, 1) }, MonsterModifiers.None, 0.1f, 1),
                new Wave(new[] { new WaveGroup(stalker, 1) }, MonsterModifiers.None, 0.1f, 1)));

            Assert.IsTrue(WasRequested(waveAudio.WarningFor(WaveIntensity.Normal)), "The first wave was not announced with a warning.");
            yield return WaitFor(() => waves.State == WaveState.Fighting, 2f);
            Assert.IsTrue(WasRequested(waveAudio.Incoming), "The wave's start was not marked.");

            yield return KillWaveUntil(() => waves.State == WaveState.Complete, 4f);
            Assert.IsTrue(WasRequested(waveAudio.WaveComplete), "Beating a wave made no sound.");

            yield return WaitFor(() => waves.WaveNumber == 2, 2f);
            Assert.IsTrue(WasRequested(waveAudio.WarningFor(WaveIntensity.Final)), "The final wave should have its own warning.");

            yield return KillWaveUntil(() => waves.State == WaveState.Cleared, 6f);
            Assert.IsTrue(WasRequested(waveAudio.AreaCleared), "Clearing the arena made no sound.");
        }

        [UnityTest]
        public IEnumerator Doors_SoundAsTheyMove_Lock_AndRefuse()
        {
            var doorAudio = Object.FindFirstObjectByType<DoorAudio>();
            Door door = SpawnRoomDoor();
            DoorSounds sounds = doorAudio.SoundsOf(door);
            Assert.IsNotNull(sounds);
            Assert.AreNotEqual(sounds, doorAudio.SoundsOf(waves.ArenaDoors[0]), "The arena's blast doors should sound their own.");

            Vector3 doorPlace = door.transform.position;
            door.Open();
            Assert.IsTrue(WasRequested(sounds.Open, doorPlace, 2f), "An opening door made no sound.");
            yield return WaitFor(() => door.State == DoorState.Open, 3f);
            Assert.IsTrue(WasRequested(sounds.Opened, doorPlace, 2f));

            door.Close();
            Assert.IsTrue(WasRequested(sounds.Close, doorPlace, 2f));
            yield return WaitFor(() => door.State == DoorState.Closed, 3f);
            Assert.IsTrue(WasRequested(sounds.Closed, doorPlace, 2f), "A door shutting made no sound.");

            door.Lock();
            requested.Clear();
            StandInFrontOf(door, 2f);
            yield return null;
            yield return TapKey(Keyboard.eKey);
            Assert.IsTrue(WasRequested(sounds.Locked, doorPlace, 2f), "Trying a locked door should be refused with a sound.");
        }

        [UnityTest]
        public IEnumerator SoundBehindAClosedDoor_IsMuffled_UntilTheDoorOpens()
        {
            Door door = SpawnRoomDoor();
            Assert.AreEqual(DoorState.Closed, door.State);
            SoundCue longSound = MonsterKinds().Single(kind => kind.DisplayName == "REVENANT").Sounds.Death;

            // The player starts in the room this door seals; "beyond" is the far side of it.
            Vector3 towardsPlayer = Vector3.Dot(Player.transform.position - door.transform.position, door.transform.forward) >= 0f
                ? door.transform.forward
                : -door.transform.forward;
            SoundHandle beyond = audio.Play(longSound, door.transform.position - towardsPlayer * 4f + Vector3.up);
            SoundHandle inside = audio.Play(longSound, door.transform.position + towardsPlayer * 3f + Vector3.up);
            yield return null;

            Assert.IsTrue(audio.IsMuffled(beyond), "A sound behind the closed door should be muffled.");
            Assert.IsFalse(audio.IsMuffled(inside), "A sound in the same room should be clear.");

            door.Open();
            yield return WaitFor(() => !audio.IsMuffled(beyond), 1.5f);
            Assert.IsTrue(audio.IsPlaying(beyond));
            Assert.IsFalse(audio.IsMuffled(beyond), "Opening the door should let the sound through clearly.");
        }

        // A crowd of every kind closes on the player from all round, all of it within earshot at once.
        [UnityTest]
        public IEnumerator LargeFight_StaysWithinTheVoiceBudget()
        {
            List<MonsterDefinition> kinds = MonsterKinds();
            int sourcesBefore = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length;
            waves.enabled = false;
            Vector3 centre = ArenaCentre();
            PlacePlayer(centre, 0f);
            yield return null;

            List<Vector3> places = ArenaCells().Select(World.CellToWorld)
                .Where(place => HorizontalDistance(place - centre) >= 4f)
                .OrderBy(place => HorizontalDistance(place - centre)).ToList();
            int spawned = 0;
            foreach (Vector3 place in places)
            {
                MonsterDefinition kind = kinds[spawned % kinds.Count];
                if (spawned >= 30 || !monsters.HasRoomFor(kind, place))
                    continue;
                if (monsters.Spawn(kind, place, 0f, MonsterModifiers.None) != null)
                    spawned++;
            }

            Assert.GreaterOrEqual(spawned, 25, "There was no room for a crowd round the player.");
            List<SoundCue> creatureCues = kinds.SelectMany(kind => CuesOf(kind.Sounds)).ToList();
            int mostAlive = 0, mostVoices = 0;
            float end = Time.time + 4f;
            while (Time.time < end)
            {
                PlayerHealth.Heal(PlayerHealth.Max);
                mostAlive = Mathf.Max(mostAlive, monsters.Alive.Count);
                mostVoices = Mathf.Max(mostVoices, audio.ActiveVoiceCount);
                foreach (SoundCue cue in creatureCues)
                    Assert.LessOrEqual(audio.CountPlaying(cue), cue.MaxVoices, $"{cue.name} played more copies than it allows.");
                yield return null;
            }

            Assert.GreaterOrEqual(mostAlive, 20, "The fight never grew large.");
            Assert.LessOrEqual(mostVoices, audio.VoiceCount);
            Assert.GreaterOrEqual(requested.Count(request => creatureCues.Contains(request.cue)), 40, "A big fight should be heard.");
            Assert.GreaterOrEqual(requested.Select(request => request.cue).Where(creatureCues.Contains).Distinct().Count(), 6,
                "A big fight should be heard in many different creature sounds.");
            Assert.AreEqual(sourcesBefore, Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length,
                "A big fight should not create any audio sources.");
        }

        [UnityTest]
        public IEnumerator Music_FollowsTheFight_FromExplorationToVictory()
        {
            var music = Object.FindFirstObjectByType<MusicDirector>();
            yield return null;
            Assert.AreEqual(MusicState.Exploration, music.State);
            Assert.AreEqual(music.Music.Get(MusicState.Exploration).Clip, music.CurrentClip);

            MonsterDefinition stalker = MonsterKinds().Single(kind => kind.DisplayName == "STALKER");
            PlacePlayer(ArenaCentre(), 0f);
            waves.Begin(WaveSet.Create(0.2f, 0.3f,
                new Wave(new[] { new WaveGroup(stalker, 1) }, MonsterModifiers.None, 0.1f, 1),
                new Wave(new[] { new WaveGroup(stalker, 1) }, MonsterModifiers.None, 0.1f, 1)));
            yield return null;
            Assert.AreEqual(MusicState.Combat, music.State, "An ordinary wave should bring on the combat music.");

            yield return KillWaveUntil(() => waves.WaveNumber == 2, 5f);
            yield return null;
            Assert.AreEqual(MusicState.Intense, music.State, "The final wave should drive the music harder.");

            yield return KillWaveUntil(() => waves.State == WaveState.Cleared, 6f);
            yield return null;
            Assert.AreEqual(MusicState.Victory, music.State);
            Assert.AreEqual(music.Music.Get(MusicState.Victory).Clip, music.CurrentClip);
        }

        [UnityTest]
        public IEnumerator Ambience_FollowsTheZone_AndTensesForTheFight()
        {
            var ambience = Object.FindFirstObjectByType<AmbienceDirector>();
            yield return new WaitForSeconds(0.3f);
            ZoneTheme spawnTheme = World.GetZoneTheme(World.WorldToCell(Player.transform.position));
            Assert.IsNotNull(spawnTheme.Ambience);
            Assert.AreEqual(spawnTheme.Ambience, ambience.CurrentBed);
            Assert.IsFalse(ambience.IsTensionPlaying);

            Vector2Int hall = CellInZone('i');
            PlacePlayer(World.CellToWorld(hall), 0f);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(World.GetZoneTheme(hall).Ambience, ambience.CurrentBed, "The bed should change with the zone.");
            Assert.AreNotEqual(spawnTheme.Ambience, ambience.CurrentBed);

            MonsterDefinition stalker = MonsterKinds().Single(kind => kind.DisplayName == "STALKER");
            PlacePlayer(ArenaCentre(), 0f);
            waves.Begin(WaveSet.Create(1f, 1f, new Wave(new[] { new WaveGroup(stalker, 1) }, MonsterModifiers.None, 0.1f, 1)));
            yield return null;
            Assert.IsTrue(ambience.IsTensionPlaying, "The fight should bring in the tension layer.");
        }

        // ---- Helpers -------------------------------------------------------------------------------------------------------

        private bool WasRequested(SoundCue cue) => cue != null && requested.Any(request => request.cue == cue);

        private bool WasRequested(SoundCue cue, Vector3 position, float tolerance = 0.5f) =>
            cue != null && requested.Any(request => request.cue == cue && HorizontalDistance(request.position - position) <= tolerance);

        private static void AssertHasSound(SoundCue cue, string what)
        {
            Assert.IsNotNull(cue, $"There is no {what} sound.");
            Assert.IsTrue(cue.HasClips, $"The {what} sound has no clips.");
        }

        private static IEnumerable<SoundCue> CuesOf(CreatureSounds sounds) =>
            new[]
            {
                sounds.Idle, sounds.Hunt, sounds.Alert, sounds.Pain, sounds.Death, sounds.MeleeWindup, sounds.MeleeStrike, sounds.Slam,
                sounds.RangedWindup, sounds.RangedLaunch, sounds.ProjectileImpact, sounds.Charge, sounds.Footstep
            }.Where(cue => cue != null);

        private List<MonsterDefinition> MonsterKinds() =>
            waves.Waves.Waves.SelectMany(wave => wave.Groups).Select(group => group.Monster).Distinct().ToList();

        private IEnumerator KillWaveUntil(System.Func<bool> condition, float timeout)
        {
            float deadline = Time.time + timeout;
            while (!condition() && Time.time < deadline)
            {
                foreach (MonsterController monster in waves.WaveMonsters.ToList())
                    monster.Health.TakeDamage(new DamageInfo(100000, monster.transform.position, Vector3.forward, Player.gameObject));
                PlayerHealth.Heal(PlayerHealth.Max);
                yield return null;
            }
        }

        // An open floor cell of a zone, away from its doors.
        private Vector2Int CellInZone(char zone)
        {
            LevelLayout layout = World.Layout;
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (layout.GetZone(cell) == zone && layout.IsWalkable(cell) && !layout.TryGetDoor(cell, out _))
                        return cell;
                }
            }

            Assert.Fail($"The level has no open floor in zone '{zone}'.");
            return default;
        }

        // The open floor nearest the middle of the arena.
        private Vector3 ArenaCentre()
        {
            List<Vector2Int> cells = ArenaCells();
            Vector2 middle = cells.Aggregate(Vector2.zero, (sum, cell) => sum + (Vector2)cell) / cells.Count;
            return World.CellToWorld(cells.OrderBy(cell => (cell - middle).sqrMagnitude).First());
        }

        private List<Vector2Int> ArenaCells()
        {
            LevelLayout layout = World.Layout;
            var cells = new List<Vector2Int>();
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (layout.GetZone(cell) == waves.ArenaZone && layout.IsWalkable(cell) && !layout.TryGetDoor(cell, out _))
                        cells.Add(cell);
                }
            }

            return cells;
        }
    }
}
