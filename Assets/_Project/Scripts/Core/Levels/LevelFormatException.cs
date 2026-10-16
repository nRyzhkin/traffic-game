using System;

namespace TrafficGame.Core.Levels
{
    /// <summary>A level file is malformed or refers to something that does not exist.</summary>
    public sealed class LevelFormatException : Exception
    {
        public LevelFormatException(string message) : base(message) { }

        public LevelFormatException(string message, Exception innerException) : base(message, innerException) { }
    }
}
