// PulseVisualManager.cs
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using TriGrid.Core;
using TriGrid.Pulse;

namespace TriGrid.Unity
{
    /// <summary>
    /// Manages visual representations of pulses, animating them along grid edges using DOTween.
    /// </summary>
    public class PulseVisualManager : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private Material _pulseMaterial;
        [SerializeField] private float _pulseScale = 0.25f;
        [SerializeField] private float _pulseYOffset = 0.01f;

        [Header("Pool")]
        [SerializeField] private int _initialPoolSize = 64;

        private GameObjectPool _pulsePool;
        private readonly Dictionary<int, GameObject> _pulseVisuals = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, Tween> _pulseTweens = new Dictionary<int, Tween>();

        private PulseSystem _pulseSystem;
        private Vector3 _gridOrigin;

        /// <summary>
        /// Duration of one edge traversal in seconds. Set by beat clock configuration.
        /// </summary>
        public float EdgeTravelDuration { get; set; } = 0.5f;

        public void Initialize(PulseSystem pulseSystem, Vector3 gridOrigin)
        {
            _pulseSystem = pulseSystem;
            _gridOrigin = gridOrigin;

            CreatePool();
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            KillAllTweens();
            _pulsePool?.DestroyAll();
        }

        private void CreatePool()
        {
            var prefab = GridMeshFactory.CreateVisualPrefab("Pulse",
                GridMeshFactory.GetVertexMesh(), _pulseMaterial);
            var parent = new GameObject("PulseVisuals").transform;
            parent.SetParent(transform);
            _pulsePool = new GameObjectPool(prefab, parent, _initialPoolSize);
            Destroy(prefab);
        }

        private void SubscribeToEvents()
        {
            if (_pulseSystem == null) return;
            _pulseSystem.OnPulseCreated += HandlePulseCreated;
            _pulseSystem.OnPulseDestroyed += HandlePulseDestroyed;
            _pulseSystem.OnPulseStartedMoving += HandlePulseStartedMoving;
        }

        private void UnsubscribeFromEvents()
        {
            if (_pulseSystem == null) return;
            _pulseSystem.OnPulseCreated -= HandlePulseCreated;
            _pulseSystem.OnPulseDestroyed -= HandlePulseDestroyed;
            _pulseSystem.OnPulseStartedMoving -= HandlePulseStartedMoving;
        }

        private void HandlePulseCreated(PulseData pulse)
        {
            var go = _pulsePool.Acquire();
            Vector3 pos = GridMath.VertexToWorld(pulse.CurrentVertex, _gridOrigin);
            pos.y = _gridOrigin.y + _pulseYOffset;
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * _pulseScale;
            go.name = $"Pulse_{pulse.Id}";

            _pulseVisuals[pulse.Id] = go;
        }

        private void HandlePulseDestroyed(PulseData pulse)
        {
            // Kill any active tween
            if (_pulseTweens.TryGetValue(pulse.Id, out var tween))
            {
                tween.Kill();
                _pulseTweens.Remove(pulse.Id);
            }

            if (_pulseVisuals.TryGetValue(pulse.Id, out var go))
            {
                // Fade out and release
                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.material != null)
                {
                    // Quick fade out
                    go.transform.DOScale(Vector3.zero, 0.15f)
                        .SetEase(Ease.InQuad)
                        .OnComplete(() =>
                        {
                            _pulsePool.Release(go);
                        });
                }
                else
                {
                    _pulsePool.Release(go);
                }

                _pulseVisuals.Remove(pulse.Id);
            }
        }

        private void HandlePulseStartedMoving(PulseData pulse)
        {
            if (!_pulseVisuals.TryGetValue(pulse.Id, out var go)) return;

            Vector3 start = GridMath.VertexToWorld(pulse.CurrentVertex, _gridOrigin);
            Vector3 end = GridMath.VertexToWorld(pulse.TargetVertex, _gridOrigin);
            start.y = _gridOrigin.y + _pulseYOffset;
            end.y = _gridOrigin.y + _pulseYOffset;

            // Kill any existing tween for this pulse
            if (_pulseTweens.TryGetValue(pulse.Id, out var existingTween))
            {
                existingTween.Kill();
            }

            go.transform.position = start;

            var tween = go.transform.DOMove(end, EdgeTravelDuration)
                .SetEase(Ease.Linear)
                .OnUpdate(() =>
                {
                    // Update progress on the data side
                    if (pulse.IsAlive && pulse.IsMoving)
                    {
                        pulse.ProgressAlongEdge = Mathf.Clamp01(
                            (go.transform.position - start).magnitude / (end - start).magnitude);
                    }
                })
                .OnComplete(() =>
                {
                    _pulseTweens.Remove(pulse.Id);
                    if (pulse.IsAlive)
                    {
                        _pulseSystem.CompletePulseMovement(pulse);

                        // Chain next movement if pulse is still alive
                        if (pulse.IsAlive)
                        {
                            _pulseSystem.BeginPulseMovement(pulse);
                        }
                    }
                });

            _pulseTweens[pulse.Id] = tween;
        }

        /// <summary>
        /// Scale pulse visuals based on energy for visual feedback.
        /// Call in Update if desired.
        /// </summary>
        public void UpdatePulseScales()
        {
            foreach (var pulse in _pulseSystem.ActivePulses)
            {
                if (_pulseVisuals.TryGetValue(pulse.Id, out var go))
                {
                    float scale = _pulseScale * Mathf.Lerp(0.5f, 1f, pulse.Energy);
                    go.transform.localScale = Vector3.one * scale;
                }
            }
        }

        private void KillAllTweens()
        {
            foreach (var tween in _pulseTweens.Values)
            {
                tween?.Kill();
            }
            _pulseTweens.Clear();
        }
    }
}
