using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Regenerating force shield that can absorb one hit, then recharges on a cooldown.
/// Kept separate from PlayerHealth so health and defense stay single-responsibility.
/// </summary>
[DisallowMultipleComponent]
public class PlayerForceShield : MonoBehaviour
{
    Coroutine _cooldownRoutine;
    bool _enabled;
    bool _ready;
    float _cooldownSeconds = 10f;

    public bool IsReady => _ready;
    public bool IsRecharging => _enabled && !_ready;
    public bool IsEnabled => _enabled;

    /// <summary>Invoked when readiness changes (granted, absorbed, or recharged).</summary>
    public event Action Changed;

    void OnDisable()
    {
        StopCooldown();
    }

    /// <summary>
    /// Enables the shield and marks it ready. After absorbing a hit it recharges
    /// after <paramref name="cooldownSeconds"/>.
    /// </summary>
    public void Enable(float cooldownSeconds = 10f)
    {
        _enabled = true;
        _cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
        StopCooldown();
        SetReady(true);
    }

    /// <summary>Disables the shield and clears readiness (e.g. on player death).</summary>
    public void Disable()
    {
        _enabled = false;
        StopCooldown();
        SetReady(false);
    }

    /// <summary>
    /// Absorbs a hit if ready. Returns true when the hit was blocked.
    /// </summary>
    public bool TryAbsorb()
    {
        if (!_ready)
        {
            return false;
        }

        SetReady(false);

        if (_enabled && _cooldownSeconds > 0f && isActiveAndEnabled)
        {
            _cooldownRoutine = StartCoroutine(CooldownRoutine());
        }
        else if (_enabled && _cooldownSeconds <= 0f)
        {
            SetReady(true);
        }

        return true;
    }

    IEnumerator CooldownRoutine()
    {
        // Unscaled so recharge continues while upgrade offers pause Time.timeScale.
        yield return new WaitForSecondsRealtime(_cooldownSeconds);

        _cooldownRoutine = null;
        if (!_enabled)
        {
            yield break;
        }

        SetReady(true);
    }

    void SetReady(bool ready)
    {
        if (_ready == ready)
        {
            return;
        }

        _ready = ready;
        Changed?.Invoke();
    }

    void StopCooldown()
    {
        if (_cooldownRoutine != null)
        {
            StopCoroutine(_cooldownRoutine);
            _cooldownRoutine = null;
        }
    }
}
