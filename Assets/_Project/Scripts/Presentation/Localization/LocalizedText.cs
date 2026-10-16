using System;

namespace TrafficGame.Presentation.Localization
{
    /// <summary>A text to show, as a localization key plus format arguments. Presenters pass these; views resolve them.</summary>
    public readonly struct LocalizedText : IEquatable<LocalizedText>
    {
        private static readonly object[] NoArgs = Array.Empty<object>();

        public LocalizedText(string key, params object[] args)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Args = args ?? NoArgs;
        }

        public string Key { get; }
        public object[] Args { get; }
        public bool IsEmpty => Key == null;

        public static LocalizedText None => default;

        public bool Equals(LocalizedText other)
        {
            if (Key != other.Key || (Args?.Length ?? 0) != (other.Args?.Length ?? 0))
                return false;
            for (var i = 0; i < (Args?.Length ?? 0); i++)
            {
                if (!Equals(Args[i], other.Args[i]))
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj) => obj is LocalizedText other && Equals(other);
        public override int GetHashCode() => Key?.GetHashCode() ?? 0;
        public override string ToString() => Key ?? "<none>";
    }
}
