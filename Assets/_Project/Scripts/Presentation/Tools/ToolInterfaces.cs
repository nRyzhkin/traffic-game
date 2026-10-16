using System;
using System.Collections.Generic;
using TrafficGame.Core.Construction;
using TrafficGame.Core.Roads;
using TrafficGame.Presentation.Localization;
using Unity.Mathematics;

namespace TrafficGame.Presentation.Tools
{
    public enum ToolKind
    {
        None,
        Road,
        CurvedRoad,
        Roundabout,
        Bulldoze,
        TrafficLights,
        LaneArrows
    }

    /// <summary>A map editing tool driven by <see cref="ToolController"/>. Positions are in world space.</summary>
    public interface IMapTool
    {
        void Activate();
        void Deactivate();
        void PointerMoved(float2 world);
        void PrimaryClick(float2 world);

        /// <summary>Steps back one stage; returns false when there is nothing to step back from.</summary>
        bool Cancel();

        /// <summary>Whether a primary click at the current pointer would do something; drives the ✓ button.</summary>
        bool CanConfirm { get; }

        /// <summary>
        /// Whether a single touch tap is a primary click for this tool. Tools whose clicks only select or toggle
        /// something cheap use it; tools that build keep the tap-to-aim, ✓-to-build flow.
        /// </summary>
        bool ActsOnTap { get; }
    }

    /// <summary>
    /// Pointer actions on the map in screen pixels (origin bottom-left). Mouse clicks are primary clicks;
    /// a touch tap moves the pointer (there is no hover on touch screens) and is reported as tapped, and the second
    /// tap of a double tap is a primary click instead.
    /// </summary>
    public interface IMapPointerInput
    {
        event Action<float2> PointerMoved;
        event Action<float2> Tapped;
        event Action<float2> PrimaryClicked;
        event Action SecondaryClicked;
    }

    /// <summary>Tool-level requests, coming from toolbar buttons or hotkeys.</summary>
    public interface IToolInput
    {
        event Action<ToolKind> ToolRequested;
        event Action ConfirmRequested;
        event Action UndoRequested;
        event Action RedoRequested;
        event Action CancelRequested;
        event Action NextProfileRequested;
    }

    /// <summary>The current derived road geometry.</summary>
    public interface IRoadLayoutSource
    {
        RoadLayout Layout { get; }

        /// <summary>Raised after <see cref="Layout"/> was rebuilt for a changed road network.</summary>
        event Action LayoutChanged;
    }

    /// <summary>World-space overlay showing what a tool is about to do.</summary>
    public interface IRoadPreviewView
    {
        void ShowAnchor(RoadAnchor anchor);
        void ShowPlan(RoadPlan plan, RoadGuide guide, float2? control);
        void ShowRoundabout(RoundaboutPlan plan);
        void ShowRemoval(SegmentGeometry segment);
        void Hide();
    }

    /// <summary>A lane's arrows as shown by the lane arrows tool, at the lane end.</summary>
    public readonly struct LaneArrowMarker
    {
        public LaneArrowMarker(float2 position, float2 direction, LaneArrows arrows)
        {
            Position = position;
            Direction = direction;
            Arrows = arrows;
        }

        public float2 Position { get; }
        public float2 Direction { get; }
        public LaneArrows Arrows { get; }
    }

    /// <summary>World-space overlay for the junction tools.</summary>
    public interface IJunctionPreviewView
    {
        void ShowJunction(JunctionGeometry junction, bool valid);

        /// <param name="highlighted">Index of the marker under the pointer, or -1.</param>
        void ShowLaneArrows(JunctionGeometry junction, IReadOnlyList<LaneArrowMarker> markers, int highlighted);

        void Hide();
    }

    public enum TooltipTone
    {
        Normal,
        Error
    }

    /// <summary>Small label next to the pointer.</summary>
    public interface ITooltipView
    {
        void Show(float2 world, IReadOnlyList<(LocalizedText text, TooltipTone tone)> lines);
        void Hide();
    }
}
