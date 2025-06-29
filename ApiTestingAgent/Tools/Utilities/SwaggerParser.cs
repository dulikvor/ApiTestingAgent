using System.Text.Json.Nodes;

namespace ApiTestingAgent.Tools.Utitlities
{
    public class SwaggerOperation
    {
        public string? HttpMethod { get; set; }
        public string? Url { get; set; }
        public JsonNode? Content { get; set; } // null if not present
        public string? ApiVersion { get; set; } // new property
    }

    public class SwaggerParser
    {
        private static JsonNode? ResolveRef(JsonNode root, string refPath)
        {
            if (string.IsNullOrEmpty(refPath)) return null;
            if (!refPath.StartsWith("#/")) return null; // Only handle local refs
            var parts = refPath.Substring(2).Split('/');
            JsonNode? current = root;
            foreach (var part in parts)
            {
                if (current is JsonObject obj && obj.TryGetPropertyValue(part, out var next))
                {
                    current = next;
                }
                else
                {
                    return null;
                }
            }
            return current;
        }

        private static JsonNode? ResolveRefsRecursive(JsonNode root, JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                // If this node is a $ref, resolve it and recurse
                if (obj.TryGetPropertyValue("$ref", out var refNode) && refNode is JsonValue refVal)
                {
                    var resolved = ResolveRef(root, refVal.ToString());
                    if (resolved != null)
                        return ResolveRefsRecursive(root, resolved);
                }
                // Otherwise, recursively resolve all properties
                var clone = new JsonObject();
                foreach (var kvp in obj)
                {
                    clone[kvp.Key] = ResolveRefsRecursive(root, kvp.Value);
                }
                return clone;
            }
            else if (node is JsonArray arr)
            {
                var newArr = new JsonArray();
                foreach (var item in arr)
                {
                    newArr.Add(ResolveRefsRecursive(root, item));
                }
                return newArr;
            }
            else if (node is JsonValue valNode)
            {
                return JsonValue.Create(valNode.GetValue<object?>());
            }
            else
            {
                return null;
            }
        }

        // Recursively filter a schema to only include type and enum for each property
        private static JsonNode? ExtractTypeAndEnumOnly(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                var result = new JsonObject();
                // If this is a schema object with properties
                if (obj.TryGetPropertyValue("type", out var typeNode) && typeNode is JsonValue typeVal)
                {
                    result["type"] = JsonValue.Create(typeVal.GetValue<object?>());
                }
                if (obj.TryGetPropertyValue("enum", out var enumNode) && enumNode is JsonArray enumArr)
                {
                    // Deep copy the enum array manually
                    var newEnumArr = new JsonArray();
                    foreach (var item in enumArr)
                    {
                        if (item is JsonValue val)
                            newEnumArr.Add(JsonValue.Create(val.GetValue<object?>()));
                        else
                            newEnumArr.Add(null);
                    }
                    result["enum"] = newEnumArr;
                }
                // If this is an object with properties, recurse into them
                if (obj.TryGetPropertyValue("properties", out var propsNode) && propsNode is JsonObject propsObj)
                {
                    var newProps = new JsonObject();
                    foreach (var prop in propsObj)
                    {
                        newProps[prop.Key] = ExtractTypeAndEnumOnly(prop.Value);
                    }
                    result["properties"] = newProps;
                }
                // If this is an array, recurse into items
                if (obj.TryGetPropertyValue("items", out var itemsNode))
                {
                    result["items"] = ExtractTypeAndEnumOnly(itemsNode);
                }
                return result;
            }
            else if (node is JsonArray arr)
            {
                var newArr = new JsonArray();
                foreach (var item in arr)
                {
                    newArr.Add(ExtractTypeAndEnumOnly(item));
                }
                return newArr;
            }
            else if (node is JsonValue valNode)
            {
                return JsonValue.Create(valNode.GetValue<object?>());
            }
            else
            {
                return null;
            }
        }

        public static List<SwaggerOperation> ParseOperations(string swaggerJson)
        {
            var operations = new List<SwaggerOperation>();
            var doc = JsonNode.Parse(swaggerJson);
            if (doc == null) return operations;

            // Try to get api-version from top-level info.version or x-ms-api-version
            string apiVersion = null;
            if (doc["info"] is JsonObject infoObj)
            {
                if (infoObj.TryGetPropertyValue("version", out var versionNode) && versionNode is JsonValue versionVal)
                {
                    apiVersion = versionVal.ToString();
                }
            }

            var paths = doc["paths"] as JsonObject;
            if (paths == null) return operations;

            foreach (var pathKvp in paths)
            {
                string url = pathKvp.Key;
                var methods = pathKvp.Value as JsonObject;
                if (methods == null) continue;

                foreach (var methodKvp in methods)
                {
                    string httpMethod = methodKvp.Key.ToUpperInvariant();
                    var operationObj = methodKvp.Value as JsonObject;
                    JsonNode? content = null;
                    if (operationObj != null && operationObj.TryGetPropertyValue("requestBody", out var requestBody))
                    {
                        if (requestBody is JsonObject requestBodyObj)
                        {
                            // Handle $ref in requestBody
                            if (requestBodyObj.TryGetPropertyValue("$ref", out var refNode) && refNode is JsonValue refVal)
                            {
                                var resolved = ResolveRef(doc, refVal.ToString());
                                if (resolved is JsonObject resolvedObj && resolvedObj.TryGetPropertyValue("content", out var resolvedContent))
                                    content = resolvedContent;
                            }
                            else if (requestBodyObj.TryGetPropertyValue("content", out var contentNode) && contentNode is JsonObject contentObj)
                            {
                                // Pick application/json or the first available content type
                                if (contentObj.TryGetPropertyValue("application/json", out var appJson) && appJson is JsonObject appJsonObj && appJsonObj.TryGetPropertyValue("schema", out var schemaNode))
                                {
                                    content = schemaNode;
                                }
                                else
                                {
                                    // fallback: pick first schema if present
                                    foreach (var ct in contentObj)
                                    {
                                        var value = ct.Value;
                                        if (value is JsonObject ctObj && ctObj.TryGetPropertyValue("schema", out var s))
                                        {
                                            content = s;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    // Also resolve $ref in parameters (for body parameters)
                    if (content == null && operationObj != null && operationObj.TryGetPropertyValue("parameters", out var parametersNode) && parametersNode is JsonArray parametersArr)
                    {
                        foreach (var param in parametersArr)
                        {
                            if (param is JsonObject paramObj && paramObj.TryGetPropertyValue("$ref", out var paramRefNode) && paramRefNode is JsonValue paramRefVal)
                            {
                                var resolvedParam = ResolveRef(doc, paramRefVal.ToString());
                                if (resolvedParam is JsonObject resolvedParamObj && resolvedParamObj.TryGetPropertyValue("schema", out var paramSchema))
                                {
                                    // If schema has $ref, resolve it
                                    if (paramSchema is JsonObject schemaObj && schemaObj.TryGetPropertyValue("$ref", out var schemaRefNode) && schemaRefNode is JsonValue schemaRefVal)
                                    {
                                        var resolvedSchema = ResolveRef(doc, schemaRefVal.ToString());
                                        if (resolvedSchema != null)
                                            content = resolvedSchema;
                                    }
                                    else
                                    {
                                        content = paramSchema;
                                    }
                                }
                            }
                            else if (param is JsonObject paramObj2 && paramObj2.TryGetPropertyValue("schema", out var paramSchema2))
                            {
                                // If schema has $ref, resolve it
                                if (paramSchema2 is JsonObject schemaObj2 && schemaObj2.TryGetPropertyValue("$ref", out var schemaRefNode2) && schemaRefNode2 is JsonValue schemaRefVal2)
                                {
                                    var resolvedSchema2 = ResolveRef(doc, schemaRefVal2.ToString());
                                    if (resolvedSchema2 != null)
                                        content = resolvedSchema2;
                                }
                                else
                                {
                                    content = paramSchema2;
                                }
                            }
                        }
                    }
                    // Recursively resolve all $ref in content
                    if (content != null)
                    {
                        content = ResolveRefsRecursive(doc, content);
                        content = ExtractTypeAndEnumOnly(content);
                    }
                    operations.Add(new SwaggerOperation
                    {
                        HttpMethod = httpMethod,
                        Url = url,
                        Content = content,
                        ApiVersion = apiVersion
                    });
                }
            }
            return operations;
        }
    }
}
