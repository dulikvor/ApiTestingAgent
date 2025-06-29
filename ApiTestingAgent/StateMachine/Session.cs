namespace ApiTestingAgent.StateMachine;

public class Session<TTransition>
    where TTransition : Enum
{
    public TTransition? CurrentTransition { get; private set; }
    public State<TTransition>? CurrentStep { get; private set; }

    public Dictionary<string, string> StepResult { get; } = new Dictionary<string, string>();

    public void AddStepResult(string stepResultKey, string value)
    {
        StepResult[stepResultKey] = value;
    }

    public void SetCurrentStep(State<TTransition> step, TTransition transition)
    {
        CurrentStep = step;
        CurrentTransition = transition;
    }

    public bool StepResultExists(string stepResultKey)
    {
        return StepResult.ContainsKey(stepResultKey);
    }

    public bool RemoveStepResult(string stepResultKey)
    {
        if (StepResult.ContainsKey(stepResultKey))
        {
            StepResult.Remove(stepResultKey);
            return true;
        }

        return false;
    }
}

