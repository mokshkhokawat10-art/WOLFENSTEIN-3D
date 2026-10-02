namespace Wolfenstein.Combat
{
    /// <summary>
    /// Something weapons can hurt. Put it on the object that owns the collider a shot hits, or on one of its parents.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(DamageInfo damage);
    }
}
