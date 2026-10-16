using System;
using System.Collections.Generic;
using System.Linq;

namespace TrafficGame.Core.Roads
{
    /// <summary>How traffic through a junction is controlled.</summary>
    public enum JunctionControl
    {
        /// <summary>Signs chosen from the roads that meet (see the simulation's junction rules).</summary>
        Auto,

        /// <summary>Traffic lights with phases for each pair of opposite approaches.</summary>
        TrafficLights
    }

    /// <summary>Turns allowed from a lane, painted as arrows before the junction.</summary>
    [Flags]
    public enum LaneArrows
    {
        None = 0,
        Left = 1,
        Straight = 2,
        Right = 4
    }

    /// <summary>
    /// Player choices for one junction: the control and per-lane arrows that override the default lane connections.
    /// Immutable; arrows are keyed by the arriving segment and the lane index (0 = leftmost lane arriving at the node).
    /// </summary>
    public sealed class JunctionSettings
    {
        public static readonly JunctionSettings Default =
            new(JunctionControl.Auto, false, new Dictionary<(SegmentId, int), LaneArrows>());

        private readonly IReadOnlyDictionary<(SegmentId segment, int lane), LaneArrows> _arrows;

        private JunctionSettings(JunctionControl control, bool protectedLeft, IReadOnlyDictionary<(SegmentId, int), LaneArrows> arrows)
        {
            Control = control;
            ProtectedLeft = protectedLeft;
            _arrows = arrows;
        }

        public JunctionControl Control { get; }

        /// <summary>With traffic lights: left turns get their own green arrow before the opposite through traffic goes.</summary>
        public bool ProtectedLeft { get; }

        public IEnumerable<(SegmentId segment, int lane, LaneArrows arrows)> Arrows =>
            _arrows.Select(pair => (pair.Key.segment, pair.Key.lane, pair.Value));

        public bool IsDefault => Control == JunctionControl.Auto && !ProtectedLeft && _arrows.Count == 0;

        /// <summary>Arrows the player painted on a lane, or <see cref="LaneArrows.None"/> for the default.</summary>
        public LaneArrows ArrowsOf(SegmentId segment, int lane) => _arrows.TryGetValue((segment, lane), out var arrows) ? arrows : LaneArrows.None;

        public JunctionSettings WithControl(JunctionControl control, bool protectedLeft) =>
            new(control, control == JunctionControl.TrafficLights && protectedLeft, _arrows);

        /// <summary>Paints arrows on a lane; <see cref="LaneArrows.None"/> brings back the default.</summary>
        public JunctionSettings WithArrows(SegmentId segment, int lane, LaneArrows arrows)
        {
            var copy = _arrows.ToDictionary(pair => pair.Key, pair => pair.Value);
            if (arrows == LaneArrows.None)
                copy.Remove((segment, lane));
            else
                copy[(segment, lane)] = arrows;
            return new JunctionSettings(Control, ProtectedLeft, copy);
        }

        /// <summary>Moves the arrows of one arriving segment to another (the segment was split and replaced).</summary>
        internal JunctionSettings WithSegmentReplaced(SegmentId old, SegmentId replacement) =>
            new(Control, ProtectedLeft, _arrows.ToDictionary(pair => pair.Key.segment == old ? (replacement, pair.Key.lane) : pair.Key, pair => pair.Value));

        internal JunctionSettings WithoutSegment(SegmentId segment) =>
            new(Control, ProtectedLeft, _arrows.Where(pair => pair.Key.segment != segment).ToDictionary(pair => pair.Key, pair => pair.Value));
    }
}
