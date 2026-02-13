
// GridInputHandler.cs
using UnityEngine;
using TriGrid.Core;

namespace TriGrid.Unity
{
    /// <summary>
    /// The active interaction mode determines how mouse hits are resolved.
    /// </summary>
    public enum GridInteractionMode
    {
        Face,
        Vertex,
        Edge
    }

    /// <summary>
    /// Converts mouse input to grid coordinates via plane raycasting.
    /// Does not modify grid data directly — raises events consumed by GridController.
    /// </summary>
    public class GridInputHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _camera;

        [Header("Configuration")]
        [SerializeField] private GridInteractionMode _interactionMode = GridInteractionMode.Face;
        [SerializeField] private float _planeY = 0f;

        // ─── Current hover state ───
        private FaceCoord? _hoveredFace;
        private VertexCoord? _hoveredVertex;
        private EdgeCoord? _hoveredEdge;
        private Vector3 _lastHitPoint;
        private bool _hasHit;

        // ─── Grid reference (set by GridController) ───
        private TriGridData _gridData;
        private Vector3 _gridOrigin;

        // ─── Events ───
        public event System.Action<FaceCoord> OnFaceHovered;
        public event System.Action<VertexCoord> OnVertexHovered;
        public event System.Action<EdgeCoord> OnEdgeHovered;
        public event System.Action OnHoverLost;

        public event System.Action<FaceCoord> OnFaceClicked;
        public event System.Action<VertexCoord> OnVertexClicked;
        public event System.Action<EdgeCoord> OnEdgeClicked;

        public event System.Action<FaceCoord> OnFaceRightClicked;
        public event System.Action<VertexCoord> OnVertexRightClicked;
        public event System.Action<EdgeCoord> OnEdgeRightClicked;

        // ─── Public properties ───
        public GridInteractionMode InteractionMode
        {
            get => _interactionMode;
            set => _interactionMode = value;
        }

        public FaceCoord? HoveredFace => _hoveredFace;
        public VertexCoord? HoveredVertex => _hoveredVertex;
        public EdgeCoord? HoveredEdge => _hoveredEdge;
        public Vector3 LastHitPoint => _lastHitPoint;
        public bool HasHit => _hasHit;

        /// <summary>
        /// Initialize with grid data and origin. Called by GridController.
        /// </summary>
        public void Initialize(TriGridData gridData, Vector3 gridOrigin)
        {
            _gridData = gridData;
            _gridOrigin = gridOrigin;

            if (_camera == null)
                _camera = Camera.main;
        }

        private void Update()
        {
            if (_camera == null || _gridData == null)
                return;

            UpdateRaycast();
            HandleClicks();
        }

        private void UpdateRaycast()
        {
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            Plane plane = new Plane(Vector3.up, new Vector3(0, _planeY, 0));

            if (plane.Raycast(ray, out float enter))
            {
                _hasHit = true;
                _lastHitPoint = ray.GetPoint(enter);
                ResolveHover(_lastHitPoint);
            }
            else
            {
                if (_hasHit)
                {
                    ClearHover();
                }
                _hasHit = false;
            }
        }

        private void ResolveHover(Vector3 hitPoint)
        {
            switch (_interactionMode)
            {
                case GridInteractionMode.Face:
                    ResolveFaceHover(hitPoint);
                    break;
                case GridInteractionMode.Vertex:
                    ResolveVertexHover(hitPoint);
                    break;
                case GridInteractionMode.Edge:
                    ResolveEdgeHover(hitPoint);
                    break;
            }
        }

        private void ResolveFaceHover(Vector3 hitPoint)
        {
            if (GridMath.TryWorldToFace(hitPoint, _gridData.MinQ, _gridData.MinR,
                _gridData.MaxQ, _gridData.MaxR, out var face, _gridOrigin))
            {
                if (!_hoveredFace.HasValue || !_hoveredFace.Value.Equals(face))
                {
                    _hoveredFace = face;
                    _hoveredVertex = null;
                    _hoveredEdge = null;
                    OnFaceHovered?.Invoke(face);
                }
            }
            else
            {
                ClearHover();
            }
        }

        private void ResolveVertexHover(Vector3 hitPoint)
        {
            var vertex = GridMath.WorldToClosestVertex(hitPoint, _gridOrigin);
            if (_gridData.IsInBounds(vertex))
            {
                if (!_hoveredVertex.HasValue || !_hoveredVertex.Value.Equals(vertex))
                {
                    _hoveredVertex = vertex;
                    _hoveredFace = null;
                    _hoveredEdge = null;
                    OnVertexHovered?.Invoke(vertex);
                }
            }
            else
            {
                ClearHover();
            }
        }

        private void ResolveEdgeHover(Vector3 hitPoint)
        {
            var edge = GridMath.WorldToClosestEdge(hitPoint, _gridOrigin);
            if (!_hoveredEdge.HasValue || !_hoveredEdge.Value.Equals(edge))
            {
                _hoveredEdge = edge;
                _hoveredFace = null;
                _hoveredVertex = null;
                OnEdgeHovered?.Invoke(edge);
            }
        }

        private void ClearHover()
        {
            bool hadHover = _hoveredFace.HasValue || _hoveredVertex.HasValue || _hoveredEdge.HasValue;
            _hoveredFace = null;
            _hoveredVertex = null;
            _hoveredEdge = null;

            if (hadHover)
                OnHoverLost?.Invoke();
        }

        private void HandleClicks()
        {
            if (!_hasHit) return;

            // Left click
            if (Input.GetMouseButtonDown(0))
            {
                switch (_interactionMode)
                {
                    case GridInteractionMode.Face:
                        if (_hoveredFace.HasValue)
                            OnFaceClicked?.Invoke(_hoveredFace.Value);
                        break;
                    case GridInteractionMode.Vertex:
                        if (_hoveredVertex.HasValue)
                            OnVertexClicked?.Invoke(_hoveredVertex.Value);
                        break;
                    case GridInteractionMode.Edge:
                        if (_hoveredEdge.HasValue)
                            OnEdgeClicked?.Invoke(_hoveredEdge.Value);
                        break;
                }
            }

            // Right click
            if (Input.GetMouseButtonDown(1))
            {
                switch (_interactionMode)
                {
                    case GridInteractionMode.Face:
                        if (_hoveredFace.HasValue)
                            OnFaceRightClicked?.Invoke(_hoveredFace.Value);
                        break;
                    case GridInteractionMode.Vertex:
                        if (_hoveredVertex.HasValue)
                            OnVertexRightClicked?.Invoke(_hoveredVertex.Value);
                        break;
                    case GridInteractionMode.Edge:
                        if (_hoveredEdge.HasValue)
                            OnEdgeRightClicked?.Invoke(_hoveredEdge.Value);
                        break;
                }
            }
        }
    }
}
