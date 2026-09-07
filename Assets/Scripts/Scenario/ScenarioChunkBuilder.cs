using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Builds scenario chunks procedurally at runtime: background + biome props,
/// with optional cloud piles used only on the horizontal seam between scenarios.
/// </summary>
[DisallowMultipleComponent]
public class ScenarioChunkBuilder : MonoBehaviour
{
    [Header("Art Pools")]
    [SerializeField] Sprite[] plateauSprites;
    [SerializeField] Sprite[] craterSprites;
    [SerializeField] Sprite[] starSprites;
    [SerializeField] Sprite[] cloudSprites;

    [Header("Planetary Density")]
    [SerializeField] Vector2Int plateauCount = new Vector2Int(4, 7);
    [SerializeField] Vector2Int craterCount = new Vector2Int(6, 11);
    [SerializeField] Vector2 plateauWorldWidth = new Vector2(0.38f, 0.95f);
    [SerializeField] Vector2 craterWorldWidth = new Vector2(0.22f, 0.65f);
    [SerializeField] [Min(0f)] float planetaryMinGap = 0.35f;

    [Header("Space Density")]
    [SerializeField] Vector2Int starCount = new Vector2Int(50, 85);
    [SerializeField] Vector2 starWorldWidth = new Vector2(0.03f, 0.14f);
    [SerializeField] [Min(0f)] float starMinGap = 0.08f;

    [Header("Cloud Seam (transition line only)")]
    [SerializeField] Vector2Int cloudsPerSeam = new Vector2Int(60, 85);
    [SerializeField] Vector2 cloudWorldWidth = new Vector2(2.2f, 4.8f);
    [SerializeField] [Min(0.02f)] float cloudSeamThickness = 2.0f;
    [SerializeField] [Min(1)] int cloudSeamRows = 7;

    [Header("Sorting")]
    [SerializeField] int backgroundOrderOffset = 0;
    [SerializeField] int craterOrderOffset = 1;
    [SerializeField] int plateauOrderOffset = 2;
    [SerializeField] int starOrderOffset = 1;
    [SerializeField] int cloudOrderOffset = 10;

    struct OccupiedSpot
    {
        public Vector2 position;
        public float radius;
    }

    Sprite _whiteSprite;
    Texture2D _whiteTexture;
    readonly Dictionary<int, Sprite> _gradientSprites = new();
    readonly Dictionary<int, Texture2D> _gradientTextures = new();
    readonly List<OccupiedSpot> _occupied = new();

    public void EnsureArtLoaded()
    {
        if ((plateauSprites == null || plateauSprites.Length == 0)
            || (craterSprites == null || craterSprites.Length == 0)
            || (starSprites == null || starSprites.Length == 0)
            || (cloudSprites == null || cloudSprites.Length == 0))
        {
            TryAutoAssignArt();
        }
    }

    /// <summary>
    /// Rebuilds background + biome decorations under <paramref name="chunkRoot"/>.
    /// Does not place clouds — use <see cref="BuildCloudSeam"/> on the scenario boundary.
    /// </summary>
    public void Build(
        Transform chunkRoot,
        ScenarioDefinition scenario,
        float halfWidth,
        float halfHeight,
        int baseSortingOrder)
    {
        if (chunkRoot == null || scenario == null)
        {
            return;
        }

        EnsureArtLoaded();
        EnsureWhiteSprite();
        ClearChildren(chunkRoot);

        var rng = new System.Random(scenario.seed);

        BuildBackground(chunkRoot, scenario, halfWidth, halfHeight, baseSortingOrder);

        _occupied.Clear();

        // Keep biome props well clear of the top seam where transition clouds sit,
        // so plateaus/craters don't overhang into the previous scenario.
        float contentHalfHeight = Mathf.Max(0.5f, halfHeight - cloudSeamThickness * 1.15f);

        if (scenario.kind == ScenarioKind.Planetary)
        {
            BuildPlanetary(chunkRoot, rng, halfWidth, contentHalfHeight, baseSortingOrder);
        }
        else
        {
            BuildSpace(chunkRoot, rng, halfWidth, contentHalfHeight, baseSortingOrder);
        }
    }

    /// <summary>
    /// Dense horizontal cloud pile on the dividing line between two scenarios.
    /// Clouds overlap freely and are packed across the full seam width.
    /// </summary>
    public void BuildCloudSeam(
        Transform seamRoot,
        int seed,
        float halfWidth,
        int baseSortingOrder)
    {
        if (seamRoot == null)
        {
            return;
        }

        EnsureArtLoaded();
        ClearChildren(seamRoot);

        var rng = new System.Random(seed);
        int count = RandomRange(rng, cloudsPerSeam);
        int rows = Mathf.Max(1, cloudSeamRows);
        float rowSpan = cloudSeamThickness;

        for (int i = 0; i < count; i++)
        {
            Sprite sprite = Pick(rng, cloudSprites);
            if (sprite == null)
            {
                continue;
            }

            float targetWidth = LerpRange(rng, cloudWorldWidth);
            // Spread across the full width with heavy overlap; stagger in a few rows.
            float rowT = rows <= 1 ? 0.5f : (i % rows) / (rows - 1f);
            float y = Mathf.Lerp(-rowSpan * 0.5f, rowSpan * 0.5f, rowT);
            y += LerpRange(rng, -rowSpan * 0.12f, rowSpan * 0.12f);

            Vector2 pos = new Vector2(
                LerpRange(rng, -halfWidth * 1.05f, halfWidth * 1.05f),
                y);

            // Art is authored vertically — rotate flat so the pile follows the seam line.
            var cloud = CreateSpriteChild(
                seamRoot,
                $"Cloud_{i}",
                sprite,
                pos,
                targetWidth,
                rotationDegrees: 90f,
                flipX: rng.NextDouble() < 0.5,
                sortingOrder: baseSortingOrder + cloudOrderOffset + i,
                color: Color.white);
            FitSpriteLongAxis(cloud, targetWidth);
        }
    }

    public void ReleaseRuntimeAssets()
    {
        foreach (var pair in _gradientSprites)
        {
            if (pair.Value != null)
            {
                Destroy(pair.Value);
            }
        }

        foreach (var pair in _gradientTextures)
        {
            if (pair.Value != null)
            {
                Destroy(pair.Value);
            }
        }

        _gradientSprites.Clear();
        _gradientTextures.Clear();

        if (_whiteSprite != null)
        {
            Destroy(_whiteSprite);
            _whiteSprite = null;
        }

        if (_whiteTexture != null)
        {
            Destroy(_whiteTexture);
            _whiteTexture = null;
        }
    }

    void BuildBackground(
        Transform chunkRoot,
        ScenarioDefinition scenario,
        float halfWidth,
        float halfHeight,
        int baseSortingOrder)
    {
        var go = new GameObject("Background");
        go.transform.SetParent(chunkRoot, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = baseSortingOrder + backgroundOrderOffset;

        if (scenario.useGradient)
        {
            renderer.sprite = GetOrCreateGradientSprite(scenario);
            renderer.color = Color.white;
        }
        else
        {
            renderer.sprite = _whiteSprite;
            renderer.color = scenario.color;
        }

        FitSpriteToSize(renderer, halfWidth * 2f, halfHeight * 2f);
    }

    void BuildPlanetary(Transform chunkRoot, System.Random rng, float halfWidth, float halfHeight, int baseSortingOrder)
    {
        int craters = RandomRange(rng, craterCount);
        for (int i = 0; i < craters; i++)
        {
            SpawnDecor(
                chunkRoot,
                "Crater",
                Pick(rng, craterSprites),
                rng,
                halfWidth,
                halfHeight,
                craterWorldWidth,
                planetaryMinGap,
                baseSortingOrder + craterOrderOffset,
                allowFlip: true);
        }

        int plateaus = RandomRange(rng, plateauCount);
        for (int i = 0; i < plateaus; i++)
        {
            SpawnDecor(
                chunkRoot,
                "Plateau",
                Pick(rng, plateauSprites),
                rng,
                halfWidth,
                halfHeight,
                plateauWorldWidth,
                planetaryMinGap,
                baseSortingOrder + plateauOrderOffset,
                allowFlip: true);
        }
    }

    void BuildSpace(Transform chunkRoot, System.Random rng, float halfWidth, float halfHeight, int baseSortingOrder)
    {
        int stars = RandomRange(rng, starCount);
        for (int i = 0; i < stars; i++)
        {
            SpawnDecor(
                chunkRoot,
                "Star",
                Pick(rng, starSprites),
                rng,
                halfWidth,
                halfHeight,
                starWorldWidth,
                starMinGap,
                baseSortingOrder + starOrderOffset,
                allowFlip: false,
                randomRotation: false);
        }
    }

    void SpawnDecor(
        Transform chunkRoot,
        string prefix,
        Sprite sprite,
        System.Random rng,
        float halfWidth,
        float halfHeight,
        Vector2 worldWidthRange,
        float minGap,
        int sortingOrder,
        bool allowFlip,
        bool randomRotation = true)
    {
        if (sprite == null)
        {
            return;
        }

        float targetWidth = LerpRange(rng, worldWidthRange);
        float radius = targetWidth * 0.5f;
        float marginX = Mathf.Max(0.1f, halfWidth - radius);
        float marginY = Mathf.Max(0.1f, halfHeight - radius);

        if (!TryFindFreePosition(rng, marginX, marginY, radius, minGap, out Vector2 pos))
        {
            return;
        }

        _occupied.Add(new OccupiedSpot { position = pos, radius = radius });

        CreateSpriteChild(
            chunkRoot,
            $"{prefix}_{_occupied.Count}",
            sprite,
            pos,
            targetWidth,
            rotationDegrees: randomRotation ? LerpRange(rng, 0f, 360f) : 0f,
            flipX: allowFlip && rng.NextDouble() < 0.5,
            sortingOrder: sortingOrder,
            color: Color.white);
    }

    bool TryFindFreePosition(
        System.Random rng,
        float marginX,
        float marginY,
        float radius,
        float minGap,
        out Vector2 pos)
    {
        const int maxAttempts = 40;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            pos = new Vector2(
                LerpRange(rng, -marginX, marginX),
                LerpRange(rng, -marginY, marginY));

            bool ok = true;
            for (int i = 0; i < _occupied.Count; i++)
            {
                float required = _occupied[i].radius + radius + minGap;
                if (Vector2.Distance(pos, _occupied[i].position) < required)
                {
                    ok = false;
                    break;
                }
            }

            if (ok)
            {
                return true;
            }
        }

        pos = default;
        return false;
    }

    static SpriteRenderer CreateSpriteChild(
        Transform parent,
        string objectName,
        Sprite sprite,
        Vector2 localPos,
        float targetWorldWidth,
        float rotationDegrees,
        bool flipX,
        int sortingOrder,
        Color color)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
        go.transform.localScale = Vector3.one;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        renderer.flipX = flipX;

        FitSpriteToWidth(renderer, targetWorldWidth);
        return renderer;
    }

    static void FitSpriteToWidth(SpriteRenderer renderer, float targetWorldWidth)
    {
        if (renderer == null || renderer.sprite == null || targetWorldWidth <= 0f)
        {
            return;
        }

        float nativeWidth = renderer.sprite.bounds.size.x;
        if (nativeWidth <= 0.0001f)
        {
            return;
        }

        float scale = targetWorldWidth / nativeWidth;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    static void FitSpriteLongAxis(SpriteRenderer renderer, float targetWorldSize)
    {
        if (renderer == null || renderer.sprite == null || targetWorldSize <= 0f)
        {
            return;
        }

        Vector2 native = renderer.sprite.bounds.size;
        float longAxis = Mathf.Max(native.x, native.y);
        if (longAxis <= 0.0001f)
        {
            return;
        }

        float scale = targetWorldSize / longAxis;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    static void FitSpriteToSize(SpriteRenderer renderer, float worldWidth, float worldHeight)
    {
        if (renderer == null || renderer.sprite == null)
        {
            return;
        }

        Vector2 native = renderer.sprite.bounds.size;
        if (native.x <= 0.0001f || native.y <= 0.0001f)
        {
            return;
        }

        renderer.transform.localScale = new Vector3(worldWidth / native.x, worldHeight / native.y, 1f);
    }

    Sprite GetOrCreateGradientSprite(ScenarioDefinition scenario)
    {
        int key = scenario.seed;
        if (_gradientSprites.TryGetValue(key, out Sprite existing) && existing != null)
        {
            return existing;
        }

        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = $"ScenarioGradient_{scenario.seed}",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        for (int x = 0; x < size; x++)
        {
            float t = x / (size - 1f);
            Color pixelColor = Color.Lerp(scenario.gradientColor, scenario.color, t);
            for (int y = 0; y < size; y++)
            {
                texture.SetPixel(x, y, pixelColor);
            }
        }

        texture.Apply(false, true);

        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        sprite.name = texture.name;

        _gradientTextures[key] = texture;
        _gradientSprites[key] = sprite;
        return sprite;
    }

    void EnsureWhiteSprite()
    {
        if (_whiteSprite != null)
        {
            return;
        }

        _whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        _whiteTexture.name = "ScenarioChunkWhiteTexture";
        _whiteTexture.SetPixel(0, 0, Color.white);
        _whiteTexture.Apply(false, true);

        _whiteSprite = Sprite.Create(
            _whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        _whiteSprite.name = "ScenarioChunkWhiteSprite";
    }

    static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying)
            {
                Object.Destroy(child);
            }
            else
            {
                Object.DestroyImmediate(child);
            }
        }
    }

    static Sprite Pick(System.Random rng, Sprite[] pool)
    {
        if (pool == null || pool.Length == 0)
        {
            return null;
        }

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Sprite sprite = pool[rng.Next(0, pool.Length)];
            if (sprite != null)
            {
                return sprite;
            }
        }

        return null;
    }

    static int RandomRange(System.Random rng, Vector2Int range)
    {
        int min = Mathf.Min(range.x, range.y);
        int max = Mathf.Max(range.x, range.y);
        return rng.Next(min, max + 1);
    }

    static float LerpRange(System.Random rng, Vector2 range)
    {
        return LerpRange(rng, range.x, range.y);
    }

    static float LerpRange(System.Random rng, float a, float b)
    {
        float min = Mathf.Min(a, b);
        float max = Mathf.Max(a, b);
        return min + (float)rng.NextDouble() * (max - min);
    }

    void TryAutoAssignArt()
    {
#if UNITY_EDITOR
        if (plateauSprites == null || plateauSprites.Length == 0)
        {
            plateauSprites = LoadSpritesAt("Assets/Art/Plateaus");
        }

        if (craterSprites == null || craterSprites.Length == 0)
        {
            craterSprites = LoadSpritesAt("Assets/Art/Craters");
        }

        if (starSprites == null || starSprites.Length == 0)
        {
            starSprites = LoadSpritesAt("Assets/Art/Stars");
        }

        if (cloudSprites == null || cloudSprites.Length == 0)
        {
            cloudSprites = LoadSpritesAt("Assets/Art/Clouds");
        }
#endif
    }

#if UNITY_EDITOR
    static Sprite[] LoadSpritesAt(string folder)
    {
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
        var list = new List<Sprite>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                list.Add(sprite);
            }
        }

        return list.ToArray();
    }

    void OnValidate()
    {
        plateauCount.x = Mathf.Max(0, plateauCount.x);
        plateauCount.y = Mathf.Max(plateauCount.x, plateauCount.y);
        craterCount.x = Mathf.Max(0, craterCount.x);
        craterCount.y = Mathf.Max(craterCount.x, craterCount.y);
        starCount.x = Mathf.Max(0, starCount.x);
        starCount.y = Mathf.Max(starCount.x, starCount.y);
        cloudsPerSeam.x = Mathf.Max(0, cloudsPerSeam.x);
        cloudsPerSeam.y = Mathf.Max(cloudsPerSeam.x, cloudsPerSeam.y);
        planetaryMinGap = Mathf.Max(0f, planetaryMinGap);
        starMinGap = Mathf.Max(0f, starMinGap);
        cloudSeamThickness = Mathf.Max(0.02f, cloudSeamThickness);
        cloudSeamRows = Mathf.Max(1, cloudSeamRows);

        TryAutoAssignArt();
    }
#endif
}
