using UnityEngine;

/// <summary>
/// Locks the orthographic camera to a fixed world width (Fit to Width).
/// Taller portrait aspects (18:9, 19.5:9, 20:9) reveal extra height; width stays the same as 16:9.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(-100)]
public class CameraFitToWidth : MonoBehaviour
{
    [SerializeField]
    [Min(0.01f)]
    [Tooltip("Visible world width in units. 5.625 matches Unity's default ortho size 5 on a 9:16 (16:9 portrait) screen.")]
    float targetWorldWidth = 5.625f;

    Camera _camera;
    int _lastPixelWidth;
    int _lastPixelHeight;
    float _lastTargetWidth;

    void OnEnable()
    {
        CacheCamera();
        Apply();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.update -= EditorApplyIfNeeded;
            UnityEditor.EditorApplication.update += EditorApplyIfNeeded;
        }
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorApplyIfNeeded;
#endif
    }

    void Awake()
    {
        CacheCamera();
        Apply();
    }

    void LateUpdate()
    {
        ApplyIfNeeded();
    }

    void OnValidate()
    {
        CacheCamera();
        Apply();
    }

    void CacheCamera()
    {
        if (_camera == null)
        {
            TryGetComponent(out _camera);
        }
    }

    void ApplyIfNeeded()
    {
        if (_camera == null)
        {
            return;
        }

        if (_camera.pixelWidth == _lastPixelWidth
            && _camera.pixelHeight == _lastPixelHeight
            && Mathf.Approximately(targetWorldWidth, _lastTargetWidth))
        {
            return;
        }

        Apply();
    }

    void Apply()
    {
        CacheCamera();

        if (_camera == null || targetWorldWidth <= 0f)
        {
            return;
        }

        int pixelWidth = Mathf.Max(1, _camera.pixelWidth);
        int pixelHeight = Mathf.Max(1, _camera.pixelHeight);
        float aspect = (float)pixelWidth / pixelHeight;

        _camera.orthographic = true;
        _camera.orthographicSize = targetWorldWidth / (2f * aspect);

        _lastPixelWidth = pixelWidth;
        _lastPixelHeight = pixelHeight;
        _lastTargetWidth = targetWorldWidth;
    }

#if UNITY_EDITOR
    void EditorApplyIfNeeded()
    {
        if (this == null || Application.isPlaying)
        {
            return;
        }

        ApplyIfNeeded();
    }
#endif
}
