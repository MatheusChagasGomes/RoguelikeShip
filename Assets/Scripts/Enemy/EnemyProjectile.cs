using UnityEngine;

/// <summary>
/// Enemy bullet that travels toward a launch direction and damages the player on contact.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
public class EnemyProjectile : MonoBehaviour
{
    [Header("Motion")]
    [SerializeField] [Min(0.1f)] float speed = 8f;
    [SerializeField] Vector2 direction = Vector2.down;

    [Header("Combat")]
    [SerializeField] [Min(1)] int damage = 1;

    [Header("Lifetime")]
    [Tooltip("Destroy when this far outside the camera view (world units).")]
    [SerializeField] [Min(0f)] float despawnPadding = 1f;
    [SerializeField] Camera worldCamera;

    Rigidbody2D _body;
    Vector2 _velocity;

    public void Launch(Vector2 worldPosition, Vector2 travelDirection, float travelSpeed)
    {
        transform.position = worldPosition;
        direction = travelDirection.sqrMagnitude > 0.0001f ? travelDirection.normalized : Vector2.down;
        speed = Mathf.Max(0.1f, travelSpeed);
        _velocity = direction * speed;
        AlignToDirection();

        if (_body != null)
        {
            _body.position = worldPosition;
        }
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
        AlignToDirection();
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
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (other.TryGetComponent(out PlayerHealth playerHealth))
        {
            playerHealth.TakeDamage(damage);
        }

        Destroy(gameObject);
    }

    void AlignToDirection()
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
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
