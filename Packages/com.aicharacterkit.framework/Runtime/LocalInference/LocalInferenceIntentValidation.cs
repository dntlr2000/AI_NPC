using System;
using System.Collections.Generic;
using AiCharacterKit.Transport.V4;

namespace AiCharacterKit.LocalInference
{
    /// <summary>Validates ordered classification independently of JSON, Unity and gameplay effects.</summary>
    public static class LocalInferenceIntentValidation
    {
        /// <summary>Requires exactly one known decision per declared trigger.</summary>
        public static void Validate(IReadOnlyList<string> decisions, int count)
        {
            if (count < 0 || count > AiNpcContractV4.MaxTriggerCount || decisions == null || decisions.Count != count)
                throw LocalInferenceErrors.InvalidResponse();
            foreach (var decision in decisions)
                if (decision != "match" && decision != "no_match" && decision != "uncertain")
                    throw LocalInferenceErrors.InvalidResponse();
        }

        /// <summary>Maps matched positions to trusted IDs, rejecting invalid declarations instead of inventing IDs.</summary>
        public static string[] MatchedIds(IReadOnlyList<string> decisions, IReadOnlyList<AiNpcTriggerDto> triggers)
        {
            if (triggers == null) throw LocalInferenceErrors.InvalidRequest();
            Validate(decisions, triggers.Count);
            var known = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>();
            for (var i = 0; i < triggers.Count; i++)
            {
                var trigger = triggers[i];
                if (trigger == null || string.IsNullOrWhiteSpace(trigger.triggerId) || !known.Add(trigger.triggerId))
                    throw LocalInferenceErrors.InvalidRequest();
                if (decisions[i] == "match") result.Add(trigger.triggerId);
            }
            return result.ToArray();
        }
    }
}
