using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Companion drone that orbits the player and auto-fires at nearby enemies.
/// </summary>
[DisallowMultipleComponent]
public class AllyDrone : MonoBehaviour
{
    [SerializeField] [Min(0.1f)] float orbitRadius = 1.1f;
    [SerializeField] [Min(0f)] float orbitSpeed = 1.6f;
    [SerializeField] [Min(0.05f)] float followSmoothTime = 0.12f;
    [SerializeField] [Min(0.05f)] float fireInterval = 0.55f;
    [SerializeField] [Min(0.1f)] float projectileSpeed = 12f;
    [SerializeField] [Min(0.5f)] float targetRange = 8f;
    [SerializeField] [Min(0.05f)] float visualScale = 0.35f;

    Transform _owner;
    PlayerShooting _ownerShooting;
    float _orbitPhase;
    float _nextFireTime;
    Vector2 _velocity;
    SpriteRenderer _renderer;
    static Sprite _sharedSprite;
    static Texture2D _sharedTexture;

    public void Initialize(Transform owner, PlayerShooting ownerShooting, float phaseOffset)
    {
        _owner = owner;
        _ownerShooting = ownerShooting;
        _orbitPhase = phaseOffset;
        _nextFireTime = Time.time + fireInterval * 0.5f;
        EnsureVisual();
    }

    void LateUpdate()
    {
        if (_owner == null)
        {
            Destroy(gameObject);
            return;
        }

        _orbitPhase += orbitSpeed * Time.deltaTime;
        Vector2 desired = (Vector2)_owner.position
            + new Vector2(Mathf.Cos(_orbitPhase), Mathf.Sin(_orbitPhase)) * orbitRadius;
        Vector2 next = Vector2.SmoothDamp(transform.position, desired, ref _velocity, followSmoothTime);
        transform.position = new Vector3(next.x, next.y, _owner.position.z);

        if (Time.time < _nextFireTime)
        {
            return;
        }

        if (!TryFindTarget(out Vector2 targetPosition))
        {
            return;
        }

        FireAt(targetPosition);
        _nextFireTime = Time.time + fireInterval;
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

        PlayerProjectile projectile = Instantiate(
            _ownerShooting.ProjectilePrefab,
            origin,
            Quaternion.identity);
        projectile.Configure(
            Mathf.Max(1, _ownerShooting.ProjectileDamage),
            0,
            0f,
            0);
        projectile.Launch(origin, direction, projectileSpeed > 0.1f ? projectileSpeed : _ownerShooting.ProjectileSpeed);
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
        _renderer.color = new Color(0.45f, 0.9f, 1f, 0.95f);
        _renderer.sortingOrder = 20;
        transform.localScale = Vector3.one * visualScale;
    }
}
