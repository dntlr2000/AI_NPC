using System;
using AiCharacterKit.Transport.V4;

namespace AiCharacterKit.LocalInference
{
    /// <summary>Copies mutable transport data without a JSON or Unity dependency.</summary>
    internal static class LocalInferenceSnapshots
    {
        /// <summary>Copies every V4 field and nested array while retaining invalid nulls for normal validation.</summary>
        internal static AiNpcRequestEnvelopeDto Copy(AiNpcRequestEnvelopeDto source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var character = source.character;
            var grounding = source.grounding;
            var result = new AiNpcRequestEnvelopeDto
            {
                schemaVersion = source.schemaVersion, requestId = source.requestId,
                sessionId = source.sessionId, userText = source.userText,
                character = character == null ? null : new CharacterSnapshotDto
                {
                    characterId = character.characterId, displayName = character.displayName,
                    personality = character.personality, speechStyle = character.speechStyle,
                    exampleDialogue = character.exampleDialogue, defaultEmotion = character.defaultEmotion
                },
                grounding = grounding == null ? null : new NpcGroundingSnapshotDto
                {
                    revision = grounding.revision, background = grounding.background,
                    goalsAndValues = grounding.goalsAndValues,
                    behavioralRules = grounding.behavioralRules == null ? null : (string[])grounding.behavioralRules.Clone(),
                    dialogueExamples = grounding.dialogueExamples == null ? null : (string[])grounding.dialogueExamples.Clone(),
                    facts = grounding.facts == null ? null : Array.ConvertAll(grounding.facts, CopyFact)
                },
                triggers = source.triggers == null ? null : Array.ConvertAll(source.triggers, CopyTrigger)
            };
            return result;
        }

        /// <summary>Preserves a fact's ID, type, statement and priority without sharing writable objects.</summary>
        private static NpcContextFactDto CopyFact(NpcContextFactDto value) => value == null ? null :
            new NpcContextFactDto { factId = value.factId, kind = value.kind, statement = value.statement, priority = value.priority };

        /// <summary>Copies only the declared semantic trigger fields.</summary>
        private static AiNpcTriggerDto CopyTrigger(AiNpcTriggerDto value) => value == null ? null :
            new AiNpcTriggerDto { triggerId = value.triggerId, conditionDescription = value.conditionDescription };
    }
}
