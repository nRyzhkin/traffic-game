using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrafficGame.Views.UI
{
    public enum IconKind
    {
        Road,
        CurvedRoad,
        Roundabout,
        Bulldoze,
        TrafficLight,
        LaneArrows,
        Pause,
        Menu,
        Lock,
        Wallet,
        Flag,
        Star,
        StarOutline,
        Check,
        CheckCircle,
        Back,
        Undo,
        Redo,
        Replay,
        Forward,
        Sun,
        Moon,
        ThemeAuto,
        Lightbulb,
        Sandbox,
        Play,
        FastForward,
        ChevronRight,
        ChevronUp,
        ChevronDown
    }

    /// <summary>
    /// Vector icon on a 24-unit grid, drawn from SVG path data so the UI needs no image assets. It is drawn in its
    /// resolved <c>color</c> (the stylesheet sets <c>.icon { color: var(--icon-color) }</c>, and themes set the variable
    /// per state) unless <see cref="Tint"/> overrides it.
    /// </summary>
    public sealed class IconElement : VisualElement
    {
        private const float Grid = 24f;
        private const string StarPath = "M12 3l2.8 5.9 6.2.7-4.6 4.3 1.3 6.1L12 16.9 6.3 20l1.3-6.1L3 9.6l6.2-.7z";

        private static readonly Dictionary<IconKind, IconShape[]> Shapes = new()
        {
            [IconKind.Road] = new[] { IconShape.Stroke("M8 3L5 21M16 3l3 18M12 4v3M12 10.5v3M12 17v3") },
            [IconKind.CurvedRoad] = new[] { IconShape.Stroke("M4 21c0-9 6-15 16-15M9 21c0-6 4-10 11-10") },
            [IconKind.Roundabout] = new[] { IconShape.Stroke(Circle(12f, 12f, 5f) + "M12 2v5M12 17v5M2 12h5M17 12h5") },
            [IconKind.Bulldoze] = new[] { IconShape.Stroke("M3 17h12v-5H3zM5 12V8h6l2 4M18 7v12M15 13h3") },
            [IconKind.TrafficLight] = new[]
            {
                IconShape.Stroke(RoundRect(8f, 2f, 8f, 16f, 3f) + "M12 18v4"),
                IconShape.Fill(Circle(12f, 6.5f, 1.4f) + Circle(12f, 10f, 1.4f) + Circle(12f, 13.5f, 1.4f))
            },
            [IconKind.LaneArrows] = new[] { IconShape.Stroke("M16 21V5M12.5 8.5L16 5l3.5 3.5M8 21v-7c0-2-1-3-3-3H4M6 8.5L3.5 11 6 13.5") },
            [IconKind.Pause] = new[] { IconShape.Stroke("M9 6v12M15 6v12", 2.6f) },
            [IconKind.Menu] = new[] { IconShape.Stroke("M4 7h16M4 12h16M4 17h16") },
            [IconKind.Lock] = new[] { IconShape.Stroke("M8 10V7a4 4 0 0 1 8 0v3"), IconShape.Fill(RoundRect(5f, 10f, 14f, 11f, 2f)) },
            [IconKind.Wallet] = new[] { IconShape.Stroke(RoundRect(3f, 6f, 18f, 13f, 3f) + "M3 10h18M16 14.5h2") },
            [IconKind.Flag] = new[] { IconShape.Stroke("M5 21V4h11l-2 4 2 4H5") },
            [IconKind.Star] = new[] { IconShape.Fill(StarPath) },
            [IconKind.StarOutline] = new[] { IconShape.Stroke(StarPath, 1.8f) },
            [IconKind.Check] = new[] { IconShape.Stroke("M5 12l5 5 9-10", 2.4f) },
            [IconKind.CheckCircle] = new[] { IconShape.Stroke(Circle(12f, 12f, 9f) + "M8 12.5l2.7 2.7L16.5 9.5", 2.2f) },
            [IconKind.Back] = new[] { IconShape.Stroke("M19 12H5M12 19l-7-7 7-7") },
            [IconKind.Undo] = new[] { IconShape.Stroke("M9 14L4 9l5-5M4 9h10a6 6 0 0 1 0 12h-3") },
            [IconKind.Redo] = new[] { IconShape.Stroke("M15 14l5-5-5-5M20 9H10a6 6 0 0 0 0 12h3") },
            [IconKind.Replay] = new[] { IconShape.Stroke("M4 12a8 8 0 1 0 2.3-5.7M4 4v5h5", 2.2f) },
            [IconKind.Forward] = new[] { IconShape.Stroke("M5 12h14M13 5l7 7-7 7", 2.2f) },
            [IconKind.Sun] = new[]
            {
                IconShape.Stroke(Circle(12f, 12f, 4f) + "M12 2v2M12 20v2M2 12h2M20 12h2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4")
            },
            [IconKind.Moon] = new[] { IconShape.Stroke("M20 14.5A8 8 0 1 1 9.5 4a6.5 6.5 0 0 0 10.5 10.5z") },
            [IconKind.ThemeAuto] = new[] { IconShape.Stroke(Circle(12f, 12f, 8f)), IconShape.Fill("M12 4a8 8 0 0 1 0 16z") },
            [IconKind.Lightbulb] = new[] { IconShape.Stroke("M9 18h6M10 21h4M12 3a6 6 0 0 0-3.5 10.9c.6.5 1 1.2 1 2.1h5c0-.9.4-1.6 1-2.1A6 6 0 0 0 12 3z") },
            [IconKind.Sandbox] = new[]
            {
                IconShape.Stroke(RoundRect(3f, 3f, 7f, 7f, 2f) + RoundRect(14f, 3f, 7f, 7f, 2f) + RoundRect(3f, 14f, 7f, 7f, 2f) + "M17.5 14v7M14 17.5h7")
            },
            [IconKind.Play] = new[] { IconShape.Fill("M8 5.5v13l10.5-6.5z") },
            [IconKind.FastForward] = new[] { IconShape.Fill("M4 6l7 6-7 6zM13 6l7 6-7 6z") },
            [IconKind.ChevronRight] = new[] { IconShape.Stroke("M9 6l6 6-6 6") },
            [IconKind.ChevronUp] = new[] { IconShape.Stroke("M6 15l6-6 6 6", 2.4f) },
            [IconKind.ChevronDown] = new[] { IconShape.Stroke("M6 9l6 6 6-6", 2.4f) }
        };

        private IconKind _kind;
        private Color? _tint;
        private Color _drawnColor;
        private int _runningTransitions;
        private IVisualElementScheduledItem _repaintLoop;

        public IconElement(IconKind kind)
        {
            _kind = kind;
            pickingMode = PickingMode.Ignore;
            AddToClassList("icon");
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
            RegisterCallback<TransitionRunEvent>(OnTransitionRun);
            RegisterCallback<TransitionEndEvent>(OnTransitionFinished);
            RegisterCallback<TransitionCancelEvent>(OnTransitionFinished);
        }

        public IconKind Kind
        {
            get => _kind;
            set
            {
                if (_kind == value)
                    return;
                _kind = value;
                MarkDirtyRepaint();
            }
        }

        /// <summary>Overrides the themed color; null goes back to the theme.</summary>
        public Color? Tint
        {
            get => _tint;
            set
            {
                _tint = value;
                MarkDirtyRepaint();
            }
        }

        /// <summary>Styles were resolved again (a state or theme class changed): the color may have changed.</summary>
        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt) => MarkDirtyRepaint();

        /// <summary>
        /// The mesh is drawn in one color, so while the color transitions (theme switch, selection) the icon has to be
        /// redrawn every frame; the loop runs only while a transition does.
        /// </summary>
        private void OnTransitionRun(TransitionRunEvent evt)
        {
            _runningTransitions++;
            if (_repaintLoop == null)
                _repaintLoop = schedule.Execute(RepaintIfColorChanged).Every(0);
            else
                _repaintLoop.Resume();
        }

        private void OnTransitionFinished(EventBase evt)
        {
            _runningTransitions = Mathf.Max(0, _runningTransitions - 1);
            if (_runningTransitions == 0)
                _repaintLoop?.Pause();
            MarkDirtyRepaint();
        }

        private void RepaintIfColorChanged()
        {
            if (_tint == null && resolvedStyle.color != _drawnColor)
                MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            var size = Mathf.Min(rect.width, rect.height);
            if (size <= 0f || !Shapes.TryGetValue(_kind, out var shapes))
                return;

            var origin = rect.center - new Vector2(size, size) * 0.5f;
            var scale = size / Grid;
            var color = _tint ?? resolvedStyle.color;
            _drawnColor = color;
            var painter = context.painter2D;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            painter.strokeColor = color;
            painter.fillColor = color;

            foreach (var shape in shapes)
            {
                painter.BeginPath();
                foreach (var command in shape.Commands)
                {
                    switch (command.Kind)
                    {
                        case PathCommandKind.Move:
                            painter.MoveTo(origin + command.P3 * scale);
                            break;
                        case PathCommandKind.Line:
                            painter.LineTo(origin + command.P3 * scale);
                            break;
                        case PathCommandKind.Cubic:
                            painter.BezierCurveTo(origin + command.P1 * scale, origin + command.P2 * scale, origin + command.P3 * scale);
                            break;
                        default:
                            painter.ClosePath();
                            break;
                    }
                }

                if (shape.Filled)
                {
                    painter.Fill();
                }
                else
                {
                    painter.lineWidth = shape.StrokeWidth * scale;
                    painter.Stroke();
                }
            }
        }

        private static string Circle(float cx, float cy, float r) =>
            Invariant($"M{cx - r} {cy}a{r} {r} 0 1 0 {2f * r} 0a{r} {r} 0 1 0 {-2f * r} 0z");

        private static string RoundRect(float x, float y, float w, float h, float r) =>
            Invariant($"M{x + r} {y}h{w - 2f * r}a{r} {r} 0 0 1 {r} {r}v{h - 2f * r}a{r} {r} 0 0 1 {-r} {r}h{2f * r - w}a{r} {r} 0 0 1 {-r} {-r}v{2f * r - h}a{r} {r} 0 0 1 {r} {-r}z");

        private static string Invariant(System.FormattableString text) => System.FormattableString.Invariant(text);

        private readonly struct IconShape
        {
            private IconShape(string path, bool filled, float strokeWidth)
            {
                Commands = SvgPath.Parse(path);
                Filled = filled;
                StrokeWidth = strokeWidth;
            }

            public IReadOnlyList<PathCommand> Commands { get; }
            public bool Filled { get; }
            public float StrokeWidth { get; }

            public static IconShape Stroke(string path, float width = 2f) => new(path, false, width);
            public static IconShape Fill(string path) => new(path, true, 0f);
        }
    }
}
