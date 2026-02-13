// GridVisualManager.cs
using System.Collections.Generic;
using UnityEngine;
using TriGrid.Core;

namespace TriGrid.Unity
{
    /// <summary>
    /// Manages all visual GameObjects for the grid.
    /// Subscribes to TriGridData events and maintains pooled visual elements.
    /// </summary>
    public class GridVisualManager : MonoBehaviour
    {
        [Header("Materials")]
        [SerializeField] private Material _faceMaterial;
        [SerializeField] private Material _faceHoverMaterial;
        [SerializeField] private Material _edgeMaterial;
        [SerializeField] private Material _edgeBoundaryMaterial;
        [SerializeField] private Material _vertexMaterial;
        [SerializeField] private Material _vertexBoundaryMaterial;
        [SerializeField] private Material _vertexEmitterMaterial;
        [SerializeField] private Material _vertexReflectorMaterial;
        [SerializeField] private Material _previewMaterial;

        [Header("Visual Settings")]
        [SerializeField] private float _edgeWidth = 0.06f;
        [SerializeField] private float _vertexScale = 0.2f;
        [SerializeField] private float _vertexBoundaryScale = 0.3f;
        [SerializeField] private float _faceYOffset = 0f;
        [SerializeField] private float _edgeYOffset = 0.001f;
        [SerializeField] private float _vertexYOffset = 0.002f;

        [Header("Pool Settings")]
        [SerializeField] private int _initialFacePoolSize = 200;
        [SerializeField] private int _initialEdgePoolSize = 400;
        [SerializeField] private int _initialVertexPoolSize = 200;

        // ─── Pools ───
        private GameObjectPool _facePoolL;
        private GameObjectPool _facePoolR;
        private GameObjectPool _edgePool;
        private GameObjectPool _vertexPool;

        // ─── Active visual tracking ───
        private readonly Dictionary<FaceCoord, GameObject> _activeFaceVisuals = new Dictionary<FaceCoord, GameObject>();
        private readonly Dictionary<EdgeCoord, GameObject> _activeEdgeVisuals = new Dictionary<EdgeCoord, GameObject>();
        private readonly Dictionary<VertexCoord, GameObject> _activeVertexVisuals = new Dictionary<VertexCoord, GameObject>();

        // ─── Preview ───
        private GameObject _previewObject;

        // ─── References ───
        private TriGridData _gridData;
        private Vector3 _gridOrigin;

        // ─── Pool parents ───
        private Transform _faceParent;
        private Transform _edgeParent;
        private Transform _vertexParent;

        public void Initialize(TriGridData gridData, Vector3 gridOrigin)
        {
            _gridData = gridData;
            _gridOrigin = gridOrigin;

            CreatePoolParents();
            CreatePools();
            CreatePreviewObject();
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            _facePoolL?.DestroyAll();
            _facePoolR?.DestroyAll();
            _edgePool?.DestroyAll();
            _vertexPool?.DestroyAll();
        }

        // ─────────────────────────────────────────────
        //  SETUP
        // ─────────────────────────────────────────────

        private void CreatePoolParents()
        {
            _faceParent = new GameObject("FaceVisuals").transform;
            _faceParent.SetParent(transform);
            _edgeParent = new GameObject("EdgeVisuals").transform;
            _edgeParent.SetParent(transform);
            _vertexParent = new GameObject("VertexVisuals").transform;
            _vertexParent.SetParent(transform);
        }

        private void CreatePools()
        {
            // Face pools (one per orientation since they use different meshes)
            var facePrefabL = GridMeshFactory.CreateVisualPrefab("FaceL", GridMeshFactory.GetFaceMeshL(), _faceMaterial);
            var facePrefabR = GridMeshFactory.CreateVisualPrefab("FaceR", GridMeshFactory.GetFaceMeshR(), _faceMaterial);
            var edgePrefab = GridMeshFactory.CreateVisualPrefab("Edge", GridMeshFactory.GetEdgeMesh(), _edgeMaterial);
            var vertexPrefab = GridMeshFactory.CreateVisualPrefab("Vertex", GridMeshFactory.GetVertexMesh(), _vertexMaterial);

            _facePoolL = new GameObjectPool(facePrefabL, _faceParent, _initialFacePoolSize / 2);
            _facePoolR = new GameObjectPool(facePrefabR, _faceParent, _initialFacePoolSize / 2);
            _edgePool = new GameObjectPool(edgePrefab, _edgeParent, _initialEdgePoolSize);
            _vertexPool = new GameObjectPool(vertexPrefab, _vertexParent, _initialVertexPoolSize);

            // Clean up template prefabs
            Destroy(facePrefabL);
            Destroy(facePrefabR);
            Destroy(edgePrefab);
            Destroy(vertexPrefab);
        }

        private void CreatePreviewObject()
        {
            _previewObject = GridMeshFactory.CreateVisualPrefab("Preview",
                GridMeshFactory.GetFaceMeshL(), _previewMaterial);
            _previewObject.transform.SetParent(transform);
            _previewObject.SetActive(false);
        }

        // ─────────────────────────────────────────────
        //  EVENT SUBSCRIPTION
        // ─────────────────────────────────────────────

        private void SubscribeToEvents()
        {
            if (_gridData == null) return;

            _gridData.OnFaceAdded += HandleFaceAdded;
            _gridData.OnFaceRemoved += HandleFaceRemoved;
            _gridData.OnVertexCreated += HandleVertexCreated;
            _gridData.OnVertexDestroyed += HandleVertexDestroyed;
            _gridData.OnEdgeCreated += HandleEdgeCreated;
            _gridData.OnEdgeDestroyed += HandleEdgeDestroyed;
            _gridData.OnVertexBoundaryChanged += HandleVertexBoundaryChanged;
            _gridData.OnEdgeBoundaryChanged += HandleEdgeBoundaryChanged;
            _gridData.OnEmitterPlaced += HandleEmitterPlaced;
            _gridData.OnEmitterRemoved += HandleEmitterRemoved;
            _gridData.OnReflectorPlaced += HandleReflectorPlaced;
            _gridData.OnReflectorRemoved += HandleReflectorRemoved;
        }

        private void UnsubscribeFromEvents()
        {
            if (_gridData == null) return;

            _gridData.OnFaceAdded -= HandleFaceAdded;
            _gridData.OnFaceRemoved -= HandleFaceRemoved;
            _gridData.OnVertexCreated -= HandleVertexCreated;
            _gridData.OnVertexDestroyed -= HandleVertexDestroyed;
            _gridData.OnEdgeCreated -= HandleEdgeCreated;
            _gridData.OnEdgeDestroyed -= HandleEdgeDestroyed;
            _gridData.OnVertexBoundaryChanged -= HandleVertexBoundaryChanged;
            _gridData.OnEdgeBoundaryChanged -= HandleEdgeBoundaryChanged;
            _gridData.OnEmitterPlaced -= HandleEmitterPlaced;
            _gridData.OnEmitterRemoved -= HandleEmitterRemoved;
            _gridData.OnReflectorPlaced -= HandleReflectorPlaced;
            _gridData.OnReflectorRemoved -= HandleReflectorRemoved;
        }

        // ─────────────────────────────────────────────
        //  FACE VISUALS
        // ─────────────────────────────────────────────

        private void HandleFaceAdded(FaceCoord face)
        {
            if (_activeFaceVisuals.ContainsKey(face)) return;

            var pool = face.Orient == Orientation.L ? _facePoolL : _facePoolR;
            var go = pool.Acquire();

            Vector3 center = GridMath.FaceCenterToWorld(face, _gridOrigin);
            center.y = _gridOrigin.y + _faceYOffset;
            go.transform.position = center;
            go.name = face.ToString();

            _activeFaceVisuals[face] = go;
        }

        private void HandleFaceRemoved(FaceCoord face)
        {
            if (_activeFaceVisuals.TryGetValue(face, out var go))
            {
                var pool = face.Orient == Orientation.L ? _facePoolL : _facePoolR;
                pool.Release(go);
                _activeFaceVisuals.Remove(face);
            }
        }

        // ─────────────────────────────────────────────
        //  EDGE VISUALS
        // ─────────────────────────────────────────────

        private void HandleEdgeCreated(EdgeCoord edge)
        {
            if (_activeEdgeVisuals.ContainsKey(edge)) return;

            var go = _edgePool.Acquire();
            PositionEdgeVisual(go, edge);

            bool isBoundary = _gridData.IsBoundaryEdge(edge);
            UpdateEdgeMaterial(go, isBoundary);

            go.name = edge.ToString();
            _activeEdgeVisuals[edge] = go;
        }

        private void HandleEdgeDestroyed(EdgeCoord edge)
        {
            if (_activeEdgeVisuals.TryGetValue(edge, out var go))
            {
                _edgePool.Release(go);
                _activeEdgeVisuals.Remove(edge);
            }
        }

        private void HandleEdgeBoundaryChanged(EdgeCoord edge, bool isBoundary)
        {
            if (_activeEdgeVisuals.TryGetValue(edge, out var go))
            {
                UpdateEdgeMaterial(go, isBoundary);
            }
        }

        private void PositionEdgeVisual(GameObject go, EdgeCoord edge)
        {
            var (a, b) = GridTopology.EdgeEndpoints(edge);
            Vector3 pa = GridMath.VertexToWorld(a, _gridOrigin);
            Vector3 pb = GridMath.VertexToWorld(b, _gridOrigin);

            Vector3 mid = (pa + pb) * 0.5f;
            mid.y = _gridOrigin.y + _edgeYOffset;

            Vector3 dir = pb - pa;
            float length = dir.magnitude;

            go.transform.position = mid;
            go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            // Edge mesh is 1 unit along X; we scale X to edge length, Z to width
            go.transform.localScale = new Vector3(length, 1f, _edgeWidth);
            // Correct rotation: the mesh extends along local X, but LookRotation aligns Z
            go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up) *
                                    Quaternion.Euler(0, 90, 0);
        }

        private void UpdateEdgeMaterial(GameObject go, bool isBoundary)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = isBoundary ? _edgeBoundaryMaterial : _edgeMaterial;
            }
        }

        // ─────────────────────────────────────────────
        //  VERTEX VISUALS
        // ─────────────────────────────────────────────

        private void HandleVertexCreated(VertexCoord vertex)
        {
            if (_activeVertexVisuals.ContainsKey(vertex)) return;

            var go = _vertexPool.Acquire();
            Vector3 pos = GridMath.VertexToWorld(vertex, _gridOrigin);
            pos.y = _gridOrigin.y + _vertexYOffset;
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * _vertexScale;

            go.name = vertex.ToString();
            _activeVertexVisuals[vertex] = go;

            // Set initial material
            UpdateVertexAppearance(vertex);
        }

        private void HandleVertexDestroyed(VertexCoord vertex)
        {
            if (_activeVertexVisuals.TryGetValue(vertex, out var go))
            {
                _vertexPool.Release(go);
                _activeVertexVisuals.Remove(vertex);
            }
        }

        private void HandleVertexBoundaryChanged(VertexCoord vertex, bool isBoundary)
        {
            UpdateVertexAppearance(vertex);
        }

        private void HandleEmitterPlaced(VertexCoord vertex, EmitterConfig config)
        {
            UpdateVertexAppearance(vertex);
        }

        private void HandleEmitterRemoved(VertexCoord vertex)
        {
            UpdateVertexAppearance(vertex);
        }

        private void HandleReflectorPlaced(VertexCoord vertex, int direction)
        {
            UpdateVertexAppearance(vertex);
        }

        private void HandleReflectorRemoved(VertexCoord vertex)
        {
            UpdateVertexAppearance(vertex);
        }

        private void UpdateVertexAppearance(VertexCoord vertex)
        {
            if (!_activeVertexVisuals.TryGetValue(vertex, out var go)) return;
            if (!_gridData.TryGetVertexState(vertex, out var state)) return;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            // Priority: Emitter > Reflector > Boundary > Normal
            if (state.HasEmitter)
            {
                renderer.sharedMaterial = _vertexEmitterMaterial;
                go.transform.localScale = Vector3.one * _vertexBoundaryScale;
            }
            else if (state.HasReflector)
            {
                renderer.sharedMaterial = _vertexReflectorMaterial;
                go.transform.localScale = Vector3.one * _vertexBoundaryScale;
                // Rotate to show direction
                float angle = state.ReflectorDirection * 60f;
                go.transform.rotation = Quaternion.Euler(0, angle, 0);
            }
            else if (state.IsBoundary)
            {
                renderer.sharedMaterial = _vertexBoundaryMaterial;
                go.transform.localScale = Vector3.one * _vertexBoundaryScale;
            }
            else
            {
                renderer.sharedMaterial = _vertexMaterial;
                go.transform.localScale = Vector3.one * _vertexScale;
            }
        }

        // ─────────────────────────────────────────────
        //  PREVIEW / HOVER
        // ─────────────────────────────────────────────

        /// <summary>
        /// Show a preview of a face at the given coordinate.
        /// </summary>
        public void ShowFacePreview(FaceCoord face)
        {
            if (_previewObject == null) return;

            // Swap mesh based on orientation
            var mf = _previewObject.GetComponent<MeshFilter>();
            if (mf != null)
            {
                mf.sharedMesh = face.Orient == Orientation.L
                    ? GridMeshFactory.GetFaceMeshL()
                    : GridMeshFactory.GetFaceMeshR();
            }

            Vector3 center = GridMath.FaceCenterToWorld(face, _gridOrigin);
            //Debug.Log("ShowFacePreview face:" +  face.ToString() + " FaceCenterToWorld center:" + center.ToString() );
            center.y = _gridOrigin.y + _faceYOffset + 0.001f; // Slightly above faces
            _previewObject.transform.position = center;
            _previewObject.SetActive(true);
        }

        /// <summary>
        /// Show a preview at a vertex position.
        /// </summary>
        public void ShowVertexPreview(VertexCoord vertex)
        {
            if (_previewObject == null) return;

            var mf = _previewObject.GetComponent<MeshFilter>();
            if (mf != null)
                mf.sharedMesh = GridMeshFactory.GetVertexMesh();

            Vector3 pos = GridMath.VertexToWorld(vertex, _gridOrigin);
            pos.y = _gridOrigin.y + _vertexYOffset + 0.001f;
            _previewObject.transform.position = pos;
            _previewObject.transform.localScale = Vector3.one * _vertexBoundaryScale;
            _previewObject.SetActive(true);
        }

        /// <summary>
        /// Hide the preview object.
        /// </summary>
        public void HidePreview()
        {
            if (_previewObject != null)
                _previewObject.SetActive(false);
        }

        // ─────────────────────────────────────────────
        //  UTILITY
        // ─────────────────────────────────────────────

        /// <summary>
        /// Get the visual GameObject for a face, if it exists.
        /// </summary>
        public GameObject GetFaceVisual(FaceCoord face)
        {
            _activeFaceVisuals.TryGetValue(face, out var go);
            return go;
        }

        /// <summary>
        /// Get the visual GameObject for a vertex, if it exists.
        /// </summary>
        public GameObject GetVertexVisual(VertexCoord vertex)
        {
            _activeVertexVisuals.TryGetValue(vertex, out var go);
            return go;
        }

        /// <summary>
        /// Get the visual GameObject for an edge, if it exists.
        /// </summary>
        public GameObject GetEdgeVisual(EdgeCoord edge)
        {
            _activeEdgeVisuals.TryGetValue(edge, out var go);
            return go;
        }
    }
}
