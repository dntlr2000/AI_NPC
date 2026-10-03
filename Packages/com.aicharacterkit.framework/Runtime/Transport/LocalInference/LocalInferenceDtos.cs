using System;

namespace AiCharacterKit.Transport.LocalInference
{
    [Serializable]
    public sealed class LocalInstallFile
    {
        public string path;
        public long sizeBytes;
        public string sha256;
    }

    [Serializable]
    public sealed class LocalInstallManifest
    {
        public int schemaVersion = 1;
        public string runtimeVersion;
        public string modelVersion;
        public string templateVersion;
        public LocalInstallFile model;
        public LocalInstallFile executable;
        public LocalInstallFile chatTemplate;
        public LocalInstallFile[] runtimeFiles;
    }

    [Serializable]
    public sealed class LocalChatMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    public sealed class LocalThinkingOptions { public bool enable_thinking; }

    [Serializable]
    public sealed class LocalChatRequest
    {
        public string model = "ai-character-kit-local";
        public LocalChatMessage[] messages;
        public bool stream;
        public int max_tokens = 256;
        public float temperature = 0.2f;
        public int seed = 42;
        public bool cache_prompt;
        public string reasoning_effort = "none";
        public LocalThinkingOptions chat_template_kwargs = new LocalThinkingOptions();
    }

    [Serializable]
    public sealed class LocalTemplateRequest
    {
        public LocalChatMessage[] messages;
        public bool add_generation_prompt = true;
        public LocalThinkingOptions chat_template_kwargs = new LocalThinkingOptions();
    }

    [Serializable]
    public sealed class LocalTemplateResponse { public string prompt; }

    [Serializable]
    public sealed class LocalTokenizeRequest
    {
        public string content;
        public bool add_special;
        public bool parse_special = true;
        public bool with_pieces;
    }

    [Serializable]
    public sealed class LocalTokenizeResponse { public int[] tokens; }

    [Serializable]
    public sealed class LocalCompletionResponse { public LocalCompletionChoice[] choices; }

    [Serializable]
    public sealed class LocalCompletionChoice
    {
        public string finish_reason;
        public LocalCompletionMessage message;
    }

    [Serializable]
    public sealed class LocalCompletionMessage
    {
        public string role;
        public string content;
        public string reasoning_content;
    }

    [Serializable]
    public sealed class LocalResponseFormat
    {
        public string type = "json_schema";
        public LocalSchemaDefinition json_schema = new LocalSchemaDefinition();
    }

    [Serializable]
    public sealed class LocalSchemaDefinition
    {
        public string name = "npc_reply";
        public bool strict = true;
        public LocalObjectSchema schema = new LocalObjectSchema();
    }

    [Serializable]
    public sealed class LocalObjectSchema
    {
        public string type = "object";
        public bool additionalProperties;
        public string[] required = { "dialogue", "emotion", "gesture", "matchedTriggerIds" };
        public LocalReplyProperties properties = new LocalReplyProperties();
    }

    [Serializable]
    public sealed class LocalReplyProperties
    {
        public LocalDialogueSchema dialogue = new LocalDialogueSchema();
        public LocalEnumSchema emotion = new LocalEnumSchema { @enum = new[] { "neutral", "happy", "sad", "angry", "concerned" } };
        public LocalEnumSchema gesture = new LocalEnumSchema { @enum = new[] { "none", "nod", "wave" } };
        public LocalTriggerArraySchema matchedTriggerIds = new LocalTriggerArraySchema();
    }

    [Serializable]
    public sealed class LocalDialogueSchema
    {
        public string type = "string";
        public int minLength = 1;
        public int maxLength = 600;
    }

    [Serializable]
    public sealed class LocalEnumSchema
    {
        public string type = "string";
        public string[] @enum;
    }

    [Serializable]
    public sealed class LocalTriggerArraySchema
    {
        public string type = "array";
        public int maxItems;
        public LocalEnumSchema items = new LocalEnumSchema();
    }

    [Serializable]
    public sealed class LocalTriggerList { public V4.AiNpcTriggerDto[] items; }
}
