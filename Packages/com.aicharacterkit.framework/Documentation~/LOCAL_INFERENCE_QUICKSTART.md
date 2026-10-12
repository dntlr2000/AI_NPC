# Windows Local Inference (Unreleased)

Real NPC dialogue runs without internet, OpenAI or the Node backend. This source feature targets Unity 6000.5 Windows Editor and Windows x64 **Mono**, first measured on 32GB RAM / RTX 5070 Laptop 8GB. It is not included in immutable `v0.4.0`. Other platforms, IL2CPP, CPU-only performance, local speech, streaming, RAG and persistent memory are outside this implementation.

## Prepare the external installation

1. Supply the measured **Gemma 4 E4B IT Q4_K_M GGUF**, complete **llama.cpp b11146 Vulkan Windows x64** distribution, and measured LM Studio `model.yaml` or extracted Jinja template. Keep the model outside Unity; the kit never downloads or copies weights.
2. Open **Tools > AI Character Kit > Local Inference > Prepare Installation**. Select those files and choose a writable external directory for `local-inference.json`. Setup checks the pinned model/executable/template hashes, records DLL sizes/hashes and writes only the manifest and derived template. Failed verification does not replace an existing manifest.
3. Create **Assets > Create > AI Character Kit > Local Inference Profile** and assign the manifest path. Relative manifest entries resolve from its directory; absolute external model paths work. Use a local writable folder, not a network share. Transient process files use `work/run-*` beside the manifest and are cleaned on stop when possible.

Pinned SHA-256 values in `LocalInferenceDefaults`:

| File | SHA-256 |
| --- | --- |
| Model | `3f72a20a06f626c78e6c475ae07a64c88b2663149c0f6197b56bf7cf1f37585c` |
| Executable | `7b886298b688509ced3e92b420edd57dd3d665da72c1fc207d7a537be5870352` |
| Template | `37d87b94f5d7ce4a3ba69fd625d63b2062502efd256c993b76a9764ae4b4ce53` |

The built-in GGUF template is not substituted for the measured template. Different models/runtimes need separate evaluation. Model license terms remain separate from this package's MIT license; keep applicable runtime notices in the external installation.

## Connect NPCs

1. Add **LocalInferenceRuntime** to a Scene host and assign the profile. **Prepare On Enable** starts preparation automatically.
2. Choose **LocalInference** in Character Builder. Assign that runtime or place the NPC under the host, then configure grounding and presentation. Optional actions use the same consumer handlers as V3/V4.
3. Multiple NPCs share the host but retain separate memory/reset. A Prefab can require a future parent host; Builder shows that requirement and rejects external Scene references on Prefabs.
4. Wait for **Ready**. Sending early is recoverable and does not disable the NPC. `await runtime.PrepareAsync()` explicitly prepares/restarts; `await runtime.StopAsync()` ends the host. To change the installation during play, recreate the host with the new profile.

Builder apply/reapply, preview and NPC Awake inspect static configuration only. They do not start the process or capture provider facts. Providers can initialize in Start; failed capture affects only that submission.

Override the profile in a Windows player:

```powershell
.\Game.exe -aiNpcLocalManifest "E:\NPC Installation\local-inference.json"
```

The player reads the original external model. Provide matching executable/DLLs as a separate installation; none are bundled into the UPM package.

## Sample

Import **AI NPC Prototypes**, then **Tools > AI Character Kit > Samples > Create Local Grounded NPC**. Assign the generated Local Inference profile's manifest and enter Play Mode. One runner serves Mira and Toma. They share **one gate and town alarm**, while dialogue memory and Reset memory remain separate. The host panel has the shared state toggles and preparation/retry; each NPC has Send, Reset memory and the actual action status. Change the alarm during generation to exercise the final `CanExecute` authorization. The panel supports Built-in/URP and does not need the Input System.

The sample finalizes gate action replies **after** execution or rejection. Successful opening, an already-open gate and an alarm rejection use editable factual receipts on `LocalGroundedNpc` (Korean and English; other languages use the English receipt). These replace the proposed action dialogue before presentation and memory commit, including its emotion/gesture. Ordinary conversation and state questions still use the model. No second model call or automatic retry is made. Current observations are placed after historical dialogue in the trusted prompt so older state descriptions are not treated as current facts. Retained user statements are also quoted there as untrusted recall data, keeping personal memory distinct from world state. This temporary recall view is included in token counting and drops the same oldest turns as the main history; it is not stored separately.

For an already-generated scene, update its imported `LocalNpc` scripts alongside the package. The sample panel creates a missing `LocalGroundedGate` at runtime, initializing it from the first guard's former gate/alarm fields. Existing scenes, profiles and GUIDs need no regeneration. To author shared initial state before Play Mode, add `LocalGroundedGate` to the host in the Inspector. Standalone guards without a shared host retain their own initial state. Newly generated scenes contain the shared gate explicitly. In this repository, the existing imported sample scripts have also been updated.

An optional component implementing `INpcActionResponseFormatter` on the conversation GameObject opts a **local** NPC into this order: validate generated response → route the configured action once → format its actual result → validate/commit final dialogue → present. Its `FormatResponse` must return a valid, bounded reply and must not execute actions itself. `NpcConversationBehaviour` suppresses the usual later action observer for that composition. `LocalInferenceConversationClient` and `LocalInferenceRuntime.CreateClient` also accept an optional `finalizeResponse` callback for explicit composition. Existing Core/V4 interfaces and NPCs without the formatter keep their previous behavior; this sample improvement does not provide semantic validation for arbitrary API/model dialogue.

## Bounds and errors

Existing V4 field limits and **512KiB serialized UTF-8** request size remain. Separately, the actual formatted prompt plus 256 reserved output tokens must fit **4,096 tokens**. Old history is omitted first; an oversized current profile/grounding/trigger/input fails without truncation.

Phase 17 corrects the pinned tokenizer's special-token handling: actual input 3,840 plus the output reserve fits, while 3,841 is rejected before generation. It also waits for cancelled HTTP work before releasing its queue slot and before completing an explicit runner stop. Existing SinglePass prompt/schema and sample defaults are preserved. The optional two-stage infrastructure is not enabled for E4B because its new held-out signal score missed the adoption criterion; the API candidate also failed its fixed signal criteria and remains unadopted. `NpcConversationBehaviour.LastInferenceDiagnostics` reports this NPC's latest admitted local generation, with unknown token usage represented by `-1`, and no prompt/reply logging.

Each NPC retains at most eight successful user/assistant pairs, 16KiB UTF-8, expiring after 30 idle minutes. Grounding/revisions are never retained as history. Each host generates once at a time, with eight FIFO waiters, 30-second queue wait, 35-second request and 180-second preparation deadlines. No automatic retry or online fallback occurs.

| Code | Meaning / recovery |
| --- | --- |
| `local_configuration_error` | Check manifest, pinned files, profile and permissions. |
| `local_not_ready` | Wait for Ready and submit again. |
| `local_busy` | Queue full or wait deadline; submit later. |
| `local_timeout` | Preparation or generation deadline; explicitly retry as appropriate. |
| `local_context_limit` | Current data exceeds token window; reduce it explicitly. |
| `invalid_request` | Existing V4 or serialized byte validation failed before generation. |
| `invalid_model_response` | Invalid/truncated JSON, reasoning, commands or trigger IDs. |
| `local_runtime_unavailable` | Child exit, transport/native allocation failure; prepare again. |

Native out-of-memory shares the fixed unavailable category; raw errors are discarded. Unknown/duplicate trigger IDs and cancelled generation fail before action finalization. Failed or cancelled requests do not commit history. An action already completed before later cancellation or a formatter failure is not rolled back or retried; inspect the actual game state and action result. Cancelling one NPC leaves the shared host alive. Host removal/disable, Play Mode exit and game exit close only the owned Windows Job Object. Prompts, replies, grounding and reasoning are not logged.

## Architecture

`AiCharacterKit.LocalInference` has no Unity references. `LocalInferenceConversationClient` implements existing `IResettableAiConversationClient`; `ILocalInferenceRunner` provides prepare/generate/stop. Process management, JSON and HTTP stay in the Unity boundary; networking remains under Runtime/Unity/Networking. DTOs stay engine independent. Mock, Backend V1–V4, TTS and STT remain available. Optional speech still uses its separately configured backend.
