// TriGridData.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace TriGrid.Core
{
    /// <summary>
    /// The single source of truth for all grid topological state.
    /// Pure C# — no Unity dependencies. Fully unit-testable.
    /// Modified only from the main thread via GridController.
    /// </summary>
    public class TriGridData
    {
        // ─────────────────────────────────────────────
        //  EVENTS
        // ─────────────────────────────────────────────

        public event Action<FaceCoord> OnFaceAdded;
        public event Action<FaceCoord> OnFaceRemoved;
        public event Action<VertexCoord, bool> OnVertexBoundaryChanged;
        public event Action<EdgeCoord, bool> OnEdgeBoundaryChanged;
        public event Action<VertexCoord> OnVertexCreated;
        public event Action<VertexCoord> OnVertexDestroyed;
        public event Action<EdgeCoord> OnEdgeCreated;
        public event Action<EdgeCoord> OnEdgeDestroyed;
        public event Action<VertexCoord, EmitterConfig> OnEmitterPlaced;
        public event Action<VertexCoord> OnEmitterRemoved;
        public event Action<VertexCoord, int> OnReflectorPlaced;
        public event Action<VertexCoord> OnReflectorRemoved;

        // ─────────────────────────────────────────────
        //  STORAGE
        // ─────────────────────────────────────────────

        private readonly HashSet<FaceCoord> _activeFaces = new HashSet<FaceCoord>();
        private readonly Dictionary<VertexCoord, VertexState> _vertexData = new Dictionary<VertexCoord, VertexState>();
        private readonly Dictionary<EdgeCoord, EdgeState> _edgeData = new Dictionary<EdgeCoord, EdgeState>();

        // ─────────────────────────────────────────────
        //  BOUNDS
        // ─────────────────────────────────────────────

        public int MinQ { get; private set; }
        public int MinR { get; private set; }
        public int MaxQ { get; private set; }
        public int MaxR { get; private set; }

        // ─────────────────────────────────────────────
        //  PULSE OCCUPANCY (written by PulseSystem)
        // ─────────────────────────────────────────────

        private readonly HashSet<EdgeCoord> _occupiedEdges = new HashSet<EdgeCoord>();
        private readonly HashSet<VertexCoord> _occupiedVertices = new HashSet<VertexCoord>();

        // Reusable scratch arrays to avoid allocations in hot paths
        private readonly VertexCoord[] _scratchVertices3 = new VertexCoord[3];
        private readonly EdgeCoord[] _scratchEdges3 = new EdgeCoord[3];
        private readonly FaceCoord[] _scratchFaces6 = new FaceCoord[6];

        // ─────────────────────────────────────────────
        //  CONSTRUCTOR
        // ─────────────────────────────────────────────

        public TriGridData(int minQ, int minR, int maxQ, int maxR)
        {
            MinQ = minQ;
            MinR = minR;
            MaxQ = maxQ;
            MaxR = maxR;
        }

        // ─────────────────────────────────────────────
        //  READ-ONLY QUERIES
        // ─────────────────────────────────────────────

        public bool IsFaceActive(FaceCoord face) => _activeFaces.Contains(face);
        public int ActiveFaceCount => _activeFaces.Count;
        public IReadOnlyCollection<FaceCoord> ActiveFaces => _activeFaces;

        public bool IsVertexActive(VertexCoord v) => _vertexData.ContainsKey(v);

        public bool IsBoundaryVertex(VertexCoord v)
        {
            return _vertexData.TryGetValue(v, out var state) && state.IsBoundary;
        }

        public bool IsEdgeActive(EdgeCoord e) => _edgeData.ContainsKey(e);

        public bool IsBoundaryEdge(EdgeCoord e)
        {
            return _edgeData.TryGetValue(e, out var state) && state.IsBoundary;
        }

        public bool TryGetVertexState(VertexCoord v, out VertexState state)
        {
            return _vertexData.TryGetValue(v, out state);
        }

        public bool TryGetEdgeState(EdgeCoord e, out EdgeState state)
        {
            return _edgeData.TryGetValue(e, out state);
        }

        public VertexState GetVertexState(VertexCoord v)
        {
            _vertexData.TryGetValue(v, out var state);
            return state;
        }

        public bool HasEmitter(VertexCoord v)
        {
            return _vertexData.TryGetValue(v, out var state) && state.HasEmitter;
        }

        public bool HasReflector(VertexCoord v)
        {
            return _vertexData.TryGetValue(v, out var state) && state.HasReflector;
        }

        public int? GetReflectorDirection(VertexCoord v)
        {
            if (_vertexData.TryGetValue(v, out var state) && state.HasReflector)
                return state.ReflectorDirection;
            return null;
        }

        public IReadOnlyDictionary<VertexCoord, VertexState> AllVertices => _vertexData;
        public IReadOnlyDictionary<EdgeCoord, EdgeState> AllEdges => _edgeData;

        /// <summary>
        /// Get all active faces adjacent to an edge.
        /// </summary>
        public void GetActiveFacesForEdge(EdgeCoord edge, List<FaceCoord> result)
        {
            result.Clear();
            var (f1, f2) = GridTopology.EdgeJoinsFaces(edge);
            if (_activeFaces.Contains(f1)) result.Add(f1);
            if (_activeFaces.Contains(f2)) result.Add(f2);
        }

        /// <summary>
        /// Check if a face is within the configured grid bounds.
        /// </summary>
        public bool IsInBounds(FaceCoord face)
        {
            return face.Q >= MinQ && face.Q <= MaxQ && face.R >= MinR && face.R <= MaxR;
        }

        /// <summary>
        /// Check if a vertex is within the configured grid bounds (with margin).
        /// </summary>
        public bool IsInBounds(VertexCoord v)
        {
            return v.U >= MinQ && v.U <= MaxQ + 1 && v.V >= MinR && v.V <= MaxR + 1;
        }

        // ─────────────────────────────────────────────
        //  PULSE OCCUPANCY API
        // ─────────────────────────────────────────────

        public void MarkEdgeOccupied(EdgeCoord e) => _occupiedEdges.Add(e);
        public void UnmarkEdgeOccupied(EdgeCoord e) => _occupiedEdges.Remove(e);
        public void MarkVertexOccupied(VertexCoord v) => _occupiedVertices.Add(v);
        public void UnmarkVertexOccupied(VertexCoord v) => _occupiedVertices.Remove(v);
        public bool IsEdgeOccupiedByPulse(EdgeCoord e) => _occupiedEdges.Contains(e);
        public bool IsVertexOccupiedByPulse(VertexCoord v) => _occupiedVertices.Contains(v);

        /// <summary>
        /// Check if any pulse is currently on any edge or vertex of the given face.
        /// </summary>
        public bool IsFaceOccupiedByPulse(FaceCoord face)
        {
            GridTopology.FaceVertices(face, _scratchVertices3);
            for (int i = 0; i < 3; i++)
            {
                if (_occupiedVertices.Contains(_scratchVertices3[i]))
                    return true;
            }

            GridTopology.FaceBorderEdges(face, _scratchEdges3);
            for (int i = 0; i < 3; i++)
            {
                if (_occupiedEdges.Contains(_scratchEdges3[i]))
                    return true;
            }

            return false;
        }

        // ─────────────────────────────────────────────
        //  MUTATION: ADD FACE
        // ─────────────────────────────────────────────

        public GridChangeResult AddFace(FaceCoord face)
        {
            if (!IsInBounds(face))
                return GridChangeResult.OutOfBounds;

            if (_activeFaces.Contains(face))
                return GridChangeResult.AlreadyExists;

            _activeFaces.Add(face);

            // Update vertices
            GridTopology.FaceVertices(face, _scratchVertices3);
            for (int i = 0; i < 3; i++)
            {
                var v = _scratchVertices3[i];
                if (!_vertexData.TryGetValue(v, out var vState))
                {
                    vState = new VertexState();
                    _vertexData[v] = vState;
                    OnVertexCreated?.Invoke(v);
                }
                vState.ReferenceCount++;
            }

            // Update edges
            GridTopology.FaceBorderEdges(face, _scratchEdges3);
            for (int i = 0; i < 3; i++)
            {
                var e = _scratchEdges3[i];
                if (!_edgeData.TryGetValue(e, out var eState))
                {
                    eState = new EdgeState();
                    _edgeData[e] = eState;
                    OnEdgeCreated?.Invoke(e);
                }
                eState.ActiveFaceCount++;
                bool wasBoundary = eState.IsBoundary;
                eState.IsBoundary = eState.ActiveFaceCount == 1;
                if (wasBoundary != eState.IsBoundary)
                    OnEdgeBoundaryChanged?.Invoke(e, eState.IsBoundary);
            }

            // Recompute boundary status for affected vertices
            for (int i = 0; i < 3; i++)
            {
                RecomputeVertexBoundary(_scratchVertices3[i]);
            }

            OnFaceAdded?.Invoke(face);
            return GridChangeResult.Success;
        }

        // ─────────────────────────────────────────────
        //  MUTATION: REMOVE FACE
        // ─────────────────────────────────────────────

        public GridChangeResult RemoveFace(FaceCoord face)
        {
            if (!_activeFaces.Contains(face))
                return GridChangeResult.NotFound;

            // Check pulse conflict
            if (IsFaceOccupiedByPulse(face))
                return GridChangeResult.PulseConflict;

            _activeFaces.Remove(face);

            // Update edges
            GridTopology.FaceBorderEdges(face, _scratchEdges3);
            for (int i = 0; i < 3; i++)
            {
                var e = _scratchEdges3[i];
                if (_edgeData.TryGetValue(e, out var eState))
                {
                    eState.ActiveFaceCount--;
                    if (eState.ActiveFaceCount <= 0)
                    {
                        _edgeData.Remove(e);
                        if (eState.IsBoundary)
                            OnEdgeBoundaryChanged?.Invoke(e, false);
                        OnEdgeDestroyed?.Invoke(e);
                    }
                    else
                    {
                        bool wasBoundary = eState.IsBoundary;
                        eState.IsBoundary = eState.ActiveFaceCount == 1;
                        if (wasBoundary != eState.IsBoundary)
                            OnEdgeBoundaryChanged?.Invoke(e, eState.IsBoundary);
                    }
                }
            }

            // Update vertices
            GridTopology.FaceVertices(face, _scratchVertices3);
            for (int i = 0; i < 3; i++)
            {
                var v = _scratchVertices3[i];
                if (_vertexData.TryGetValue(v, out var vState))
                {
                    vState.ReferenceCount--;
                    if (vState.ReferenceCount <= 0)
                    {
                        // Clean up emitter/reflector if present
                        if (vState.HasEmitter)
                        {
                            vState.HasEmitter = false;
                            OnEmitterRemoved?.Invoke(v);
                        }
                        if (vState.HasReflector)
                        {
                            vState.HasReflector = false;
                            OnReflectorRemoved?.Invoke(v);
                        }
                        _vertexData.Remove(v);
                        OnVertexDestroyed?.Invoke(v);
                    }
                }
            }

            // Recompute boundary status for remaining vertices
            for (int i = 0; i < 3; i++)
            {
                if (_vertexData.ContainsKey(_scratchVertices3[i]))
                {
                    RecomputeVertexBoundary(_scratchVertices3[i]);
                }
            }

            OnFaceRemoved?.Invoke(face);
            return GridChangeResult.Success;
        }

        // ─────────────────────────────────────────────
        //  MUTATION: EMITTERS
        // ─────────────────────────────────────────────

        public GridChangeResult PlaceEmitter(VertexCoord v, EmitterConfig config)
        {
            if (!_vertexData.TryGetValue(v, out var state))
                return GridChangeResult.InvalidTarget;

            if (state.HasEmitter)
                return GridChangeResult.AlreadyExists;

            state.HasEmitter = true;
            state.EmitterConfig = config;
            OnEmitterPlaced?.Invoke(v, config);
            return GridChangeResult.Success;
        }

        public GridChangeResult RemoveEmitter(VertexCoord v)
        {
            if (!_vertexData.TryGetValue(v, out var state))
                return GridChangeResult.InvalidTarget;

            if (!state.HasEmitter)
                return GridChangeResult.NotFound;

            state.HasEmitter = false;
            state.EmitterConfig = default;
            OnEmitterRemoved?.Invoke(v);
            return GridChangeResult.Success;
        }

        // ─────────────────────────────────────────────
        //  MUTATION: REFLECTORS
        // ─────────────────────────────────────────────

        public GridChangeResult PlaceReflector(VertexCoord v, int direction60)
        {
            if (!_vertexData.TryGetValue(v, out var state))
                return GridChangeResult.InvalidTarget;

            if (state.HasReflector)
                return GridChangeResult.AlreadyExists;

            direction60 = ((direction60 % 6) + 6) % 6;
            state.HasReflector = true;
            state.ReflectorDirection = direction60;
            OnReflectorPlaced?.Invoke(v, direction60);
            return GridChangeResult.Success;
        }

        public GridChangeResult RemoveReflector(VertexCoord v)
        {
            if (!_vertexData.TryGetValue(v, out var state))
                return GridChangeResult.InvalidTarget;

            if (!state.HasReflector)
                return GridChangeResult.NotFound;

            state.HasReflector = false;
            OnReflectorRemoved?.Invoke(v);
            return GridChangeResult.Success;
        }

        /// <summary>
        /// Update the direction of an existing reflector.
        /// </summary>
        public GridChangeResult UpdateReflectorDirection(VertexCoord v, int newDirection60)
        {
            if (!_vertexData.TryGetValue(v, out var state))
                return GridChangeResult.InvalidTarget;

            if (!state.HasReflector)
                return GridChangeResult.NotFound;

            newDirection60 = ((newDirection60 % 6) + 6) % 6;
            state.ReflectorDirection = newDirection60;
            OnReflectorPlaced?.Invoke(v, newDirection60);
            return GridChangeResult.Success;
        }

        // ─────────────────────────────────────────────
        //  NOTE ASSIGNMENT
        // ─────────────────────────────────────────────

        public GridChangeResult AssignNote(VertexCoord v, int noteIndex)
        {
            if (!_vertexData.TryGetValue(v, out var state))
                return GridChangeResult.InvalidTarget;

            state.AssignedNote = noteIndex;
            return GridChangeResult.Success;
        }

        // ─────────────────────────────────────────────
        //  INTERNAL HELPERS
        // ─────────────────────────────────────────────

        private void RecomputeVertexBoundary(VertexCoord v)
        {
            if (!_vertexData.TryGetValue(v, out var state))
                return;

            GridTopology.VertexTouchesFaces(v, _scratchFaces6);
            int activeCount = 0;
            for (int i = 0; i < 6; i++)
            {
                if (_activeFaces.Contains(_scratchFaces6[i]))
                    activeCount++;
            }

            bool newBoundary = activeCount < 6 && activeCount > 0;
            if (state.IsBoundary != newBoundary)
            {
                state.IsBoundary = newBoundary;
                OnVertexBoundaryChanged?.Invoke(v, newBoundary);
            }
        }

        // ─────────────────────────────────────────────
        //  CLEAR / RESET
        // ─────────────────────────────────────────────

        /// <summary>
        /// Remove all faces and reset all state. Does not fire individual events.
        /// </summary>
        public void Clear()
        {
            _activeFaces.Clear();
            _vertexData.Clear();
            _edgeData.Clear();
            _occupiedEdges.Clear();
            _occupiedVertices.Clear();
        }

        // ─────────────────────────────────────────────
        //  EMITTER ENUMERATION
        // ─────────────────────────────────────────────

        /// <summary>
        /// Get all vertices that have emitters. Allocates a list.
        /// </summary>
        public List<VertexCoord> GetAllEmitters()
        {
            var result = new List<VertexCoord>();
            foreach (var kvp in _vertexData)
            {
                if (kvp.Value.HasEmitter)
                    result.Add(kvp.Key);
            }
            return result;
        }

        /// <summary>
        /// Get all vertices that have reflectors. Allocates a list.
        /// </summary>
        public List<VertexCoord> GetAllReflectors()
        {
            var result = new List<VertexCoord>();
            foreach (var kvp in _vertexData)
            {
                if (kvp.Value.HasReflector)
                    result.Add(kvp.Key);
            }
            return result;
        }
    }
}
