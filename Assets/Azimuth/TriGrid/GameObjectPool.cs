
// GameObjectPool.cs
using System.Collections.Generic;
using UnityEngine;

namespace TriGrid.Unity
{
    /// <summary>
    /// Generic GameObject pool for efficient reuse of visual elements.
    /// </summary>
    public class GameObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly Stack<GameObject> _available;
        private readonly HashSet<GameObject> _inUse;
        private readonly int _growthIncrement;

        public int TotalCount => _available.Count + _inUse.Count;
        public int AvailableCount => _available.Count;
        public int InUseCount => _inUse.Count;

        public GameObjectPool(GameObject prefab, Transform parent, int initialCapacity = 64,
            int growthIncrement = 32)
        {
            _prefab = prefab;
            _parent = parent;
            _growthIncrement = growthIncrement;
            _available = new Stack<GameObject>(initialCapacity);
            _inUse = new HashSet<GameObject>();

            Preallocate(initialCapacity);
        }

        private void Preallocate(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var go = Object.Instantiate(_prefab, _parent);
                go.SetActive(false);
                _available.Push(go);
            }
        }

        /// <summary>
        /// Acquire a GameObject from the pool. Activates it.
        /// </summary>
        public GameObject Acquire()
        {
            if (_available.Count == 0)
            {
                Preallocate(_growthIncrement);
            }

            var go = _available.Pop();
            go.SetActive(true);
            _inUse.Add(go);
            return go;
        }

        /// <summary>
        /// Return a GameObject to the pool. Deactivates it.
        /// </summary>
        public void Release(GameObject go)
        {
            if (go == null) return;

            if (_inUse.Remove(go))
            {
                go.SetActive(false);
                go.transform.SetParent(_parent);
                _available.Push(go);
            }
        }

        /// <summary>
        /// Release all currently in-use objects back to the pool.
        /// </summary>
        public void ReleaseAll()
        {
            foreach (var go in _inUse)
            {
                if (go != null)
                {
                    go.SetActive(false);
                    go.transform.SetParent(_parent);
                    _available.Push(go);
                }
            }
            _inUse.Clear();
        }

        /// <summary>
        /// Destroy all pooled objects.
        /// </summary>
        public void DestroyAll()
        {
            foreach (var go in _available)
            {
                if (go != null) Object.Destroy(go);
            }
            foreach (var go in _inUse)
            {
                if (go != null) Object.Destroy(go);
            }
            _available.Clear();
            _inUse.Clear();
        }
    }
}
