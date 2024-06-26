﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// An array of ray sensors placed over a circular sector.
///
/// The sector is placed around the local Y axis, so forward direction for this sensor
/// is the local UP direction.
///
/// Be aware, that Unity cannot instance object while in prefab mode. So when this
/// script detects that you are in prefab mode it will just populate a list of placements shown
/// with gizmos, but it won't instance any sensor until this prefab is placed in the scene.
/// </summary>
[ExecuteAlways]
public class Whiskers : MonoBehaviour
{
    /// <summary>
    /// A class wrapping a list of ray sensors to make it easier to search for them.
    /// </summary>
    private class RaySensorList : IEnumerable<RaySensor>
    {
        // It should have 2N + 3 sensors.
        // Think in this array of sensors as looking to UP direction,
        // Inside this list:
        //  * Top left sensor is always at index 0.
        //  * Center sensor is always at the middle index.
        //  * Top right sensor is always at the end index.
        private List<RaySensor> _raySensors;
        
        /// <summary>
        /// Current amount of sensors in this list.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// Get the center sensor.
        /// </summary>
        public RaySensor CenterSensor => _raySensors[Count / 2];
        
        /// <summary>
        /// Get the leftmost sensor (assuming whiskers locally looks to UP direction).
        /// </summary>
        public RaySensor LeftMostSensor => _raySensors[0];
        
        /// <summary>
        /// Get the rightmost sensor (assuming whiskers locally looks to UP direction).
        /// </summary>
        public RaySensor RightMostSensor => _raySensors[Count - 1];
        
        /// <summary>
        /// Get the sensor at the given index counting from the leftmost sensor to center.
        /// </summary>
        /// <param name="index">0 index is the leftmost sensor</param>
        /// <returns></returns>
        public RaySensor GetSensorFromLeft(int index) => _raySensors[index];
        
        /// <summary>
        ///  Get the sensor at the given index counting from the rightmost sensor to center.
        /// </summary>
        /// <param name="index">0 index is the rightmost sensor</param>
        /// <returns></returns>
        public RaySensor GetSensorFromRight(int index) => _raySensors[Count - 1 - index];

        public RaySensorList(List<RaySensor> raySensors)
        {
            _raySensors = raySensors;
            Count = _raySensors.Count;
        }

        /// <summary>
        /// Remove every sensor in the list.
        ///
        /// This method leaves list empty.
        /// </summary>
        public void Clear()
        {
            foreach (RaySensor sensor in _raySensors)
            {
#if UNITY_EDITOR
                if (sensor != null) DestroyImmediate(sensor.gameObject);
#else
                if (sensor != null) Destroy(sensor.gameObject);
#endif
            }
            _raySensors?.Clear();
            Count = 0;
        }

        public IEnumerator<RaySensor> GetEnumerator()
        {
            return _raySensors.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
    
    /// <summary>
    /// Struct to represent ray ends for every sensor in prefab local space.
    /// </summary>
    [Serializable]
    public struct RayEnds
    {
        public Vector3 start;
        public Vector3 end;
    }
    
    [Header("CONFIGURATION:")]
    [Tooltip("Ray sensor to instance.")]
    [SerializeField] private GameObject sensor;
    [Tooltip("Layers to be detected by this sensor.")] 
    [SerializeField] private LayerMask layerMask;
    [Tooltip("Number of rays for this sensor: (sensorResolution * 2) + 3")]
    [Range(0.0f, 10.0f)]
    [SerializeField] private int sensorResolution = 1;
    [Tooltip("Angular width in degrees for this sensor.")]
    [Range(0.0f, 180.0f)]
    [SerializeField] private float semiConeDegrees = 45.0f;
    [Tooltip("Maximum range for these rays.")]
    [SerializeField] private float range = 1.0f;
    [Tooltip("Minimum range for these rays. Useful to make rays start not at the agent's center.")]
    [SerializeField] private float minimumRange = 0.2f;
    [Tooltip("Range proportion for whiskers at left side. 0.0 = leftmost sensor, 1.0 = center sensor.")]
    [SerializeField] private AnimationCurve leftRangeSemiCone = AnimationCurve.EaseInOut(
        0.0f, 
        0.2f, 
        1.0f, 
        1.0f);
    [Tooltip("Range proportion for whiskers at right side. 0.0 = center sensor, 1.0 = rightmost sensor.")]
    [SerializeField] private AnimationCurve rightRangeSemiCone = AnimationCurve.EaseInOut(
        0.0f,
        1.0f,
        1.0f,
        0.2f);
    [Space]
    [Tooltip("Event to trigger when a collider is detected by this sensor.")]    
    [SerializeField] private UnityEvent<Collider2D> colliderDetected;
    [Tooltip("Event to trigger when no collider is detected by this sensor.")]
    [SerializeField] private UnityEvent noColliderDetected;
    [Header("DEBUG")]
    [Tooltip("Whether to show gizmos for sensors.")]
    [SerializeField] private bool showGizmos = true;
    [Tooltip("Color for this scripts gizmos.")]
    [SerializeField] private Color gizmoColor = Color.yellow;

    /// <summary>
    /// This sensor layer mask.
    /// </summary>
    public LayerMask SensorsLayerMask
    {
        get => layerMask;
        set
        {
            layerMask = value;
            foreach (RaySensor raySensor in _sensors)
            {
                raySensor.SensorLayerMask = value;
            }
        }
    }
    
    /// <summary>
    /// Number of rays for this sensor: (sensorResolution * 2) + 3
    /// </summary>
    public int SensorResolution
    {
        get => sensorResolution;
        set
        {
            sensorResolution = value;
            _onValidationUpdatePending = true;
        }
    }

    /// <summary>
    /// Angular width in degrees for this sensor.
    /// </summary>
    public float SemiConeDegrees
    {
        get => semiConeDegrees;
        set
        {
            semiConeDegrees = value;
            _onValidationUpdatePending = true;
        }
    }

    /// <summary>
    /// Maximum range for these rays.
    /// </summary>
    public float Range
    {
        get => range; 
        set
        {
            range = value;
            _onValidationUpdatePending = true;
        }
    }

    /// <summary>
    /// Minimum range for these rays. Useful to make rays start not at the agent's center.
    /// </summary>
    public float MinimumRange
    {
        get => minimumRange;
        set
        {
            minimumRange = value;
            _onValidationUpdatePending = true;
        }
    }

    /// <summary>
    /// Number of rays for this sensor with given resolution.
    /// </summary>
    public int SensorAmount => (sensorResolution * 2) + 3;

    /// <summary>
    /// Whether this sensor detects any collider.
    /// </summary>
    public bool IsAnyColliderDetected
    {
        get
        {
            if (_sensors == null) return false;
            foreach (RaySensor sensor in _sensors)
            {
                if (sensor.IsColliderDetected) return true;
            }
            return false;
        }
    }
    
    /// <summary>
    /// Set of detected colliders.
    /// </summary>
    public HashSet<Collider2D> DetectedColliders
    {
        get
        {
            var detectedColliders = new HashSet<Collider2D>();
            foreach (RaySensor sensor in _sensors)
            {
                if (sensor.IsColliderDetected) detectedColliders.Add(sensor.DetectedCollider);
            }
            return detectedColliders;
        } 
    }

    /// <summary>
    /// List of detected hits.
    ///
    /// It's got as a list of tuples of (hit, detecting sensor index).
    /// </summary>
    public List<(RaycastHit2D, int)> DetectedHits
    {
        get
        {
            var detectedHits = new List<(RaycastHit2D, int)>();
            int sensorIndex = 0;
            foreach (RaySensor sensor in _sensors)
            {
                if (sensor.IsColliderDetected) detectedHits.Add((sensor.DetectedHit, sensorIndex));
                sensorIndex++;
            }
            return detectedHits;
        }
    }
    
    private RaySensorList _sensors;
    public List<RayEnds> _rayEnds;

    private bool _onValidationUpdatePending = false;

    private void Start()
    {
#if UNITY_EDITOR
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            // Script is executing in prefab mode
            // Nothing done at the moment.
        }
        else
        {
            // Script is executing in edit mode (but not in prefab mode, so in scene mode)
            // OR script is executing in Play mode
            UpdateSensorsPlacement();
            SubscribeToSensorsEvents();
            UpdateGizmosConfiguration();
        }
#else
        UpdateSensorPlacement();
        SubscribeToSensorsEvents();
        UpdateGizmosConfiguration();
#endif
    }

    private void OnEnable()
    {
        // OnEnable runs before Start, so first time OnEnable is called, sensors are not
        // initialized. That's why I call to SubscribeToSensorsEvents in Start. Nevertheless
        // I call it here too just in case object is disabled and then enabled again.
        SubscribeToSensorsEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromSensorsEvents();
    }
    
    /// <summary>
    /// When a value changes on Inspector, update prefab appearance.
    /// </summary>
    private void LateUpdate()
    {
        if (_onValidationUpdatePending)
        {
            UpdateSensorsPlacement();
            SubscribeToSensorsEvents();
            UpdateGizmosConfiguration();
            _onValidationUpdatePending = false;
        }
    }
    
    /// <summary>
    /// Bind a listener to the colliderDetected event.
    /// </summary>
    /// <param name="action">Method to bind.</param>
    public void SubscribeToColliderDetected(UnityAction<Collider2D> action)
    {
        colliderDetected.AddListener(action);
    }
    
    /// <summary>
    /// Unbind a listener from the colliderDetected event.
    /// </summary>
    /// <param name="action">Method to unbind.</param>
    public void UnsubscribeFromColliderDetected(UnityAction<Collider2D> action)
    {
        colliderDetected.RemoveListener(action);
    }
    
    /// <summary>
    /// Bind a listener to the noColliderDetected event.
    /// </summary>
    /// <param name="action">Method to bind.</param>
    public void SubscribeToNoColliderDetected(UnityAction action)
    {
        noColliderDetected.AddListener(action);
    }
    
    /// <summary>
    /// Unbind a listener from the noColliderDetected event.
    /// </summary>
    /// <param name="action">Method to unbind.</param>
    public void UnsubscribeFromNoColliderDetected(UnityAction action)
    {
        noColliderDetected.RemoveListener(action);
    }

    /// <summary>
    /// Called when a collider is detected.
    /// </summary>
    /// <param name="collider">Collided detected by sensor.</param>
    public void OnColliderDetected(Collider2D collider)
    {
        if (colliderDetected != null) colliderDetected.Invoke(collider);
    }
    
    /// <summary>
    /// Called when no collider is detected.
    /// </summary>
    public void OnColliderNoLongerDetected()
    {
        if (DetectedColliders.Count == 0) noColliderDetected.Invoke();
    }

    /// <summary>
    /// Create a new list of sensors and place them.
    /// </summary>
    private void SetupSensors()
    {
        PopulateSensors();
        PlaceSensors();
        SetSensorsLayerMask();
    }

    /// <summary>
    /// Create a new list of sensors.
    /// </summary>
    private void PopulateSensors()
    {
        _sensors?.Clear();
        ClearStraggledSensors();
        List<RaySensor> raySensors = new List<RaySensor>();
        for (int i = 0; i < SensorAmount; i++)
        {
            GameObject sensorInstance = Instantiate(sensor, transform);
            raySensors.Add(sensorInstance.GetComponent<RaySensor>());
        }
        _sensors = new RaySensorList(raySensors);
    }

    /// <summary>
    /// Subscribe to sensors events.
    /// </summary>
    private void SubscribeToSensorsEvents()
    {
        if (_sensors == null) return;
        foreach (RaySensor raySensor in _sensors)
        {
            raySensor.SubscribeToColliderDetected(OnColliderDetected);
            raySensor.SubscribeToNoColliderDetected(OnColliderNoLongerDetected);
        }
    }

    /// <summary>
    /// Unsubscribe from sensors events.
    /// </summary>
    private void UnsubscribeFromSensorsEvents()
    {
        if (_sensors == null) return;
        foreach (RaySensor raySensor in _sensors)
        {
            raySensor.UnsubscribeFromColliderDetected(OnColliderDetected);
            raySensor.UnsubscribeFromNoColliderDetected(OnColliderNoLongerDetected);
        }
    }

    /// <summary>
    /// Set layer mask for sensors.
    /// </summary>
    private void SetSensorsLayerMask()
    {
        SensorsLayerMask = layerMask;
    }

    /// <summary>
    /// <p>Destroy any child rays sensor that may be left behind after clearing the sensor list.</p>
    /// <br/>
    /// <p>When domain reloading the child sensor objects are not destroyed although
    /// list is cleared. So I need to search and destroy for child sensors manually.</p>
    /// </summary>
    private void ClearStraggledSensors()
    {
        RaySensor[] childSensors = GetComponentsInChildren<RaySensor>();
        foreach (RaySensor raySensor in childSensors)
        {
#if UNITY_EDITOR
            if (raySensor != null) DestroyImmediate(raySensor.gameObject);
#else
            if (raySensor != null) Destroy(raySensor.gameObject);
#endif
        }
    }
    

    /// <summary>
    /// Place sensors in the correct positions for current resolution and current range sector
    /// </summary>
    private void PlaceSensors()
    {
        List<RayEnds> rayEnds = _rayEnds;

        int i = 0;
        foreach (RayEnds rayEnd in rayEnds)
        {
            _sensors.GetSensorFromLeft(i).SetRayOrigin(transform.TransformPoint(rayEnd.start));
            _sensors.GetSensorFromLeft(i).SetRayTarget(transform.TransformPoint(rayEnd.end));
            i++;
        }
    }

    /// <summary>
    /// If in prefab mode then recalculate sensor ends positions. If in scene or play mode
    /// then it recalculates sensor ends positions and instantiate sensors in those positions.
    /// </summary>
    private void UpdateSensorsPlacement()
    {
#if UNITY_EDITOR
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            // Script is executing in prefab mode
            PopulateRayEnds();
        }
        else
        {
            // Script is executing in edit mode (but not necessarily prefab mode)
            // OR script is executing in Play mode
            PopulateRayEnds();
            SetupSensors();
        }
#else
        PopulateRayEnds();
        SetupSensors();
#endif
    }
    
    /// <summary>
    /// Calculate local positions for sensor ends and store them to be serialized along the prefab.
    /// </summary>
    /// <returns>New list for sensor ends local positions.</returns>
    private List<RayEnds> GetRayEnds()
    {
        List<RayEnds> rayEnds = new List<RayEnds>();
        
        if (transform == null) return rayEnds;
        
        float totalPlacementAngle = semiConeDegrees * 2;
        float placementAngleInterval = totalPlacementAngle / (SensorAmount - 1);
        
        // Remember: local forward is UP direction in local space.
        Vector3 forwardSensorPlacement = Vector3.up * minimumRange;

        for (int i = 0; i < SensorAmount; i++)
        {
            float currentAngle = semiConeDegrees - (placementAngleInterval * i);
            Vector3 placementVector = Quaternion.AngleAxis(currentAngle, Vector3.forward) * forwardSensorPlacement;
            // Vector3 placementVectorEnd = placementVector.normalized * range;
            Vector3 placementVectorEnd = placementVector.normalized * 
                                         (minimumRange + GetSensorLength(i));
            
            Vector3 sensorStart = placementVector;
            Vector3 sensorEnd = placementVectorEnd;
            
            RayEnds newRayEnds = new RayEnds();
            newRayEnds.start = sensorStart;
            newRayEnds.end = sensorEnd;
            rayEnds.Add(newRayEnds);
        }
        return rayEnds;
    }
    
    /// <summary>
    /// Whether this index is the one of the center sensor.
    /// </summary>
    /// <param name="index">Sensor index</param>
    /// <returns>True if center sensor has this index.</returns>
    public bool IsCenterSensor(int index)=> index == SensorAmount / 2;
    
    /// <summary>
    /// Calculates and returns the length of a sensor based on the sensor index provided.
    ///
    /// It uses index to use the proper proportion curve for left and right side.
    /// </summary>
    /// <param name="sensorIndex">Index of this sensor</param>
    /// <returns>This sensor length from minimum range.</returns>
    public float GetSensorLength(int sensorIndex)
    {
        int middleSensorIndex = SensorAmount / 2;
        if (sensorIndex < middleSensorIndex)
        {
            return GetLeftSensorLength(sensorIndex, middleSensorIndex);
        }
        return GetRightSensorLength(sensorIndex, middleSensorIndex);
    }

    /// <summary>
    /// Calculate the length of the left sensor based on the sensor index using left range semi cone
    /// curve.
    /// </summary>
    /// <param name="sensorIndex">Index of this sensor.</param>
    /// <param name="middleSensorIndex">Middle sensor index.</param>
    /// <returns>This sensor length from minimum range.</returns>
    private float GetLeftSensorLength(int sensorIndex, int middleSensorIndex)
    {
        float curvePoint = Mathf.InverseLerp(0, middleSensorIndex, sensorIndex);
        float curvePointRange = leftRangeSemiCone.Evaluate(curvePoint) * (range-minimumRange);
        return curvePointRange;
    }
    
    /// <summary>
    /// Calculate the length of the right sensor based on the sensor index using right range semi cone
    /// curve.
    /// </summary>
    /// <param name="sensorIndex">Index of this sensor.</param>
    /// <param name="middleSensorIndex">Middle sensor index.</param>
    /// <returns>This sensor length from minimum range.</returns>
    private float GetRightSensorLength(int sensorIndex, int middleSensorIndex)
    {
        float curvePoint = Mathf.InverseLerp( middleSensorIndex, SensorAmount-1, sensorIndex);
        float curvePointRange = rightRangeSemiCone.Evaluate(curvePoint) * (range-minimumRange);
        return curvePointRange;
    }
    
    /// <summary>
    /// Update gizmos configuration of every sensor in the list.
    /// </summary>
    private void UpdateGizmosConfiguration()
    {
        if (_sensors == null) return;
        foreach (RaySensor raySensor in _sensors)
        {
            raySensor.ShowGizmos = showGizmos;
        }
    }

    /// <summary>
    /// Calculate sensor ends and store them to be serialized along the prefab.
    /// </summary>
    private void PopulateRayEnds()
    {
        _rayEnds = GetRayEnds();
    }
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        // Remember: You cannot create objects from OnValidate(). So just mark the update as pending
        // and create those objects from LateUpdate().
        _onValidationUpdatePending = true;
    }
    
    private void OnDrawGizmos()
    {
        // Draw gizmos only if in prefab mode. If in scene or play mode the gizmos
        // I'm interested in are those drawn from the sensors.
        if (showGizmos && _rayEnds != null && PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Gizmos.color = gizmoColor;
            foreach (RayEnds rayEnds in _rayEnds)
            {
                Gizmos.DrawLine(transform.TransformPoint(rayEnds.start), transform.TransformPoint(rayEnds.end));
            }
        }
    }
#endif
}
