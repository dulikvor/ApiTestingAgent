using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Net;
using ApiTestingAgent.Tools.Utitlities;
using System.Text.Json.Serialization;
using ApiTestingAgent.Data.Stream;
using ApiTestingAgent.Data;
using ApiTestingAgent.StateMachine;
using ApiTestingAgent.Resources.Schemas;
using System.Text.Json;

namespace ApiTestingAgent.Tools
{
    public class ExecutionPlanResult
    {
        [JsonPropertyName("totalSteps")]
        public int TotalSteps { get; set; }

        [JsonPropertyName("successfulSteps")]
        public int SuccessfulSteps { get; set; }

        [JsonPropertyName("failedSteps")]
        public int FailedSteps { get; set; }

        [JsonPropertyName("stepResults")]
        public List<StepExecutionResult> StepResults { get; set; } = new();
    }

    public class StepExecutionResult
    {
        [JsonPropertyName("stepNumber")]
        public int StepNumber { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("httpStatusCode")]
        public HttpStatusCode HttpStatusCode { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("responseContent")]
        public string ResponseContent { get; set; } = string.Empty;

        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }
    }

    public class ContentValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// Tool for executing an entire execution plan stored in the session.
    /// </summary>
    public class ExecutionPlanTools
    {
        private readonly IRestClient _restClient;
        private readonly IStreamWriter _streamWriter;

        public ExecutionPlanTools(IRestClient restClient, IStreamWriter streamWriter)
        {
            _restClient = restClient ?? throw new ArgumentNullException(nameof(restClient));
            _streamWriter = streamWriter ?? throw new ArgumentNullException(nameof(streamWriter));
        }

        /// <summary>
        /// Executes an entire execution plan that was previously stored in the session.
        /// The execution plan contains a series of REST API calls to be executed in sequence.
        /// Execution stops immediately if any step fails validation or throws an exception.
        /// </summary>
        /// <returns>ExecutionPlanResult containing execution results for LLM analysis</returns>
        [KernelFunction("execute_plan")]
        [Description("Executes an entire execution plan stored in the session. " +
                    "The execution plan contains a series of REST API calls that will be executed in sequence. " +
                    "Execution stops immediately when any step fails validation or throws an exception. " +
                    "Returns ExecutionPlanResult object with: " +
                    "- totalSteps (int): Total number of steps in the plan " +
                    "- successfulSteps (int): Number of steps that completed successfully " +
                    "- failedSteps (int): Number of steps that failed " +
                    "- stepResults (array): Detailed results for each executed step containing stepNumber, method, url, httpStatusCode, success (boolean), responseContent (string), and errorMessage (string if failed). " +
                    "Use this tool when the user wants to execute the complete test plan that was previously created and confirmed.")]
        public async Task<ExecutionPlanResult> ExecutePlanAsync()
        {
            Console.WriteLine("ExecutionPlanTools.ExecutePlanAsync called");
            var httpContext = (HttpContext)CallContext.GetData("HttpContext")!;
            var userName = (string)CallContext.GetData("UserNameKey")!;
            
            // Get the session to retrieve the execution plan
            var session = SessionStore<Session<ApiTestStateTransitions>, ApiTestStateTransitions>.GetSessions(userName);
            
            // Retrieve the execution plan from session
            if (!session.StepResult.TryGetValue("SelectedExecutionPlanJson", out var planJson) || string.IsNullOrEmpty(planJson))
            {
                await _streamWriter.WriteTextToStreamAsync(httpContext, "No execution plan found in session. Please create and confirm an execution plan first.");
                throw new InvalidOperationException("No execution plan found in session");
            }

            List<ExecutionPlanStep>? executionPlan;
            try
            {
                executionPlan = System.Text.Json.JsonSerializer.Deserialize<List<ExecutionPlanStep>>(planJson);
            }
            catch (Exception ex)
            {
                await _streamWriter.WriteTextToStreamAsync(httpContext, $"Failed to parse execution plan: {ex.Message}");
                throw new InvalidOperationException($"Failed to parse execution plan: {ex.Message}");
            }

            if (executionPlan == null || !executionPlan.Any())
            {
                await _streamWriter.WriteTextToStreamAsync(httpContext, "Execution plan is empty");
                throw new InvalidOperationException("Execution plan is empty");
            }

            var result = new ExecutionPlanResult
            {
                TotalSteps = executionPlan.Count
            };

            await _streamWriter.WriteTextToStreamAsync(httpContext, $"Starting execution of plan with {executionPlan.Count} steps");

            // Execute each step in the plan
            for (int i = 0; i < executionPlan.Count; i++)
            {
                var step = executionPlan[i];
                var stepResult = new StepExecutionResult
                {
                    StepNumber = step.StepNumber,
                    Method = step.Method,
                    Url = step.Url
                };

                try
                {
                    await _streamWriter.WriteTextToStreamAsync(httpContext, $"Executing Step {step.StepNumber}: {step.Method} {step.Url}");
                    
                    // Prepare request body
                    string requestBody = string.Empty;
                    if (step.Body != null)
                    {
                        requestBody = System.Text.Json.JsonSerializer.Serialize(step.Body);
                    }

                    // Execute the REST call
                    var restResponse = await _restClient.InvokeRest(step.Method, step.Url, null!, requestBody);
                    
                    stepResult.HttpStatusCode = restResponse.HttpStatusCode;
                    stepResult.ResponseContent = restResponse.Content;

                    // Check if the step met expectations
                    bool stepSucceeded = true;
                    var validationErrors = new List<string>();
                    
                    if (step.Expectation != null)
                    {
                        // Validate status code
                        if (step.Expectation.ExpectedStatusCode.HasValue)
                        {
                            var expectedCode = (HttpStatusCode)step.Expectation.ExpectedStatusCode.Value;
                            if (restResponse.HttpStatusCode != expectedCode)
                            {
                                stepSucceeded = false;
                                validationErrors.Add($"Expected status code {expectedCode} but got {restResponse.HttpStatusCode}");
                            }
                        }
                        
                        // Validate content structure if expected content is provided
                        if (step.Expectation.ExpectedContent != null)
                        {
                            var contentValidationResult = ValidateResponseContent(restResponse.Content, step.Expectation.ExpectedContent);
                            if (!contentValidationResult.IsValid)
                            {
                                stepSucceeded = false;
                                validationErrors.AddRange(contentValidationResult.Errors);
                            }
                        }
                    }
                    else
                    {
                        // Default success criteria: 2xx status codes
                        stepSucceeded = ((int)restResponse.HttpStatusCode >= 200 && (int)restResponse.HttpStatusCode < 300);
                        
                        if (!stepSucceeded)
                        {
                            validationErrors.Add($"HTTP status code {restResponse.HttpStatusCode} indicates failure");
                        }
                    }

                    stepResult.Success = stepSucceeded;
                    
                    if (stepSucceeded)
                    {
                        result.SuccessfulSteps++;
                        await _streamWriter.WriteTextToStreamAsync(httpContext, $"✓ Step {step.StepNumber} completed successfully - Status: {restResponse.HttpStatusCode}");
                        result.StepResults.Add(stepResult);
                    }
                    else
                    {
                        result.FailedSteps++;
                        stepResult.ErrorMessage = string.Join("; ", validationErrors);
                        await _streamWriter.WriteTextToStreamAsync(httpContext, $"✗ Step {step.StepNumber} failed - {stepResult.ErrorMessage}");
                        
                        // Stop execution when a step fails to meet expectations
                        result.StepResults.Add(stepResult);
                        await _streamWriter.WriteTextToStreamAsync(httpContext, $"Execution stopped at step {step.StepNumber}. Returning results for analysis.");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    stepResult.Success = false;
                    stepResult.ErrorMessage = ex.Message;
                    result.FailedSteps++;
                    result.StepResults.Add(stepResult);
                    
                    await _streamWriter.WriteTextToStreamAsync(httpContext, $"✗ Step {step.StepNumber} failed with exception: {ex.Message}");
                    await _streamWriter.WriteTextToStreamAsync(httpContext, $"Execution stopped at step {step.StepNumber}. Returning results for analysis.");
                    break;
                }
            }

            // Store the execution results in session
            var resultsJson = System.Text.Json.JsonSerializer.Serialize(result);
            session.AddStepResult("ExecutionPlanResults", resultsJson);

            await _streamWriter.WriteTextToStreamAsync(httpContext, 
                $"Execution completed: {result.SuccessfulSteps}/{result.TotalSteps} steps successful, {result.FailedSteps} failed");

            Console.WriteLine($"ExecutionPlanTools.ExecutePlanAsync completed - {result.SuccessfulSteps}/{result.TotalSteps} successful");
            
            return result;
        }

        /// <summary>
        /// Validates that the response content contains the expected structure.
        /// </summary>
        /// <param name="responseContent">The actual response content as JSON string</param>
        /// <param name="expectedContent">The expected content structure</param>
        /// <returns>ContentValidationResult indicating if validation passed and any errors</returns>
        private static ContentValidationResult ValidateResponseContent(string responseContent, object expectedContent)
        {
            var result = new ContentValidationResult { IsValid = true };
            
            try
            {
                // Parse response content as JSON
                using var responseDoc = System.Text.Json.JsonDocument.Parse(responseContent);
                var responseRoot = responseDoc.RootElement;
                
                // Serialize expected content to JSON for comparison
                var expectedJson = System.Text.Json.JsonSerializer.Serialize(expectedContent);
                using var expectedDoc = System.Text.Json.JsonDocument.Parse(expectedJson);
                var expectedRoot = expectedDoc.RootElement;
                
                // Validate the structure
                ValidateJsonElement(responseRoot, expectedRoot, "", result);
            }
            catch (System.Text.Json.JsonException ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Invalid JSON in response: {ex.Message}");
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Content validation error: {ex.Message}");
            }
            
            return result;
        }
        
        /// <summary>
        /// Recursively validates JSON elements to ensure the response contains expected structure.
        /// </summary>
        private static void ValidateJsonElement(JsonElement actual, JsonElement expected, string path, ContentValidationResult result)
        {
            // Check if the expected element exists in actual
            if (expected.ValueKind == JsonValueKind.Object)
            {
                if (actual.ValueKind != JsonValueKind.Object)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Expected object at path '{path}', but got {actual.ValueKind}");
                    return;
                }
                
                // Check each property in expected exists in actual
                foreach (var expectedProp in expected.EnumerateObject())
                {
                    var propPath = string.IsNullOrEmpty(path) ? expectedProp.Name : $"{path}.{expectedProp.Name}";
                    
                    if (actual.TryGetProperty(expectedProp.Name, out var actualProp))
                    {
                        ValidateJsonElement(actualProp, expectedProp.Value, propPath, result);
                    }
                    else
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Missing expected property '{expectedProp.Name}' at path '{path}'");
                    }
                }
            }
            else if (expected.ValueKind == JsonValueKind.Array)
            {
                if (actual.ValueKind != JsonValueKind.Array)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Expected array at path '{path}', but got {actual.ValueKind}");
                    return;
                }
                
                // For arrays, we just check that actual has at least as many elements as expected
                // and validate the structure of the first element if expected array is not empty
                var expectedArray = expected.EnumerateArray().ToList();
                var actualArray = actual.EnumerateArray().ToList();
                
                if (expectedArray.Count > 0 && actualArray.Count > 0)
                {
                    ValidateJsonElement(actualArray[0], expectedArray[0], $"{path}[0]", result);
                }
                else if (expectedArray.Count > 0 && actualArray.Count == 0)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Expected non-empty array at path '{path}', but got empty array");
                }
            }
            // For primitive types, we just check the type matches (not the exact value)
            else if (expected.ValueKind != actual.ValueKind)
            {
                result.IsValid = false;
                result.Errors.Add($"Expected {expected.ValueKind} at path '{path}', but got {actual.ValueKind}");
            }
        }
    }
}
