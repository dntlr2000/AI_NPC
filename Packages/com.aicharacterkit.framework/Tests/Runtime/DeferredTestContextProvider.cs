using System;
using System.Collections.Generic;
using AiCharacterKit.Core;
using AiCharacterKit.Unity;
using UnityEngine;

namespace AiCharacterKit.Core.Tests
{
    /// <summary>
    /// Provides a test-only runtime component that can persist in Prefabs and becomes usable in Start.
    /// </summary>
    public sealed class DeferredTestContextProvider : NpcContextProviderBehaviour
    {
        public bool IsReady { get; private set; }
        public int CaptureCount { get; private set; }
        public IReadOnlyList<NpcContextFact> Facts { get; set; } = Array.Empty<NpcContextFact>();

        /// <summary>
        /// Simulates a game-state service that finishes initialization after all Awake calls.
        /// </summary>
        private void Start()
        {
            IsReady = true;
        }

        /// <summary>
        /// Fails before Play Mode initialization and returns current test observations afterward.
        /// </summary>
        public override IReadOnlyList<NpcContextFact> CaptureFacts()
        {
            CaptureCount++;
            if (!Application.isPlaying || !IsReady)
            {
                throw new InvalidOperationException("Private game-state service is not ready.");
            }

            return Facts;
        }
    }
}
