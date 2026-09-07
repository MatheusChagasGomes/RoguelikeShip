using UnityEngine;

/// <summary>
/// Kamikaze enemy: rushes straight at the player, deals contact damage, and dies on impact.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyTwo : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform player;

    [Header("Charge")]
    [SerializeField] [Min(0.1f)] float chargeSpeed = 4.5f;
    [SerializeField] [Min(1)] int collisionDamage = 1;
    [Tooltip("Seconds after spawn before the kamikaze charge begins.")]
    [SerializeField] [Min(0f)] float chargeDelay = 0.15f;

    Rigidbody2D _body;
    EnemyHealth _health;
    float _baseChargeSpeed;
    float _chargeStartTime;
    bool _hasExploded;

    void Awake()
    {
        TryGetComponent(out _body);
        TryGetComponent(out _health);

        _body.bodyType = RigidbodyType2D.Kinematic;
        _body.gravityScale = 0f;
        _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        _baseChargeSpeed = chargeSpeed;
    }

    /// <summary>
    /// Scales charge speed from Inspector base values for the current difficulty loop.
    /// </summary>
    public void ApplyDifficultyScaling(float chargeSpeedScale)
    {
        chargeSpeed = Mathf.Max(0.1f, _baseChargeSpeed * Mathf.Max(0.01f, chargeSpeedScale));
    }

    void Start()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        _chargeStartTime = Time.time + chargeDelay;
    }

    void FixedUpdate()
    {
        if (_hasExploded || _health == null || !_health.IsAlive || player == null || Time.time < _chargeStartTime)
        {
            return;
        }

        Vector2 current = _body.position;
        Vector2 toPlayer = (Vector2)player.position - current;
        if (toPlayer.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector2 direction = toPlayer.normalized;
        Vector2 next = current + direction * (chargeSpeed * Time.fixedDeltaTime);
        _body.MovePosition(next);
        AlignToDirection(direction);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_hasExploded || !other.CompareTag("Player"))
        {
            return;
        }

        if (other.TryGetComponent(out PlayerHealth playerHealth)
            && !playerHealth.IgnoresEnemyCollisionDamage)
        {
            playerHealth.TakeDamage(collisionDamage);
        }

        Explode();
    }

    void Explode()
    {
        if (_hasExploded || _health == null)
        {
            return;
        }

        _hasExploded = true;
        _health.TakeDamage(_health.CurrentHealth);
    }

    void AlignToDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
