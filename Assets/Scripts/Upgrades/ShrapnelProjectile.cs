using UnityEngine;

/// <summary>
/// Small debris launched outward when an explosive projectile detonates.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class ShrapnelProjectile : MonoBehaviour
{
    [SerializeField] [Min(0.1f)] float speed = 9f;
    [SerializeField] [Min(1)] int damage = 1;
    [SerializeField] [Min(0.05f)] float lifetime = 0.45f;
    [SerializeField] Color color = new Color(0.95f, 0.75f, 0.25f, 0.9f);

    Rigidbody2D _body;
    Vector2 _velocity;
    float _age;

    public static void Launch(Vector3 origin, Vector2 direction, float travelSpeed, int projectileDamage)
    {
        var go = new GameObject("Shrapnel");
        go.transform.position = origin;
        var shrapnel = go.AddComponent<ShrapnelProjectile>();
        shrapnel.Configure(travelSpeed, projectileDamage);
        shrapnel.Fire(direction);
    }

    void Configure(float travelSpeed, int projectileDamage)
    {
        speed = Mathf.Max(0.1f, travelSpeed);
        damage = Mathf.Max(1, projectileDamage);
    }

    void Awake()
    {
        TryGetComponent(out _body);
        _body.bodyType = RigidbodyType2D.Kinematic;
        _body.gravityScale = 0f;
        _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (TryGetComponent(out CircleCollider2D collider))
        {
            collider.isTrigger = true;
            collider.radius = 0.08f;
        }

        var renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = ExplosionPulseSprite.Circle;
        renderer.color = color;
        renderer.sortingOrder = 35;
        transform.localScale = Vector3.one * 0.18f;
    }

    void Fire(Vector2 direction)
    {
        _velocity = direction.sqrMagnitude > 0.0001f ? direction.normalized * speed : Vector2.up * speed;
    }

    void FixedUpdate()
    {
        _body.MovePosition(_body.position + _velocity * Time.fixedDeltaTime);
    }

    void Update()
    {
        _age += Time.deltaTime;
        if (_age >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent(out EnemyHealth enemyHealth) || !enemyHealth.IsAlive)
        {
            return;
        }

        enemyHealth.TakeDamage(damage);
        Destroy(gameObject);
    }
}
