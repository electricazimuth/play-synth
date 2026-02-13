
// GridStates.cs
using System;

namespace TriGrid.Core
{
    /// <summary>
    /// Mutable state associated with an active vertex.
    /// </summary>
    public class VertexState
    {
        /// <summary>
        /// Number of active faces referencing this vertex.
        /// When this reaches 0, the vertex is removed.
        /// </summary>
        public int ReferenceCount;

        /// <summary>
        /// True if this vertex lies on the boundary of the structure
        /// (fewer than 6 active touching faces).
        /// </summary>
        public bool IsBoundary;

        /// <summary>
        /// MIDI note or custom pitch index assigned to this vertex.
        /// -1 means unassigned.
        /// </summary>
        public int AssignedNote;

        /// <summary>
        /// Whether this vertex has an emitter placed on it.
        /// </summary>
        public bool HasEmitter;

        /// <summary>
        /// Emitter configuration, valid only if HasEmitter is true.
        /// </summary>
        public EmitterConfig EmitterConfig;

        /// <summary>
        /// Whether this vertex has a reflector placed on it.
        /// </summary>
        public bool HasReflector;

        /// <summary>
        /// Reflector forced direction index [0..5], valid only if HasReflector is true.
        /// </summary>
        public int ReflectorDirection;

        public VertexState()
        {
            ReferenceCount = 0;
            IsBoundary = true;
            AssignedNote = -1;
            HasEmitter = false;
            HasReflector = false;
            ReflectorDirection = 0;
        }
    }

    /// <summary>
    /// Mutable state associated with an active edge.
    /// </summary>
    public class EdgeState
    {
        /// <summary>
        /// Number of active faces on each side of this edge (0, 1, or 2).
        /// </summary>
        public int ActiveFaceCount;

        /// <summary>
        /// True if exactly one adjacent face is active (this is a boundary edge).
        /// </summary>
        public bool IsBoundary;

        public EdgeState()
        {
            ActiveFaceCount = 0;
            IsBoundary = false;
        }
    }

    /// <summary>
    /// Configuration for an emitter placed on a vertex.
    /// </summary>
    [Serializable]
    public struct EmitterConfig
    {
        /// <summary>
        /// Instrument identifier for pulses emitted by this emitter.
        /// </summary>
        public int InstrumentId;

        /// <summary>
        /// Emission interval in beats (e.g., 1.0 = every beat, 0.5 = every half beat).
        /// </summary>
        public float BeatInterval;

        /// <summary>
        /// Initial energy of emitted pulses.
        /// </summary>
        public float InitialEnergy;

        /// <summary>
        /// Initial direction index [0..5] for emitted pulses.
        /// -1 means emit in all 6 directions.
        /// </summary>
        public int InitialDirection;

        public static EmitterConfig Default => new EmitterConfig
        {
            InstrumentId = 0,
            BeatInterval = 1.0f,
            InitialEnergy = 1.0f,
            InitialDirection = -1
        };
    }

    /// <summary>
    /// Result of a grid modification operation.
    /// </summary>
    public enum GridChangeResult
    {
        Success,
        AlreadyExists,
        NotFound,
        OutOfBounds,
        PulseConflict,
        InvalidTarget
    }
}
