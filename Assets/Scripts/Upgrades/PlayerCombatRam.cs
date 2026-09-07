using UnityEngine;

/// <summary>
/// Ramming upgrade: damages enemies on contact based on max health and grants collision immunity.
/// </summary>
[DisallowMultipleComponent]
public class PlayerCombatRam : MonoBehaviour
{
    [SerializeField] PlayerHealth playerHealth;
    [Range(0.01f, 1f)] [SerializeField] float healthDamageFraction = 0.1f;

    void Awake()
    {
        if (playerHealth == null)
        {
            TryGetComponent(out playerHealth);
        }
    }

    void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.IgnoresEnemyCollisionDamage = true;
        }
    }

    void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.IgnoresEnemyCollisionDamage = false;
        }
    }

    public void Configure(PlayerHealth health, float damageFraction)
    {
        playerHealth = health;
        healthDamageFraction = Mathf.Clamp(damageFraction, 0.01f, 1f);

        if (isActiveAndEnabled && playerHealth != null)
        {
            playerHealth.IgnoresEnemyCollisionDamage = true;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (playerHealth == null || !playerHealth.IsAlive)
        {
            return;
        }

        if (!other.TryGetComponent(out EnemyHealth enemyHealth) || !enemyHealth.IsAlive)
        {
            return;
        }

        int damage = Mathf.Max(1, Mathf.FloorToInt(playerHealth.MaxHealth * healthDamageFraction));
        enemyHealth.TakeDamage(damage);
    }
}
