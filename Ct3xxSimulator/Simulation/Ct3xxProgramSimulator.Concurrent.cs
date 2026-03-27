// Provides Ct3xx Program Simulator Concurrent for the simulator core simulation support.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Ct3xxProgramParser.Model;

namespace Ct3xxSimulator.Simulation;

public partial class Ct3xxProgramSimulator
{
    private const long ConcurrentProcessPollIntervalMs = 50L;

    private void RunConcurrentGroupScheduler(IReadOnlyList<SequenceNode> items)
    {
        var branches = CreateConcurrentBranches(items);
        InitializeConcurrentBranchStates(items);

        _currentConcurrentEvent = "group_sync:start";
        PublishStateSnapshot();
        _currentConcurrentEvent = null;

        while (branches.Any(branch => !branch.IsCompleted))
        {
            CheckCancellation();

            var progressed = false;

            progressed |= ResumeReadyConcurrentWaits(branches);
            progressed |= CompleteFinishedConcurrentProcesses(branches);
            progressed |= AdvanceReadyConcurrentBranches(branches);

            if (progressed)
            {
                continue;
            }

            var nextEventTimeMs = GetNextConcurrentEventTime(branches);
            if (nextEventTimeMs.HasValue && nextEventTimeMs.Value > _simulatedTimeMs)
            {
                AdvanceTime(nextEventTimeMs.Value - _simulatedTimeMs);
                continue;
            }

            if (branches.Any(branch => branch.RunningProcess != null))
            {
                Thread.Sleep((int)ConcurrentProcessPollIntervalMs);
                AdvanceTime(ConcurrentProcessPollIntervalMs);
                continue;
            }

            break;
        }

        _currentConcurrentEvent = "group_sync:completed";
        PublishStateSnapshot();
        _currentConcurrentEvent = null;
        _concurrentBranchStates.Clear();
    }

    private List<ConcurrentBranchExecutionState> CreateConcurrentBranches(IReadOnlyList<SequenceNode> items)
    {
        var result = new List<ConcurrentBranchExecutionState>(items.Count);
        for (var index = 0; index < items.Count; index++)
        {
            var steps = new List<SequenceNode>();
            FlattenConcurrentBranchSteps(items[index], steps);
            result.Add(new ConcurrentBranchExecutionState(
                index,
                GetBranchName(items[index], index),
                DescribeSequenceNode(items[index]),
                steps));
        }

        return result;
    }

    private static void FlattenConcurrentBranchSteps(SequenceNode node, List<SequenceNode> steps)
    {
        switch (node)
        {
            case Group group when !string.Equals(group.ExecMode, "concurrent", StringComparison.OrdinalIgnoreCase):
                foreach (var child in group.Items)
                {
                    FlattenConcurrentBranchSteps(child, steps);
                }

                break;

            default:
                steps.Add(node);
                break;
        }
    }

    private bool ResumeReadyConcurrentWaits(IEnumerable<ConcurrentBranchExecutionState> branches)
    {
        var progressed = false;
        foreach (var branch in branches.Where(branch => branch.PendingWait != null && branch.PendingWait.ResumeAtTimeMs <= _simulatedTimeMs).ToList())
        {
            progressed = true;
            using (BeginConcurrentBranchScope(branch.BranchIndex))
            {
                var branchName = branch.BranchName;
                var pendingWait = branch.PendingWait!;
                branch.PendingWait = null;

                UpdateConcurrentBranchState(branch.BranchIndex, "running", DescribeSequenceNode(pendingWait.Test), null, $"Wait abgeschlossen ({pendingWait.DelayMs} ms)");
                _currentConcurrentEvent = $"branch_resumed:{branchName}";
                PublishStateSnapshot();
                _currentConcurrentEvent = null;

                PublishStepEvaluation(pendingWait.Test, TestOutcome.Pass, details: $"WaitTime={pendingWait.DelayMs} ms");
                _context.MarkOutcome(TestOutcome.Pass);
                _observer.OnTestCompleted(pendingWait.Test, TestOutcome.Pass);
                _executionController.WaitAfterTest(pendingWait.Test, _cancellationToken);

                if (branch.StepIndex >= branch.Steps.Count && branch.RunningProcess == null)
                {
                    MarkConcurrentBranchCompleted(branch);
                }
            }
        }

        return progressed;
    }

    private bool CompleteFinishedConcurrentProcesses(IEnumerable<ConcurrentBranchExecutionState> branches)
    {
        var progressed = false;
        foreach (var branch in branches.Where(branch => branch.RunningProcess != null && branch.RunningProcess.Process.HasExited).ToList())
        {
            progressed = true;
            using (BeginConcurrentBranchScope(branch.BranchIndex))
            {
                CompleteConcurrentTest(branch.RunningProcess!);
                _activeConcurrentTests.Remove(branch.RunningProcess!);
                branch.RunningProcess = null;

                if (branch.StepIndex >= branch.Steps.Count && branch.PendingWait == null)
                {
                    MarkConcurrentBranchCompleted(branch);
                }
                else
                {
                    UpdateConcurrentBranchState(branch.BranchIndex, "running", branch.CurrentItem, null, "Parallelprozess abgeschlossen");
                    PublishStateSnapshot();
                }
            }
        }

        return progressed;
    }

    private bool AdvanceReadyConcurrentBranches(IEnumerable<ConcurrentBranchExecutionState> branches)
    {
        var progressed = false;

        foreach (var branch in branches.Where(branch => branch.CanAdvanceAt(_simulatedTimeMs)).ToList())
        {
            using (BeginConcurrentBranchScope(branch.BranchIndex))
            {
                if (!branch.HasStarted)
                {
                    branch.HasStarted = true;
                    _currentConcurrentEvent = $"branch_started:{branch.BranchName}";
                    UpdateConcurrentBranchState(branch.BranchIndex, "running", branch.CurrentItem, null, "Branch gestartet");
                    PublishStateSnapshot();
                    _currentConcurrentEvent = null;
                }

                if (ExecuteNextConcurrentBranchStep(branch))
                {
                    progressed = true;
                }
            }
        }

        return progressed;
    }

    private bool ExecuteNextConcurrentBranchStep(ConcurrentBranchExecutionState branch)
    {
        if (branch.StepIndex >= branch.Steps.Count)
        {
            MarkConcurrentBranchCompleted(branch);
            return true;
        }

        var node = branch.Steps[branch.StepIndex];
        branch.CurrentItem = DescribeSequenceNode(node);
        UpdateConcurrentBranchState(branch.BranchIndex, "running", branch.CurrentItem);
        PublishStateSnapshot();

        switch (node)
        {
            case Table table:
                _context.ApplyTable(table, _evaluator);
                branch.StepIndex++;
                if (branch.StepIndex >= branch.Steps.Count && branch.PendingWait == null && branch.RunningProcess == null)
                {
                    MarkConcurrentBranchCompleted(branch);
                }
                else
                {
                    UpdateConcurrentBranchState(branch.BranchIndex, "running", branch.GetCurrentItemDescription(), null, "Tabelle angewendet");
                    PublishStateSnapshot();
                }

                return true;

            case Test test when string.Equals(test.Id, "PWT$", StringComparison.OrdinalIgnoreCase):
                StartConcurrentWait(branch, test);
                return true;

            case Test test when TryStartConcurrentTest(test, branch.BranchIndex, out var handle):
                branch.StepIndex++;
                branch.RunningProcess = handle!;
                _activeConcurrentTests.Add(handle!);
                UpdateConcurrentBranchState(branch.BranchIndex, "waiting", DescribeSequenceNode(test), null, "Asynchroner Prozess laeuft");
                PublishStateSnapshot();
                return true;

            case Test test:
                ExecuteTestWithoutStepDuration(test);
                branch.StepIndex++;
                if (branch.StepIndex >= branch.Steps.Count && branch.PendingWait == null && branch.RunningProcess == null)
                {
                    MarkConcurrentBranchCompleted(branch);
                }
                else
                {
                    UpdateConcurrentBranchState(branch.BranchIndex, "running", branch.GetCurrentItemDescription());
                    PublishStateSnapshot();
                }

                return true;

            case Group nested:
                ExecuteGroup(nested);
                branch.StepIndex++;
                if (branch.StepIndex >= branch.Steps.Count && branch.PendingWait == null && branch.RunningProcess == null)
                {
                    MarkConcurrentBranchCompleted(branch);
                }
                else
                {
                    UpdateConcurrentBranchState(branch.BranchIndex, "running", branch.GetCurrentItemDescription());
                    PublishStateSnapshot();
                }

                return true;

            default:
                branch.StepIndex++;
                return true;
        }
    }

    private void StartConcurrentWait(ConcurrentBranchExecutionState branch, Test test)
    {
        var delayMs = ParseDurationMilliseconds(GetParameterAttribute(test.Parameters, "WaitTime"));

        _currentCurvePoints = new List<MeasurementCurvePoint>();
        _executionController.WaitBeforeTest(test, _cancellationToken);
        _observer.OnTestStarted(test);
        _currentStepName = test.Parameters?.Name ?? test.Name ?? test.Id ?? "Test";
        PublishStateSnapshot();

        branch.StepIndex++;
        branch.PendingWait = new ConcurrentPendingWait(test, delayMs, _simulatedTimeMs + delayMs);

        UpdateConcurrentBranchState(branch.BranchIndex, "waiting", DescribeSequenceNode(test), branch.PendingWait.ResumeAtTimeMs, $"WaitTime={delayMs} ms");
        _currentConcurrentEvent = $"branch_waiting:{branch.BranchName}";
        PublishStateSnapshot();
        _currentConcurrentEvent = null;
    }

    private long? GetNextConcurrentEventTime(IEnumerable<ConcurrentBranchExecutionState> branches)
    {
        long? nextEventTimeMs = null;

        foreach (var branch in branches)
        {
            if (branch.PendingWait != null)
            {
                nextEventTimeMs = nextEventTimeMs.HasValue
                    ? Math.Min(nextEventTimeMs.Value, branch.PendingWait.ResumeAtTimeMs)
                    : branch.PendingWait.ResumeAtTimeMs;
            }
        }

        if (branches.Any(branch => branch.RunningProcess != null))
        {
            var processPollTime = _simulatedTimeMs + ConcurrentProcessPollIntervalMs;
            nextEventTimeMs = nextEventTimeMs.HasValue
                ? Math.Min(nextEventTimeMs.Value, processPollTime)
                : processPollTime;
        }

        return nextEventTimeMs;
    }

    private void MarkConcurrentBranchCompleted(ConcurrentBranchExecutionState branch)
    {
        if (branch.IsCompleted)
        {
            return;
        }

        branch.IsCompleted = true;
        CompleteConcurrentBranchState(branch.BranchIndex);
        _currentConcurrentEvent = $"branch_completed:{branch.BranchName}";
        PublishStateSnapshot();
        _currentConcurrentEvent = null;
    }

    private sealed class ConcurrentBranchExecutionState
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentBranchExecutionState"/> class.
        /// </summary>
        public ConcurrentBranchExecutionState(int branchIndex, string branchName, string initialDescription, IReadOnlyList<SequenceNode> steps)
        {
            BranchIndex = branchIndex;
            BranchName = branchName;
            CurrentItem = initialDescription;
            Steps = steps;
        }

        /// <summary>
        /// Gets the branch index.
        /// </summary>
        public int BranchIndex { get; }
        /// <summary>
        /// Gets the branch name.
        /// </summary>
        public string BranchName { get; }
        /// <summary>
        /// Gets the steps.
        /// </summary>
        public IReadOnlyList<SequenceNode> Steps { get; }
        /// <summary>
        /// Gets the step index.
        /// </summary>
        public int StepIndex { get; set; }
        /// <summary>
        /// Gets the current item.
        /// </summary>
        public string? CurrentItem { get; set; }
        /// <summary>
        /// Gets a value indicating whether the started condition is met.
        /// </summary>
        public bool HasStarted { get; set; }
        /// <summary>
        /// Gets a value indicating whether the completed condition is met.
        /// </summary>
        public bool IsCompleted { get; set; }
        /// <summary>
        /// Gets the pending wait.
        /// </summary>
        public ConcurrentPendingWait? PendingWait { get; set; }
        /// <summary>
        /// Gets the running process.
        /// </summary>
        public ConcurrentTestHandle? RunningProcess { get; set; }

        /// <summary>
        /// Determines whether the advance at condition is met.
        /// </summary>
        public bool CanAdvanceAt(long simulatedTimeMs)
        {
            return !IsCompleted &&
                   PendingWait == null &&
                   RunningProcess == null &&
                   StepIndex < Steps.Count;
        }

        /// <summary>
        /// Gets the current item description.
        /// </summary>
        public string? GetCurrentItemDescription()
        {
            if (StepIndex < Steps.Count)
            {
                return DescribeSequenceNode(Steps[StepIndex]);
            }

            return CurrentItem;
        }
    }

    private sealed class ConcurrentPendingWait
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentPendingWait"/> class.
        /// </summary>
        public ConcurrentPendingWait(Test test, long delayMs, long resumeAtTimeMs)
        {
            Test = test;
            DelayMs = delayMs;
            ResumeAtTimeMs = resumeAtTimeMs;
        }

        /// <summary>
        /// Gets the test.
        /// </summary>
        public Test Test { get; }
        /// <summary>
        /// Gets the delay ms.
        /// </summary>
        public long DelayMs { get; }
        /// <summary>
        /// Gets the resume at time ms.
        /// </summary>
        public long ResumeAtTimeMs { get; }
    }
}
