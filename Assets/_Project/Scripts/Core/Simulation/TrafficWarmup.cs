namespace TrafficGame.Core.Simulation
{
    /// <summary>Runs traffic ahead of time so a level opens with its usual traffic already on the roads.</summary>
    public static class TrafficWarmup
    {
        /// <summary>Simulates <paramref name="seconds"/> of trips and traffic, then starts the statistics afresh.</summary>
        public static void Run(TrafficSimulation simulation, TripGenerator trips, float seconds, float tickSeconds)
        {
            for (var time = 0f; time < seconds; time += tickSeconds)
            {
                trips.Update(simulation, tickSeconds);
                simulation.Step(tickSeconds);
            }

            simulation.ResetStatistics();
        }
    }
}
