using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using Microsoft.SemanticKernel;

namespace ApiTestingAgent.Prompts
{
    public class PromptAndSchemaRegistry : IPromptAndSchemaRegistry
    {
        private readonly Dictionary<string, string> _prompts = new();
        private readonly Dictionary<string, (string schema, Type userType)> _schemas = new();
        private readonly Dictionary<string, KernelFunction> _semanticFunctions = new();
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
                                            .ToDictionary(p => p.Name.ToLower(), p => p);

            // Check if all schema properties exist in the actual type
            foreach (var schemaProperty in schemaProperties)
            {
                if (!actualProperties.TryGetValue(schemaProperty.Key.ToLower(), out var propertyInfo))
                {
                    Console.WriteLine($"Missing property: {schemaProperty.Key}");
                    return false;
                }
                var actualPropertyType = propertyInfo.PropertyType;

                // Handle Dictionary<string, string> for JSON Schema object with additionalProperties of type string
                var expectedTypeName = schemaProperty.Value?["type"]?.ToString().ToLower() ?? string.Empty;
                if (expectedTypeName == "object" && schemaProperty.Value?["additionalProperties"]?["type"]?.ToString().ToLower() == "string")
                {
                    if ((actualPropertyType.IsGenericType && actualPropertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                        && actualPropertyType.GetGenericArguments()[0] == typeof(string)
                        && actualPropertyType.GetGenericArguments()[1] == typeof(string))
                    {
                        continue;
                    }
                    Console.WriteLine($"Type mismatch for property: {schemaProperty.Key}. Expected: Dictionary<string, string>, Actual: {actualPropertyType}");
                    return false;
                }

                // Handle custom object types - if schema type is "object" with properties, allow custom classes
                if (expectedTypeName == "object" && schemaProperty.Value?["properties"] != null)
                {
                    // Allow any class type for complex objects with defined properties
                    if (actualPropertyType.IsClass || actualPropertyType.IsValueType)
                    {
                        continue;
                    }
                }

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
                    // Handle nullable types
                    var underlyingType = Nullable.GetUnderlyingType(actualPropertyType);
                    if (underlyingType != null)
                    {
                        // For nullable types, check the underlying type
                        if (underlyingType.Name.ToLower().Equals(expectedTypeName))
                        {
                            continue;
                        }
                        // Special case: map nullable C# Int32/Int64 to JSON Schema 'integer'
                        if ((underlyingType == typeof(int) || underlyingType == typeof(long)) && expectedTypeName == "integer")
                        {
                            continue;
                        }
                    }
                    
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
                        var enumPropertyInfo = actualType.GetProperty(schemaProperty.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        var jsonConverterAttribute = enumPropertyInfo?.GetCustomAttribute<JsonConverterAttribute>();

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

                    Console.WriteLine($"Type mismatch for property: {schemaProperty.Key}. Expected: {expectedTypeName}, Actual: {actualPropertyType}");
                    return false;
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

        // Allows overriding an existing prompt at runtime (for development/testing only)
        public void OverridePrompt(string key, string newPrompt)
        {
            var normKey = NormalizeKey(key);
            if (!_prompts.ContainsKey(normKey))
                throw new KeyNotFoundException($"Prompt not found: {key}");
            _prompts[normKey] = newPrompt;
        }

        /// <summary>
        /// Creates a semantic function from a registered prompt with simple token and temperature settings.
        /// Uses caching to avoid recreating the same function multiple times.
        /// </summary>
        /// <param name="key">The prompt key to create the function from</param>
        /// <param name="maxTokens">Maximum tokens for the response</param>
        /// <param name="temperature">Temperature setting for randomness (0.0 to 1.0)</param>
        /// <returns>A KernelFunction that can be invoked</returns>
        public KernelFunction CreateSemanticFunction(string key, int maxTokens = 500, double temperature = 0.5)
        {
            var normKey = NormalizeKey(key);
            
            // Create a cache key that includes settings to ensure uniqueness
            var cacheKey = $"{normKey}_{maxTokens}_{temperature}";
            
            // Return cached function if it exists
            if (_semanticFunctions.TryGetValue(cacheKey, out var cachedFunction))
            {
                return cachedFunction;
            }
            
            if (!_prompts.TryGetValue(normKey, out var prompt))
                throw new KeyNotFoundException($"Prompt not found: {key}");

            // Create execution settings with simplified approach
            var executionSettings = new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    { "max_tokens", maxTokens },
                    { "temperature", temperature }
                }
            };

            // Create function using Handlebars template factory
            var promptTemplateConfig = new PromptTemplateConfig
            {
                Template = prompt,
                TemplateFormat = "handlebars",
                Name = normKey,
                ExecutionSettings = new Dictionary<string, PromptExecutionSettings> 
                { 
                    { "default", executionSettings } 
                }
            };

            var function = _kernel.CreateFunctionFromPrompt(promptTemplateConfig, _templateFactory);
            
            // Cache the function for future use
            _semanticFunctions[cacheKey] = function;
            
            return function;
        }

        /// <summary>
        /// Gets a previously created semantic function by key.
        /// Returns the first cached function for the given prompt key.
        /// </summary>
        /// <param name="key">The prompt key to search for</param>
        /// <returns>The cached KernelFunction if found, null otherwise</returns>
        public KernelFunction? GetSemanticFunction(string key)
        {
            var normKey = NormalizeKey(key);
            
            // Find the first cached function that starts with the normalized key
            var cachedEntry = _semanticFunctions.FirstOrDefault(kvp => 
                kvp.Key == normKey || kvp.Key.StartsWith($"{normKey}_"));
            
            return cachedEntry.Key != null ? cachedEntry.Value : null;
        }
    }
}
