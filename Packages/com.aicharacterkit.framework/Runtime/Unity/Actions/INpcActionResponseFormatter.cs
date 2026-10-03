using AiCharacterKit.Core;

namespace AiCharacterKit.Unity.Actions
{
    /// <summary>Opt-in local dialogue formatting from an actual action outcome; never authorizes an action.</summary>
    public interface INpcActionResponseFormatter
    {
        /// <summary>Returns the reply to present and remember after the configured action router has completed.</summary>
        AiNpcResponse FormatResponse(AiNpcRequest request, AiNpcResponse proposedResponse, NpcActionResult result);
    }
}
