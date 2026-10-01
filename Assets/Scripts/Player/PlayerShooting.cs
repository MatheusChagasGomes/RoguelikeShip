using System;
using UnityEngine;

/// <summary>
/// Continuous auto-fire while the player is holding to move.
/// Stops as soon as the pointer/finger is released.
/// Uses a magazine: after magazineSize shots, weapons cool down for magazineCooldown seconds.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerShooting : MonoBehaviour
{
    const int MinMagazineSize = 3;

    [Header("Fire")]
    [SerializeField] PlayerProjectile projectilePrefab;
    [SerializeField] [Min(0.01f)] float fireInterval = 0.2f;
    [SerializeField] Transform firePoint;
    [SerializeField] Vector2 fireDirection = Vector2.up;
    [SerializeField] [Min(0.1f)] float projectileSpeed = 14f;
    [SerializeField] [Min(0f)] float multiShotSpacing = 0.35f;

    [Header("Magazine")]
    [SerializeField] [Min(1)] int magazineSize = 15;
    [SerializeField] [Min(0f)] float magazineCooldown = 5f;

    PlayerMovement _movement;
    float _nextFireTime;
    int _shotsRemaining = 15;
    float _reloadReadyTime;
    int _projectileCount = 1;
    int _projectileDamage = 1;
    int _pierceCount;
    float _explosionRadius;
    int _explosionDamage;
    bool _shrapnelEnabled;
    int _shrapnelCount;
    float _shrapnelSpeed;
    int _shrapnelDamage;
    int _firstShotDamageBonus;
    int _lastShotDamageBonus;
    bool _homingEnabled;
    float _homingTurnRate;
    float _homingRange;
    bool _instantNextReload;

    public PlayerProjectile ProjectilePrefab => projectilePrefab;
    public float ProjectileSpeed => projectileSpeed;
    public int ProjectileDamage => _projectileDamage;
    public int ProjectileCount => _projectileCount;
    public int PierceCount => _pierceCount;
    public float ExplosionRadius => _explosionRadius;
    public int ExplosionDamage => _explosionDamage;
    public int ShotsRemaining => _shotsRemaining;
    public int MagazineSize => magazineSize;
    public float MagazineCooldown => magazineCooldown;
    public int ShrapnelCount => _shrapnelCount;
    public float ShrapnelSpeed => _shrapnelSpeed;
    public int ShrapnelDamage => _shrapnelDamage;
    public int FirstShotDamageBonus => _firstShotDamageBonus;
    public int LastShotDamageBonus => _lastShotDamageBonus;
    public bool HomingEnabled => _homingEnabled;
    public float HomingTurnRate => _homingTurnRate;
    public float HomingRange => _homingRange;

    /// <summary>Invoked after magazine ammo changes. Args: current, max.</summary>
    public event Action<int, int> AmmoChanged;

    void Awake()
    {
        TryGetComponent(out _movement);

        if (firePoint == null)
        {
            firePoint = transform;
        }

        _shotsRemaining = magazineSize;
    }

    void Update()
    {
        TryFinishReload();

        if (_movement == null || !_movement.IsControlling)
        {
            _nextFireTime = 0f;
            return;
        }

        if (_shotsRemaining <= 0 || Time.time < _nextFireTime)
        {
            return;
        }

        Fire();
        ConsumeShot();
        _nextFireTime = Time.time + fireInterval;
    }

    public void SetProjectileCount(int count)
    {
        _projectileCount = Mathf.Max(1, count);
    }

    public void MultiplyProjectileCount(int multiplier)
    {
        if (multiplier == 0)
        {
            return;
        }

        _projectileCount = Mathf.Max(1, _projectileCount * multiplier);
    }

    public void SetProjectileDamage(int damage)
    {
        _projectileDamage = Mathf.Max(1, damage);
    }

    public void AddProjectileDamage(int amount)
    {
        SetProjectileDamage(_projectileDamage + amount);
    }

    public void SetPierceCount(int count)
    {
        _pierceCount = Mathf.Max(0, count);
    }

    public void SetExplosion(float radius, int areaDamage)
    {
        _explosionRadius = Mathf.Max(0f, radius);
        _explosionDamage = Mathf.Max(0, areaDamage);
    }

    public void SetShrapnel(int count, float speed, int damage)
    {
        _shrapnelEnabled = count > 0 && damage > 0;
        _shrapnelCount = Mathf.Max(0, count);
        _shrapnelSpeed = Mathf.Max(0.1f, speed);
        _shrapnelDamage = Mathf.Max(0, damage);
    }

    public void AddMagazineSize(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        magazineSize = Mathf.Max(MinMagazineSize, magazineSize + delta);
        _shotsRemaining = Mathf.Clamp(_shotsRemaining, 0, magazineSize);
        AmmoChanged?.Invoke(_shotsRemaining, magazineSize);
    }

    public void MultiplyMagazineCooldown(float multiplier)
    {
        if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f))
        {
            return;
        }

        magazineCooldown = Mathf.Max(0f, magazineCooldown * multiplier);
    }

    /// <summary>Adds a flat seconds delta to magazine reload time (negative = faster).</summary>
    public void AddMagazineCooldown(float deltaSeconds)
    {
        if (Mathf.Approximately(deltaSeconds, 0f))
        {
            return;
        }

        magazineCooldown = Mathf.Max(0f, magazineCooldown + deltaSeconds);
    }

    public void SetMagazineCooldown(float seconds)
    {
        magazineCooldown = Mathf.Max(0f, seconds);
    }

    /// <summary>Makes the next empty-magazine reload finish immediately.</summary>
    public void GrantInstantNextReload()
    {
        _instantNextReload = true;
        if (_shotsRemaining <= 0)
        {
            _reloadReadyTime = Time.time;
            TryFinishReload();
        }
    }

    public void AddFirstShotDamageBonus(int amount)
    {
        _firstShotDamageBonus = Mathf.Max(0, _firstShotDamageBonus + amount);
    }

    public void SetFirstShotDamageBonus(int amount)
    {
        _firstShotDamageBonus = Mathf.Max(0, amount);
    }

    public void AddLastShotDamageBonus(int amount)
    {
        _lastShotDamageBonus = Mathf.Max(0, _lastShotDamageBonus + amount);
    }

    public void SetLastShotDamageBonus(int amount)
    {
        _lastShotDamageBonus = Mathf.Max(0, amount);
    }

    public void SetHoming(float turnRateDegrees, float range)
    {
        _homingEnabled = turnRateDegrees > 0f && range > 0f;
        _homingTurnRate = Mathf.Max(0f, turnRateDegrees);
        _homingRange = Mathf.Max(0.5f, range);
    }

    public void ClearHoming()
    {
        _homingEnabled = false;
        _homingTurnRate = 0f;
        _homingRange = 0f;
    }

    /// <summary>
    /// Applies the player's current projectile buffs (damage extras excluded) to an ally shot.
    /// </summary>
    public void ApplyFullBuffsToProjectile(PlayerProjectile projectile, int damage)
    {
        if (projectile == null)
        {
            return;
        }

        projectile.Configure(
            Mathf.Max(1, damage),
            _pierceCount,
            _explosionRadius,
            _explosionDamage,
            _shrapnelEnabled,
            _shrapnelCount,
            _shrapnelSpeed,
            _shrapnelDamage);

        if (_homingEnabled)
        {
            projectile.SetHoming(_homingTurnRate, _homingRange);
        }
    }

    void TryFinishReload()
    {
        if (_shotsRemaining > 0 || Time.time < _reloadReadyTime)
        {
            return;
        }

        _shotsRemaining = magazineSize;
        AmmoChanged?.Invoke(_shotsRemaining, magazineSize);
    }

    void ConsumeShot()
    {
        _shotsRemaining = Mathf.Max(0, _shotsRemaining - 1);
        AmmoChanged?.Invoke(_shotsRemaining, magazineSize);

        if (_shotsRemaining <= 0)
        {
            if (_instantNextReload)
            {
                _instantNextReload = false;
                _reloadReadyTime = Time.time;
                TryFinishReload();
            }
            else
            {
                _reloadReadyTime = Time.time + magazineCooldown;
            }
        }
    }

    void Fire()
    {
        if (projectilePrefab == null)
        {
            return;
        }

        Vector2 origin = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
        int count = Mathf.Max(1, _projectileCount);
        int shotDamage = ResolveShotDamage();

        if (count == 1)
        {
            SpawnProjectile(origin, shotDamage);
            return;
        }

        float totalWidth = multiShotSpacing * (count - 1);
        float startX = -totalWidth * 0.5f;
        Vector2 right = new Vector2(fireDirection.y, -fireDirection.x).normalized;

        for (int i = 0; i < count; i++)
        {
            Vector2 offset = right * (startX + multiShotSpacing * i);
            SpawnProjectile(origin + offset, shotDamage);
        }
    }

    int ResolveShotDamage()
    {
        int damage = _projectileDamage;

        if (_shotsRemaining >= magazineSize)
        {
            damage += _firstShotDamageBonus;
        }

        if (_shotsRemaining == 1)
        {
            damage += _lastShotDamageBonus;
        }

        return Mathf.Max(1, damage);
    }

    void SpawnProjectile(Vector2 spawnPosition, int shotDamage)
    {
        PlayerProjectile projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        projectile.Configure(
            shotDamage,
            _pierceCount,
            _explosionRadius,
            _explosionDamage,
            _shrapnelEnabled,
            _shrapnelCount,
            _shrapnelSpeed,
            _shrapnelDamage);

        if (_homingEnabled)
        {
            projectile.SetHoming(_homingTurnRate, _homingRange);
        }

        projectile.Launch(spawnPosition, fireDirection, projectileSpeed);
    }
}
