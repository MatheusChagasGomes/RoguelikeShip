using UnityEngine;

/// <summary>
/// Bluish translucent circle around the player while the force shield is ready.
/// </summary>
[DisallowMultipleComponent]
public class ForceShieldVisual : MonoBehaviour
{
    [SerializeField] PlayerForceShield forceShield;
    [SerializeField] float worldRadius = 0.85f;
    [SerializeField] Color shieldColor = new Color(0.35f, 0.75f, 1f, 0.35f);
    [SerializeField] int sortingOrder = 5;

    SpriteRenderer _renderer;
    static Sprite _circleSprite;
    static Texture2D _circleTexture;

    void Awake()
    {
        if (forceShield == null)
        {
            TryGetComponent(out forceShield);
            if (forceShield == null)
            {
                forceShield = GetComponentInParent<PlayerForceShield>();
            }
        }

        EnsureVisual();
        Refresh();
    }

    void OnEnable()
    {
        if (forceShield != null)
        {
            forceShield.Changed += Refresh;
        }

        Refresh();
    }

    void OnDisable()
    {
        if (forceShield != null)
        {
            forceShield.Changed -= Refresh;
        }
    }

    public void Bind(PlayerForceShield shield)
    {
        if (forceShield == shield)
        {
            return;
        }

        if (forceShield != null)
        {
            forceShield.Changed -= Refresh;
        }

        forceShield = shield;

        if (isActiveAndEnabled && forceShield != null)
        {
            forceShield.Changed += Refresh;
        }

        Refresh();
    }

    void Refresh()
    {
        if (_renderer == null)
        {
            return;
        }

        bool show = forceShield != null && forceShield.IsReady;
        _renderer.enabled = show;
        if (show)
        {
            _renderer.color = shieldColor;
        }
    }

    void EnsureVisual()
    {
        var child = transform.Find("ForceShieldVisual");
        GameObject visualObject;
        if (child == null)
        {
            visualObject = new GameObject("ForceShieldVisual");
            visualObject.transform.SetParent(transform, false);
            visualObject.transform.localPosition = Vector3.zero;
            visualObject.transform.localRotation = Quaternion.identity;
        }
        else
        {
            visualObject = child.gameObject;
        }

        if (!visualObject.TryGetComponent(out _renderer))
        {
            _renderer = visualObject.AddComponent<SpriteRenderer>();
        }

        _renderer.sprite = GetCircleSprite();
        _renderer.color = shieldColor;
        _renderer.sortingOrder = sortingOrder;
        float diameter = Mathf.Max(0.1f, worldRadius * 2f);
        visualObject.transform.localScale = new Vector3(diameter, diameter, 1f);
        _renderer.enabled = false;
    }

    static Sprite GetCircleSprite()
    {
        if (_circleSprite != null)
        {
            return _circleSprite;
        }

        const int size = 64;
        _circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        _circleTexture.filterMode = FilterMode.Bilinear;
        _circleTexture.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;
        float radius = center;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = distance <= radius
                    ? Mathf.Clamp01(1f - (distance / radius) * 0.25f)
                    : 0f;
                _circleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        _circleTexture.Apply(false, true);
        _circleSprite = Sprite.Create(
            _circleTexture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        _circleSprite.name = "ForceShieldCircle";
        return _circleSprite;
    }
}
