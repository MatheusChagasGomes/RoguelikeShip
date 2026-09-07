using UnityEngine;

/// <summary>
/// Short-lived fire patch that damages overlapping enemies once per contact.
/// </summary>
[DisallowMultipleComponent]
public class CometTailSegment : MonoBehaviour
{
    [SerializeField] float lifetime = 0.85f;
    [SerializeField] Color color = new Color(1f, 0.35f, 0.05f, 0.55f);

    CircleCollider2D _collider;
    SpriteRenderer _renderer;
    float _radius;
    int _damage;
    float _age;
    float _startScale;

    public void Initialize(float radius, int damage)
    {
        _radius = Mathf.Max(0.1f, radius);
        _damage = Mathf.Max(1, damage);

        _collider = gameObject.AddComponent<CircleCollider2D>();
        _collider.isTrigger = true;
        _collider.radius = _radius;

        _renderer = gameObject.AddComponent<SpriteRenderer>();
        _renderer.sprite = ExplosionPulseSprite.Circle;
        _renderer.color = color;
        _renderer.sortingOrder = 5;

        _startScale = _radius * 2f;
        transform.localScale = Vector3.one * _startScale;
    }

    void Update()
    {
        _age += Time.deltaTime;
        float t = Mathf.Clamp01(_age / Mathf.Max(0.01f, lifetime));

        if (_renderer != null)
        {
            Color c = color;
            c.a = color.a * (1f - t);
            _renderer.color = c;
        }

        if (t >= 1f)
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

        enemyHealth.TakeDamage(_damage);
    }
}
