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
        _body.MovePosition(_body.position + _velocity * Time.fixedDeltaTime);

        if (IsOutsideCamera())
        {
            Destroy(gameObject);
        }
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
