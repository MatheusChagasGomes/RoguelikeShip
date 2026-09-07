using UnityEngine;

/// <summary>Shared circle sprite for short-lived VFX (comet tail, etc.).</summary>
public static class ExplosionPulseSprite
{
    static Sprite _circleSprite;
    static Texture2D _circleTexture;

    public static Sprite Circle
    {
        get
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
            _circleSprite.name = "SharedExplosionCircle";
            return _circleSprite;
        }
    }
}
