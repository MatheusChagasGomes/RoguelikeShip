using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks an enemy's hit points. Max and starting health are set in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public class EnemyHealth : MonoBehaviour
{
    static readonly List<EnemyHealth> ActiveEnemies = new();

    [Header("Health")]
    [SerializeField] [Min(1)] int maxHealth = 3;
    [Tooltip("If true, Current Health starts at Max Health on Awake.")]
    [SerializeField] bool fillOnAwake = true;
    [SerializeField] [Min(0)] int currentHealth = 3;
    [SerializeField] bool destroyOnDeath = true;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsAlive => currentHealth > 0;
    public bool IsFull => currentHealth >= maxHealth;

    /// <summary>Living/enabled enemies currently in the scene. Used by targeting (e.g. drones).</summary>
    public static IReadOnlyList<EnemyHealth> Active => ActiveEnemies;

    /// <summary>Invoked after health changes. Args: current, max.</summary>
    public event Action<int, int> HealthChanged;

    /// <summary>Invoked once when health reaches zero.</summary>
    public event Action Died;

    void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);

        if (fillOnAwake)
        {
            currentHealth = maxHealth;
        }
        else
        {
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        }
    }

    void OnEnable()
    {
        ActiveEnemies.Add(this);
    }

    void OnDisable()
    {
        ActiveEnemies.Remove(this);
    }

    /// <summary>Applies damage. Returns true if the hit was applied.</summary>
    public bool TakeDamage(int amount)
    {
        if (!IsAlive || amount <= 0)
        {
            return false;
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
        HealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Died?.Invoke();

            if (destroyOnDeath)
            {
                Destroy(gameObject);
            }
        }

        return true;
    }

    /// <summary>Sets max health and optionally refills to the new maximum.</summary>
    public void SetMaxHealth(int value, bool refill = false)
    {
        maxHealth = Mathf.Max(1, value);
        currentHealth = refill ? maxHealth : Mathf.Clamp(currentHealth, 0, maxHealth);
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
