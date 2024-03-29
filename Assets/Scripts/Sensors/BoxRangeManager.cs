﻿using UnityEngine;

/// <summary>
/// This script allows to resize a box collider in realtime.
///
/// WARNING! when using this component, collider offset should be set only by this script. So
/// let collider component offset property alone and do not edit it through the inspector.
/// </summary>
[ExecuteAlways]
public class BoxRangeManager : MonoBehaviour
{
    private enum GrowDirection
    {   
        Symmetric,
        Up,
        Down,
        Left,
        Right  
    }
    
    [Header("WIRING:")]
    [SerializeField] private BoxCollider2D boxCollider;
    
    [Header("CONFIGURATION:")]
    [Tooltip("Initial offset for this sensor.")]
    [SerializeField] private Vector2 initialOffset;
    [Tooltip("Length for this sensor.")]
    [SerializeField] private float range;
    [Tooltip("Width for this sensor.")]
    [SerializeField] private float width;
    [Tooltip("Grow direction for this sensor when width or range is change")]
    [SerializeField] private GrowDirection growDirection;
    
    public float Range
    {
        get => range;
        set
        {
            range = value;
            SetBoxSize(width, value);
        }
    }

    public float Width {
        get => width;
        set
        {
            width = value;
            SetBoxSize(value, range);
        }
    }

    private Vector2 _currentSize;
    private const float OffsetBias = 0.5f;
    
    private Vector2 GetGrowOffsetVector()
    {
        switch (growDirection)
        {
            case GrowDirection.Symmetric:
                return new Vector2(0, 0);
            case GrowDirection.Up:
                return new Vector2(0, OffsetBias);
            case GrowDirection.Down:
                return new Vector2(0, -OffsetBias);
            case GrowDirection.Left:
                return new Vector2(-OffsetBias, 0);
            case GrowDirection.Right:
                return new Vector2(OffsetBias, 0);
            default:
                return new Vector2(0, 0);
        }
    }

    private Vector2 GetGrowVector(Vector2 currentSize, Vector2 newSize)
    {
        return newSize - currentSize;
    }
    
    private void Awake()
    {
        _currentSize = new Vector2(width, range);
    }
    
    private void Start()
    {
        boxCollider.offset = initialOffset;
        SetBoxSize(width, range);
    }
    
    private void SetBoxSize(float newWidth, float newRange)
    {
        Vector2 newSize = new Vector2(newWidth, newRange);
        if (newSize != _currentSize)
        {
            Vector2 growOffsetVector = GetGrowOffsetVector();
            Vector2 growVector = GetGrowVector(_currentSize, newSize);
            _currentSize = newSize;
            boxCollider.size = newSize;
            if (boxCollider.offset == Vector2.zero)
            {
                boxCollider.offset = initialOffset + growVector * growOffsetVector;
            }
            else
            {
                boxCollider.offset += growVector * growOffsetVector;
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SetBoxSize(width, range);
    }
#endif

}
