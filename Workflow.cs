using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Z.Expressions;

namespace Workflow
{
    interface IStep
    {
        string Name { set; get; }
        List<string>? NextStep { set; get; }
        List<string>? RequireInput { set; get; }
        Dictionary<string, object>? Input { get; }
        Dictionary<string, object>? Output { get; }

        Func<Dictionary<string, object>, Dictionary<string, object>?>? Run { set; get; }

        Dictionary<string, object>? RunInternal();
        void SetStepInput(string name, object value);
    }


    internal class Workflow : IStep
    {
        public string Name { set; get; }
        public virtual List<string>? NextStep { set; get; }
        public Dictionary<string, object>? Input { protected set; get; }
        public Dictionary<string, object>? Output { protected set; get; }
        public Func<Dictionary<string, object>, Dictionary<string, object>?>? Run { set; get; }
        public List<string>? RequireInput { set; get; }

        public virtual Dictionary<string, object>? RunInternal()
        {
            Console.WriteLine($"Run Step: {this.Name}, Input: {JsonConvert.SerializeObject(this.Input)}, NextStep: {string.Join(",", this.NextStep ?? new List<string>())}");

            this.Output = this.Run?.Invoke(this.Input);

            return this.Output;
        }

        public void SetStepInput(string name, object value)
        {
            if (this.Input == null)
                this.Input = new Dictionary<string, object>();

            this.Input[name] = value;
        }

        public override int GetHashCode()
        {
            return this.Name.GetHashCode();
        }

        public override bool Equals(object? obj)
        {
            if (obj is Workflow step)
            {
                return step.Name == this.Name;
            }

            return false;
        }
    }

    class ConditionStep : Workflow
    {
        public string Condition { set; get; }
        public override List<string>? NextStep => getNextStep();
        public List<string>? IfStep { set; get; }
        public List<string>? ElseStep { set; get; }

        private List<string>? getNextStep()
        {
            if (this.Input == null || !this.Input.Any())
                return this.ElseStep;

            var match = Eval.Execute<bool>(this.Condition, this.Input);

            if (match)
                return this.IfStep;

            return this.ElseStep;
        }

        public override Dictionary<string, object>? RunInternal()
        {
            Console.WriteLine($"Run Condition Step: {this.Name}, Input: {JsonConvert.SerializeObject(this.Input)}, NextStep: {string.Join(",", this.NextStep)}");

            this.Output = this.Input;

            return this.Output;
        }
    }

    class WorkflowBuilder
    {

        private Func<string, IStep?> _stepFactory;
        private Stack<IStep> _stack = new Stack<IStep>();
        private Dictionary<string, IStep> _completeStep = new Dictionary<string, IStep>();

        public WorkflowBuilder(Func<string, IStep?> factory)
        {
            _stepFactory = factory;
        }


        public async Task<object?> RunAsync(List<string> startSteps, string endStep, Dictionary<string, object> paramerters)
        {
            _stack = new Stack<IStep>();
            startSteps.ForEach(x =>
            {
                var step = _stepFactory.Invoke(x);
                foreach (var item in paramerters)
                {
                    step.SetStepInput(item.Key, item.Value);
                }

                _stack.Push(step);
            });



            while (true)
            {
                if (_stack.Count == 0)
                {
                    await Task.Delay(10);
                    continue;
                }

                var step = _stack.Pop();
                if (step == null)
                    continue;

                var canRunStep = step.RequireInput?.All(x => _completeStep.ContainsKey(x)) ?? true;

                if (canRunStep)
                {
                    var runningStep = Task.Run(() =>
                      {
                          var output = step.RunInternal();
                          var isCompleteStep = true;

                          step.NextStep?.ForEach(x =>
                          {
                              var nextStep = _stepFactory.Invoke(x);

                              if (output != null)
                              {
                                  foreach (var item in output)
                                  {
                                      nextStep.SetStepInput(item.Key, item.Value);
                                  }
                              }

                              if (!_stack.Contains(nextStep))
                                  _stack.Push(nextStep);

                              if (_completeStep.ContainsKey(nextStep.Name))
                              {
                                  _completeStep.Remove(nextStep.Name);
                                  isCompleteStep = false;
                              }
                          });

                          if (isCompleteStep && !_completeStep.ContainsKey(step.Name))
                              _completeStep.Add(step.Name, step);

                          return output;
                      }).ConfigureAwait(false);

                    if (step.Name == endStep)
                    {
                        return await runningStep;
                    }
                }
                else
                {
                    _ = Task.Run(async () =>
                      {
                          await Task.Delay(100);
                          _stack.Push(step);
                      });
                }
            }

            return null;
        }
    }
}
