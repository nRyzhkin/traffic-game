using System;
using System.Linq;
using NUnit.Framework;
using TrafficGame.Core.Simulation;
using TrafficGame.Presentation.Cameras;

namespace TrafficGame.Tests
{
    /// <summary>
    /// Core and Presentation must stay free of Unity engine APIs so they remain plain, testable C#.
    /// Since Unity 6.5 Mathematics ships as an engine module, so the asmdefs cannot use
    /// <c>noEngineReferences</c>; this test enforces the boundary instead.
    /// </summary>
    public sealed class AssemblyBoundaryTests
    {
        private const string MathematicsModule = "UnityEngine.MathematicsModule";

        [TestCase(typeof(SimulationClock))]
        [TestCase(typeof(CameraPresenter))]
        public void LogicAssembly_UsesNoEngineApiExceptMathematics(Type typeInAssembly)
        {
            var engineReferences = typeInAssembly.Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .Where(name => name.StartsWith("Unity", StringComparison.Ordinal) && name != MathematicsModule)
                .ToArray();

            Assert.That(engineReferences, Is.Empty,
                $"{typeInAssembly.Assembly.GetName().Name} must not depend on Unity engine assemblies.");
        }
    }
}
