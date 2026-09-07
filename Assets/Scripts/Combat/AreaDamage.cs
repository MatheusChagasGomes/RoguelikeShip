using UnityEngine;

/// <summary>Shared helper for circular area-of-effect damage against enemies.</summary>
public static class AreaDamage
{
    public static void Apply(
        Vector3 center,
        float radius,
        int damage,
        Color? pulseColor = null,
        EnemyHealth exclude = null)
    {
        if (radius <= 0f || damage <= 0)
        {
            return;
        }

        ExplosionPulse.Spawn(center, radius, pulseColor);

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        for (int i = 0; i < hits.Length; i++)
        {
            if (!hits[i].TryGetComponent(out EnemyHealth enemyHealth))
            {
                continue;
            }

            if (enemyHealth == exclude)
            {
                continue;
            }

            enemyHealth.TakeDamage(damage);
        }
    }
}
