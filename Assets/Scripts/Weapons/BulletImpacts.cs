using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wolfenstein.Weapons
{
    /// <summary>
    /// Leaves a bullet mark where a shot strikes scenery. Marks come from a fixed-size pool, so once it is full
    /// the oldest mark is reused. Each mark follows the surface it is on, so marks on a sliding door move with it.
    /// Shots that hit something damageable leave no mark.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BulletImpacts : MonoBehaviour
    {
        private struct Mark
        {
            public Transform Transform;
            public Transform Surface;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public float ExpiryTime;
        }

        [SerializeField]
        private WeaponController weapon;

        [SerializeField, Tooltip("A flat mark facing along -Z, with no collider.")]
        private GameObject markPrefab;

        [SerializeField, Min(1)]
        private int maxMarks = 32;

        [SerializeField, Min(0f), Tooltip("Seconds a mark stays before it disappears.")]
        private float markLifetime = 20f;

        [SerializeField, Min(0f), Tooltip("Gap between a mark and its surface, so the two do not flicker.")]
        private float surfaceOffset = 0.01f;

        private Mark[] marks;
        private int nextMark;
        private Transform markRoot;

        public IEnumerable<Transform> ActiveMarks
        {
            get
            {
                foreach (Mark mark in marks)
                {
                    if (mark.Transform != null && mark.Transform.gameObject.activeSelf)
                        yield return mark.Transform;
                }
            }
        }

        private void Awake()
        {
            marks = new Mark[maxMarks];

            // Kept outside the player so marks stay where they were made, but in the player's scene.
            markRoot = new GameObject("Bullet Impacts").transform;
            SceneManager.MoveGameObjectToScene(markRoot.gameObject, gameObject.scene);
        }

        private void OnEnable() => weapon.Fired += HandleFired;

        private void OnDisable() => weapon.Fired -= HandleFired;

        private void OnDestroy()
        {
            if (markRoot != null)
                Destroy(markRoot.gameObject);
        }

        private void LateUpdate()
        {
            for (int i = 0; i < marks.Length; i++)
            {
                ref Mark mark = ref marks[i];
                if (mark.Transform == null || !mark.Transform.gameObject.activeSelf)
                    continue;

                if (mark.Surface == null || Time.time >= mark.ExpiryTime)
                    mark.Transform.gameObject.SetActive(false);
                else
                    FollowSurface(mark);
            }
        }

        private void HandleFired(ShotResult shot)
        {
            if (!shot.HasHit || shot.Target != null)
                return;

            RaycastHit hit = shot.Hit;
            ref Mark mark = ref marks[nextMark];
            nextMark = (nextMark + 1) % marks.Length;

            if (mark.Transform == null)
                mark.Transform = Instantiate(markPrefab, markRoot).transform;

            // Turn the mark's back to the surface, with a random roll so neighbouring marks differ.
            Quaternion rotation = Quaternion.AngleAxis(Random.Range(0f, 360f), hit.normal) *
                                  Quaternion.FromToRotation(Vector3.forward, -hit.normal);

            // Stored relative to the surface, so the mark can be re-placed however the surface moves.
            Transform surface = hit.collider.transform;
            mark.Surface = surface;
            mark.LocalPosition = surface.InverseTransformPoint(hit.point + hit.normal * surfaceOffset);
            mark.LocalRotation = Quaternion.Inverse(surface.rotation) * rotation;
            mark.ExpiryTime = Time.time + markLifetime;

            FollowSurface(mark);
            mark.Transform.gameObject.SetActive(true);
        }

        private static void FollowSurface(in Mark mark) =>
            mark.Transform.SetPositionAndRotation(
                mark.Surface.TransformPoint(mark.LocalPosition),
                mark.Surface.rotation * mark.LocalRotation);
    }
}
