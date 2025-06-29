using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using Microsoft.SemanticKernel.PromptTemplates;
using Microsoft.SemanticKernel;

namespace ApiTestingAgent.Prompts
{
    public class PromptAndSchemaRegistry : IPromptAndSchemaRegistry
    {
        private readonly Dictionary<string, string> _prompts = new();
        private readonly Dictionary<string, (string schema, Type userType)> _schemas = new();
        private readonly HandlebarsPromptTemplateFactory _templateFactory = new HandlebarsPromptTemplateFactory();
        private readonly Kernel _kernel;

        public PromptAndSchemaRegistry(Kernel kernel, string[] promptDirs, string[] schemaDirs, Dictionary<string, Type>? schemaUserTypes = null)
        {
            _kernel = kernel;

            string baseDir = AppContext.BaseDirectory;
            foreach (var dir in promptDirs)
            {
                var absDir = Path.IsPathRooted(dir) ? dir : Path.Combine(baseDir, dir);
                if (!Directory.Exists(absDir)) continue;
                foreach (var file in Directory.GetFiles(absDir, "*.txt"))
                {
                    var key = Path.GetFileNameWithoutExtension(file);
                    _prompts[NormalizeKey(key)] = File.ReadAllText(file);
                }
            }
            foreach (var dir in schemaDirs)
            {
                var absDir = Path.IsPathRooted(dir) ? dir : Path.Combine(baseDir, dir);
                if (!Directory.Exists(absDir)) continue;
                foreach (var file in Directory.GetFiles(absDir, "*.json"))
                {
                    var rawKey = Path.GetFileNameWithoutExtension(file);
                    var key = NormalizeKey(rawKey);
                    var schema = File.ReadAllText(file);
                    Type? userType = null;
                    string cleanedSchema = schema;
                    // Try to get userType from schema JSON property "x-userType" (mandatory)
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(schema);
                        if (!doc.RootElement.TryGetProperty("x-userType", out var userTypeProp))
                            throw new InvalidDataException($"Schema {key} is missing required 'x-userType' property.");
                        var userTypeName = userTypeProp.GetString();
                        if (string.IsNullOrEmpty(userTypeName))
                            throw new InvalidDataException($"Schema {key} has empty 'x-userType' property.");
                        userType = Type.GetType(userTypeName);
                        if (userType == null)
                            throw new InvalidDataException($"Schema {key} 'x-userType' could not be resolved to a .NET type: {userTypeName}");
                        // Remove 'x-userType' from the schema JSON
                        var jsonObj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(schema);
                        jsonObj?.Remove("x-userType");
                        cleanedSchema = System.Text.Json.JsonSerializer.Serialize(jsonObj);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"Invalid schema in {file}: {ex.Message}");
                    }
                    // Basic validation: ensure 'type' and 'properties' exist
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(cleanedSchema);
                        if (!doc.RootElement.TryGetProperty("type", out _) || !doc.RootElement.TryGetProperty("properties", out _))
                            throw new InvalidDataException($"Schema {key} missing required properties.");
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"Invalid schema in {file}: {ex.Message}");
                    }
                    // Validate the user type against the schema using SchemaValidator
                    try
                    {
                        if (!ValidateSchema(cleanedSchema, userType))
                        {
                            throw new InvalidDataException($"Schema {key} failed validation against user type {userType?.FullName}.");
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"Schema {key} failed validation against user type {userType?.FullName}: {ex.Message}");
                    }
                    _schemas[key] = (cleanedSchema, userType);
                }
            }
        }

        // Normalize keys: strip ".schema" if present, and make case-insensitive
        private static string NormalizeKey(string key)
        {
            if (key.EndsWith(".schema", StringComparison.OrdinalIgnoreCase))
                key = key.Substring(0, key.Length - ".schema".Length);
            return key.ToLowerInvariant();
        }

        public async Task<string> GetPrompt(string key, Dictionary<string, string>? extraArgs = null)
        {
            var normKey = NormalizeKey(key);
            if (!_prompts.TryGetValue(normKey, out var prompt))
                throw new KeyNotFoundException($"Prompt not found: {key}");

            var arguments = new KernelArguments();
            if (_schemas.TryGetValue(normKey, out var tuple) && tuple.schema != null)
            {
                arguments["output_schema"] = tuple.schema;
            }

            if (extraArgs != null)
            {
                foreach (var kvp in extraArgs)
                {
                    arguments[kvp.Key] = kvp.Value;
                }
            }

            if (arguments.Any())
            {
                var promptTemplateConfig = new PromptTemplateConfig
                {
                    Template = prompt,
                    TemplateFormat = "handlebars",
                    Name = normKey
                };

                var promptTemplate = _templateFactory.Create(promptTemplateConfig);
                var renderedPrompt = await promptTemplate.RenderAsync(_kernel, arguments);
                return renderedPrompt;
            }
            
            return prompt;
        }

        public string? GetSchema(string key)
        {
            var normKey = NormalizeKey(key);
            if (_schemas.TryGetValue(normKey, out var tuple))
                return tuple.schema;
            return null;
        }

        public Type? GetSchemaUserType(string key)
        {
            var normKey = NormalizeKey(key);
            if (_schemas.TryGetValue(normKey, out var tuple))
                return tuple.userType;
            return null;
        }

        private bool ValidateSchema(string schemaJson, Type actualType)
        {
            // Parse the JSON schema
            var schema = JsonNode.Parse(schemaJson);

            // Extract properties from the schema
            var schemaProperties = schema?["properties"]?.AsObject();
            if (schemaProperties == null) return false;

            // Get properties of the actual type
            var actualProperties = actualType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                            .ToDictionary(p => p.Name.ToLower(), p => p.PropertyType);

            // Check if all schema properties exist in the actual type
            foreach (var schemaProperty in schemaProperties)
            {
                if (!actualProperties.ContainsKey(schemaProperty.Key.ToLower()))
                {
                    Console.WriteLine($"Missing property: {schemaProperty.Key}");
                    return false;
                }

                // Verify the type of the property
                var expectedTypeName = schemaProperty.Value?["type"]?.ToString().ToLower() ?? string.Empty;
                if (expectedTypeName != null)
                {
                    var actualPropertyType = actualProperties[schemaProperty.Key.ToLower()];

                    // Check for array type compatibility
                    if (expectedTypeName == "array")
                    {
                        if (!actualPropertyType.IsArray &&
                            !(actualPropertyType.IsGenericType && actualPropertyType.GetGenericTypeDefinition() == typeof(List<>)) &&
                            !(typeof(System.Collections.IEnumerable).IsAssignableFrom(actualPropertyType) && actualPropertyType != typeof(string)))
                        {
                            Console.WriteLine($"Type mismatch for property: {schemaProperty.Key}. Expected: array, Actual: {actualType}");
                            return false;
                        }
                    }
                    else if (!actualPropertyType.Name.ToLower().Equals(expectedTypeName))
                    {
                        // Special case: map C# Int32/Int64 to JSON Schema 'integer'
                        if ((actualPropertyType == typeof(int) || actualPropertyType == typeof(long)) && expectedTypeName == "integer")
                        {
                            continue;
                        }
                        // Special case: map C# Int32/Int64 to JSON Schema 'int32'/'int64'
                        if (actualPropertyType == typeof(int) && expectedTypeName == "int32")
                        {
                            continue;
                        }
                        if (actualPropertyType == typeof(long) && expectedTypeName == "int64")
                        {
                            continue;
                        }
                        // Check if the property is an enum or a nullable enum and has the JsonStringEnumConverter attribute
                        if (actualPropertyType.IsEnum || (Nullable.GetUnderlyingType(actualPropertyType)?.IsEnum ?? false))
                        {
                            var enumType = actualPropertyType.IsEnum ? actualPropertyType : Nullable.GetUnderlyingType(actualPropertyType);
                            var propertyInfo = actualType.GetProperty(schemaProperty.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                            var jsonConverterAttribute = propertyInfo?.GetCustomAttribute<JsonConverterAttribute>();

                            if (jsonConverterAttribute?.ConverterType == typeof(JsonStringEnumConverter) && expectedTypeName == "string")
                            {
                                continue; // Enum or nullable enum with JsonStringEnumConverter is valid as a string
                            }
                        }

                        // Debug: Print all custom attributes of the property type
                        var customAttributes = actualPropertyType.GetCustomAttributes();
                        foreach (var attribute in customAttributes)
                        {
                            Console.WriteLine($"Custom Attribute: {attribute.GetType().Name}");
                        }

                        Console.WriteLine($"Type mismatch for property: {schemaProperty.Key}. Expected: {expectedTypeName}, Actual: {actualType}");
                        return false;
                    }
                }
            }

            // Check required fields
            var requiredFields = schema?["required"]?.AsArray()?.Select(r => r?.ToString());
            if (requiredFields != null)
            {
                foreach (var requiredField in requiredFields)
                {
                    if (requiredField == null) continue;
                    if (!actualProperties.ContainsKey(requiredField.ToLower()))
                    {
                        Console.WriteLine($"Missing required field: {requiredField}");
                        return false;
                    }
                }
            }

            return true;
        }

        public IEnumerable<string> RegisteredPromptKeys => _prompts.Keys;
        public IEnumerable<string> RegisteredSchemaKeys => _schemas.Keys;
    }
}
