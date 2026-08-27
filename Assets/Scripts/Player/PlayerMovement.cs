using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Moves the ship with a finger on mobile (or mouse in the editor) for a vertical 2D scroller.
/// Drag is relative: the ship keeps its offset from the pointer instead of jumping under it.
/// Sensitivity above 1 lets short one-handed swipes cover the play area.
/// </summary>
[DisallowMultipleComponent]
public class PlayerMovement : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] [Min(0f)] float followSpeed = 40f;
    [Tooltip("World movement per unit of finger travel. 1.2x covers the screen with short one-handed drags.")]
    [SerializeField] [Min(0.1f)] float sensitivityMultiplier = 1.2f;

    [Header("Bounds")]
    [SerializeField] Vector2 screenPadding = new Vector2(0.45f, 0.45f);
    [SerializeField] Camera worldCamera;

    Rigidbody2D _body;
    bool _isDragging;
    Vector2 _lastPointerWorld;
    Vector2 _targetPosition;
    float _baseFollowSpeed;
    float _moveSpeedMultiplier = 1f;
    float _emergencyFuelBonus;
    bool _emergencyFuelEnabled;

    /// <summary>True while the player is holding the pointer/finger to move.</summary>
    public bool IsControlling => _isDragging;

    /// <summary>Multiplies followSpeed (movement speed).</summary>
    public float MoveSpeedMultiplier => _moveSpeedMultiplier;

    void Awake()
    {
        TryGetComponent(out _body);

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        // Built-in triangle tip points toward +Y (top of the Game view). Keep Flip Y
        // off so the nose matches world up; Device Simulator must also be in Portrait.
        transform.localRotation = Quaternion.identity;
        if (TryGetComponent(out SpriteRenderer spriteRenderer))
        {
            spriteRenderer.flipY = false;
        }

        _baseFollowSpeed = Mathf.Max(0.01f, followSpeed);
        _targetPosition = transform.position;
        ApplyMovementMultipliers();
    }

    void OnDisable()
    {
        _isDragging = false;
    }

    void Update()
    {
        if (!TryGetPointerWorldPosition(out Vector2 pointerWorld))
        {
            _isDragging = false;
            return;
        }

        if (!_isDragging)
        {
            _isDragging = true;
            _lastPointerWorld = pointerWorld;
            _targetPosition = CurrentPosition();
        }

        Vector2 pointerDelta = pointerWorld - _lastPointerWorld;
        _lastPointerWorld = pointerWorld;
        _targetPosition = ClampToCamera(_targetPosition + pointerDelta * sensitivityMultiplier);

        if (_body == null)
        {
            MoveTowardsTarget(Time.deltaTime);
        }
    }

    void FixedUpdate()
    {
        if (_body == null)
        {
            return;
        }

        MoveTowardsTarget(Time.fixedDeltaTime);
    }

    /// <summary>Multiplies followSpeed — the ship's movement speed.</summary>
    public void SetMoveSpeedMultiplier(float multiplier)
    {
        _moveSpeedMultiplier = Mathf.Max(0.05f, multiplier);
        ApplyMovementMultipliers();
    }

    public void SetEmergencyFuelEnabled(bool enabled)
    {
        _emergencyFuelEnabled = enabled;
        if (!enabled)
        {
            _emergencyFuelBonus = 0f;
            ApplyMovementMultipliers();
        }
    }

    /// <summary>Bonus followSpeed multiplier from missing health (0 = full HP).</summary>
    public void SetEmergencyFuelBonus(float bonus)
    {
        _emergencyFuelBonus = Mathf.Max(0f, bonus);
        ApplyMovementMultipliers();
    }

    void ApplyMovementMultipliers()
    {
        float speedMul = _moveSpeedMultiplier * (1f + (_emergencyFuelEnabled ? _emergencyFuelBonus : 0f));
        followSpeed = _baseFollowSpeed * speedMul;
    }

    void MoveTowardsTarget(float deltaTime)
    {
        Vector2 nextPosition = Vector2.MoveTowards(CurrentPosition(), _targetPosition, followSpeed * deltaTime);

        if (_body != null)
        {
            _body.MovePosition(nextPosition);
            return;
        }

        transform.position = new Vector3(nextPosition.x, nextPosition.y, transform.position.z);
    }

    Vector2 CurrentPosition()
    {
        return _body != null ? _body.position : (Vector2)transform.position;
    }

    bool TryGetPointerWorldPosition(out Vector2 worldPosition)
    {
        worldPosition = default;

        if (!TryGetPointerScreenPosition(out Vector2 screenPosition) || worldCamera == null)
        {
            return false;
        }

        Vector3 world = worldCamera.ScreenToWorldPoint(screenPosition);
        worldPosition = world;
        return true;
    }

    static bool TryGetPointerScreenPosition(out Vector2 screenPosition)
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        screenPosition = default;
        return false;
    }

    Vector2 ClampToCamera(Vector2 position)
    {
        float verticalExtent = worldCamera.orthographicSize;
        float horizontalExtent = verticalExtent * worldCamera.aspect;
        Vector3 cameraPosition = worldCamera.transform.position;

        float minX = cameraPosition.x - horizontalExtent + screenPadding.x;
        float maxX = cameraPosition.x + horizontalExtent - screenPadding.x;
        float minY = cameraPosition.y - verticalExtent + screenPadding.y;
        float maxY = cameraPosition.y + verticalExtent - screenPadding.y;

        return new Vector2(
            Mathf.Clamp(position.x, minX, maxX),
            Mathf.Clamp(position.y, minY, maxY));
    }
}
