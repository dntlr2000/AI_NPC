using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiCharacterKit.Behavior;
using UnityEngine;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>Test-only global counters detect callbacks on temporary Prefab instances as well as Scene originals.</summary>
    public sealed class BehaviorTestAdapter : MonoBehaviour, INpcBehaviorValueProvider, INpcActionHandler, INpcPresentationDriver
    {
        public static int CaptureCalls, AuthorizationCalls, ExecutionCalls;
        public bool ValuesReady;
        public string ActionId => "open_gate";
        public string LastError { get; private set; }
        /// <summary>Simulates a provider that is unavailable during static authoring and becomes ready later.</summary>
        public IReadOnlyDictionary<string, NpcBehaviorValue> CaptureValues()
        {
            CaptureCalls++;
            if (!ValuesReady) throw new InvalidOperationException("private delayed provider error");
            return new Dictionary<string, NpcBehaviorValue> { ["permission"] = new NpcBehaviorValue(true) };
        }
        /// <summary>Counts real authorizations independently of configuration checks.</summary>
        public bool CanExecute(NpcActionContext context, out string reason) { AuthorizationCalls++; reason = string.Empty; return true; }
        /// <summary>Counts simulated game effects for duplicate-dispatch checks.</summary>
        public Task ExecuteAsync(NpcActionContext context, CancellationToken token) { token.ThrowIfCancellationRequested(); ExecutionCalls++; return Task.CompletedTask; }
        /// <summary>Accepts presentation state without UI dependencies.</summary>
        public void SetBusy(bool value) { }
        /// <summary>Accepts a synthetic response without retaining or logging it.</summary>
        public void PresentDialogue(string dialogue) { }
        /// <summary>Accepts structured emotion commands.</summary>
        public void PresentEmotion(NpcEmotion emotion) { }
        /// <summary>Accepts structured gesture commands.</summary>
        public void PresentGesture(NpcGesture gesture) { }
        /// <summary>Retains only the current safe diagnostic for assertions.</summary>
        public void PresentError(string error) { LastError = error; }
        /// <summary>Accepts cancellation presentation.</summary>
        public void PresentCancellation() { }
    }
}
