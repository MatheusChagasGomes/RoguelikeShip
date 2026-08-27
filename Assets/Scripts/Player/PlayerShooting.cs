using UnityEngine;

/// <summary>
/// Continuous auto-fire while the player is holding to move.
/// Stops as soon as the pointer/finger is released.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerShooting : MonoBehaviour
{
    [Header("Fire")]
    [SerializeField] PlayerProjectile projectilePrefab;
    [SerializeField] [Min(0.01f)] float fireInterval = 0.2f;
    [SerializeField] Transform firePoint;
    [SerializeField] Vector2 fireDirection = Vector2.up;
    [SerializeField] [Min(0.1f)] float projectileSpeed = 14f;
    [SerializeField] [Min(0f)] float multiShotSpacing = 0.35f;

    PlayerMovement _movement;
    float _nextFireTime;
    int _projectileCount = 1;
    int _projectileDamage = 1;
    int _pierceCount;
    float _explosionRadius;
    int _explosionDamage;

    public PlayerProjectile ProjectilePrefab => projectilePrefab;
    public float ProjectileSpeed => projectileSpeed;
    public int ProjectileDamage => _projectileDamage;
    public int PierceCount => _pierceCount;
    public float ExplosionRadius => _explosionRadius;
    public int ExplosionDamage => _explosionDamage;

    void Awake()
    {
        TryGetComponent(out _movement);

        if (firePoint == null)
        {
            firePoint = transform;
        }
    }

    void Update()
    {
        if (_movement == null || !_movement.IsControlling)
        {
            _nextFireTime = 0f;
            return;
        }

        if (Time.time < _nextFireTime)
        {
            return;
        }

        Fire();
        _nextFireTime = Time.time + fireInterval;
    }

    public void SetProjectileCount(int count)
    {
        _projectileCount = Mathf.Max(1, count);
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

    void Fire()
    {
        if (projectilePrefab == null)
        {
            return;
        }

        Vector2 origin = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
        int count = Mathf.Max(1, _projectileCount);

        if (count == 1)
        {
            SpawnProjectile(origin);
            return;
        }

        float totalWidth = multiShotSpacing * (count - 1);
        float startX = -totalWidth * 0.5f;
        Vector2 right = new Vector2(fireDirection.y, -fireDirection.x).normalized;

        for (int i = 0; i < count; i++)
        {
            Vector2 offset = right * (startX + multiShotSpacing * i);
            SpawnProjectile(origin + offset);
        }
    }

    void SpawnProjectile(Vector2 spawnPosition)
    {
        PlayerProjectile projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        projectile.Configure(_projectileDamage, _pierceCount, _explosionRadius, _explosionDamage);
        projectile.Launch(spawnPosition, fireDirection, projectileSpeed);
    }
}
