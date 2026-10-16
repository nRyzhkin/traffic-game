using System;
using UnityEngine;

namespace TrafficGame.Views.Roads
{
    /// <summary>Colors and marking sizes of roads, in meters.</summary>
    [Serializable]
    public sealed class RoadStyle
    {
        [SerializeField] private Color32 _surface = new(255, 255, 255, 255);
        [SerializeField] private Color32 _curb = new(185, 181, 172, 255);
        [SerializeField] private Color32 _divider = new(226, 178, 62, 255);
        [SerializeField] private Color32 _laneLine = new(190, 186, 177, 255);
        [SerializeField, Min(0.05f)] private float _curbWidth = 0.5f;
        [SerializeField, Min(0.05f)] private float _dividerWidth = 0.35f;
        [SerializeField, Min(0.05f)] private float _laneLineWidth = 0.25f;
        [SerializeField, Min(0.1f)] private float _dashLength = 3f;
        [SerializeField, Min(0.1f)] private float _dashGap = 5f;

        [Header("Elevated roads")]
        [SerializeField] private Color32 _bridgeEdge = new(150, 146, 138, 255);
        [SerializeField, Min(0.05f)] private float _bridgeEdgeWidth = 1.1f;
        [SerializeField] private Color32 _bridgeShadow = new(40, 36, 28, 70);

        [Header("Construction preview")]
        [SerializeField] private Color32 _previewValid = new(48, 86, 200, 90);
        [SerializeField] private Color32 _previewInvalid = new(230, 80, 70, 150);
        [SerializeField] private Color32 _previewGuide = new(90, 90, 90, 150);
        [SerializeField] private Color32 _previewAnchor = new(48, 86, 200, 235);
        [SerializeField] private Color32 _previewFreeAnchor = new(120, 120, 120, 200);

        /// <summary>The road colors of the dark theme: graphite asphalt, dim markings.</summary>
        public static RoadStyle Dark() => new()
        {
            _surface = new Color32(44, 47, 53, 255),
            _curb = new Color32(65, 69, 77, 255),
            _divider = new Color32(181, 140, 44, 255),
            _laneLine = new Color32(90, 95, 104, 255),
            _bridgeEdge = new Color32(98, 104, 116, 255),
            _bridgeShadow = new Color32(0, 0, 0, 200),
            _previewValid = new Color32(181, 196, 255, 110),
            _previewInvalid = new Color32(255, 110, 100, 130),
            _previewGuide = new Color32(199, 197, 208, 115),
            _previewAnchor = new Color32(181, 196, 255, 235),
            _previewFreeAnchor = new Color32(145, 143, 154, 200)
        };

        /// <summary>A style between <paramref name="a"/> and <paramref name="b"/> (for theme cross-fades); sizes come from <paramref name="b"/>.</summary>
        public static RoadStyle Lerp(RoadStyle a, RoadStyle b, float t) => new()
        {
            _surface = Color32.Lerp(a._surface, b._surface, t),
            _curb = Color32.Lerp(a._curb, b._curb, t),
            _divider = Color32.Lerp(a._divider, b._divider, t),
            _laneLine = Color32.Lerp(a._laneLine, b._laneLine, t),
            _bridgeEdge = Color32.Lerp(a._bridgeEdge, b._bridgeEdge, t),
            _bridgeEdgeWidth = b._bridgeEdgeWidth,
            _bridgeShadow = Color32.Lerp(a._bridgeShadow, b._bridgeShadow, t),
            _curbWidth = b._curbWidth,
            _dividerWidth = b._dividerWidth,
            _laneLineWidth = b._laneLineWidth,
            _dashLength = b._dashLength,
            _dashGap = b._dashGap,
            _previewValid = Color32.Lerp(a._previewValid, b._previewValid, t),
            _previewInvalid = Color32.Lerp(a._previewInvalid, b._previewInvalid, t),
            _previewGuide = Color32.Lerp(a._previewGuide, b._previewGuide, t),
            _previewAnchor = Color32.Lerp(a._previewAnchor, b._previewAnchor, t),
            _previewFreeAnchor = Color32.Lerp(a._previewFreeAnchor, b._previewFreeAnchor, t)
        };

        public Color32 Surface => _surface;
        public Color32 Curb => _curb;
        public Color32 Divider => _divider;
        public Color32 LaneLine => _laneLine;

        /// <summary>Railing along the sides of roads above the ground.</summary>
        public Color32 BridgeEdge => _bridgeEdge;

        public float BridgeEdgeWidth => _bridgeEdgeWidth;

        /// <summary>Shadow elevated roads cast on what lies below.</summary>
        public Color32 BridgeShadow => _bridgeShadow;
        public float CurbWidth => _curbWidth;
        public float DividerWidth => _dividerWidth;
        public float LaneLineWidth => _laneLineWidth;
        public float DashLength => _dashLength;
        public float DashGap => _dashGap;
        public Color32 PreviewValid => _previewValid;
        public Color32 PreviewInvalid => _previewInvalid;
        public Color32 PreviewGuide => _previewGuide;
        public Color32 PreviewAnchor => _previewAnchor;
        public Color32 PreviewFreeAnchor => _previewFreeAnchor;
    }
}
