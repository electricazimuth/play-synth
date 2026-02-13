// PulseSystem.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using TriGrid.Core;

namespace TriGrid.Pulse
{
    /// <summary>
    /// Manages pulse creation, propagation, and lifecycle.
    /// Reads from TriGridData, updates pulse occupancy tracking.
    /// </summary>
    public class PulseSystem
    {
        private readonly TriGridData _gridData;
        private readonly List<PulseData> _activePulses = new List<PulseData>();
        private readonly List<PulseData> _pulsesToRemove = new List<PulseData>();
        private readonly int _maxPulses;

        // ─── Events ───
        public event Action<PulseData> OnPulseCreated;
        public event Action<PulseData> OnPulseDestroyed;
        public event Action<PulseData> OnPulseArrivedAtVertex;
        public event Action<PulseData> OnPulseStartedMoving;

        /// <summary>
        /// Fired when a pulse hits a boundary vertex — the audio system subscribes to this.
        /// </summary>
        public event Action<VertexCoord, PulseData> OnPulseBoundaryHit;

        public IReadOnlyList<PulseData> ActivePulses => _activePulses;

        public PulseSystem(TriGridData gridData, int maxPulses = 64)
        {
            _gridData = gridData;
            _maxPulses = maxPulses;
        }

        /// <summary>
        /// Create a new pulse at the given vertex, traveling in the given direction.
        /// </summary>
        public PulseData CreatePulse(VertexCoord origin, int directionIndex, int instrumentId, float energy)
        {
            if (_activePulses.Count >= _maxPulses) return null;
            if (!_gridData.IsVertexActive(origin)) return null;

            var pulse = new PulseData
            {
                CurrentVertex = origin,
                TargetVertex = origin,
                DirectionIndex = directionIndex,
                InstrumentId = instrumentId,
                Energy = energy,
                IsMoving = false,
                ProgressAlongEdge = 0f
            };

            _activePulses.Add(pulse);
            _gridData.MarkVertexOccupied(origin);

            OnPulseCreated?.Invoke(pulse);
            return pulse;
        }

        /// <summary>
        /// Emit pulses from all emitters. Called by the beat clock.
        /// </summary>
        public void EmitFromAllEmitters()
        {
            var emitters = _gridData.GetAllEmitters();
            foreach (var v in emitters)
            {
                if (!_gridData.TryGetVertexState(v, out var state)) continue;
                var config = state.EmitterConfig;

                if (config.InitialDirection == -1)
                {
                    // Emit in all 6 directions
                    for (int d = 0; d < 6; d++)
                    {
                        var target = GridMath.GetAdjacentVertex(v, d);
                        if (_gridData.IsVertexActive(target))
                        {
                            CreatePulse(v, d, config.InstrumentId, config.InitialEnergy);
                        }
                    }
                }
                else
                {
                    CreatePulse(v, config.InitialDirection, config.InstrumentId, config.InitialEnergy);
                }
            }
        }

        /// <summary>
        /// Begin moving a pulse from its current vertex to the next vertex.
        /// Returns false if the pulse cannot move (dead end).
        /// </summary>
        public bool BeginPulseMovement(PulseData pulse)
        {
            if (!pulse.IsAlive || pulse.IsMoving) return false;

            // Determine next direction
            int nextDir = pulse.DirectionIndex;

            // Check for reflector at current vertex
            int? reflectorDir = _gridData.GetReflectorDirection(pulse.CurrentVertex);
            if (reflectorDir.HasValue)
            {
                nextDir = reflectorDir.Value;
            }

            // Try to find a valid direction
            VertexCoord nextVertex = GridMath.GetAdjacentVertex(pulse.CurrentVertex, nextDir);
            EdgeCoord edge = GridTopology.GetEdgeBetween(pulse.CurrentVertex, nextVertex);

            // Check if the edge and target vertex are active
            if (!_gridData.IsEdgeActive(edge) || !_gridData.IsVertexActive(nextVertex))
            {
                // Try to find an alternative: reflect (opposite direction)
                // or find any valid adjacent edge
                bool found = false;

                // First try reflection (bounce back)
                int reflectedDir = GridMath.OppositeDirection(nextDir);
                VertexCoord reflectedVertex = GridMath.GetAdjacentVertex(pulse.CurrentVertex, reflectedDir);
                EdgeCoord reflectedEdge = GridTopology.GetEdgeBetween(pulse.CurrentVertex, reflectedVertex);

                if (_gridData.IsEdgeActive(reflectedEdge) && _gridData.IsVertexActive(reflectedVertex))
                {
                    nextDir = reflectedDir;
                    nextVertex = reflectedVertex;
                    edge = reflectedEdge;
                    found = true;
                }

                if (!found)
                {
                    // Try all 6 directions
                    for (int d = 0; d < 6; d++)
                    {
                        VertexCoord candidate = GridMath.GetAdjacentVertex(pulse.CurrentVertex, d);
                        EdgeCoord candidateEdge = GridTopology.GetEdgeBetween(pulse.CurrentVertex, candidate);

                        if (_gridData.IsEdgeActive(candidateEdge) && _gridData.IsVertexActive(candidate))
                        {
                            nextDir = d;
                            nextVertex = candidate;
                            edge = candidateEdge;
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    // No valid path — destroy pulse
                    DestroyPulse(pulse);
                    return false;
                }
            }

            // Unmark current vertex, mark edge
            _gridData.UnmarkVertexOccupied(pulse.CurrentVertex);
            _gridData.MarkEdgeOccupied(edge);

            pulse.TargetVertex = nextVertex;
            pulse.CurrentEdge = edge;
            pulse.DirectionIndex = nextDir;
            pulse.IsMoving = true;
            pulse.ProgressAlongEdge = 0f;

            OnPulseStartedMoving?.Invoke(pulse);
            return true;
        }

        /// <summary>
        /// Called when a pulse arrives at its target vertex (e.g., DOTween callback).
        /// </summary>
        public void CompletePulseMovement(PulseData pulse)
        {
            if (!pulse.IsAlive || !pulse.IsMoving) return;

            // Unmark edge, mark target vertex
            _gridData.UnmarkEdgeOccupied(pulse.CurrentEdge);
            _gridData.MarkVertexOccupied(pulse.TargetVertex);

            pulse.CurrentVertex = pulse.TargetVertex;
            pulse.IsMoving = false;
            pulse.ProgressAlongEdge = 1f;

            // Decay energy
            pulse.Energy *= 0.95f; // Configurable decay factor

            OnPulseArrivedAtVertex?.Invoke(pulse);

            // Check if this is a boundary vertex → trigger sound
            if (_gridData.IsBoundaryVertex(pulse.CurrentVertex))
            {
                OnPulseBoundaryHit?.Invoke(pulse.CurrentVertex, pulse);
                pulse.Energy *= 0.85f; // Additional decay on boundary hit
            }

            // Kill pulse if energy too low
            if (pulse.Energy < 0.01f)
            {
                DestroyPulse(pulse);
            }
        }

        /// <summary>
        /// Destroy a pulse and clean up occupancy.
        /// </summary>
        public void DestroyPulse(PulseData pulse)
        {
            if (!pulse.IsAlive) return;

            pulse.IsAlive = false;

            if (pulse.IsMoving)
            {
                _gridData.UnmarkEdgeOccupied(pulse.CurrentEdge);
            }
            else
            {
                _gridData.UnmarkVertexOccupied(pulse.CurrentVertex);
            }

            _pulsesToRemove.Add(pulse);
            OnPulseDestroyed?.Invoke(pulse);
        }

        /// <summary>
        /// Clean up destroyed pulses. Call once per frame after all pulse processing.
        /// </summary>
        public void CleanupDestroyedPulses()
        {
            if (_pulsesToRemove.Count > 0)
            {
                foreach (var pulse in _pulsesToRemove)
                {
                    _activePulses.Remove(pulse);
                }
                _pulsesToRemove.Clear();
            }
        }

        /// <summary>
        /// Destroy all active pulses.
        /// </summary>
        public void DestroyAllPulses()
        {
            for (int i = _activePulses.Count - 1; i >= 0; i--)
            {
                DestroyPulse(_activePulses[i]);
            }
            CleanupDestroyedPulses();
        }
    }
}
