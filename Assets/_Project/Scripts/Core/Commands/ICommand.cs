namespace TrafficGame.Core.Commands
{
    /// <summary>
    /// A reversible change of game state. <see cref="Execute"/> is called again on redo and must
    /// produce exactly the same state (including ids) as the first time.
    /// </summary>
    public interface ICommand
    {
        void Execute();
        void Undo();
    }
}
