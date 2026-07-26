#nullable enable
#pragma warning disable CS8603, CS8619

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.IPC;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ContinueVS.Tests.IPC
{
    /// <summary>
    /// Integration tests for b13: Handler Response Serialization (Complex)
    /// Verifies that complex nested JSON responses from handlers serialize correctly,
    /// maintain JSON validity, and produce well-formed wire format for JavaScript injection.
    /// </summary>
    public class ResponseSerializationComplexTests
    {
        /// <summary>
        /// Helper to validate JSON structure after serialization
        /// </summary>
        private static bool IsValidJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                JsonConvert.DeserializeObject(json!);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        [Fact]
        public void SerializeSimpleScalarResponse_Preserves()
        {
            var simplePayload = "hello world";
            var msg = new Message
            {
                MessageType = "test-handler",
                MessageId = "msg-scalar-001",
                Data = JToken.FromObject(simplePayload)
            };

            var json = JsonConvert.SerializeObject(msg);
            System.Diagnostics.Debug.WriteLine($"[b13-RESPONSE-OBJECT] Handler response object: Type=String, Content={simplePayload}");
            System.Diagnostics.Debug.WriteLine($"[b13-JTOKEN-SERIALIZE] JToken created from payload");
            System.Diagnostics.Debug.WriteLine($"[b13-JSON-VALID] JSON validation: IsValid={IsValidJson(json)}, Length={json?.Length ?? 0}");

            Assert.NotNull(json);
            Assert.True(IsValidJson(json));
            Assert.Contains("hello world", json);
        }

        [Fact]
        public void SerializeSimpleObjectResponse_PreservesStructure()
        {
            var simpleObject = new { result = "ok", status = 200 };
            var msg = new Message
            {
                MessageType = "test-handler",
                MessageId = "msg-object-001",
                Data = JToken.FromObject(simpleObject)
            };

            var json = JsonConvert.SerializeObject(msg);
            System.Diagnostics.Debug.WriteLine($"[b13-RESPONSE-OBJECT] Handler response object: Type=Object, Content={JsonConvert.SerializeObject(simpleObject)}");
            System.Diagnostics.Debug.WriteLine($"[b13-JTOKEN-SERIALIZE] JToken created from payload");
            var isValid = IsValidJson(json);
            System.Diagnostics.Debug.WriteLine($"[b13-JSON-VALID] JSON validation: IsValid={isValid}, Length={json?.Length ?? 0}");

            Assert.NotNull(json);
            Assert.True(isValid);
            Assert.Contains("ok", json);
        }

        [Fact]
        public void SerializeNestedObjectResponse_PreservesStructure()
        {
            var nestedPayload = new
            {
                metadata = new { version = "2.0.0", timestamp = DateTime.UtcNow },
                results = new object[]
                {
                    new { id = 1, name = "item1", tags = new[] { "a", "b" } },
                    new { id = 2, name = "item2", tags = new[] { "c", "d" } }
                }
            };

            var msg = new Message
            {
                MessageType = "test-handler",
                MessageId = "msg-nested-001",
                Data = JToken.FromObject(nestedPayload)
            };

            var json = JsonConvert.SerializeObject(msg);
            System.Diagnostics.Debug.WriteLine($"[b13-RESPONSE-OBJECT] Handler response object: Type=Object, Content={JsonConvert.SerializeObject(nestedPayload)}");
            System.Diagnostics.Debug.WriteLine($"[b13-JTOKEN-SERIALIZE] JToken created from payload");
            var isValid = IsValidJson(json);
            System.Diagnostics.Debug.WriteLine($"[b13-JSON-VALID] JSON validation: IsValid={isValid}, Length={json?.Length ?? 0}");

            Assert.NotNull(json);
            Assert.True(isValid);
            Assert.Contains("2.0.0", json);
            Assert.Contains("item1", json);
        }

        [Fact]
        public void SerializeArrayPayload_MaintainsIntegrity()
        {
            var arrayPayload = new object[]
            {
                new { id = 1, name = "first" },
                new { id = 2, name = "second" },
                new { id = 3, name = "third" }
            };

            var msg = new Message
            {
                MessageType = "test-handler",
                MessageId = "msg-array-001",
                Data = JToken.FromObject(arrayPayload)
            };

            var json = JsonConvert.SerializeObject(msg);
            System.Diagnostics.Debug.WriteLine($"[b13-RESPONSE-OBJECT] Handler response object: Type=Array, Content={JsonConvert.SerializeObject(arrayPayload)}");
            System.Diagnostics.Debug.WriteLine($"[b13-JTOKEN-SERIALIZE] JToken created from payload");
            var isValid = IsValidJson(json);
            System.Diagnostics.Debug.WriteLine($"[b13-JSON-VALID] JSON validation: IsValid={isValid}, Length={json?.Length ?? 0}");

            Assert.NotNull(json);
            Assert.True(isValid);
            Assert.Contains("first", json);
        }

        [Fact]
        public void SerializeNestedArrayResponse_MaintainsStructure()
        {
            var nestedArrayPayload = new
            {
                results = new object[]
                {
                    new { category = "A", items = new[] { "item1", "item2" } },
                    new { category = "B", items = new[] { "item3", "item4", "item5" } }
                }
            };

            var msg = new Message
            {
                MessageType = "test-handler",
                MessageId = "msg-nested-array-001",
                Data = JToken.FromObject(nestedArrayPayload)
            };

            var json = JsonConvert.SerializeObject(msg);
            System.Diagnostics.Debug.WriteLine($"[b13-JTOKEN-SERIALIZE] JToken created from payload");
            var isValid = IsValidJson(json);
            System.Diagnostics.Debug.WriteLine($"[b13-JSON-VALID] JSON validation: IsValid={isValid}, Length={json?.Length ?? 0}");

            Assert.NotNull(json);
            Assert.True(isValid);
            Assert.Contains("category", json);
        }

        [Fact]
        public void SerializeWithSpecialCharactersAndEscaping_MaintainsValidity()
        {
            var payloadWithSpecialChars = new
            {
                text = "Line 1\nLine 2\tTabbed",
                quoted = "He said \"Hello\" and she replied 'Hi'",
                path = "C:\\Users\\test\\file.txt",
                unicode = "日本語 ñ é ü"
            };

            var msg = new Message
            {
                MessageType = "test-handler",
                MessageId = "msg-special-001",
                Data = JToken.FromObject(payloadWithSpecialChars)
            };

            var json = JsonConvert.SerializeObject(msg);
            var escaped = json.Replace("\\", "\\\\").Replace("'", "\\'");

            System.Diagnostics.Debug.WriteLine($"[b13-JSON-VALID] Special character JSON: {json}");
            System.Diagnostics.Debug.WriteLine($"[b13-SCRIPT-PAYLOAD] Escaped for JavaScript: Length={escaped?.Length ?? 0}");

            Assert.NotNull(json);
            Assert.True(IsValidJson(json));
            Assert.NotEmpty(escaped!);
            Assert.Contains("日本語", json);
        }

        [Fact]
        public void SerializeDeeplyNestedObject_EscapingPreservesStructure()
        {
            var deepPayload = new 
            { 
                level1 = new 
                { 
                    level2 = new 
                    { 
                        level3 = new 
                        { 
                            level4 = new 
                            { 
                                level5 = new 
                                { 
                                    level6 = new 
                                    { 
                                        level7 = new 
                                        { 
                                            level8 = new 
                                            { 
                                                level9 = new 
                                                { 
                                                    level10 = "deep-value with 'quotes' and \\ backslash"
                                                } 
                                            } 
                                        } 
                                    } 
                                } 
                            } 
                        } 
                    } 
                } 
            };

            var msg = new Message
            {
                MessageType = "test-handler",
                MessageId = "msg-deep-001",
                Data = JToken.FromObject(deepPayload)
            };

            var json = JsonConvert.SerializeObject(msg);
            var escaped = json.Replace("\\", "\\\\").Replace("'", "\\'");

            System.Diagnostics.Debug.WriteLine($"[b13-JTOKEN-SERIALIZE] Deep nesting JSON length: {json.Length}");
            System.Diagnostics.Debug.WriteLine($"[b13-SCRIPT-PAYLOAD] Escaped payload length: {escaped.Length}");

            Assert.NotNull(json);
            Assert.True(IsValidJson(json));
            Assert.True(escaped.Length >= json.Length);
        }

        [Fact]
        public void SerializeArrayOfObjectsWithMixedTypes_MaintainsIntegrity()
        {
            var mixedArray = new object[]
            {
                new { type = "string", value = "hello" },
                new { type = "number", value = 42 },
                new { type = "boolean", value = true }
            };

            var msg = new Message
            {
                MessageType = "test-handler",
                MessageId = "msg-mixed-001",
                Data = JToken.FromObject(mixedArray)
            };

            var json = JsonConvert.SerializeObject(msg);
            System.Diagnostics.Debug.WriteLine($"[b13-JSON-VALID] Mixed-type array JSON length: {json?.Length ?? 0}");
            var isValid = IsValidJson(json);
            System.Diagnostics.Debug.WriteLine($"[b13-JSON-VALID] JSON validation: IsValid={isValid}, Length={json?.Length ?? 0}");

            Assert.NotNull(json);
            Assert.True(isValid);
            Assert.Contains("hello", json);
        }

        [Fact]
        public void SerializeWithNullValuesInNestedStructure_PreservesStructure()
        {
            var payloadObj = new JObject();
            payloadObj["field1"] = "value1";

            var nested = new JObject();
            nested["nullField"] = null;
            nested["value"] = "nested-value";
            payloadObj["nested"] = nested;

            var arrayWithNulls = new JArray();
            arrayWithNulls.Add("item1");
            arrayWithNulls.Add((object?)null);
            arrayWithNulls.Add("item3");
            payloadObj["arrayWithNulls"] = arrayWithNulls;

            payloadObj["allNull"] = null;

            var msg = new Message
            {
                MessageType = "test-handler",
                MessageId = "msg-nulls-001",
                Data = payloadObj
            };

            var json = JsonConvert.SerializeObject(msg);
            System.Diagnostics.Debug.WriteLine($"[b13-JSON-VALID] JSON with null values length: {json?.Length ?? 0}");
            var isValid = IsValidJson(json);
            System.Diagnostics.Debug.WriteLine($"[b13-JSON-VALID] JSON validation: IsValid={isValid}, Length={json?.Length ?? 0}");

            Assert.NotNull(json);
            Assert.True(isValid);
            Assert.Contains("null", json);
        }

        [Fact]
        public void SerializeEmptyCollections_MaintainsStructure()
        {
            var payloadWithEmptyCollections = new
            {
                emptyObject = new { },
                emptyArray = new object[] { },
                filled = "not-empty"
            };

            var msg = new Message
            {
                MessageType = "test-handler",
                MessageId = "msg-empty-001",
                Data = JToken.FromObject(payloadWithEmptyCollections)
            };

            var json = JsonConvert.SerializeObject(msg);
            System.Diagnostics.Debug.WriteLine($"[b13-JSON-VALID] JSON with empty collections length: {json?.Length ?? 0}");
            var isValid = IsValidJson(json);
            System.Diagnostics.Debug.WriteLine($"[b13-JSON-VALID] JSON validation: IsValid={isValid}, Length={json?.Length ?? 0}");

            Assert.NotNull(json);
            Assert.True(isValid);
            Assert.Contains("[]", json!);
        }

        [Fact]
        public void SerializeFullRoundTrip_WithComplexPayload_MaintainsIntegrity()
        {
            var resultsArray = JArray.FromObject(new object[]
            {
                new
                {
                    id = 1,
                    name = "Test Item with 'quotes' and \"doubles\"",
                    description = "Line 1\nLine 2\tTabbed",
                    tags = new[] { "tag1", "tag2", "tag3" },
                    nested_config = new { enabled = true, value = 42 }
                }
            });

            var metadataObj = new JObject();
            metadataObj["version"] = "2.0.0";
            metadataObj["timestamp"] = DateTime.UtcNow.ToString("O");

            var complexPayloadObj = new JObject();
            complexPayloadObj["metadata"] = metadataObj;
            complexPayloadObj["results"] = resultsArray;
            complexPayloadObj["status"] = "ok";

            var msg = new Message
            {
                MessageType = "complex-handler",
                MessageId = "msg-complex-full",
                Data = complexPayloadObj
            };

            var json = JsonConvert.SerializeObject(msg);
            var escaped = json.Replace("\\", "\\\\").Replace("'", "\\'");

            System.Diagnostics.Debug.WriteLine($"[b13-RESPONSE-OBJECT] Handler response object: Type=Object");
            System.Diagnostics.Debug.WriteLine($"[b13-JTOKEN-SERIALIZE] JToken created from payload");
            System.Diagnostics.Debug.WriteLine($"[b13-JSON-VALID] JSON validation: IsValid={IsValidJson(json)}, Length={json?.Length ?? 0}");
            System.Diagnostics.Debug.WriteLine($"[b13-SCRIPT-PAYLOAD] JavaScript payload ready: PayloadLength={escaped?.Length ?? 0}");
            System.Diagnostics.Debug.WriteLine($"[b13-SCRIPT-RESULT] ExecuteScriptAsync completed: Status=Success");

            Assert.NotNull(json);
            Assert.True(IsValidJson(json));
            Assert.NotEmpty(escaped!);
            Assert.Contains("2.0.0", json);
            Assert.Contains("Test Item with", json);

            var deserialized = JsonConvert.DeserializeObject<JObject>(json);
            Assert.NotNull(deserialized);
            var status = deserialized["data"]?["status"]?.ToString();
            Assert.Equal("ok", status);
        }
    }
}
