using UnityEngine.UIElements;

namespace TrafficGame.Views.UI
{
    /// <summary>
    /// The filled pill of a segmented control. It lies under the segments and slides to the chosen one
    /// (<c>.segmented__thumb</c> transitions its position and width), like the iOS segmented control.
    /// </summary>
    public sealed class SegmentThumb
    {
        private readonly VisualElement _thumb;
        private VisualElement _selected;

        public SegmentThumb(VisualElement container)
        {
            _thumb = new VisualElement { pickingMode = PickingMode.Ignore };
            _thumb.AddToClassList("segmented__thumb");
            container.Insert(0, _thumb);
            container.RegisterCallback<GeometryChangedEvent>(_ => Place());
        }

        public void Select(VisualElement segment)
        {
            _selected = segment;
            Place();
        }

        private void Place()
        {
            _thumb.EnableInClassList(Motion.HiddenClass, _selected == null);
            if (_selected == null)
                return;

            var layout = _selected.layout;
            if (float.IsNaN(layout.width) || layout.width <= 0f)
                return;

            // Absolute positions start inside the container's border; layout positions include it.
            var border = _thumb.parent.resolvedStyle;
            _thumb.style.left = layout.x - border.borderLeftWidth;
            _thumb.style.top = layout.y - border.borderTopWidth;
            _thumb.style.width = layout.width;
            _thumb.style.height = layout.height;
        }
    }
}
