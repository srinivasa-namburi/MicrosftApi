using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;

public class UserInput
{

    [SKFunction("Requests input from the user")]    
    public async Task<string> RequestUserInput(SKContext context)
    {
        //TODO: Console.ReadLine, etc
        throw new NotImplementedException();
    }
}