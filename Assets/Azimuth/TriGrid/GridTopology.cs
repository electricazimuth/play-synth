
// GridTopology.cs
using System.Runtime.CompilerServices;

namespace TriGrid.Core
{
    /// <summary>
    /// Pure static functions encoding all topological relationships between
    /// faces, edges, and vertices on the equilateral triangle grid.
    /// All functions are pure arithmetic on coordinate fields — no dictionary lookups.
    /// </summary>
    public static class GridTopology
    {
        // ─────────────────────────────────────────────
        //  FACE RELATIONSHIPS
        // ─────────────────────────────────────────────

        /// <summary>
        /// Get the 3 neighboring faces of a given face.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FaceNeighbors(FaceCoord face, FaceCoord[] result)
        {
            int q = face.Q, r = face.R;
            if (face.Orient == Orientation.L)
            {
                result[0] = new FaceCoord(q, r, Orientation.R);
                result[1] = new FaceCoord(q, r - 1, Orientation.R);
                result[2] = new FaceCoord(q - 1, r, Orientation.R);
            }
            else
            {
                result[0] = new FaceCoord(q, r + 1, Orientation.L);
                result[1] = new FaceCoord(q + 1, r, Orientation.L);
                result[2] = new FaceCoord(q, r, Orientation.L);
            }
        }

        /// <summary>
        /// Get the 3 neighboring faces as a new array.
        /// </summary>
        public static FaceCoord[] FaceNeighbors(FaceCoord face)
        {
            var result = new FaceCoord[3];
            FaceNeighbors(face, result);
            return result;
        }

        /// <summary>
        /// Get the 3 corner vertices of a face.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FaceVertices(FaceCoord face, VertexCoord[] result)
        {
            int q = face.Q, r = face.R;
            if (face.Orient == Orientation.L)
            {
                result[0] = new VertexCoord(q, r + 1);
                result[1] = new VertexCoord(q + 1, r);
                result[2] = new VertexCoord(q, r);
            }
            else
            {
                result[0] = new VertexCoord(q + 1, r + 1);
                result[1] = new VertexCoord(q + 1, r);
                result[2] = new VertexCoord(q, r + 1);
            }
        }

        /// <summary>
        /// Get the 3 corner vertices as a new array.
        /// </summary>
        public static VertexCoord[] FaceVertices(FaceCoord face)
        {
            var result = new VertexCoord[3];
            FaceVertices(face, result);
            return result;
        }

        /// <summary>
        /// Get the 3 border edges of a face.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FaceBorderEdges(FaceCoord face, EdgeCoord[] result)
        {
            int q = face.Q, r = face.R;
            if (face.Orient == Orientation.L)
            {
                result[0] = new EdgeCoord(q, r, EdgeDirection.E);
                result[1] = new EdgeCoord(q, r, EdgeDirection.N);
                result[2] = new EdgeCoord(q, r, EdgeDirection.W);
            }
            else
            {
                result[0] = new EdgeCoord(q, r + 1, EdgeDirection.N);
                result[1] = new EdgeCoord(q + 1, r, EdgeDirection.W);
                result[2] = new EdgeCoord(q, r, EdgeDirection.E);
            }
        }

        /// <summary>
        /// Get the 3 border edges as a new array.
        /// </summary>
        public static EdgeCoord[] FaceBorderEdges(FaceCoord face)
        {
            var result = new EdgeCoord[3];
            FaceBorderEdges(face, result);
            return result;
        }

        // ─────────────────────────────────────────────
        //  EDGE RELATIONSHIPS
        // ─────────────────────────────────────────────

        /// <summary>
        /// Get the two faces that share this edge.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (FaceCoord, FaceCoord) EdgeJoinsFaces(EdgeCoord edge)
        {
            int q = edge.Q, r = edge.R;
            switch (edge.Dir)
            {
                case EdgeDirection.E:
                    return (new FaceCoord(q, r, Orientation.R), new FaceCoord(q, r, Orientation.L));
                case EdgeDirection.N:
                    return (new FaceCoord(q, r, Orientation.L), new FaceCoord(q, r - 1, Orientation.R));
                case EdgeDirection.W:
                    return (new FaceCoord(q, r, Orientation.L), new FaceCoord(q - 1, r, Orientation.R));
                default:
                    return (default, default);
            }
        }

        /// <summary>
        /// Get the two endpoint vertices of an edge.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (VertexCoord, VertexCoord) EdgeEndpoints(EdgeCoord edge)
        {
            int q = edge.Q, r = edge.R;
            switch (edge.Dir)
            {
                case EdgeDirection.E:
                    return (new VertexCoord(q + 1, r), new VertexCoord(q, r + 1));
                case EdgeDirection.N:
                    return (new VertexCoord(q + 1, r), new VertexCoord(q, r));
                case EdgeDirection.W:
                    return (new VertexCoord(q, r + 1), new VertexCoord(q, r));
                default:
                    return (default, default);
            }
        }

        /// <summary>
        /// Get the two edges that continue in the same line as this edge.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (EdgeCoord, EdgeCoord) EdgeContinues(EdgeCoord edge)
        {
            int q = edge.Q, r = edge.R;
            switch (edge.Dir)
            {
                case EdgeDirection.E:
                    return (new EdgeCoord(q + 1, r - 1, EdgeDirection.E),
                            new EdgeCoord(q - 1, r + 1, EdgeDirection.E));
                case EdgeDirection.N:
                    return (new EdgeCoord(q + 1, r, EdgeDirection.N),
                            new EdgeCoord(q - 1, r, EdgeDirection.N));
                case EdgeDirection.W:
                    return (new EdgeCoord(q, r + 1, EdgeDirection.W),
                            new EdgeCoord(q, r - 1, EdgeDirection.W));
                default:
                    return (default, default);
            }
        }

        // ─────────────────────────────────────────────
        //  VERTEX RELATIONSHIPS
        // ─────────────────────────────────────────────

        /// <summary>
        /// Get the 6 faces that touch (are incident to) this vertex.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void VertexTouchesFaces(VertexCoord v, FaceCoord[] result)
        {
            int u = v.U, vv = v.V;
            result[0] = new FaceCoord(u - 1, vv, Orientation.R);
            result[1] = new FaceCoord(u, vv, Orientation.L);
            result[2] = new FaceCoord(u, vv - 1, Orientation.R);
            result[3] = new FaceCoord(u, vv - 1, Orientation.L);
            result[4] = new FaceCoord(u - 1, vv - 1, Orientation.R);
            result[5] = new FaceCoord(u - 1, vv, Orientation.L);
        }

        /// <summary>
        /// Get the 6 faces touching this vertex as a new array.
        /// </summary>
        public static FaceCoord[] VertexTouchesFaces(VertexCoord v)
        {
            var result = new FaceCoord[6];
            VertexTouchesFaces(v, result);
            return result;
        }

        /// <summary>
        /// Get the 6 edges that protrude from this vertex.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void VertexProtrudesEdges(VertexCoord v, EdgeCoord[] result)
        {
            int u = v.U, vv = v.V;
            result[0] = new EdgeCoord(u, vv, EdgeDirection.W);
            result[1] = new EdgeCoord(u, vv, EdgeDirection.N);
            result[2] = new EdgeCoord(u, vv - 1, EdgeDirection.E);
            result[3] = new EdgeCoord(u, vv - 1, EdgeDirection.W);
            result[4] = new EdgeCoord(u - 1, vv, EdgeDirection.N);
            result[5] = new EdgeCoord(u - 1, vv, EdgeDirection.E);
        }

        /// <summary>
        /// Get the 6 protruding edges as a new array.
        /// </summary>
        public static EdgeCoord[] VertexProtrudesEdges(VertexCoord v)
        {
            var result = new EdgeCoord[6];
            VertexProtrudesEdges(v, result);
            return result;
        }

        /// <summary>
        /// Get the 6 adjacent vertices.
        /// Order matches GridMath.AdjacentOffsets: E, NE, NW, W, SW, SE.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void VertexAdjacentVertices(VertexCoord v, VertexCoord[] result)
        {
            int u = v.U, vv = v.V;
            result[0] = new VertexCoord(u + 1, vv);      // E
            result[1] = new VertexCoord(u + 1, vv - 1);  // NE
            result[2] = new VertexCoord(u, vv - 1);      // NW
            result[3] = new VertexCoord(u - 1, vv);      // W
            result[4] = new VertexCoord(u - 1, vv + 1);  // SW
            result[5] = new VertexCoord(u, vv + 1);      // SE
        }

        /// <summary>
        /// Get the 6 adjacent vertices as a new array.
        /// </summary>
        public static VertexCoord[] VertexAdjacentVertices(VertexCoord v)
        {
            var result = new VertexCoord[6];
            VertexAdjacentVertices(v, result);
            return result;
        }

        /// <summary>
        /// Get the canonical EdgeCoord between two adjacent vertices.
        /// Returns default if vertices are not adjacent.
        /// </summary>
        public static EdgeCoord GetEdgeBetween(VertexCoord a, VertexCoord b)
        {
            int du = b.U - a.U;
            int dv = b.V - a.V;

            // Map the 6 possible (du, dv) pairs to canonical edge coordinates.
            // Using the endpoint tables to derive the canonical form:
            // Edge (q,r,N) has endpoints (q+1,r) and (q,r)
            // Edge (q,r,W) has endpoints (q,r+1) and (q,r)
            // Edge (q,r,E) has endpoints (q+1,r) and (q,r+1)

            if (du == 1 && dv == 0)       // E direction: a->(a.u+1, a.v)
                return new EdgeCoord(a.U, a.V, EdgeDirection.N);      // endpoints: (q+1,r) and (q,r) where q=a.U, r=a.V
            if (du == 0 && dv == -1)      // NW direction: a->(a.u, a.v-1)
                return new EdgeCoord(a.U, a.V - 1, EdgeDirection.W);  // endpoints: (q, r+1) and (q, r) where q=a.U, r=a.V-1
            if (du == 1 && dv == -1)      // NE direction: a->(a.u+1, a.v-1)
                return new EdgeCoord(a.U, a.V - 1, EdgeDirection.E);  // endpoints: (q+1, r) and (q, r+1) where q=a.U, r=a.V-1
            if (du == -1 && dv == 0)      // W direction: a->(a.u-1, a.v)
                return new EdgeCoord(a.U - 1, a.V, EdgeDirection.N);
            if (du == 0 && dv == 1)       // SE direction: a->(a.u, a.v+1)
                return new EdgeCoord(a.U, a.V, EdgeDirection.W);
            if (du == -1 && dv == 1)      // SW direction: a->(a.u-1, a.v)
                return new EdgeCoord(a.U - 1, a.V, EdgeDirection.E);

            return default; // not adjacent
        }
    }
}
