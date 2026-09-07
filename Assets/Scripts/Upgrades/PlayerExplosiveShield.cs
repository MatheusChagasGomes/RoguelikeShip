using UnityEngine;

/// <summary>
/// When the force shield absorbs a hit, detonates for area damage.
/// </summary>
[DisallowMultipleComponent]
public class PlayerExplosiveShield : MonoBehaviour
{
    [SerializeField] PlayerForceShield forceShield;
    [SerializeField] [Min(0f)] float explosionRadius = 1.5f;
    [SerializeField] [Min(0)] int explosionDamage = 2;
    [SerializeField] Color explosionColor = new Color(0.45f, 0.75f, 1f, 0.5f);

    void Awake()
    {
        if (forceShield == null)
        {
            TryGetComponent(out forceShield);
        }
    }

    void OnEnable()
    {
        if (forceShield != null)
        {
            forceShield.Absorbed += HandleShieldAbsorbed;
        }
    }

    void OnDisable()
    {
        if (forceShield != null)
        {
            forceShield.Absorbed -= HandleShieldAbsorbed;
        }
    }

    public void Configure(float radius, int damage)
    {
        explosionRadius = Mathf.Max(0f, radius);
        explosionDamage = Mathf.Max(0, damage);
    }

    void HandleShieldAbsorbed(Vector3 position)
    {
        AreaDamage.Apply(position, explosionRadius, explosionDamage, explosionColor);
    }
}
