using UnityEngine;

/// <summary>
/// Brief translucent circle that shows an explosion area-of-effect.
/// </summary>
public class ExplosionPulse : MonoBehaviour
{
    [SerializeField] float lifetime = 0.28f;
    [SerializeField] float endScaleMultiplier = 1.15f;
    [SerializeField] Color color = new Color(1f, 0.55f, 0.2f, 0.45f);

    SpriteRenderer _renderer;
    float _startScale;
    float _age;
    static Sprite _circleSprite;
    static Texture2D _circleTexture;

    public static void Spawn(Vector3 worldPosition, float radius, Color? pulseColor = null)
    {
        if (radius <= 0f)
        {
            return;
        }

        var go = new GameObject("ExplosionPulse");
        go.transform.position = worldPosition;
        var pulse = go.AddComponent<ExplosionPulse>();
        if (pulseColor.HasValue)
        {
            pulse.color = pulseColor.Value;
        }

        pulse.Initialize(radius);
    }

    void Initialize(float radius)
    {
        _renderer = gameObject.AddComponent<SpriteRenderer>();
        _renderer.sprite = GetCircleSprite();
        _renderer.color = color;
        _renderer.sortingOrder = 40;

        _startScale = Mathf.Max(0.1f, radius * 2f);
        transform.localScale = Vector3.one * _startScale;
    }

    void Update()
    {
        if (_renderer == null)
        {
            Destroy(gameObject);
            return;
        }

        _age += Time.deltaTime;
        float t = Mathf.Clamp01(_age / Mathf.Max(0.01f, lifetime));
        float scale = Mathf.Lerp(_startScale, _startScale * endScaleMultiplier, t);
        transform.localScale = Vector3.one * scale;

        Color c = color;
        c.a = color.a * (1f - t);
        _renderer.color = c;

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
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
                    ? Mathf.Clamp01(1f - (distance / radius) * 0.15f)
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
        _circleSprite.name = "ExplosionPulseCircle";
        return _circleSprite;
    }
}
