// GridMeshFactory.cs
using UnityEngine;
using TriGrid.Core;

namespace TriGrid.Unity
{
    /// <summary>
    /// Creates shared meshes and prefabs for grid visual elements.
    /// Meshes are created once and shared across all instances via GPU instancing.
    /// </summary>
    public static class GridMeshFactory
    {
        private static Mesh _faceMeshL;
        private static Mesh _faceMeshR;
        private static Mesh _edgeMesh;
        private static Mesh _vertexMesh;

        /// <summary>
        /// Get or create the shared mesh for an up-pointing (L) triangle face.
        /// Vertices in local space, centered at the centroid.
        /// </summary>
        public static Mesh GetFaceMeshL()
        {
            if (_faceMeshL != null) return _faceMeshL;

            float s = GridMath.SideLength;
            float h = GridMath.TriHeight;

            // Up-pointing triangle: base at bottom, apex at top
            // Vertices relative to centroid at origin
            Vector3 v0 = new Vector3(-s / 2f, 0, -h / 3f);      // bottom-left
            Vector3 v1 = new Vector3(s / 2f, 0, -h / 3f);       // bottom-right
            Vector3 v2 = new Vector3(0, 0, 2f * h / 3f);        // top

            _faceMeshL = new Mesh { name = "TriFace_L" };
            _faceMeshL.vertices = new[] { v0, v1, v2 };
            _faceMeshL.triangles = new[] { 0, 2, 1 }; // CCW winding for upward normal
            _faceMeshL.uv = new[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0.5f, 1)
            };
            _faceMeshL.normals = new[] { Vector3.up, Vector3.up, Vector3.up };
            _faceMeshL.RecalculateBounds();

            return _faceMeshL;
        }

        /// <summary>
        /// Get or create the shared mesh for a down-pointing (R) triangle face.
        /// </summary>
        public static Mesh GetFaceMeshR()
        {
            if (_faceMeshR != null) return _faceMeshR;

            float s = GridMath.SideLength;
            float h = GridMath.TriHeight;

            // Down-pointing triangle: base at top, apex at bottom
            Vector3 v0 = new Vector3(-s / 2f, 0, h / 3f);       // top-left
            Vector3 v1 = new Vector3(s / 2f, 0, h / 3f);        // top-right
            Vector3 v2 = new Vector3(0, 0, -2f * h / 3f);       // bottom

            _faceMeshR = new Mesh { name = "TriFace_R" };
            _faceMeshR.vertices = new[] { v0, v1, v2 };
            _faceMeshR.triangles = new[] { 0, 1, 2 }; // CCW winding for upward normal
            _faceMeshR.uv = new[]
            {
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(0.5f, 0)
            };
            _faceMeshR.normals = new[] { Vector3.up, Vector3.up, Vector3.up };
            _faceMeshR.RecalculateBounds();

            return _faceMeshR;
        }

        /// <summary>
        /// Get or create the shared mesh for an edge visual (thin quad).
        /// The quad is 1 unit long along local X, centered at origin.
        /// Scale X to match edge length, scale Z for width.
        /// </summary>
        public static Mesh GetEdgeMesh()
        {
            if (_edgeMesh != null) return _edgeMesh;

            float halfLen = 0.5f;
            float halfWidth = 0.5f; // Will be scaled by transform

            _edgeMesh = new Mesh { name = "EdgeQuad" };
            _edgeMesh.vertices = new[]
            {
                new Vector3(-halfLen, 0, -halfWidth),
                new Vector3(halfLen, 0, -halfWidth),
                new Vector3(halfLen, 0, halfWidth),
                new Vector3(-halfLen, 0, halfWidth)
            };
            _edgeMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _edgeMesh.uv = new[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };
            _edgeMesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            _edgeMesh.RecalculateBounds();

            return _edgeMesh;
        }

        /// <summary>
        /// Get or create the shared mesh for a vertex visual (flat hexagonal disc).
        /// </summary>
        public static Mesh GetVertexMesh()
        {
            if (_vertexMesh != null) return _vertexMesh;

            // Create a hexagonal disc with 6 triangles
            int segments = 6;
            float radius = 0.5f; // Will be scaled by transform

            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            var uvs = new Vector2[segments + 1];
            var normals = new Vector3[segments + 1];

            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            normals[0] = Vector3.up;

            /* -- downwards winding
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                uvs[i + 1] = new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f);
                normals[i + 1] = Vector3.up;

                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }*/
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                
                // UVs look correct, mapping circle to 0-1 square
                uvs[i + 1] = new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f);
                
                // Normals are manually set to UP, which is good for lighting, 
                // but doesn't stop the rasterizer from culling the face if winding is wrong.
                normals[i + 1] = Vector3.up;

                // FIX: Swap the order of vertices to make the triangle winding Clockwise
                triangles[i * 3] = 0;                        // Center
                triangles[i * 3 + 1] = (i + 1) % segments + 1; // Next Vertex
                triangles[i * 3 + 2] = i + 1;                // Current Vertex
            }

            _vertexMesh = new Mesh { name = "VertexHex" };
            _vertexMesh.vertices = vertices;
            _vertexMesh.triangles = triangles;
            _vertexMesh.uv = uvs;
            _vertexMesh.normals = normals;
            _vertexMesh.RecalculateBounds();

            return _vertexMesh;
        }

        /// <summary>
        /// Create a prefab-like GameObject with MeshFilter and MeshRenderer.
        /// This is used as the template for object pools.
        /// </summary>
        public static GameObject CreateVisualPrefab(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.SetActive(false);
            return go;
        }
    }
}
