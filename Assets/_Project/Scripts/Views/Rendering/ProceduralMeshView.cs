using UnityEngine;

namespace TrafficGame.Views.Rendering
{
    /// <summary>Base for views that draw one runtime-generated mesh with the shared map material.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public abstract class ProceduralMeshView : MonoBehaviour
    {
        private Mesh _mesh;
        private LayeredMeshes _layers;

        /// <summary>Draw order among map layers; higher is drawn on top.</summary>
        protected abstract int SortingOrder { get; }

        protected MeshRenderer Renderer => GetComponent<MeshRenderer>();

        /// <summary>Extra meshes at other sorting orders, for views that draw on several elevation levels.</summary>
        protected LayeredMeshes Layers => _layers ??= new LayeredMeshes(transform, Renderer.sharedMaterial);

        protected void Apply(MeshBuilder builder)
        {
            // Presenters may still clear their views while the scene is being torn down.
            if (this == null)
                return;

            if (_mesh == null)
            {
                _mesh = new Mesh { name = GetType().Name };
                GetComponent<MeshFilter>().sharedMesh = _mesh;
                Renderer.sortingOrder = SortingOrder;
            }

            builder.ApplyTo(_mesh);
        }

        protected virtual void OnDestroy()
        {
            if (_mesh != null)
                Destroy(_mesh);
            _layers?.Destroy();
        }
    }
}
