using System.Collections.Generic;
using UnityEngine;

namespace TrafficGame.Views.Rendering
{
    /// <summary>
    /// Several runtime meshes of one view, each drawn at its own sorting order (one per elevation level, shadows
    /// apart). Fill <see cref="this[int]"/> between <see cref="Clear"/> and <see cref="Apply"/>; renderers are child
    /// objects created on first use with the view's map material.
    /// </summary>
    public sealed class LayeredMeshes
    {
        private readonly Transform _parent;
        private readonly Material _material;
        private readonly SortedDictionary<int, Layer> _layers = new();
        private readonly Dictionary<int, Material> _materials = new();

        public LayeredMeshes(Transform parent, Material material)
        {
            _parent = parent;
            _material = material;
        }

        public MeshBuilder this[int sortingOrder]
        {
            get
            {
                if (!_layers.TryGetValue(sortingOrder, out var layer))
                    _layers.Add(sortingOrder, layer = new Layer());
                return layer.Builder;
            }
        }

        /// <summary>Draws the layer at <paramref name="sortingOrder"/> with another material (for example shadows).</summary>
        public void UseMaterial(int sortingOrder, Material material) => _materials[sortingOrder] = material;

        public void Clear()
        {
            foreach (var layer in _layers.Values)
                layer.Builder.Clear();
        }

        public void Apply()
        {
            // Views may still be cleared while the scene is torn down.
            if (_parent == null)
                return;

            foreach (var (order, layer) in _layers)
            {
                if (layer.Mesh == null)
                {
                    if (layer.Builder.VertexCount == 0)
                        continue;
                    Create(order, layer);
                }

                layer.Builder.ApplyTo(layer.Mesh);
            }
        }

        public void Destroy()
        {
            foreach (var layer in _layers.Values)
            {
                if (layer.Mesh != null)
                    Object.Destroy(layer.Mesh);
            }

            _layers.Clear();
        }

        private void Create(int order, Layer layer)
        {
            var child = new GameObject($"{_parent.name} {order}");
            child.transform.SetParent(_parent, false);
            layer.Mesh = new Mesh { name = child.name };
            child.AddComponent<MeshFilter>().sharedMesh = layer.Mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _materials.TryGetValue(order, out var material) ? material : _material;
            renderer.sortingOrder = order;
        }

        private sealed class Layer
        {
            public readonly MeshBuilder Builder = new();
            public Mesh Mesh;
        }
    }
}
