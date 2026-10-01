using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player bullet that travels upward with its own collider for enemy hits.
/// Supports pierce and contact explosions via runtime configuration.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
public class PlayerProjectile : MonoBehaviour
{
    [Header("Motion")]
    [SerializeField] [Min(0.1f)] float speed = 14f;
    [SerializeField] Vector2 direction = Vector2.up;

    [Header("Combat")]
    [SerializeField] [Min(1)] int damage = 1;
    [SerializeField] [Min(0)] int pierceRemaining;
    [SerializeField] [Min(0f)] float explosionRadius;
    [SerializeField] [Min(0)] int explosionDamage;
    [SerializeField] LayerMask explosionMask = ~0;
    [SerializeField] Color explosionPulseColor = new Color(1f, 0.55f, 0.2f, 0.45f);

    [Header("Shrapnel")]
    [SerializeField] bool shrapnelEnabled;
    [SerializeField] [Min(0)] int shrapnelCount;
    [SerializeField] [Min(0.1f)] float shrapnelSpeed = 9f;
    [SerializeField] [Min(0)] int shrapnelDamage = 1;

    [Header("Homing")]
    [SerializeField] bool homingEnabled;
    [SerializeField] [Min(0f)] float homingTurnRate = 80f;
    [SerializeField] [Min(0.5f)] float homingRange = 7f;

    [Header("Lifetime")]
    [Tooltip("Destroy when this far outside the camera view (world units).")]
    [SerializeField] [Min(0f)] float despawnPadding = 1f;
    [SerializeField] Camera worldCamera;

    Rigidbody2D _body;
    Vector2 _velocity;

    public void Launch(Vector2 worldPosition, Vector2 travelDirection, float travelSpeed)
    {
        transform.position = worldPosition;
        direction = travelDirection.sqrMagnitude > 0.0001f ? travelDirection.normalized : Vector2.up;
        speed = Mathf.Max(0.1f, travelSpeed);
        _velocity = direction * speed;

        if (_body != null)
        {
            _body.position = worldPosition;
        }
    }

    public void Configure(
        int projectileDamage,
        int pierceCount,
        float explosionRadiusWorld,
        int explosionDamageAmount,
        bool enableShrapnel = false,
        int debrisCount = 0,
        float debrisSpeed = 9f,
        int debrisDamage = 1)
    {
        damage = Mathf.Max(1, projectileDamage);
        pierceRemaining = Mathf.Max(0, pierceCount);
        explosionRadius = Mathf.Max(0f, explosionRadiusWorld);
        explosionDamage = Mathf.Max(0, explosionDamageAmount);
        shrapnelEnabled = enableShrapnel;
        shrapnelCount = Mathf.Max(0, debrisCount);
        shrapnelSpeed = Mathf.Max(0.1f, debrisSpeed);
        shrapnelDamage = Mathf.Max(0, debrisDamage);
    }

    public void SetHoming(float turnRateDegrees, float range)
    {
        homingEnabled = turnRateDegrees > 0f && range > 0f;
        homingTurnRate = Mathf.Max(0f, turnRateDegrees);
        homingRange = Mathf.Max(0.5f, range);
    }

    void Awake()
    {
        TryGetComponent(out _body);
        _body.bodyType = RigidbodyType2D.Kinematic;
        _body.gravityScale = 0f;
        _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _body.simulated = true;

        if (TryGetComponent(out CapsuleCollider2D capsuleCollider))
        {
            capsuleCollider.isTrigger = true;
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        _velocity = direction.normalized * speed;
    }

    void FixedUpdate()
    {
        if (homingEnabled)
        {
            ApplyHoming(Time.fixedDeltaTime);
        }

        _body.MovePosition(_body.position + _velocity * Time.fixedDeltaTime);

        if (IsOutsideCamera())
        {
            Destroy(gameObject);
        }
    }

    void ApplyHoming(float deltaTime)
    {
        if (!TryFindHomingTarget(out Vector2 targetPosition))
        {
            return;
        }

        Vector2 toTarget = targetPosition - _body.position;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector2 currentDirection = _velocity.sqrMagnitude > 0.0001f ? _velocity.normalized : direction;
        float maxRadians = Mathf.Deg2Rad * homingTurnRate * deltaTime;
        Vector2 nextDirection = Vector3.RotateTowards(currentDirection, toTarget.normalized, maxRadians, 0f);
        direction = nextDirection;
        _velocity = direction * speed;
    }

    bool TryFindHomingTarget(out Vector2 targetPosition)
    {
        targetPosition = default;
        IReadOnlyList<EnemyHealth> enemies = EnemyHealth.Active;
        float bestSqr = homingRange * homingRange;
        bool found = false;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyHealth enemy = enemies[i];
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            Vector2 toEnemy = (Vector2)enemy.transform.position - _body.position;
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

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent(out EnemyHealth enemyHealth))
        {
            return;
        }

        if (!enemyHealth.TakeDamage(damage))
        {
            return;
        }

        if (explosionRadius > 0f && explosionDamage > 0)
        {
            Detonate(other.transform.position, enemyHealth);
        }

        if (pierceRemaining > 0)
        {
            pierceRemaining--;
            return;
        }

        Destroy(gameObject);
    }

    void Detonate(Vector3 center, EnemyHealth primaryHit)
    {
        AreaDamage.Apply(center, explosionRadius, explosionDamage, explosionPulseColor, primaryHit);

        if (shrapnelEnabled && shrapnelCount > 0 && shrapnelDamage > 0)
        {
            SpawnShrapnel(center);
        }
    }

    void SpawnShrapnel(Vector3 center)
    {
        float arcStart = -75f;
        float arcSpan = 150f;

        for (int i = 0; i < shrapnelCount; i++)
        {
            float t = shrapnelCount == 1 ? 0.5f : (float)i / (shrapnelCount - 1);
            float angle = (arcStart + arcSpan * t) * Mathf.Deg2Rad;
            Vector2 debrisDirection = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            ShrapnelProjectile.Launch(center, debrisDirection, shrapnelSpeed, shrapnelDamage);
        }
    }

    bool IsOutsideCamera()
    {
        if (worldCamera == null)
        {
            return false;
        }

        float verticalExtent = worldCamera.orthographicSize;
        float horizontalExtent = verticalExtent * worldCamera.aspect;
        Vector3 cameraPosition = worldCamera.transform.position;
        Vector2 position = _body.position;

        return position.x < cameraPosition.x - horizontalExtent - despawnPadding
            || position.x > cameraPosition.x + horizontalExtent + despawnPadding
            || position.y < cameraPosition.y - verticalExtent - despawnPadding
            || position.y > cameraPosition.y + verticalExtent + despawnPadding;
    }
}
