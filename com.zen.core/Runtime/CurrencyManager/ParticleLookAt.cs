using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// The "coins fly to the HUD" effect (CurrencyManager.Add*). One ParticleSystem is only the renderer: this component
/// emits every coin itself and moves it along a fixed timeline, so the motion is the same on every device:
///   1. Burst  - the coin pops out of the source with an overshoot (ease-out-back) and a random scatter.
///   2. Hold   - it floats for a moment (a short, random pause: the coins do not leave together).
///   3. Fly    - it accelerates (flyCurve) along a curved path (quadratic Bezier bent to one side) to the target,
///               shrinking and spinning; the target follows even when it moves.
///   4. Arrive - the coin disappears at the target, the target punches, OnTarget(count) tells how many arrived.
/// The coins leave one after another (spawnInterval), the amount of the Add decides how many (minCoins..maxCoins).
/// Time is unscaled: the effect also plays over a paused game (a result popup). Put the money texture on the material
/// of the ParticleSystem; flipFrames &gt; 1 cuts it into frames played as a flip (see the money sheet).
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class ParticleLookAt : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform _target;

    [Tooltip("The target is a UI element of a Screen Space - Overlay canvas (its position is turned into world space).")]
    [SerializeField] private bool _isUIMode = false;

    [Tooltip("Keep every coin on the Z of its source (flat 2D / UI).")]
    [SerializeField] private bool _shouldIgnoreZ = false;

    [Header("Coins")]
    [Tooltip("Coins of a small amount (the number of coins grows with the log of the amount).")]
    [SerializeField, Min(1)] private int _minCoins = 8;
    [Tooltip("Coins of a big amount.")]
    [SerializeField, Min(1)] private int _maxCoins = 24;
    [Tooltip("Seconds between two coins leaving the source.")]
    [SerializeField, Min(0f)] private float _spawnInterval = 0.025f;

    [Header("1. Burst")]
    [SerializeField, Min(0.01f)] private float _burstTime = 0.35f;
    [Tooltip("How far a coin pops out, in coin sizes (min, max).")]
    [SerializeField] private Vector2 _burstDistance = new Vector2(2f, 5f);

    [Header("2. Hold")]
    [Tooltip("Seconds a coin floats before it flies (min, max).")]
    [SerializeField] private Vector2 _holdTime = new Vector2(0.05f, 0.25f);

    [Header("3. Fly")]
    [Tooltip("Seconds of the flight to the target (min, max).")]
    [SerializeField] private Vector2 _flyTime = new Vector2(0.55f, 0.85f);
    [Tooltip("Progress of the flight over its time: slow start, fast end = the coin is pulled in.")]
    [SerializeField] private AnimationCurve _flyCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0.6f), new Keyframe(1f, 1f, 2.6f, 0f));
    [Tooltip("How far the path bends sideways, as a fraction of the distance.")]
    [SerializeField, Range(0f, 1f)] private float _arc = 0.25f;
    [Tooltip("Size at the target, as a fraction of the coin size.")]
    [SerializeField, Range(0f, 1f)] private float _endScale = 0.5f;
    [Tooltip("Degrees per second (random direction). 0 = no spin.")]
    [SerializeField, Min(0f)] private float _spin = 360f;

    [Header("4. Arrive")]
    [Tooltip("The target pops when coins arrive.")]
    [SerializeField] private bool _punchTarget = true;
    [SerializeField, Min(0f)] private float _punchScale = 0.2f;

    [Header("Money sheet")]
    [Tooltip("Frames of the money texture in a row (the flip). 1 = a single image.")]
    [SerializeField, Min(1)] private int _flipFrames = 1;
    [Tooltip("How many times the flip plays during the life of a coin.")]
    [SerializeField, Min(1)] private int _flipCycles = 3;

    /// <summary>Coins that reached the target in this frame (the label of the currency can count up).</summary>
    public event Action<int> OnTarget;

    /// <summary>The last coin of an effect arrived.</summary>
    public event Action<int> OnEmitDone;

    private struct Coin
    {
        public Vector3 origin;
        public Vector3 offset;
        public float burst;
        public float flyStart;
        public float total;
        public float side;
        public float spin;
        public float rotation;
    }

    private struct Pending
    {
        public float time;
        public uint id;
    }

    private const int Mask = 1023;   // coin states live in a ring indexed by the particle's random seed

    private readonly Coin[] _coins = new Coin[Mask + 1];
    private readonly List<Pending> _pending = new List<Pending>(32);
    private int _pendingHead;
    private uint _nextId;
    private bool _isActive;
    private int _expected;   // coins of the running effects
    private int _arrivedTotal;

    private ParticleSystem _ps;
    private ParticleSystem.Particle[] _particles;
    private float _baseSize = 1f;
    private Color32 _baseColor = new Color32(255, 255, 255, 255);
    private Canvas _targetCanvas;

    public Transform Target
    {
        get => _target;
        set => SetTargetTransform(value);
    }

    private void OnDisable()
    {
        _pending.Clear();
        _pendingHead = 0;
        _isActive = false;
        _expected = 0;
        _arrivedTotal = 0;
        if (_ps != null)
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    /// <summary>
    /// Starts the effect: coins burst out of start and fly to end (or the Target). amount decides the number of coins
    /// (minCoins..maxCoins on a log scale); CurrencyManager passes the amount that was added.
    /// </summary>
    public void Emit(Transform start, Transform end = null, int amount = 16, bool lookAtTarget = true)
    {
        Init();
        if (end != null)
            SetTargetTransform(end);

        Vector3 origin = start != null ? ToWorld(start, start.GetComponentInParent<Canvas>()) : transform.position;
        float now = Time.unscaledTime;
        int coins = CoinCount(amount);
        _expected += coins;
        for (int i = 0; i < coins; i++)
        {
            uint id = ++_nextId;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(_burstDistance.x, _burstDistance.y) * _baseSize;
            float burst = _burstTime * Random.Range(0.85f, 1.15f);
            float hold = Random.Range(_holdTime.x, _holdTime.y);
            float fly = Random.Range(_flyTime.x, _flyTime.y);
            _coins[id & Mask] = new Coin
            {
                origin = origin,
                offset = new Vector3(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance + _baseSize * 0.5f, 0f),
                burst = burst,
                flyStart = burst + hold,
                total = burst + hold + fly,
                side = Random.value < 0.5f ? -1f : 1f,
                spin = _spin * (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.6f, 1f),
                rotation = Random.Range(0f, 360f),
            };
            _pending.Add(new Pending { time = now + i * _spawnInterval, id = id });
        }

        _isActive = true;
        if (!_ps.isPlaying)
            _ps.Play();
    }

    public void SetTargetTransform(Transform value)
    {
        _target = value;
        _targetCanvas = _target != null ? _target.GetComponentInParent<Canvas>() : null;
    }

    private void LateUpdate()
    {
        if (!_isActive)
            return;

        EmitDue();
        if (_pendingHead >= _pending.Count && _ps.particleCount == 0)
        {
            _isActive = false;
            _pending.Clear();
            _pendingHead = 0;
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            // A frame hitch can end a coin before it was seen arriving: the listeners must still get every coin.
            if (_arrivedTotal < _expected)
                OnTarget?.Invoke(_expected - _arrivedTotal);
            _expected = 0;
            _arrivedTotal = 0;
            OnEmitDone?.Invoke(0);
            return;
        }

        int alive = _ps.GetParticles(_particles);
        if (alive == 0)
            return;

        Vector3 target = _target != null ? ToWorld(_target, _targetCanvas) : transform.position;
        float arriveGuard = Time.unscaledDeltaTime * 1.5f;
        int arrived = 0;
        for (int i = 0; i < alive; i++)
        {
            ref ParticleSystem.Particle p = ref _particles[i];
            Coin c = _coins[p.randomSeed & Mask];
            float age = p.startLifetime - p.remainingLifetime;
            Vector3 burstPos = c.origin + c.offset;
            Vector3 pos;
            float scale;

            if (age < c.burst)
            {
                float u = age / c.burst;
                pos = c.origin + c.offset * EaseOutCubic(u);
                scale = EaseOutBack(u);
            }
            else if (age < c.flyStart)
            {
                float bob = Mathf.Sin(age * 9f + c.rotation);
                pos = burstPos + Vector3.up * (bob * _baseSize * 0.12f);
                scale = 1f + bob * 0.05f;
            }
            else
            {
                float u = Mathf.Clamp01((age - c.flyStart) / Mathf.Max(0.01f, c.total - c.flyStart));
                float e = _flyCurve.Evaluate(u);
                Vector3 toTarget = target - burstPos;
                Vector3 bend = new Vector3(-toTarget.y, toTarget.x, 0f).normalized * (c.side * _arc * toTarget.magnitude);
                Vector3 control = (burstPos + target) * 0.5f + bend;
                pos = Bezier(burstPos, control, target, e);
                scale = Mathf.Lerp(1f, _endScale, e);
            }

            if (p.remainingLifetime <= arriveGuard)
            {
                pos = target;
                scale = _endScale;
                p.remainingLifetime = 0f;
                arrived++;
            }

            if (_shouldIgnoreZ)
                pos.z = c.origin.z;
            p.position = pos;
            p.velocity = Vector3.zero;
            p.startSize = _baseSize * scale;
            p.rotation = c.rotation + c.spin * age;
            p.startColor = new Color32(_baseColor.r, _baseColor.g, _baseColor.b, (byte)(_baseColor.a * Mathf.Clamp01(age / 0.08f)));
        }
        _ps.SetParticles(_particles, alive);

        if (arrived > 0)
        {
            _arrivedTotal += arrived;
            PunchTarget();
            OnTarget?.Invoke(arrived);
        }
    }

    private void EmitDue()
    {
        float now = Time.unscaledTime;
        while (_pendingHead < _pending.Count && _pending[_pendingHead].time <= now)
        {
            uint id = _pending[_pendingHead].id;
            _pendingHead++;
            var emit = new ParticleSystem.EmitParams
            {
                position = _coins[id & Mask].origin,
                velocity = Vector3.zero,
                startLifetime = _coins[id & Mask].total,
                startSize = 0.0001f,
                startColor = new Color32(_baseColor.r, _baseColor.g, _baseColor.b, 0),
                rotation = _coins[id & Mask].rotation,
                angularVelocity = 0f,
                randomSeed = id,
                applyShapeToPosition = false,
            };
            _ps.Emit(emit, 1);
        }
    }

    // The ParticleSystem only draws: no emission of its own, world space, unscaled time, no size / color / rotation
    // over lifetime (this component sets them every frame). Play mode only, so the prefab is not changed.
    private void Init()
    {
        if (_ps != null)
            return;
        _ps = GetComponent<ParticleSystem>();

        var main = _ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = true;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.maxParticles = Mathf.Max(main.maxParticles, _maxCoins * 4);
        _baseSize = Mathf.Max(0.0001f, main.startSize.constantMax);
        if (main.startColor.mode == ParticleSystemGradientMode.Color)
            _baseColor = main.startColor.color;

        var emission = _ps.emission;
        emission.enabled = false;
        var shape = _ps.shape;
        shape.enabled = false;
        var size = _ps.sizeOverLifetime;
        size.enabled = false;
        var color = _ps.colorOverLifetime;
        color.enabled = false;
        var rotation = _ps.rotationOverLifetime;
        rotation.enabled = false;

        if (_flipFrames > 1)
        {
            var sheet = _ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = _flipFrames;
            sheet.numTilesY = 1;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.cycleCount = _flipCycles;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
        }

        _particles = new ParticleSystem.Particle[main.maxParticles];
    }

    private int CoinCount(int amount)
    {
        float t = Mathf.InverseLerp(1f, 4f, Mathf.Log10(Mathf.Max(1, amount)));
        return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(_minCoins, _maxCoins, t)), 1, _maxCoins);
    }

    private void PunchTarget()
    {
        if (!_punchTarget || _target == null)
            return;
        _target.DOKill(true);
        _target.DOPunchScale(Vector3.one * _punchScale, 0.18f, 1, 0f).SetUpdate(true);
    }

    // A UI element of an overlay canvas has screen pixels as its position: the world point behind that pixel.
    private Vector3 ToWorld(Transform t, Canvas canvas)
    {
        Vector3 position = t.position;
        if (_isUIMode && canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay && t is RectTransform)
        {
            Camera cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            if (cam != null)
            {
                position.z = Mathf.Abs(cam.transform.position.z - transform.position.z);
                position = cam.ScreenToWorldPoint(position);
            }
        }
        return position;
    }

    private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float s = 1f - t;
        return s * s * a + 2f * s * t * b + t * t * c;
    }

    private static float EaseOutCubic(float u)
    {
        float s = 1f - u;
        return 1f - s * s * s;
    }

    private static float EaseOutBack(float u)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float s = u - 1f;
        return 1f + c3 * s * s * s + c1 * s * s;
    }
}
