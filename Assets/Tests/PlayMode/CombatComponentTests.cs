using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wolfenstein.Combat;
using Wolfenstein.Weapons;

namespace Wolfenstein.Tests
{
    /// <summary>
    /// Phase 3 building blocks on their own: the rules of Health and AmmoSupply, without a level.
    /// </summary>
    public sealed class CombatComponentTests
    {
        private GameObject owner;

        [SetUp]
        public void CreateOwner() => owner = new GameObject("Combat Test Object");

        [TearDown]
        public void DestroyOwner() => Object.Destroy(owner);

        [Test]
        public void Health_StartsFull()
        {
            var health = owner.AddComponent<Health>();

            Assert.AreEqual(health.Max, health.Current);
            Assert.IsFalse(health.IsDead);
        }

        [Test]
        public void Health_TakeDamage_LowersHealthAndReportsIt()
        {
            var health = owner.AddComponent<Health>();
            var changes = new List<int>();
            DamageInfo? received = null;
            health.Changed += changes.Add;
            health.Damaged += damage => received = damage;

            health.TakeDamage(Damage(30));

            Assert.AreEqual(health.Max - 30, health.Current);
            CollectionAssert.AreEqual(new[] { health.Max - 30 }, changes);
            Assert.AreEqual(30, received?.Amount);
        }

        [Test]
        public void Health_LethalDamage_StopsAtZeroAndDiesOnce()
        {
            var health = owner.AddComponent<Health>();
            int deaths = 0;
            health.Died += () => deaths++;

            health.TakeDamage(Damage(health.Max * 3));
            health.TakeDamage(Damage(10));

            Assert.AreEqual(0, health.Current);
            Assert.IsTrue(health.IsDead);
            Assert.AreEqual(1, deaths);
        }

        [Test]
        public void Health_IgnoresNonPositiveDamage()
        {
            var health = owner.AddComponent<Health>();
            int changes = 0;
            health.Changed += _ => changes++;

            health.TakeDamage(Damage(0));
            health.TakeDamage(Damage(-20));

            Assert.AreEqual(health.Max, health.Current);
            Assert.AreEqual(0, changes);
        }

        [Test]
        public void Health_Heal_StopsAtMaxAndCannotRevive()
        {
            var health = owner.AddComponent<Health>();
            health.TakeDamage(Damage(30));

            Assert.AreEqual(30, health.Heal(50), "Healing should only restore what was lost.");
            Assert.AreEqual(health.Max, health.Current);

            health.TakeDamage(Damage(health.Max));
            Assert.AreEqual(0, health.Heal(50));
            Assert.IsTrue(health.IsDead);
        }

        [Test]
        public void Ammo_TryConsume_TakesRoundsOnlyWhenAllAreAvailable()
        {
            var ammo = owner.AddComponent<AmmoSupply>();
            int start = ammo.Current;
            Assume.That(start, Is.GreaterThan(0));

            Assert.IsTrue(ammo.TryConsume(1));
            Assert.AreEqual(start - 1, ammo.Current);

            Assert.IsFalse(ammo.TryConsume(ammo.Current + 1));
            Assert.AreEqual(start - 1, ammo.Current, "A failed attempt should not take any rounds.");

            Assert.IsTrue(ammo.TryConsume(ammo.Current));
            Assert.AreEqual(0, ammo.Current);
            Assert.IsFalse(ammo.TryConsume(1));
        }

        [Test]
        public void Ammo_Add_StopsAtMax()
        {
            var ammo = owner.AddComponent<AmmoSupply>();
            var changes = new List<int>();
            ammo.Changed += changes.Add;
            int room = ammo.Max - ammo.Current;

            Assert.AreEqual(room, ammo.Add(ammo.Max));
            Assert.AreEqual(ammo.Max, ammo.Current);
            Assert.AreEqual(0, ammo.Add(5), "A full supply should take no more rounds.");
            Assert.AreEqual(room > 0 ? 1 : 0, changes.Count);
        }

        private static DamageInfo Damage(int amount) => new DamageInfo(amount, Vector3.zero, Vector3.forward, null);
    }
}
