using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AiCharacterKit.LocalInference;
using AiCharacterKit.Transport.V4;
namespace AiCharacterKit.Unity.Networking { public static partial class LocalInferenceJsonCodec {
        private sealed class StageReplyReader
        {
            private readonly string text;
            private int index;

            /// <summary>Bounds untrusted output before inspecting its JSON tokens.</summary>
            public StageReplyReader(string text)
            {
                if (string.IsNullOrWhiteSpace(text) || text.Length > 16384) throw LocalInferenceErrors.InvalidResponse();
                this.text = text;
            }

            /// <summary>Decodes one complete JSON key token for protocol checks without inspecting quoted payload text.</summary>
            public string ReadStringToken()
            {
                var value = ReadString();
                if (index != text.Length) throw LocalInferenceErrors.InvalidResponse();
                return value;
            }

            /// <summary>Requires exactly the selected stage's fields and a complete JSON object.</summary>
            public AiNpcResponsePayloadDto Read(bool dialogueOnly = false)
            {
                var result = new AiNpcResponsePayloadDto();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                Expect('{');
                do
                {
                    var name = ReadString();
                    if (!seen.Add(name)) throw LocalInferenceErrors.InvalidResponse();
                    Expect(':');
                    switch (name)
                    {
                        case "dialogue": result.dialogue = ReadString(); break;
                        case "emotion": result.emotion = ReadString(); break;
                        case "gesture": result.gesture = ReadString(); break;
                        case "matchedTriggerIds":
                            if (dialogueOnly) throw LocalInferenceErrors.InvalidResponse();
                            result.matchedTriggerIds = ReadStrings(); break;
                        default: throw LocalInferenceErrors.InvalidResponse();
                    }
                } while (Consume(','));
                Expect('}');
                SkipWhitespace();
                if (index != text.Length || seen.Count != (dialogueOnly ? 3 : 4)) throw LocalInferenceErrors.InvalidResponse();
                return result;
            }

            /// <summary>Parses only the single bounded decision vector without accepting extra or duplicate fields.</summary>
            public string[] ReadDecisions()
            {
                Expect('{');
                if (ReadString() != "decisions") throw LocalInferenceErrors.InvalidResponse();
                Expect(':'); var decisions = ReadStrings(); Expect('}'); SkipWhitespace();
                if (index != text.Length) throw LocalInferenceErrors.InvalidResponse();
                return decisions;
            }

            /// <summary>Parses a bounded string-only trigger array without accepting null or nested values.</summary>
            private string[] ReadStrings()
            {
                var values = new List<string>();
                Expect('[');
                if (Consume(']')) return values.ToArray();
                do
                {
                    if (values.Count >= AiNpcContractV4.MaxTriggerCount) throw LocalInferenceErrors.InvalidResponse();
                    values.Add(ReadString());
                } while (Consume(','));
                Expect(']');
                return values.ToArray();
            }

            /// <summary>Decodes JSON string escapes while rejecting raw controls and incomplete escapes.</summary>
            private string ReadString()
            {
                Expect('"');
                var value = new StringBuilder();
                while (index < text.Length)
                {
                    var c = text[index++];
                    if (c == '"') return value.ToString();
                    if (c < 32) throw LocalInferenceErrors.InvalidResponse();
                    if (c != '\\') { value.Append(c); continue; }
                    if (index == text.Length) throw LocalInferenceErrors.InvalidResponse();
                    switch (text[index++])
                    {
                        case '"': value.Append('"'); break;
                        case '\\': value.Append('\\'); break;
                        case '/': value.Append('/'); break;
                        case 'b': value.Append('\b'); break;
                        case 'f': value.Append('\f'); break;
                        case 'n': value.Append('\n'); break;
                        case 'r': value.Append('\r'); break;
                        case 't': value.Append('\t'); break;
                        case 'u':
                            if (index + 4 > text.Length || !ushort.TryParse(text.Substring(index, 4),
                                NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                                throw LocalInferenceErrors.InvalidResponse();
                            value.Append((char)code); index += 4; break;
                        default: throw LocalInferenceErrors.InvalidResponse();
                    }
                }
                throw LocalInferenceErrors.InvalidResponse();
            }

            /// <summary>Consumes one required punctuation token after JSON whitespace.</summary>
            private void Expect(char value)
            {
                if (!Consume(value)) throw LocalInferenceErrors.InvalidResponse();
            }

            /// <summary>Consumes optional punctuation without changing the cursor on mismatch.</summary>
            private bool Consume(char value)
            {
                SkipWhitespace();
                if (index >= text.Length || text[index] != value) return false;
                index++;
                return true;
            }

            /// <summary>Skips only whitespace defined by JSON rather than arbitrary Unicode whitespace.</summary>
            private void SkipWhitespace()
            {
                while (index < text.Length && (text[index] == ' ' || text[index] == '\r'
                    || text[index] == '\n' || text[index] == '\t')) index++;
            }
        }

} }
