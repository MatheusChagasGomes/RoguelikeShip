using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Tracks the player's hit points. After taking damage, grants brief invulnerability
/// with a rapid blink on the sprite for the same duration.
/// Optional PlayerForceShield absorbs hits before HP is reduced.
/// </summary>
[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] [Min(1)] int maxHealth = 3;
    [Tooltip("If true, Current Health starts at Max Health on Awake.")]
    [SerializeField] bool fillOnAwake = true;
    [SerializeField] [Min(0)] int currentHealth = 3;

    [Header("Invulnerability")]
    [Tooltip("Seconds of invulnerability after taking damage.")]
    [SerializeField] [Min(0f)] float invulnerabilityDuration = 1f;
    [Tooltip("Half-period of the blink while invulnerable (seconds). Smaller = faster blink.")]
    [SerializeField] [Min(0.01f)] float blinkInterval = 0.08f;
    [SerializeField] SpriteRenderer spriteRenderer;

    [Header("Defense")]
    [SerializeField] PlayerForceShield forceShield;

    Coroutine _invulnerabilityRoutine;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsAlive => currentHealth > 0;
    public bool IsFull => currentHealth >= maxHealth;
    public bool IsInvulnerable => _invulnerabilityRoutine != null;
    public PlayerForceShield ForceShield => forceShield;

    /// <summary>Missing health as 0..1 (0 = full, 1 = nearly dead / dead).</summary>
    public float MissingHealth01 => maxHealth <= 0 ? 0f : 1f - (Mathf.Clamp01((float)currentHealth / maxHealth));

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

        if (spriteRenderer == null)
        {
            TryGetComponent(out spriteRenderer);
        }

        if (forceShield == null)
        {
            TryGetComponent(out forceShield);
        }
    }

    void OnDisable()
    {
        EndInvulnerability();
    }

    /// <summary>Applies damage. Returns true if the hit was applied (including shield absorb).</summary>
    public bool TakeDamage(int amount)
    {
        if (!IsAlive || IsInvulnerable || amount <= 0)
        {
            return false;
        }

        if (forceShield != null && forceShield.TryAbsorb())
        {
            BeginInvulnerability();
            return true;
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
        HealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            EndInvulnerability();
            forceShield?.Disable();
            Died?.Invoke();
            return true;
        }

        BeginInvulnerability();
        return true;
    }

    /// <summary>Restores health up to Max Health. Returns true if any HP was gained.</summary>
    public bool Heal(int amount)
    {
        if (!IsAlive || amount <= 0 || IsFull)
        {
            return false;
        }

        int previous = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

        if (currentHealth == previous)
        {
            return false;
        }

        HealthChanged?.Invoke(currentHealth, maxHealth);
        return true;
    }

    /// <summary>Sets max health and optionally refills to the new maximum.</summary>
    public void SetMaxHealth(int value, bool refill = false)
    {
        maxHealth = Mathf.Max(1, value);
        currentHealth = refill ? maxHealth : Mathf.Clamp(currentHealth, 0, maxHealth);
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>Adds (or subtracts) max health. Positive delta also heals by the same amount.</summary>
    public void ModifyMaxHealth(int delta, bool healByDelta = true)
    {
        int previousMax = maxHealth;
        maxHealth = Mathf.Max(1, maxHealth + delta);

        if (delta > 0 && healByDelta)
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + (maxHealth - previousMax));
        }
        else
        {
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        }

        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    void BeginInvulnerability()
    {
        if (invulnerabilityDuration <= 0f)
        {
            return;
        }

        EndInvulnerability();
        _invulnerabilityRoutine = StartCoroutine(InvulnerabilityRoutine());
    }

    void EndInvulnerability()
    {
        if (_invulnerabilityRoutine != null)
        {
            StopCoroutine(_invulnerabilityRoutine);
            _invulnerabilityRoutine = null;
        }

        SetSpriteVisible(true);
    }

    IEnumerator InvulnerabilityRoutine()
    {
        float endTime = Time.time + invulnerabilityDuration;
        bool visible = false;

        while (Time.time < endTime)
        {
            SetSpriteVisible(visible);
            visible = !visible;
            yield return new WaitForSeconds(blinkInterval);
        }

        SetSpriteVisible(true);
        _invulnerabilityRoutine = null;
    }

    void SetSpriteVisible(bool visible)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = visible;
        }
    }
}
