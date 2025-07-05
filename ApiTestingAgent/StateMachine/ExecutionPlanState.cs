using ApiTestingAgent.Agent;
using ApiTestingAgent.Contracts.SemanticKernel;
using ApiTestingAgent.Data.Stream;
using ApiTestingAgent.Prompts;
using ApiTestingAgent.Resources.Schemas;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ApiTestingAgent.StateMachine
{
    public class ExecutionPlanState : State<ApiTestStateTransitions>
    {
        public override string GetName() => nameof(ExecutionPlanState);

        public ExecutionPlanState(
            ILogger<State<ApiTestStateTransitions>> logger,
            StreamReporter streamReporter,
            IPromptAndSchemaRegistry promptAndSchemaRegistry,
            IStateFactory stateFactory,
            IChatCompletionAgent chatCompletionAgent)
            : base(logger, streamReporter, promptAndSchemaRegistry, stateFactory, chatCompletionAgent)
        {
        }

        public override async Task<(ApiTestStateTransitions, bool)> HandleState(
            StateContext<ApiTestStateTransitions> context,
            Session<ApiTestStateTransitions> session,
            ApiTestStateTransitions transition,
            ChatHistory chatHistory)
        {
            var prompt = await _promptAndSchemaRegistry.GetPrompt("ExecutionPlanSelect");
            chatHistory.Add(new ChatMessageContent(AuthorRole.System, prompt));

            var chatMessagesContent = await _chatCompletionAgent.GetChatCompletionAsync(chatHistory);

            var messages = chatMessagesContent.ToList();
            if (messages.Count != 1)
                throw new InvalidOperationException($"Expected a single response message, got {messages.Count}.");
            var originalMessage = messages[0];
            
            // Print the content of chatMessageContent for debugging
            Console.WriteLine($"chatMessageContent.Content: {originalMessage.Content}");
            
            // Extract JSON from markdown code blocks if present
            var jsonContent = ExtractJsonFromMarkdown(originalMessage.Content!);
            
            // Deserialize the planner result to ExecutionPlanSelectOutput
            ExecutionPlanSelectOutput? executionPlanOutput = null;
            try
            {
                executionPlanOutput = System.Text.Json.JsonSerializer.Deserialize<ExecutionPlanSelectOutput>(jsonContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to deserialize ExecutionPlanSelectOutput: {ex.Message}");
                // If deserialization fails, retry
                return (ApiTestStateTransitions.ExecutionPlanSelect, true);
            }

            Console.WriteLine("ExecutionPlanSelect (JSON):\n" + System.Text.Json.JsonSerializer.Serialize(executionPlanOutput, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n\n");

            // Apply changes to existing plan if changes are detected
            var modifiedPlan = ProcessExecutionPlanChanges(executionPlanOutput, session);

            // Store the final execution plan in session
            if (modifiedPlan.Any())
            {
                var planString = FormatExecutionPlan(modifiedPlan);
                session.AddStepResult("SelectedExecutionPlan", planString);
                
                // Also store the plan as JSON for future use
                var planJson = System.Text.Json.JsonSerializer.Serialize(modifiedPlan);
                session.AddStepResult("SelectedExecutionPlanJson", planJson);
                
                Console.WriteLine($"Updated execution plan stored with {modifiedPlan.Count} steps");
            }

            // Check if execution plan is confirmed and we should proceed to the next state
            if (executionPlanOutput!.IsConfirmed)
            {
                // Move to command invocation if execution plan is confirmed
                var nextState = _stateFactory.Create<CommandInvokeState, ApiTestStateTransitions>();
                context.SetState(nextState);
                session.SetCurrentStep(context.GetCurrentState(), ApiTestStateTransitions.CommandInvocation);
                return (ApiTestStateTransitions.CommandInvocation, true);
            }

            Console.WriteLine($"ResponseToUser:\n{executionPlanOutput!.UserResponse!}");
            await _streamReporter.ReportAsync(new List<ChatMessageContent> { originalMessage.CloneWithContent(executionPlanOutput!.UserResponse!) });

            return (ApiTestStateTransitions.ExecutionPlanSelect, false);
        }

        private static string FormatExecutionPlan(List<ExecutionPlanStep> executionPlan)
        {
            var lines = new List<string>();
            foreach (var step in executionPlan)
            {
                var bodyInfo = step.Body != null ? $", Body: {System.Text.Json.JsonSerializer.Serialize(step.Body)}" : "";
                var expectationInfo = step.Expectation != null 
                    ? $", Expected: {step.Expectation.ExpectedStatusCode}" 
                    : "";
                
                lines.Add($"Step {step.StepNumber}: {step.Method} {step.Url}{bodyInfo}{expectationInfo}");
            }
            return string.Join("\n", lines);
        }

        private static string FormatExecutionPlanChanges(List<ExecutionPlanChange> changes)
        {
            var lines = new List<string>();
            foreach (var change in changes)
            {
                lines.Add($"Change: {change.ChangeType} - Step {change.StepNumber} - {change.Description}");
            }
            return string.Join("\n", lines);
        }

        private static List<ExecutionPlanStep> ApplyChangesToPlan(
            List<ExecutionPlanStep> existingPlan, 
            List<ExecutionPlanStep> updatedPlan, 
            List<ExecutionPlanChange> changes)
        {
            // Start with a copy of the existing plan
            var modifiedPlan = new List<ExecutionPlanStep>(existingPlan);
            
            Console.WriteLine($"Applying {changes.Count} changes to execution plan:");
            
            // Sort changes to apply them in the correct order: removes first, then updates, then adds
            var sortedChanges = changes
                .OrderBy(c => c.ChangeType == "removed" ? 0 : 
                             c.ChangeType == "updated" ? 1 : 2)
                .ThenBy(c => c.StepNumber)
                .ToList();
            
            foreach (var change in sortedChanges)
            {
                Console.WriteLine($"  {change.ChangeType} - Step {change.StepNumber}: {change.Description}");
                
                switch (change.ChangeType.ToLower())
                {
                    case "removed":
                        // Remove the step from existing plan
                        var stepToRemove = modifiedPlan.FirstOrDefault(s => s.StepNumber == change.StepNumber);
                        if (stepToRemove != null)
                        {
                            modifiedPlan.Remove(stepToRemove);
                            Console.WriteLine($"    Removed step {change.StepNumber}");
                        }
                        break;
                        
                    case "updated":
                        // Update existing step with data from updatedPlan
                        var existingStep = modifiedPlan.FirstOrDefault(s => s.StepNumber == change.StepNumber);
                        var newStepData = updatedPlan.FirstOrDefault(s => s.StepNumber == change.StepNumber);
                        if (existingStep != null && newStepData != null)
                        {
                            existingStep.Method = newStepData.Method;
                            existingStep.Url = newStepData.Url;
                            existingStep.Body = newStepData.Body;
                            existingStep.Expectation = newStepData.Expectation;
                            Console.WriteLine($"    Updated step {change.StepNumber}");
                        }
                        break;
                        
                    case "added":
                        // Add new step - can be inserted at a specific position or added at the end
                        var newAddedStep = updatedPlan.FirstOrDefault(s => s.StepNumber == change.StepNumber);
                        if (newAddedStep != null)
                        {
                            if (change.StepNumber <= modifiedPlan.Count)
                            {
                                // Insert at specific position - shift existing steps down
                                foreach (var step in modifiedPlan.Where(s => s.StepNumber >= change.StepNumber))
                                {
                                    step.StepNumber++;
                                }
                                // Insert the new step
                                modifiedPlan.Insert(change.StepNumber - 1, new ExecutionPlanStep
                                {
                                    StepNumber = change.StepNumber,
                                    Method = newAddedStep.Method,
                                    Url = newAddedStep.Url,
                                    Body = newAddedStep.Body,
                                    Expectation = newAddedStep.Expectation
                                });
                                Console.WriteLine($"    Inserted step at position {change.StepNumber}");
                            }
                            else
                            {
                                // Add at the end
                                modifiedPlan.Add(new ExecutionPlanStep
                                {
                                    StepNumber = modifiedPlan.Count + 1,
                                    Method = newAddedStep.Method,
                                    Url = newAddedStep.Url,
                                    Body = newAddedStep.Body,
                                    Expectation = newAddedStep.Expectation
                                });
                                Console.WriteLine($"    Added step {modifiedPlan.Count}");
                            }
                        }
                        break;
                }
            }
            
            // Ensure step numbers are sequential after all changes
            for (int i = 0; i < modifiedPlan.Count; i++)
            {
                modifiedPlan[i].StepNumber = i + 1;
            }
            
            return modifiedPlan;
        }

        private static List<ExecutionPlanStep> ProcessExecutionPlanChanges(
            ExecutionPlanSelectOutput? executionPlanOutput, 
            Session<ApiTestStateTransitions> session)
        {
            var modifiedPlan = new List<ExecutionPlanStep>();
            
            if (executionPlanOutput?.Changes?.Any() == true)
            {
                // Get existing plan from session
                var existingPlanJson = session.StepResult.TryGetValue("SelectedExecutionPlanJson", out var planJson) ? planJson : null;
                List<ExecutionPlanStep>? existingPlan = null;
                
                if (!string.IsNullOrEmpty(existingPlanJson))
                {
                    try
                    {
                        existingPlan = System.Text.Json.JsonSerializer.Deserialize<List<ExecutionPlanStep>>(existingPlanJson);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to deserialize existing plan: {ex.Message}");
                    }
                }
                
                // Apply changes to existing plan or use updated plan as base
                modifiedPlan = ApplyChangesToPlan(existingPlan ?? new List<ExecutionPlanStep>(), 
                                               executionPlanOutput.UpdatedPlan ?? new List<ExecutionPlanStep>(), 
                                               executionPlanOutput.Changes);
            }
            
            return modifiedPlan;
        }

        private static string ExtractJsonFromMarkdown(string content)
        {
            // Remove markdown code block markers if present
            var trimmedContent = content.Trim();
            
            // Check if content starts with ```json and ends with ```
            if (trimmedContent.StartsWith("```json"))
            {
                // Find the end of the opening marker
                var startIndex = trimmedContent.IndexOf('\n', 7); // 7 = length of "```json"
                if (startIndex == -1) startIndex = 7;
                else startIndex++; // Skip the newline
                
                // Find the closing ```
                var endIndex = trimmedContent.LastIndexOf("```");
                if (endIndex > startIndex)
                {
                    return trimmedContent.Substring(startIndex, endIndex - startIndex).Trim();
                }
            }
            
            // If no markdown code blocks found, return original content
            return trimmedContent;
        }
    }
}
