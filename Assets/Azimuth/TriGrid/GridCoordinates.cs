
// GridCoordinates.cs
using System;

namespace TriGrid.Core
{
    /// <summary>
    /// Orientation of a triangle face within a rhombus cell.
    /// L = Up-pointing (left), R = Down-pointing (right).
    /// </summary>
    public enum Orientation : byte
    {
        L = 0, // Up-pointing
        R = 1  // Down-pointing
    }

    /// <summary>
    /// Edge direction within the triangle grid.
    /// Each rhombus cell has three edge types.
    /// </summary>
    public enum EdgeDirection : byte
    {
        N = 0, // North
        W = 1, // West
        E = 2  // East (the diagonal seam splitting the rhombus)
    }

    /// <summary>
    /// Immutable vertex coordinate on the triangle lattice.
    /// Vertices are identified by integer pair (u, v) in axial coordinates.
    /// </summary>
    public readonly struct VertexCoord : IEquatable<VertexCoord>
    {
        public readonly int U;
        public readonly int V;

        public VertexCoord(int u, int v)
        {
            U = u;
            V = v;
        }

        public bool Equals(VertexCoord other) => U == other.U && V == other.V;
        public override bool Equals(object obj) => obj is VertexCoord other && Equals(other);
        public override int GetHashCode() => unchecked(U * 7919 + V * 7927);
        public override string ToString() => $"V({U},{V})";

        public static bool operator ==(VertexCoord a, VertexCoord b) => a.Equals(b);
        public static bool operator !=(VertexCoord a, VertexCoord b) => !a.Equals(b);
    }

    /// <summary>
    /// Immutable face (triangle) coordinate.
    /// Each rhombus at (q,r) contains two faces: L (up-pointing) and R (down-pointing).
    /// </summary>
    public readonly struct FaceCoord : IEquatable<FaceCoord>
    {
        public readonly int Q;
        public readonly int R;
        public readonly Orientation Orient;

        public FaceCoord(int q, int r, Orientation orient)
        {
            Q = q;
            R = r;
            Orient = orient;
        }

        public bool Equals(FaceCoord other) => Q == other.Q && R == other.R && Orient == other.Orient;
        public override bool Equals(object obj) => obj is FaceCoord other && Equals(other);
        public override int GetHashCode() => unchecked(Q * 7919 + R * 7927 + (int)Orient * 7933);
        public override string ToString() => $"F({Q},{R},{Orient})";

        public static bool operator ==(FaceCoord a, FaceCoord b) => a.Equals(b);
        public static bool operator !=(FaceCoord a, FaceCoord b) => !a.Equals(b);
    }

    /// <summary>
    /// Immutable edge coordinate.
    /// Edges are canonically identified by (q, r, direction).
    /// </summary>
    public readonly struct EdgeCoord : IEquatable<EdgeCoord>
    {
        public readonly int Q;
        public readonly int R;
        public readonly EdgeDirection Dir;

        public EdgeCoord(int q, int r, EdgeDirection dir)
        {
            Q = q;
            R = r;
            Dir = dir;
        }

        public bool Equals(EdgeCoord other) => Q == other.Q && R == other.R && Dir == other.Dir;
        public override bool Equals(object obj) => obj is EdgeCoord other && Equals(other);
        public override int GetHashCode() => unchecked(Q * 7919 + R * 7927 + (int)Dir * 7933);
        public override string ToString() => $"E({Q},{R},{Dir})";

        public static bool operator ==(EdgeCoord a, EdgeCoord b) => a.Equals(b);
        public static bool operator !=(EdgeCoord a, EdgeCoord b) => !a.Equals(b);
    }
}
