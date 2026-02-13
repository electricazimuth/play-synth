// PulseData.cs
using TriGrid.Core;

namespace TriGrid.Pulse
{
    /// <summary>
    /// Runtime data for a single pulse traveling the grid.
    /// </summary>
    public class PulseData
    {
        private static int _nextId = 0;

        public int Id { get; private set; }
        public VertexCoord CurrentVertex;
        public VertexCoord TargetVertex;
        public EdgeCoord CurrentEdge;
        public int DirectionIndex; // 0..5
        public float Energy;
        public int InstrumentId;
        public float ProgressAlongEdge; // 0..1
        public bool IsMoving;
        public bool IsAlive;

        public PulseData()
        {
            Id = _nextId++;
            IsAlive = true;
        }

        public static void ResetIdCounter() => _nextId = 0;
    }
}