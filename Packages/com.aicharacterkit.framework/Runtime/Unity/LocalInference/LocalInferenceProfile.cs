using System;
using AiCharacterKit.LocalInference;
using UnityEngine;

namespace AiCharacterKit.Unity.LocalInference
{
    /// <summary>References an external installation without embedding a model or machine-specific defaults in the package.</summary>
    [CreateAssetMenu(menuName = "AI Character Kit/Local Inference Profile", fileName = "LocalInferenceProfile")]
    public sealed class LocalInferenceProfile : ScriptableObject
    {
        [SerializeField] private string manifestPath = string.Empty;
        public string ManifestPath => manifestPath;

        /// <summary>Checks only authored path syntax; file hashing and process startup belong to preparation.</summary>
        public bool TryValidateConfiguration(out string error)
        {
            try
            {
                ResolveManifestPath(Application.isPlaying ? Environment.GetCommandLineArgs() : Array.Empty<string>());
                error = string.Empty;
                return true;
            }
            catch
            {
                error = "Local inference requires a valid external installation manifest path.";
                return false;
            }
        }

        /// <summary>Resolves an optional explicit player argument ahead of the profile's authored path.</summary>
        public string ResolveManifestPath(string[] arguments)
        {
            var selected = manifestPath;
            var overridden = false;
            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i] != "-aiNpcLocalManifest") continue;
                if (overridden || i + 1 >= arguments.Length) throw LocalInferenceErrors.Configuration();
                selected = arguments[++i];
                overridden = true;
            }
            return LocalInferenceInstallation.RequireLocalPath(selected, Environment.CurrentDirectory);
        }
    }
}
