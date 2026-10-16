using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace TrafficGame.Editor
{
    /// <summary>
    /// Reproducible project settings. Change settings here instead of by hand,
    /// then run <c>Traffic Game → Apply Project Setup</c>.
    /// </summary>
    public static class ProjectSetup
    {
        private const string ProductName = "Traffic Game";
        private const string WebGLTemplateFolder = "Assets/WebGLTemplates/PrimeGames";
        private const string WebGLTemplate = "PROJECT:PrimeGames";

        [MenuItem("Traffic Game/Apply Project Setup", priority = 0)]
        public static void Apply()
        {
            PlayerSettings.productName = ProductName;

            var webGL = NamedBuildTarget.WebGL;
            PlayerSettings.SetManagedStrippingLevel(webGL, ManagedStrippingLevel.Medium);
            PlayerSettings.SetIl2CppCodeGeneration(webGL, Il2CppCodeGeneration.OptimizeSize);

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.dataCaching = true;

            // The PrimeGames template is installed from the PrimeSDK window; both orientations stay allowed.
            if (Directory.Exists(WebGLTemplateFolder))
                PlayerSettings.WebGL.template = WebGLTemplate;
            else
                Debug.LogWarning($"[{nameof(ProjectSetup)}] {WebGLTemplateFolder} is missing: install the PrimeGames WebGL template from the PrimeSDK window.");

            AssetDatabase.SaveAssets();
            Debug.Log($"[{nameof(ProjectSetup)}] Project settings applied.");
        }
    }
}
