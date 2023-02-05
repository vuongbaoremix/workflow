using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Linq;
using Workflow;

var input = new Dictionary<string, object>() {
    { "value", 1 }
};

var stepA = new Workflow.Workflow
{
    Name = "A",
    NextStep = new List<string> { "B", "C", "F", "G" },
    Run = (input) =>
    {
        return new Dictionary<string, object>()
        {
            { "A_out", input["value"] },
        };
    }
};

var stepB = new Workflow.Workflow
{
    Name = "B",
    NextStep = new List<string> { "D" },
    Run = (input) =>
    {
        int output = 0;
        if (input.ContainsKey("B_out"))
        {
            output = ((int)input["B_out"]) + 1;
        }
        else
        {
            output = ((int)input["A_out"]) + 1;
        }

        return new Dictionary<string, object>()
        {
            { "B_out", output },
        };
    },
    RequireInput = new List<string> { "A" }
};


var stepC = new Workflow.Workflow
{
    Name = "C",
    NextStep = new List<string> { "E" },
    Run = (input) =>
    {
        var output = ((int)input["A_out"]) + 10;

        return new Dictionary<string, object>()
        {
            { "C_out", output },
        };
    },
    RequireInput = new List<string> { "A" }
};

var stepD = new ConditionStep { Name = "D", Condition = "B_out < 5", IfStep = new List<string> { "B" }, ElseStep = new List<string> { "E" }, RequireInput = new List<string> { "B" } };
var stepE = new Workflow.Workflow
{
    Name = "E",
    RequireInput = new List<string> { "C", "D", "F", "G" },
    Run = (input) =>
    {
        var value = ((int)input["B_out"]) + ((int)input["C_out"]) + ((int)input["F_out"]) + ((int)input["G_out"]);

        return new Dictionary<string, object>()
        {
            { "output", value }
        };
    }
};


var stepF = new Workflow.Workflow
{
    Name = "F",
    NextStep = new List<string> { "G", "E" },
    Run = (input) => 
    {
        var output = ((int)input["A_out"]) + 2;

        return new Dictionary<string, object>()
        {
            { "F_out", output },
        };
    },
    RequireInput = new List<string> { "A" }
};


var stepG = new Workflow.Workflow
{
    Name = "G",
    NextStep = new List<string> { "E" },
    Run = (input) =>
    {
        var output = (((int)input["A_out"]) + 1) * ((int)input["F_out"]);

        return new Dictionary<string, object>()
        {
            { "G_out", output },
        };
    },
    RequireInput = new List<string> { "A", "F" }
};


Dictionary<string, IStep> allStep = new Dictionary<string, IStep>() {
    { "A", stepA },
    { "B", stepB },
    { "C", stepC },
    { "D", stepD },
    { "F", stepF },
    { "G", stepG },
    { "E", stepE }
};
 
Func<string, IStep> factory = (name) =>
{
    return allStep[name];
};

var workflow = new WorkflowBuilder(factory);

var output = await workflow.RunAsync(new List<string> { "A" }, "E", input);

Console.WriteLine($"Output: {JsonConvert.SerializeObject(output)}");
