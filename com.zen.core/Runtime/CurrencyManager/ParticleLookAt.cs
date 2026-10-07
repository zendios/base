using System;
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(ParticleSystem))]
public class ParticleLookAt : MonoBehaviour
{
    #region Serialize Fields
    [Header("Target Configuration")]
    [SerializeField]
    private Transform _target;

    [Tooltip("Enable this if the target is a UI element in a 2D Canvas.")]
    [SerializeField]
    private bool _isUIMode = false;

    [Tooltip("If true, the Z-axis is ignored during movement and rotation (Crucial for flat 2D/UI).")]
    [SerializeField]
    private bool _shouldIgnoreZ = false;

    [Header("Movement & Rotation Settings")]
    [Tooltip("How fast the particles rotate towards the target.")]
    [SerializeField]
    private float _drift = 10f;

    [SerializeField]
    private float _deltaAngle = 90f;

    [Tooltip("The proportion of the particle's lifetime (from the end) during which it will home in on the target. (e.g., 0.6 means the last 60% of its lifetime).")]
    [Range(0.1f, 1f)]
    [SerializeField]
    private float _lockAtTime = 0.5f;
    
    [SerializeField]
    private float _distanceToKill = 0.01f;

    [SerializeField]
    private bool _isRotationEnabled = false;
    #endregion

    #region Private Fields
    private ParticleSystem _particleSystem;
    private ParticleSystem.MainModule _mainModule;
    private ParticleSystem.EmissionModule _emissionModule;
    private ParticleSystem.Particle[] _particles;

    private float _sqrDistanceToKill;
    private Canvas _parentCanvas;
    private Camera _cachedCamera;
    private int _numParticlesAlive;
    #endregion

    #region Public Properties
    public Transform Target
    {
        get
        {
            return _target;
        }
        set
        {
            SetTargetTransform(value);
        }
    }

    public float StartLifetime
    {
        get
        {
            return _mainModule.startLifetime.constant;
        }
        set
        {
            ParticleSystem.MinMaxCurve curve = _mainModule.startLifetime;
            curve.constant = value;
            _mainModule.startLifetime = curve;
        }
    }
    #endregion

    #region Events
    public event Action<int> OnTarget;
    public event Action<int> OnEmitDone;
    #endregion

    #region Unity Lifecycle
    private void OnEnable()
    {
        InitializeSystem();
    }

    private void LateUpdate()
    {
        if (_target == null || _particleSystem == null || _particleSystem.isStopped)
        {
            return;
        }

        Vector3 targetWorldPos = ResolveTargetPosition();
        bool isWorldSpace = _mainModule.simulationSpace == ParticleSystemSimulationSpace.World;
        Vector3 targetPosToUse = isWorldSpace ? targetWorldPos : transform.InverseTransformPoint(targetWorldPos);

        _numParticlesAlive = _particleSystem.GetParticles(_particles);
        bool hasChanges = false;
        bool hasTargetReachedThisFrame = false;

        float lerpFactor = Mathf.Clamp01(Time.deltaTime * _drift);

        for (int i = 0; i < _numParticlesAlive; i++)
        {
            float maxLifetime = _particles[i].startLifetime;
            float lockDuration = _lockAtTime * maxLifetime;
            float remainingLifetime = _particles[i].remainingLifetime;

            // Homing Phase
            if (remainingLifetime < lockDuration && lockDuration > 0f)
            {
                hasChanges = true;

                // 1. Instantly zero out internal physical forces to prevent fighting with manual translation
                _particles[i].velocity = Vector3.zero;

                // 2. Perform frame-rate independent Ease-Out positioning towards target
                Vector3 currentParticlePos = _particles[i].position;
                Vector3 nextPos = Vector3.Lerp(currentParticlePos, targetPosToUse, lerpFactor);

                if (_shouldIgnoreZ)
                {
                    nextPos.z = currentParticlePos.z;
                }

                _particles[i].position = nextPos;

                // 3. Smooth Rotation toward target direction
                if (_isRotationEnabled)
                {
                    Vector3 direction = targetPosToUse - _particles[i].position;
                    if (_shouldIgnoreZ)
                    {
                        direction.z = 0f;
                    }

                    if (direction.sqrMagnitude > 0.0001f)
                    {
                        float targetAngle = Mathf.Atan2(-direction.y, direction.x) * Mathf.Rad2Deg + _deltaAngle;
                        _particles[i].rotation = Mathf.Lerp(_particles[i].rotation, targetAngle, lerpFactor);
                    }
                }

                // 4. Distance Kill Check using optimized squared magnitude
                Vector3 toTargetVec = targetPosToUse - _particles[i].position;
                if (_shouldIgnoreZ)
                {
                    toTargetVec.z = 0f;
                }

                if (toTargetVec.sqrMagnitude <= _sqrDistanceToKill)
                {
                    _particles[i].remainingLifetime = 0f;
                    hasTargetReachedThisFrame = true;
                }
            }
        }

        // Apply all modifications back in a single batched native call to save C++ interop overhead
        if (hasChanges)
        {
            _particleSystem.SetParticles(_particles, _numParticlesAlive);
        }

        // Securely invoke performance-safe event notifications
        if (hasTargetReachedThisFrame)
        {
            OnTarget?.Invoke(0);
        }

        if (_numParticlesAlive > 0 && _particleSystem.particleCount == 0)
        {
            OnEmitDone?.Invoke(0);
        }
    }
    #endregion

    #region Public API
    public void Emit(Transform start, Transform end = null, int burstCount = 16, bool lookAtTarget = true)
    {
        if (lookAtTarget && end != null)
        {
            transform.LookAt(end, Vector3.up);
        }

        SetTransformParticle(start);
        SetTargetTransform(end);

        // Safe burst setting protecting against index out of bounds on the emission module
        if (_emissionModule.burstCount > 0)
        {
            ParticleSystem.Burst burst = _emissionModule.GetBurst(0);
            burst.count = burstCount;
            _emissionModule.SetBurst(0, burst);
        }
        else
        {
            _emissionModule.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)burstCount) });
        }

        _particleSystem.Play();
    }

    public void SetTargetTransform(Transform value)
    {
        _target = value;
        if (_target != null)
        {
            _parentCanvas = _isUIMode ? _target.GetComponentInParent<Canvas>() : null;
            CacheCamera();
        }
    }
    #endregion

    #region Private Helpers
    private void InitializeSystem()
    {
        if (_particleSystem == null)
        {
            _particleSystem = GetComponent<ParticleSystem>();
        }

        if (_particleSystem != null)
        {
            _mainModule = _particleSystem.main;
            _emissionModule = _particleSystem.emission;
        }

        int maxParticles = _mainModule.maxParticles;
        if (_particles == null || _particles.Length < maxParticles)
        {
            _particles = new ParticleSystem.Particle[maxParticles];
        }

        _sqrDistanceToKill = _distanceToKill * _distanceToKill;

        if (_target != null)
        {
            _parentCanvas = _isUIMode ? _target.GetComponentInParent<Canvas>() : null;
            CacheCamera();
        }
    }

    private void CacheCamera()
    {
        if (_parentCanvas != null)
        {
            _cachedCamera = _parentCanvas.worldCamera;
        }

        if (_cachedCamera == null)
        {
            _cachedCamera = Camera.main;
        }
    }

    private Vector3 ResolveTargetPosition()
    {
        if (_target == null)
        {
            return Vector3.zero;
        }

        Vector3 targetPosition = _target.position;

        // Clean UI Screen Point to World Coordinate resolution avoiding Camera.main on update
        if (_isUIMode && _parentCanvas != null && _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            RectTransform rectTransform = _target as RectTransform;
            if (rectTransform != null && _cachedCamera != null)
            {
                Vector3 screenPoint = rectTransform.position;
                screenPoint.z = Mathf.Abs(_cachedCamera.transform.position.z - transform.position.z);
                targetPosition = _cachedCamera.ScreenToWorldPoint(screenPoint);
            }
        }

        if (_shouldIgnoreZ)
        {
            targetPosition.z = transform.position.z;
        }

        return targetPosition;
    }

    private void SetTransformParticle(Transform from)
    {
        if (from != null)
        {
            Vector3 targetPosition = from.position;
            if (_shouldIgnoreZ)
            {
                targetPosition.z = transform.position.z;
            }
            transform.position = targetPosition;
        }
        else
        {
            transform.position = Vector3.zero;
        }
    }
    #endregion
}