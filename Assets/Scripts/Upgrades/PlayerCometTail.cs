using UnityEngine;

/// <summary>
/// Leaves damaging fire segments along the player's path.
/// </summary>
[DisallowMultipleComponent]
public class PlayerCometTail : MonoBehaviour
{
    [SerializeField] Transform trackedTransform;
    [SerializeField] [Min(0f)] float segmentRadius = 0.55f;
    [SerializeField] [Min(0)] int segmentDamage = 1;
    [SerializeField] [Min(0.05f)] float segmentSpacing = 0.22f;

    Vector2 _lastSegmentPosition;
    bool _hasLastSegment;

    void Awake()
    {
        if (trackedTransform == null)
        {
            trackedTransform = transform;
        }
    }

    public void Configure(Transform target, float radius, int damage, float spacing)
    {
        trackedTransform = target != null ? target : transform;
        segmentRadius = Mathf.Max(0f, radius);
        segmentDamage = Mathf.Max(0, damage);
        segmentSpacing = Mathf.Max(0.05f, spacing);
        _hasLastSegment = false;
    }

    void Update()
    {
        if (trackedTransform == null || segmentRadius <= 0f || segmentDamage <= 0)
        {
            return;
        }

        Vector2 current = trackedTransform.position;
        if (!_hasLastSegment)
        {
            _lastSegmentPosition = current;
            _hasLastSegment = true;
            SpawnSegment(current);
            return;
        }

        if ((current - _lastSegmentPosition).sqrMagnitude < segmentSpacing * segmentSpacing)
        {
            return;
        }

        _lastSegmentPosition = current;
        SpawnSegment(current);
    }

    void SpawnSegment(Vector2 position)
    {
        var segmentObject = new GameObject("CometTailSegment");
        segmentObject.transform.position = position;
        var segment = segmentObject.AddComponent<CometTailSegment>();
        segment.Initialize(segmentRadius, segmentDamage);
    }
}
