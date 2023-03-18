using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Orchestration.Extensions;

namespace SKonsole.Utils;

public static class PlanUtils
{
    public static async Task<SKContext> ExecutePlanAsync(IKernel kernel, IDictionary<string, ISKFunction> planner, SKContext executionResults, int maxSteps = 10)
    {
        kernel.Log.LogInformation("Executing plan:\n{plan}", executionResults.Variables.ToPlan().PlanString);

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
                    kernel.Log.LogInformation("Plan Execution Complete!\n{planResult}", results.Variables.ToPlan().Result);
                    break;
                }
            }
            else
            {
                kernel.Log.LogInformation("Execution failed (step n={step}):\n{plan}", step, results.Variables.ToPlan().Result);

                break;
            }

            executionResults = results;
        }

        sw.Stop();
        kernel.Log.LogInformation("Execution complete in {executionTime} ms!", sw.ElapsedMilliseconds);
        return executionResults;
    }
}