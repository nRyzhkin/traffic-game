using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TrafficGame.Views.UI
{
    public enum PathCommandKind
    {
        Move,
        Line,
        Cubic,
        Close
    }

    /// <summary>One drawing step; lines and moves use only <see cref="P3"/>, cubic curves all three points.</summary>
    public readonly struct PathCommand
    {
        public PathCommand(PathCommandKind kind, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            Kind = kind;
            P1 = p1;
            P2 = p2;
            P3 = p3;
        }

        public PathCommandKind Kind { get; }
        public Vector2 P1 { get; }
        public Vector2 P2 { get; }
        public Vector2 P3 { get; }
    }

    /// <summary>
    /// Parses SVG path data (M L H V C Q A Z, absolute and relative) into moves, lines and cubic curves; arcs and
    /// quadratic curves become cubics. Enough for hand-written icon paths.
    /// </summary>
    public static class SvgPath
    {
        public static IReadOnlyList<PathCommand> Parse(string data)
        {
            var result = new List<PathCommand>();
            var reader = new Reader(data);
            var current = Vector2.zero;
            var start = Vector2.zero;
            var command = 'M';

            while (reader.SkipSeparators())
            {
                if (char.IsLetter(reader.Peek))
                    command = reader.Next();

                var relative = char.IsLower(command);
                var origin = relative ? current : Vector2.zero;
                switch (char.ToUpperInvariant(command))
                {
                    case 'M':
                        current = origin + reader.Point();
                        start = current;
                        result.Add(new PathCommand(PathCommandKind.Move, default, default, current));
                        command = relative ? 'l' : 'L'; // further pairs are line-tos
                        break;
                    case 'L':
                        current = origin + reader.Point();
                        result.Add(new PathCommand(PathCommandKind.Line, default, default, current));
                        break;
                    case 'H':
                        current = new Vector2((relative ? current.x : 0f) + reader.Number(), current.y);
                        result.Add(new PathCommand(PathCommandKind.Line, default, default, current));
                        break;
                    case 'V':
                        current = new Vector2(current.x, (relative ? current.y : 0f) + reader.Number());
                        result.Add(new PathCommand(PathCommandKind.Line, default, default, current));
                        break;
                    case 'C':
                    {
                        var c1 = origin + reader.Point();
                        var c2 = origin + reader.Point();
                        current = origin + reader.Point();
                        result.Add(new PathCommand(PathCommandKind.Cubic, c1, c2, current));
                        break;
                    }
                    case 'Q':
                    {
                        var control = origin + reader.Point();
                        var end = origin + reader.Point();
                        result.Add(new PathCommand(PathCommandKind.Cubic, current + (control - current) * (2f / 3f),
                            end + (control - end) * (2f / 3f), end));
                        current = end;
                        break;
                    }
                    case 'A':
                    {
                        var radii = reader.Point();
                        var rotation = reader.Number();
                        var largeArc = reader.Number() != 0f;
                        var sweep = reader.Number() != 0f;
                        var end = origin + reader.Point();
                        AddArc(result, current, end, radii, rotation, largeArc, sweep);
                        current = end;
                        break;
                    }
                    case 'Z':
                        result.Add(new PathCommand(PathCommandKind.Close, default, default, start));
                        current = start;
                        break;
                    default:
                        throw new FormatException($"Unsupported path command '{command}' in \"{data}\".");
                }
            }

            return result;
        }

        /// <summary>Endpoint arc to cubic curves, per the SVG implementation notes (F.6.5), at most a quarter turn each.</summary>
        private static void AddArc(List<PathCommand> result, Vector2 from, Vector2 to, Vector2 radii, float rotationDegrees,
            bool largeArc, bool sweep)
        {
            var rx = Mathf.Abs(radii.x);
            var ry = Mathf.Abs(radii.y);
            if (rx < 1e-5f || ry < 1e-5f || (from - to).sqrMagnitude < 1e-10f)
            {
                result.Add(new PathCommand(PathCommandKind.Line, default, default, to));
                return;
            }

            var phi = rotationDegrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(phi);
            var sin = Mathf.Sin(phi);
            var half = (from - to) * 0.5f;
            var x1 = cos * half.x + sin * half.y;
            var y1 = -sin * half.x + cos * half.y;

            var lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
            if (lambda > 1f)
            {
                var scale = Mathf.Sqrt(lambda);
                rx *= scale;
                ry *= scale;
            }

            var numerator = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
            var denominator = rx * rx * y1 * y1 + ry * ry * x1 * x1;
            var factor = Mathf.Sqrt(Mathf.Max(0f, numerator / denominator)) * (largeArc == sweep ? -1f : 1f);
            var cx1 = factor * rx * y1 / ry;
            var cy1 = -factor * ry * x1 / rx;
            var center = new Vector2(cos * cx1 - sin * cy1, sin * cx1 + cos * cy1) + (from + to) * 0.5f;

            var startAngle = Mathf.Atan2((y1 - cy1) / ry, (x1 - cx1) / rx);
            var endAngle = Mathf.Atan2((-y1 - cy1) / ry, (-x1 - cx1) / rx);
            var delta = endAngle - startAngle;
            if (sweep && delta < 0f)
                delta += 2f * Mathf.PI;
            else if (!sweep && delta > 0f)
                delta -= 2f * Mathf.PI;

            var segments = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(delta) / (Mathf.PI * 0.5f) - 1e-4f));
            var step = delta / segments;
            var k = 4f / 3f * Mathf.Tan(step / 4f);
            Vector2 Point(float angle) => center + Rotate(new Vector2(rx * Mathf.Cos(angle), ry * Mathf.Sin(angle)), cos, sin);
            Vector2 Tangent(float angle) => Rotate(new Vector2(-rx * Mathf.Sin(angle), ry * Mathf.Cos(angle)), cos, sin);

            for (var i = 0; i < segments; i++)
            {
                var a0 = startAngle + step * i;
                var a1 = a0 + step;
                var p0 = Point(a0);
                var p3 = i == segments - 1 ? to : Point(a1);
                result.Add(new PathCommand(PathCommandKind.Cubic, p0 + Tangent(a0) * k, p3 - Tangent(a1) * k, p3));
            }
        }

        private static Vector2 Rotate(Vector2 v, float cos, float sin) => new(cos * v.x - sin * v.y, sin * v.x + cos * v.y);

        private sealed class Reader
        {
            private readonly string _text;
            private int _index;

            public Reader(string text) => _text = text;

            public char Peek => _text[_index];

            public char Next() => _text[_index++];

            /// <summary>Skips spaces and commas; false at the end of the data.</summary>
            public bool SkipSeparators()
            {
                while (_index < _text.Length && (char.IsWhiteSpace(_text[_index]) || _text[_index] == ','))
                    _index++;
                return _index < _text.Length;
            }

            public Vector2 Point() => new(Number(), Number());

            public float Number()
            {
                SkipSeparators();
                var begin = _index;
                if (_index < _text.Length && (_text[_index] == '-' || _text[_index] == '+'))
                    _index++;

                var seenDot = false;
                while (_index < _text.Length)
                {
                    var c = _text[_index];
                    if (char.IsDigit(c))
                    {
                        _index++;
                    }
                    else if (c == '.' && !seenDot)
                    {
                        seenDot = true;
                        _index++;
                    }
                    else if ((c == 'e' || c == 'E') && _index > begin)
                    {
                        _index++;
                        if (_index < _text.Length && (_text[_index] == '-' || _text[_index] == '+'))
                            _index++;
                    }
                    else
                    {
                        break;
                    }
                }

                if (_index == begin)
                    throw new FormatException($"Expected a number at {begin} in \"{_text}\".");
                return float.Parse(_text.Substring(begin, _index - begin), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }
    }
}
