// GridDebugVisualizer.cs
using UnityEngine;
using TriGrid.Core;

namespace TriGrid.Unity
{
    /// <summary>
    /// Draws debug gizmos for the grid in the Scene view.
    /// Useful for development and testing.
    /// </summary>
    public class GridDebugVisualizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridController _gridController;

        [Header("Display Options")]
        [SerializeField] private bool _showGridOutline = true;
        [SerializeField] private bool _showAllPossibleFaces = false;
        [SerializeField] private bool _showVertexLabels = false;
        [SerializeField] private bool _showBoundaryHighlight = true;
        [SerializeField] private Vector3 _gridOrigin = Vector3.zero;

        [Header("Colors")]
        [SerializeField] private Color _gridLineColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);
        [SerializeField] private Color _activeFaceColor = new Color(0.2f, 0.6f, 1f, 0.3f);
        [SerializeField] private Color _boundaryEdgeColor = new Color(1f, 0.8f, 0.2f, 0.8f);
        [SerializeField] private Color _boundaryVertexColor = new Color(1f, 0.4f, 0.2f, 0.8f);

        private TriGridData GridData => _gridController?.GridData;

        private readonly VertexCoord[] _faceVerts = new VertexCoord[3];

        private void OnDrawGizmos()
        {
            if (_showGridOutline)
            {
                DrawGridOutline();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (GridData == null) return;

            if (_showAllPossibleFaces)
            {
                DrawAllPossibleFaces();
            }

            if (_showBoundaryHighlight)
            {
                DrawBoundaryHighlights();
            }
        }

        private void DrawGridOutline()
        {
            int minQ = _gridController != null ? GridData?.MinQ ?? 0 : 0;
            int minR = _gridController != null ? GridData?.MinR ?? 0 : 0;
            int maxQ = _gridController != null ? GridData?.MaxQ ?? 11 : 11;
            int maxR = _gridController != null ? GridData?.MaxR ?? 11 : 11;

            Gizmos.color = _gridLineColor;

            for (int u = minQ; u <= maxQ + 1; u++)
            {
                for (int v = minR; v <= maxR + 1; v++)
                {
                    var vert = new VertexCoord(u, v);
                    Vector3 pos = GridMath.VertexToWorld(vert, _gridOrigin);

                    // Draw edges to adjacent vertices (only right and up to avoid doubles)
                    if (u < maxQ + 1)
                    {
                        var right = new VertexCoord(u + 1, v);
                        Vector3 rPos = GridMath.VertexToWorld(right, _gridOrigin);
                        Gizmos.DrawLine(pos, rPos);
                    }

                    if (v < maxR + 1)
                    {
                        var up = new VertexCoord(u, v + 1);
                        Vector3 uPos = GridMath.VertexToWorld(up, _gridOrigin);
                        Gizmos.DrawLine(pos, uPos);
                    }

                    if (u < maxQ + 1 && v > minR)
                    {
                        var diag = new VertexCoord(u + 1, v - 1);
                        Vector3 dPos = GridMath.VertexToWorld(diag, _gridOrigin);
                        Gizmos.DrawLine(pos, dPos);
                    }
                }
            }
        }

        private void DrawAllPossibleFaces()
        {
            Gizmos.color = _activeFaceColor;

            foreach (var face in GridData.ActiveFaces)
            {
                GridTopology.FaceVertices(face, _faceVerts);
                Vector3 v0 = GridMath.VertexToWorld(_faceVerts[0], _gridOrigin);
                Vector3 v1 = GridMath.VertexToWorld(_faceVerts[1], _gridOrigin);
                Vector3 v2 = GridMath.VertexToWorld(_faceVerts[2], _gridOrigin);

                Gizmos.DrawLine(v0, v1);
                Gizmos.DrawLine(v1, v2);
                Gizmos.DrawLine(v2, v0);
            }
        }

        private void DrawBoundaryHighlights()
        {
            // Draw boundary edges
            Gizmos.color = _boundaryEdgeColor;
            foreach (var kvp in GridData.AllEdges)
            {
                if (kvp.Value.IsBoundary)
                {
                    var (a, b) = GridTopology.EdgeEndpoints(kvp.Key);
                    Vector3 pa = GridMath.VertexToWorld(a, _gridOrigin);
                    Vector3 pb = GridMath.VertexToWorld(b, _gridOrigin);
                    Gizmos.DrawLine(pa, pb);
                }
            }

            // Draw boundary vertices
            Gizmos.color = _boundaryVertexColor;
            foreach (var kvp in GridData.AllVertices)
            {
                if (kvp.Value.IsBoundary)
                {
                    Vector3 pos = GridMath.VertexToWorld(kvp.Key, _gridOrigin);
                    Gizmos.DrawWireSphere(pos, 0.15f);
                }
            }
        }
    }
}
