using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Companion automaton that orbits the player. Behavior depends on <see cref="AllyDroneRole"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class AllyDrone : MonoBehaviour
{
    [SerializeField] [Min(0.1f)] float orbitRadius = 1.1f;
    [SerializeField] [Min(0f)] float orbitSpeed = 1.6f;
    [SerializeField] [Min(0.05f)] float followSmoothTime = 0.12f;
    [SerializeField] [Min(0.05f)] float fireInterval = 0.55f;
    [SerializeField] [Min(0.1f)] float projectileSpeed = 12f;
    [SerializeField] [Min(0.5f)] float targetRange = 8f;
    [SerializeField] [Min(0.05f)] float visualScale = 0.35f;
    [SerializeField] [Min(0.1f)] float contactDamageInterval = 0.35f;
    [SerializeField] [Min(1)] int contactDamage = 1;
    [SerializeField] [Min(0.5f)] float chargeCooldown = 5f;
    [SerializeField] [Min(1f)] float chargeSpeed = 11f;
    [SerializeField] [Min(0.1f)] float chargeDuration = 0.85f;
    [SerializeField] [Min(1)] int chargeDamage = 2;
    [SerializeField] [Min(0.5f)] float medicHealInterval = 2f;
    [SerializeField] [Min(1)] int medicHealAmount = 1;

    Transform _owner;
    PlayerShooting _ownerShooting;
    PlayerHealth _ownerHealth;
    AllyDroneRole _role = AllyDroneRole.Blocker;
    int _powerTier;
    float _orbitPhase;
    float _nextFireTime;
    float _nextContactTime;
    float _nextChargeTime;
    float _nextHealTime;
    float _chargeEndTime;
    bool _isCharging;
    Vector2 _chargeDirection = Vector2.up;
    Vector2 _velocity;
    SpriteRenderer _renderer;
    CircleCollider2D _collider;
    Rigidbody2D _body;
    static Sprite _sharedSprite;
    static Texture2D _sharedTexture;

    public AllyDroneRole Role => _role;
    public bool BlocksProjectiles =>
        _role == AllyDroneRole.Blocker
        || (_role == AllyDroneRole.Charger && _powerTier > 0 && _isCharging);

    public void Initialize(
        Transform owner,
        PlayerShooting ownerShooting,
        PlayerHealth ownerHealth,
        float phaseOffset,
        AllyDroneRole role)
    {
        _owner = owner;
        _ownerShooting = ownerShooting;
        _ownerHealth = ownerHealth;
        _orbitPhase = phaseOffset;
        _role = role == AllyDroneRole.None ? AllyDroneRole.Blocker : role;
        _nextFireTime = Time.time + fireInterval * 0.5f;
        _nextChargeTime = Time.time + 0.5f;
        _nextHealTime = Time.time + medicHealInterval;
        EnsurePhysics();
        EnsureVisual();
        ApplyPowerVisuals();
    }

    public void SetOrbitPhase(float phaseOffset)
    {
        _orbitPhase = phaseOffset;
    }

    public void SetPowerTier(int tier)
    {
        _powerTier = Mathf.Max(0, tier);
        ApplyPowerVisuals();
    }

    void LateUpdate()
    {
        if (_owner == null)
        {
            Destroy(gameObject);
            return;
        }

        switch (_role)
        {
            case AllyDroneRole.Charger:
                UpdateCharger();
                break;
            case AllyDroneRole.Shooter:
                UpdateOrbit();
                UpdateShooter();
                break;
            case AllyDroneRole.Medic:
                UpdateOrbit();
                UpdateMedic();
                break;
            default:
                UpdateOrbit();
                break;
        }
    }

    void UpdateOrbit()
    {
        _orbitPhase += EffectiveOrbitSpeed() * Time.deltaTime;
        Vector2 desired = (Vector2)_owner.position
            + new Vector2(Mathf.Cos(_orbitPhase), Mathf.Sin(_orbitPhase)) * EffectiveOrbitRadius();
        Vector2 next = Vector2.SmoothDamp(transform.position, desired, ref _velocity, followSmoothTime);
        transform.position = new Vector3(next.x, next.y, _owner.position.z);
    }

    void UpdateShooter()
    {
        if (Time.time < _nextFireTime)
        {
            return;
        }

        if (!TryFindTarget(out Vector2 targetPosition))
        {
            return;
        }

        FireAt(targetPosition);
        _nextFireTime = Time.time + EffectiveFireInterval();
    }

    void UpdateMedic()
    {
        if (_ownerHealth == null || Time.time < _nextHealTime)
        {
            return;
        }

        _ownerHealth.Heal(EffectiveMedicHealAmount());
        _nextHealTime = Time.time + EffectiveMedicHealInterval();
    }

    void UpdateCharger()
    {
        if (_isCharging)
        {
            transform.position += (Vector3)(_chargeDirection * EffectiveChargeSpeed() * Time.deltaTime);
            if (_powerTier > 0)
            {
                ClearNearbyEnemyProjectiles(0.75f);
            }

            if (Time.time >= _chargeEndTime)
            {
                EndCharge();
            }

            return;
        }

        UpdateOrbit();

        if (Time.time < _nextChargeTime)
        {
            return;
        }

        if (!TryFindTarget(out Vector2 targetPosition))
        {
            return;
        }

        Vector2 toTarget = targetPosition - (Vector2)transform.position;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return;
        }

        _chargeDirection = toTarget.normalized;
        _isCharging = true;
        _chargeEndTime = Time.time + chargeDuration;
        _nextChargeTime = Time.time + EffectiveChargeCooldown();
        _velocity = Vector2.zero;
    }

    void EndCharge()
    {
        _isCharging = false;
        _velocity = Vector2.zero;
    }

    bool TryFindTarget(out Vector2 targetPosition)
    {
        targetPosition = default;
        IReadOnlyList<EnemyHealth> enemies = EnemyHealth.Active;
        float bestSqr = targetRange * targetRange;
        bool found = false;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyHealth enemy = enemies[i];
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            Vector2 toEnemy = (Vector2)enemy.transform.position - (Vector2)transform.position;
            float sqr = toEnemy.sqrMagnitude;
            if (sqr > bestSqr)
            {
                continue;
            }

            bestSqr = sqr;
            targetPosition = enemy.transform.position;
            found = true;
        }

        return found;
    }

    void FireAt(Vector2 targetPosition)
    {
        if (_ownerShooting == null || _ownerShooting.ProjectilePrefab == null)
        {
            return;
        }

        Vector2 origin = transform.position;
        Vector2 direction = targetPosition - origin;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector2.up;
        }

        int damage = Mathf.Max(1, _ownerShooting.ProjectileDamage);
        PlayerProjectile projectile = Instantiate(
            _ownerShooting.ProjectilePrefab,
            origin,
            Quaternion.identity);

        if (_powerTier > 0)
        {
            _ownerShooting.ApplyFullBuffsToProjectile(projectile, damage);
        }
        else
        {
            projectile.Configure(damage, 0, 0f, 0);
        }

        projectile.Launch(origin, direction, projectileSpeed > 0.1f ? projectileSpeed : _ownerShooting.ProjectileSpeed);
    }

    static readonly Collider2D[] ProjectileOverlapBuffer = new Collider2D[24];

    void ClearNearbyEnemyProjectiles(float radius)
    {
        int hitCount = Physics2D.OverlapCircle(
            transform.position,
            radius,
            ContactFilter2D.noFilter,
            ProjectileOverlapBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = ProjectileOverlapBuffer[i];
            if (hit != null && hit.TryGetComponent(out EnemyProjectile enemyProjectile))
            {
                Destroy(enemyProjectile.gameObject);
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryDealContactDamage(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryDealContactDamage(other);
    }

    void TryDealContactDamage(Collider2D other)
    {
        if (_role != AllyDroneRole.Blocker && !(_role == AllyDroneRole.Charger && _isCharging))
        {
            return;
        }

        if (!other.TryGetComponent(out EnemyHealth enemyHealth) || !enemyHealth.IsAlive)
        {
            return;
        }

        if (Time.time < _nextContactTime)
        {
            return;
        }

        int damage = _role == AllyDroneRole.Charger ? EffectiveChargeDamage() : EffectiveContactDamage();
        if (!enemyHealth.TakeDamage(damage))
        {
            return;
        }

        _nextContactTime = Time.time + contactDamageInterval;

        if (_role == AllyDroneRole.Charger && _isCharging)
        {
            EndCharge();
        }
    }

    float EffectiveOrbitRadius() => orbitRadius;

    float EffectiveOrbitSpeed()
    {
        if (_role == AllyDroneRole.Blocker && _powerTier > 0)
        {
            return orbitSpeed * 2.25f;
        }

        return orbitSpeed;
    }

    float EffectiveFireInterval() => fireInterval;

    int EffectiveContactDamage() => contactDamage;

    int EffectiveChargeDamage() => chargeDamage;

    float EffectiveChargeSpeed() => chargeSpeed;

    float EffectiveChargeCooldown() => _powerTier > 0 ? 3f : chargeCooldown;

    int EffectiveMedicHealAmount() => _powerTier > 0 ? 2 : medicHealAmount;

    float EffectiveMedicHealInterval() => medicHealInterval;

    void EnsurePhysics()
    {
        if (!TryGetComponent(out _body))
        {
            _body = gameObject.AddComponent<Rigidbody2D>();
        }

        _body.bodyType = RigidbodyType2D.Kinematic;
        _body.gravityScale = 0f;
        _body.simulated = true;
        _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (!TryGetComponent(out _collider))
        {
            _collider = gameObject.AddComponent<CircleCollider2D>();
        }

        _collider.isTrigger = true;
        _collider.radius = _role == AllyDroneRole.Blocker ? 0.85f : 0.55f;
    }

    void EnsureVisual()
    {
        if (!TryGetComponent(out _renderer))
        {
            _renderer = gameObject.AddComponent<SpriteRenderer>();
        }

        if (_sharedSprite == null)
        {
            _sharedTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _sharedTexture.SetPixel(0, 0, Color.white);
            _sharedTexture.Apply(false, true);
            _sharedSprite = Sprite.Create(
                _sharedTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
        }

        _renderer.sprite = _sharedSprite;
        _renderer.sortingOrder = 20;
        ApplyPowerVisuals();
    }

    void ApplyPowerVisuals()
    {
        if (_renderer == null)
        {
            return;
        }

        _renderer.color = RoleColor(_role);
        float scale = visualScale * (_powerTier > 0 ? 1.25f : 1f);
        if (_role == AllyDroneRole.Blocker)
        {
            scale *= 1.1f;
        }

        transform.localScale = Vector3.one * scale;
    }

    static Color RoleColor(AllyDroneRole role)
    {
        return role switch
        {
            AllyDroneRole.Charger => new Color(1f, 0.45f, 0.2f, 0.95f),
            AllyDroneRole.Shooter => new Color(1f, 0.85f, 0.25f, 0.95f),
            AllyDroneRole.Medic => new Color(0.35f, 1f, 0.55f, 0.95f),
            _ => new Color(0.45f, 0.9f, 1f, 0.95f),
        };
    }
}
