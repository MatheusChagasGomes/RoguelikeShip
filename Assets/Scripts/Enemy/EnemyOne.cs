using UnityEngine;

/// <summary>
/// Enemy_1 behaviour: enters the play area, drifts gently, and fires toward the player.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyOne : MonoBehaviour
{
    [Header("References")]
    [SerializeField] EnemyProjectile projectilePrefab;
    [SerializeField] Transform firePoint;
    [SerializeField] Transform player;
    [SerializeField] Camera worldCamera;

    [Header("Enter")]
    [SerializeField] [Min(0.1f)] float enterSpeed = 2.5f;
    [Tooltip("Viewport Y (0 bottom, 1 top) where the ship settles after entering.")]
    [SerializeField] [Range(0.35f, 0.95f)] float hoverViewportY = 0.72f;
    [SerializeField] [Min(0.01f)] float arriveDistance = 0.12f;

    [Header("Drift")]
    [SerializeField] [Min(0f)] float driftAmplitude = 0.55f;
    [SerializeField] [Min(0f)] float driftSpeed = 1.1f;
    [SerializeField] [Min(0.05f)] float driftFollowSmoothTime = 0.4f;
    [Tooltip("Seconds to ease into full drift amplitude after arriving.")]
    [SerializeField] [Min(0.05f)] float driftBlendDuration = 1.25f;

    [Header("Fire")]
    [SerializeField] [Min(0.1f)] float fireInterval = 1.4f;
    [SerializeField] [Min(0.1f)] float projectileSpeed = 8f;
    [SerializeField] [Min(0f)] float fireDelayAfterEnter = 0.35f;

    Rigidbody2D _body;
    EnemyHealth _health;
    Vector2 _hoverCenter;
    Vector2 _smoothVelocity;
    float _baseEnterSpeed;
    float _baseFireInterval;
    float _baseProjectileSpeed;
    float _nextFireTime;
    float _driftPhase;
    float _driftBlend;
    bool _hasEntered;

    void Awake()
    {
        TryGetComponent(out _body);
        TryGetComponent(out _health);

        _body.bodyType = RigidbodyType2D.Kinematic;
        _body.gravityScale = 0f;
        _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        _baseEnterSpeed = enterSpeed;
        _baseFireInterval = fireInterval;
        _baseProjectileSpeed = projectileSpeed;

        if (firePoint == null)
        {
            firePoint = transform;
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }
    }

    /// <summary>
    /// Scales combat pacing from Inspector base values for the current difficulty loop.
    /// </summary>
    public void ApplyDifficultyScaling(
        float fireIntervalScale,
        float minFireInterval,
        float projectileSpeedScale,
        float enterSpeedScale)
    {
        fireInterval = Mathf.Max(minFireInterval, _baseFireInterval * Mathf.Max(0.01f, fireIntervalScale));
        projectileSpeed = Mathf.Max(0.1f, _baseProjectileSpeed * Mathf.Max(0.01f, projectileSpeedScale));
        enterSpeed = Mathf.Max(0.1f, _baseEnterSpeed * Mathf.Max(0.01f, enterSpeedScale));
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

        _hoverCenter = ResolveHoverCenter();
        _nextFireTime = Time.time + fireDelayAfterEnter;
    }

    void FixedUpdate()
    {
        if (_health != null && !_health.IsAlive)
        {
            return;
        }

        UpdateMovement();

        if (_hasEntered)
        {
            TryFire();
        }
    }

    void UpdateMovement()
    {
        Vector2 current = _body.position;
        float deltaTime = Time.fixedDeltaTime;

        if (!_hasEntered)
        {
            float distance = Vector2.Distance(current, _hoverCenter);
            // Ease down near the hover line so arrival does not hard-stop.
            float approachT = Mathf.Clamp01(distance / 1.25f);
            float maxSpeed = Mathf.Lerp(enterSpeed * 0.35f, enterSpeed, approachT);

            Vector2 next = Vector2.MoveTowards(current, _hoverCenter, maxSpeed * deltaTime);
            _body.MovePosition(next);
            _smoothVelocity = (next - current) / Mathf.Max(deltaTime, 0.0001f);

            if (Vector2.Distance(next, _hoverCenter) <= arriveDistance)
            {
                BeginHover(next);
            }

            return;
        }

        _driftBlend = Mathf.MoveTowards(_driftBlend, 1f, deltaTime / driftBlendDuration);
        _driftPhase += driftSpeed * deltaTime;

        float amplitude = driftAmplitude * _driftBlend;
        Vector2 desired = _hoverCenter + new Vector2(
            Mathf.Sin(_driftPhase) * amplitude,
            Mathf.Cos(_driftPhase * 0.65f) * (amplitude * 0.35f));

        Vector2 drifted = Vector2.SmoothDamp(
            current,
            desired,
            ref _smoothVelocity,
            driftFollowSmoothTime,
            enterSpeed,
            deltaTime);
        _body.MovePosition(drifted);
    }

    void BeginHover(Vector2 arrivePosition)
    {
        _hasEntered = true;
        _hoverCenter = arrivePosition;
        _driftPhase = 0f;
        _driftBlend = 0f;
        _nextFireTime = Time.time + fireDelayAfterEnter;
    }

    void TryFire()
    {
        if (projectilePrefab == null || player == null || Time.time < _nextFireTime)
        {
            return;
        }

        Vector2 spawnPosition = firePoint != null ? (Vector2)firePoint.position : _body.position;
        Vector2 toPlayer = (Vector2)player.position - spawnPosition;
        if (toPlayer.sqrMagnitude <= 0.0001f)
        {
            toPlayer = Vector2.down;
        }

        EnemyProjectile projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        projectile.Launch(spawnPosition, toPlayer, projectileSpeed);
        _nextFireTime = Time.time + fireInterval;
    }

    Vector2 ResolveHoverCenter()
    {
        if (worldCamera == null)
        {
            return _body.position;
        }

        float depth = Mathf.Abs(worldCamera.transform.position.z - transform.position.z);
        Vector3 world = worldCamera.ViewportToWorldPoint(new Vector3(0.5f, hoverViewportY, depth));
        return new Vector2(_body.position.x, world.y);
    }
}
