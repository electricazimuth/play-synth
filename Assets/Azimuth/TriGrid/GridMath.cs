
// GridMath.cs
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace TriGrid.Core
{
    /// <summary>
    /// Pure static utility class for all coordinate conversions and grid math.
    /// The grid lives on the XZ plane (Y = 0). 
    /// All conversions go through this class so the side-length constant is never duplicated.
    /// </summary>
    public static class GridMath
    {
        /// <summary>
        /// Side length of each equilateral triangle in world units.
        /// </summary>
        public const float SideLength = 2.0f;

        /// <summary>
        /// Half the side length, precomputed.
        /// </summary>
        public const float HalfSide = SideLength * 0.5f;

        /// <summary>
        /// Height of an equilateral triangle with the configured side length.
        /// h = S * sqrt(3) / 2
        /// </summary>
        public static readonly float TriHeight = SideLength * Mathf.Sqrt(3f) / 2f;

        /// <summary>
        /// One third of the triangle height, precomputed.
        /// </summary>
        public static readonly float TriHeightThird = TriHeight / 3f;

        /// <summary>
        /// Two thirds of the triangle height, precomputed.
        /// </summary>
        public static readonly float TriHeightTwoThirds = TriHeight * 2f / 3f;

        // Axis vectors for the lattice (XZ plane):
        // i = (SideLength, 0)       -> horizontal step
        // j = (SideLength/2, TriHeight) -> diagonal step
        // In Unity: x maps to world X, "y" in 2D maps to world Z.

        private static readonly float Ix = SideLength;
        private static readonly float Iz = 0f;
        private static readonly float Jx = HalfSide;
        private static readonly float Jz = TriHeight;

        // Inverse matrix for world -> grid conversion
        // | Ix  Jx |^-1    1/det * |  Jz  -Jx |
        // | Iz  Jz |             = | -Iz   Ix |
        private static readonly float Det = Ix * Jz - Jx * Iz;
        private static readonly float InvIx = Jz / Det;
        private static readonly float InvIz = -Iz / Det;
        private static readonly float InvJx = -Jx / Det;
        private static readonly float InvJz = Ix / Det;

        /// <summary>
        /// The 6 canonical adjacent vertex offsets, ordered clockwise from East.
        /// Index 0 = East, 1 = NE, 2 = NW, 3 = West, 4 = SW, 5 = SE.
        /// </summary>
        public static readonly VertexCoord[] AdjacentOffsets = new VertexCoord[6]
        {
            new VertexCoord( 1,  0), // 0: East
            new VertexCoord( 1, -1), // 1: NE  (note: in staggered coords)
            new VertexCoord( 0, -1), // 2: NW  -- corrected from adjacency table
            new VertexCoord(-1,  0), // 3: West
            new VertexCoord(-1,  1), // 4: SW
            new VertexCoord( 0,  1), // 5: SE
        };

        // ─────────────────────────────────────────────
        //  VERTEX CONVERSIONS
        // ─────────────────────────────────────────────

        /// <summary>
        /// Convert a vertex grid coordinate to world position (XZ plane, Y=0).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 VertexToWorld(VertexCoord v, Vector3 gridOrigin = default)
        {
            float x = Ix * v.U + Jx * v.V;
            float z = Iz * v.U + Jz * v.V;
            return new Vector3(x + gridOrigin.x, gridOrigin.y, z + gridOrigin.z);
        }

        /// <summary>
        /// Convert a world position to fractional vertex coordinates.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 WorldToVertexFractional(Vector3 worldPos, Vector3 gridOrigin = default)
        {
            float lx = worldPos.x - gridOrigin.x;
            float lz = worldPos.z - gridOrigin.z;
            float u = InvIx * lx + InvJx * lz;
            float v = InvIz * lx + InvJz * lz;
            return new Vector2(u, v);
        }

        /// <summary>
        /// Convert a world position to the closest vertex coordinate.
        /// Uses rounding on fractional axial coordinates with hexagonal rounding correction.
        /// </summary>
        public static VertexCoord WorldToClosestVertex(Vector3 worldPos, Vector3 gridOrigin = default)
        {
            Vector2 frac = WorldToVertexFractional(worldPos, gridOrigin);

            // Hexagonal / triangular rounding:
            // Round each coordinate, then fix the one with the largest rounding error.
            float fu = frac.x;
            float fv = frac.y;
            float fw = -fu - fv; // third cube coordinate

            int ru = Mathf.RoundToInt(fu);
            int rv = Mathf.RoundToInt(fv);
            int rw = Mathf.RoundToInt(fw);

            float du = Mathf.Abs(ru - fu);
            float dv = Mathf.Abs(rv - fv);
            float dw = Mathf.Abs(rw - fw);

            // The constraint is u + v + w = 0. Fix the largest deviation.
            if (du > dv && du > dw)
                ru = -rv - rw;
            else if (dv > dw)
                rv = -ru - rw;
            // else rw is adjusted implicitly (we don't store w)

            return new VertexCoord(ru, rv);
        }

        // ─────────────────────────────────────────────
        //  FACE CONVERSIONS
        // ─────────────────────────────────────────────

        /// <summary>
        /// Convert a face coordinate to its center world position.
        /// </summary>
        public static Vector3 BUG_BROKEN_FaceCenterToWorld_OFFSET(FaceCoord f, Vector3 gridOrigin = default)
        {
            // Lower-left vertex of the rhombus is at (q, r)
            Vector3 basePos = VertexToWorld(new VertexCoord(f.Q, f.R), gridOrigin);

            if (f.Orient == Orientation.L)
            {
                // L face center: base + (1/2 * i_world, 1/3 * j_world) 
                // Center of up-pointing triangle is at 1/3 height from base
                float cx = basePos.x + HalfSide + Jx / 3f;
                float cz = basePos.z + Jz / 3f;
                return new Vector3(cx, gridOrigin.y, cz);
            }
            else
            {
                // R face center: base + (i_world + 1/3 * j_world_x, 2/3 * j_world_z)
                float cx = basePos.x + Ix / 2f + Jx * 2f / 3f;
                float cz = basePos.z + Jz * 2f / 3f;
                return new Vector3(cx, gridOrigin.y, cz);
            }
        }

        /// <summary>
        /// Convert a face coordinate to its center world position.
        /// </summary>
        public static Vector3 FaceCenterToWorld(FaceCoord f, Vector3 gridOrigin = default)
        {
            // Lower-left vertex of the rhombus is at (q, r)
            Vector3 basePos = VertexToWorld(new VertexCoord(f.Q, f.R), gridOrigin);

            if (f.Orient == Orientation.L)
            {
                // L face center (Up pointing):
                // The centroid X is exactly halfway along the side length.
                // The centroid Z is 1/3 up the height.
                float cx = basePos.x + HalfSide;
                float cz = basePos.z + Jz / 3f;
                return new Vector3(cx, gridOrigin.y, cz);
            }
            else
            {
                // R face center (Down pointing):
                // The centroid X aligns exactly with the full SideLength (Ix) relative to the base.
                // The centroid Z is 2/3 up the height.
                float cx = basePos.x + Ix;
                float cz = basePos.z + Jz * 2f / 3f;
                return new Vector3(cx, gridOrigin.y, cz);
            }
        }

        /// <summary>
        /// Convert a world position to the face coordinate it falls within.
        /// Returns null if outside grid bounds (when bounds checking is enabled).
        /// </summary>
        public static FaceCoord WorldToFace(Vector3 worldPos, Vector3 gridOrigin = default)
        {
            Vector2 frac = WorldToVertexFractional(worldPos, gridOrigin);

            int q = Mathf.FloorToInt(frac.x);
            int r = Mathf.FloorToInt(frac.y);

            float fracQ = frac.x - q;
            float fracR = frac.y - r;

            Orientation orient = (fracQ + fracR < 1.0f) ? Orientation.L : Orientation.R;

            return new FaceCoord(q, r, orient);
        }

        /// <summary>
        /// Try to convert a world position to a face coordinate, respecting grid bounds.
        /// </summary>
        public static bool TryWorldToFace(Vector3 worldPos, int minQ, int minR, int maxQ, int maxR,
            out FaceCoord result, Vector3 gridOrigin = default)
        {
            result = WorldToFace(worldPos, gridOrigin);
            return result.Q >= minQ && result.Q <= maxQ && result.R >= minR && result.R <= maxR;
        }

        // ─────────────────────────────────────────────
        //  EDGE CONVERSIONS
        // ─────────────────────────────────────────────

        /// <summary>
        /// Get the midpoint world position of an edge.
        /// </summary>
        public static Vector3 EdgeMidpointToWorld(EdgeCoord e, Vector3 gridOrigin = default)
        {
            var (a, b) = GridTopology.EdgeEndpoints(e);
            Vector3 pa = VertexToWorld(a, gridOrigin);
            Vector3 pb = VertexToWorld(b, gridOrigin);
            return (pa + pb) * 0.5f;
        }

        /// <summary>
        /// Convert a world position to the closest edge, given the face it's in.
        /// </summary>
        public static EdgeCoord WorldToClosestEdge(Vector3 worldPos, Vector3 gridOrigin = default)
        {
            FaceCoord face = WorldToFace(worldPos, gridOrigin);
            var edges = GridTopology.FaceBorderEdges(face);

            float bestDist = float.MaxValue;
            EdgeCoord bestEdge = edges[0];

            for (int i = 0; i < 3; i++)
            {
                Vector3 mid = EdgeMidpointToWorld(edges[i], gridOrigin);
                float dist = (worldPos - mid).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestEdge = edges[i];
                }
            }

            return bestEdge;
        }

        // ─────────────────────────────────────────────
        //  DISTANCE
        // ─────────────────────────────────────────────

        /// <summary>
        /// Compute the grid distance between two vertices using the triangle Manhattan distance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int VertexDistance(VertexCoord a, VertexCoord b)
        {
            int du = b.U - a.U;
            int dv = b.V - a.V;
            // On a triangular lattice with axial coords, distance = max(|du|, |dv|, |du+dv|)
            // This is the hex distance since vertices form a hex grid.
            return Mathf.Max(Mathf.Abs(du), Mathf.Max(Mathf.Abs(dv), Mathf.Abs(du + dv)));
        }

        /// <summary>
        /// Compute the grid distance between two faces using the ABC coordinate system.
        /// a = v, b = u, c = u + v + R
        /// Distance = |Δa| + |Δb| + |Δc|
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FaceDistance(FaceCoord a, FaceCoord b)
        {
            int a1 = a.R, a2 = b.R;
            int b1 = a.Q, b2 = b.Q;
            int c1 = a.Q + a.R + (int)a.Orient;
            int c2 = b.Q + b.R + (int)b.Orient;

            return Mathf.Abs(a2 - a1) + Mathf.Abs(b2 - b1) + Mathf.Abs(c2 - c1);
        }

        // ─────────────────────────────────────────────
        //  DIRECTION UTILITIES
        // ─────────────────────────────────────────────

        /// <summary>
        /// Get the direction index [0..5] from vertex A to adjacent vertex B.
        /// Returns -1 if B is not adjacent to A.
        /// </summary>
        public static int GetDirectionIndex(VertexCoord from, VertexCoord to)
        {
            int du = to.U - from.U;
            int dv = to.V - from.V;

            for (int i = 0; i < 6; i++)
            {
                if (AdjacentOffsets[i].U == du && AdjacentOffsets[i].V == dv)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Get the adjacent vertex in the given direction [0..5] from a source vertex.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VertexCoord GetAdjacentVertex(VertexCoord from, int directionIndex)
        {
            var offset = AdjacentOffsets[directionIndex];
            return new VertexCoord(from.U + offset.U, from.V + offset.V);
        }

        /// <summary>
        /// Rotate a direction index by a number of 60° steps.
        /// Positive = clockwise, negative = counter-clockwise.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RotateDirection(int directionIndex, int steps)
        {
            return ((directionIndex + steps) % 6 + 6) % 6;
        }

        /// <summary>
        /// Get the opposite direction (180° rotation).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int OppositeDirection(int directionIndex)
        {
            return (directionIndex + 3) % 6;
        }
    }
}
