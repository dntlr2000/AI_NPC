using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Behavior;
using AiCharacterKit.Core;
using AiCharacterKit.Unity.Actions;
using AiCharacterKit.Unity.Behavior;
using UnityEditor;
using UnityEngine;

namespace AiCharacterKit.Editor
{
    internal static partial class CharacterBuilderAssetService
    {
        /// <summary>Saves only validated detached behavior data to a consumer-owned asset with Undo.</summary>
        public static bool TrySaveBehaviorProfile(NpcBehaviorProfile existing, NpcBehaviorDefinition draft, string folderPath, string name,
            out NpcBehaviorProfile saved, out string error)
        {
            saved = null;
            if (!NpcBehaviorValidator.TryValidate(draft, out error) || !TryNormalizeWritableFolder(folderPath, out var folder, out error)) return false;
            if (existing != null && !TryValidateWritableAsset(existing, "NpcBehaviorProfile", out error)) return false;
            Undo.IncrementCurrentGroup(); var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Save NPC Behavior Profile");
            var assetPath = string.Empty;
            try
            {
                if (existing != null)
                {
                    Undo.RecordObject(existing, "Save NPC Behavior Profile"); existing.SetDefinition(draft);
                    EditorUtility.SetDirty(existing); saved = existing;
                }
                else
                {
                    EnsureFolder(folder);
                    saved = ScriptableObject.CreateInstance<NpcBehaviorProfile>(); saved.SetDefinition(draft);
                    saved.name = GetSafeAssetName(name, "NpcBehaviorProfile", "NpcBehaviorProfile");
                    assetPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + saved.name + ".asset");
                    AssetDatabase.CreateAsset(saved, assetPath);
                    Undo.RegisterCreatedObjectUndo(saved, "Create NPC Behavior Profile");
                }
                AssetDatabase.SaveAssets(); Undo.CollapseUndoOperations(undoGroup); error = string.Empty; return true;
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.LoadMainAssetAtPath(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
                if (saved != null && !EditorUtility.IsPersistent(saved) && saved != existing) UnityEngine.Object.DestroyImmediate(saved);
                saved = null; error = "Behavior profile could not be saved. Check the consumer Assets folder and file permissions."; return false;
            }
        }

        /// <summary>Imports Basic bindings into an unsaved equivalent Advanced draft without modifying the source asset.</summary>
        public static NpcBehaviorDefinition ImportBasicBehavior(NpcActionProfile profile)
        {
            if (profile == null || !profile.TryValidate(out _)) throw new ArgumentException("Select a valid Basic action profile.");
            var draft = new NpcBehaviorDefinition();
            foreach (var binding in profile.Bindings)
            {
                draft.signals.Add(new NpcBehaviorSignal { triggerId = binding.triggerId, conditionDescription = binding.conditionDescription, exampleUserText = binding.exampleUserText });
                draft.actionRules.Add(new NpcBehaviorActionRule { ruleId = binding.triggerId, triggerId = binding.triggerId, actionId = binding.actionId, priority = binding.priority });
            }
            return draft;
        }

        /// <summary>Runs the production evaluator with explicit synthetic state and simulated handlers only.</summary>
        public static NpcBehaviorTrace PreviewBehavior(NpcBehaviorDefinition draft, IReadOnlyDictionary<string, NpcBehaviorValue> variables,
            IReadOnlyDictionary<string, NpcBehaviorValue> external, IReadOnlyList<string> matchedIds)
        {
            var handlers = new List<INpcActionHandler>(); var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in draft.actionRules) if (seen.Add(rule.actionId)) handlers.Add(new PreviewBehaviorHandler(rule.actionId));
            var runtime = new NpcBehaviorRuntime(draft, handlers, () => 0, true);
            if (variables != null) foreach (var pair in variables)
            {
                string error;
                var valid = pair.Value.Kind == NpcBehaviorValueKind.Boolean ? runtime.TrySetBoolean(pair.Key, pair.Value.Boolean, out error)
                    : runtime.TrySetNumber(pair.Key, pair.Value.Number, out error);
                if (!valid) throw new ArgumentException(error);
            }
            var request = new AiNpcRequest("preview", "Preview", "Neutral", "Brief", "Hello", NpcEmotion.Neutral, "preview");
            runtime.ProcessTurnAsync(new NpcTurnContext(request, new AiNpcResponse("Preview", NpcEmotion.Neutral, NpcGesture.None, matchedIds)),
                new[] { new PreviewBehaviorValues(external) }, CancellationToken.None).GetAwaiter().GetResult();
            return runtime.LastTrace;
        }

        private sealed class PreviewBehaviorHandler : INpcActionHandler
        {
            public string ActionId { get; }
            /// <summary>Creates a simulation-only handler with no game references.</summary>
            public PreviewBehaviorHandler(string id) { ActionId = id; }
            /// <summary>Models an allowed final authorization without querying a consumer.</summary>
            public bool CanExecute(NpcActionContext context, out string reason) { reason = string.Empty; return true; }
            /// <summary>Completes the simulated action without producing a game effect.</summary>
            public Task ExecuteAsync(NpcActionContext context, CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.CompletedTask; }
        }
        private sealed class PreviewBehaviorValues : INpcBehaviorValueProvider
        {
            private readonly IReadOnlyDictionary<string, NpcBehaviorValue> values;
            /// <summary>Stores explicitly entered simulation values.</summary>
            public PreviewBehaviorValues(IReadOnlyDictionary<string, NpcBehaviorValue> values) { this.values = values ?? new Dictionary<string, NpcBehaviorValue>(); }
            /// <summary>Returns synthetic values without invoking any scene provider.</summary>
            public IReadOnlyDictionary<string, NpcBehaviorValue> CaptureValues() => values;
        }
    }
}
