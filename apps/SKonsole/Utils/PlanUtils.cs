using System.Diagnostics;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Orchestration.Extensions;

namespace SKonsole.Utils;

public static class PlanUtils
{
    public static async Task<SKContext> ExecutePlanAsync(IKernel kernel, IDictionary<string, ISKFunction> planner, SKContext executionResults, int maxSteps = 10)
    {
        Console.WriteLine("Executing plan:");
        Console.WriteLine(executionResults.Variables.ToPlan().PlanString);

        Stopwatch sw = new();
        sw.Start();

        // loop until complete or at most N steps
        for (int step = 1; !executionResults.Variables.ToPlan().IsComplete && step < maxSteps; step++)
        {
            var results = await kernel.RunAsync(executionResults.Variables, planner["ExecutePlan"]);
            if (results.Variables.ToPlan().IsSuccessful)
            {
                if (results.Variables.ToPlan().IsComplete)
                {
                    Console.WriteLine("Plan Execution Complete!\n");
                    Console.WriteLine(results.Variables.ToPlan().Result);
                    break;
                }
            }
            else
            {
                Console.WriteLine($"Execution failed (step n={step}):");
                Console.WriteLine(results.Variables.ToPlan().Result);
                break;
            }

            executionResults = results;
        }

        sw.Stop();
        Console.WriteLine($"Execution complete in {sw.ElapsedMilliseconds} ms!");
        return executionResults;
    }
}