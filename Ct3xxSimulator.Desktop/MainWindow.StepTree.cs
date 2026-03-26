using System;
using System.Collections.Generic;
using System.Linq;
using Ct3xxProgramParser.Model;
using Ct3xxSimulator.Desktop.ViewModels;

namespace Ct3xxSimulator.Desktop;

public partial class MainWindow
{
    private void BuildStepTree(Ct3xxProgram program)
    {
        StepTreeRootNodes.Clear();
        _stepTreeNodes.Clear();
        _groupTreeNodes.Clear();
        SelectedStepTreeNode = null;

        foreach (var item in program.RootItems)
        {
            AddSequenceNodeToTree(item, null, StepTreeRootNodes);
        }

        if (program.DutLoop != null)
        {
            var loopNode = new StepTreeNodeViewModel(program.DutLoop.Name ?? "DUT Loop", isGroup: true);
            loopNode.KeepExpanded = true;
            loopNode.IsExpanded = true;
            loopNode.GroupMode = "Test Loop";
            if (!string.IsNullOrWhiteSpace(program.DutLoop.LoopCount))
            {
                loopNode.GroupHint = $"Wiederholt den enthaltenen Ablauf fuer die konfigurierte DUT-Anzahl. LoopCnt={program.DutLoop.LoopCount}.";
            }
            else
            {
                loopNode.GroupHint = "Wiederholt den enthaltenen Ablauf fuer die konfigurierte DUT-Anzahl.";
            }
            foreach (var item in program.DutLoop.Items)
            {
                AddSequenceNodeToTree(item, loopNode, loopNode.Children);
            }

            StepTreeRootNodes.Add(loopNode);
        }
    }

    private void RebuildFlatStepTreeFromResults()
    {
        StepTreeRootNodes.Clear();
        _stepTreeNodes.Clear();
        _groupTreeNodes.Clear();
        SelectedStepTreeNode = null;

        foreach (var step in StepResults)
        {
            var node = new StepTreeNodeViewModel(step.StepName, isGroup: false);
            node.ApplyResult(step);
            node.Refresh();
            StepTreeRootNodes.Add(node);
        }
    }

    private void RebuildStepTreeForTimelineIndex(int timelineIndex)
    {
        if (!_isLoadedSnapshotSession && _program != null)
        {
            BuildStepTree(_program);
            foreach (var entry in _stepEvaluationHistory.Where(item => IsResultVisibleAtTimeline(item.Result, timelineIndex)))
            {
                if (entry.Test != null)
                {
                    ApplyEvaluationToStepTree(entry.Test, entry.Result);
                }
            }

            return;
        }

        RebuildFlatStepTreeFromResults(timelineIndex);
    }

    private int? GetLatestTimelineIndexForSelectedNode()
    {
        return GetLatestTimelineIndexForNode(SelectedStepTreeNode);
    }

    private static int? GetLatestTimelineIndexForNode(StepTreeNodeViewModel? node)
    {
        if (node == null)
        {
            return null;
        }

        int? latest = node.Result?.TimelineIndex;
        foreach (var child in node.Children)
        {
            var childIndex = GetLatestTimelineIndexForNode(child);
            if (childIndex.HasValue && (!latest.HasValue || childIndex.Value > latest.Value))
            {
                latest = childIndex.Value;
            }
        }

        return latest;
    }

    private void RebuildFlatStepTreeFromResults(int timelineIndex)
    {
        StepTreeRootNodes.Clear();
        _stepTreeNodes.Clear();
        _groupTreeNodes.Clear();
        SelectedStepTreeNode = null;

        foreach (var step in StepResults.Where(step => IsResultVisibleAtTimeline(step, timelineIndex)))
        {
            var node = new StepTreeNodeViewModel(step.StepName, isGroup: false);
            node.ApplyResult(step);
            node.Refresh();
            StepTreeRootNodes.Add(node);
        }
    }

    private bool IsResultVisibleAtTimeline(StepResultViewModel result, int timelineIndex)
    {
        if (!result.TimelineIndex.HasValue)
        {
            return timelineIndex >= _timeline.Count - 1;
        }

        return result.TimelineIndex.Value <= timelineIndex;
    }

    private void AddSequenceNodeToTree(SequenceNode item, StepTreeNodeViewModel? parent, ICollection<StepTreeNodeViewModel> target)
    {
        switch (item)
        {
            case Group group:
                var groupNode = new StepTreeNodeViewModel(group.Name ?? group.Id ?? "Gruppe", isGroup: true, parent);
                groupNode.IsExpanded = true;
                ConfigureGroupPresentation(groupNode, group);
                _groupTreeNodes[group] = groupNode;
                target.Add(groupNode);
                foreach (var child in group.Items)
                {
                    AddSequenceNodeToTree(child, groupNode, groupNode.Children);
                }

                break;

            case Test test:
                var testNode = new StepTreeNodeViewModel(test.Parameters?.Name ?? test.Name ?? test.Id ?? "Test", isGroup: false, parent)
                {
                    ExpectedEvaluationCount = EstimateEvaluationCount(test)
                };
                _stepTreeNodes[test] = testNode;
                target.Add(testNode);
                foreach (var child in test.Items)
                {
                    AddSequenceNodeToTree(child, testNode, testNode.Children);
                }
                break;
        }
    }

    private static void ConfigureGroupPresentation(StepTreeNodeViewModel groupNode, Group group)
    {
        var isConcurrent = string.Equals(group.ExecMode, "concurrent", StringComparison.OrdinalIgnoreCase);
        var hasLoopCount = !string.IsNullOrWhiteSpace(group.LoopCount);

        if (isConcurrent)
        {
            groupNode.GroupMode = hasLoopCount ? "Concurrent Loop" : "Concurrent";
            groupNode.GroupHint = hasLoopCount
                ? $"Die enthaltenen Schritte laufen parallel. Die Gruppe wird mit LoopCnt={group.LoopCount} wiederholt."
                : "Die enthaltenen Schritte laufen parallel und koennen gleichzeitig aktiv sein.";
            return;
        }

        if (hasLoopCount)
        {
            groupNode.GroupMode = "Loop";
            groupNode.GroupHint = $"Die enthaltenen Schritte werden gemaess LoopCnt={group.LoopCount} mehrfach durchlaufen.";
            return;
        }

        groupNode.GroupMode = "Gruppe";
        if (!string.IsNullOrWhiteSpace(group.ExecMode))
        {
            groupNode.GroupHint = $"ExecMode={group.ExecMode}";
        }
    }

    private static int EstimateEvaluationCount(Test test)
    {
        if (!string.Equals(test.Id, "PET$", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        var count = test.Parameters?.Tables
            .SelectMany(table => table.Records)
            .Count(record => !string.Equals(record.Disabled, "yes", StringComparison.OrdinalIgnoreCase)) ?? 1;

        return Math.Max(1, count);
    }

    private void ApplyEvaluationToStepTree(Test test, StepResultViewModel result)
    {
        if (!_stepTreeNodes.TryGetValue(test, out var testNode))
        {
            var fallbackNode = new StepTreeNodeViewModel(result.StepName, isGroup: false);
            fallbackNode.ApplyResult(result);
            fallbackNode.Refresh();
            StepTreeRootNodes.Add(fallbackNode);
            return;
        }

        if (testNode.ExpectedEvaluationCount <= 1)
        {
            testNode.ApplyResult(result);
            RefreshStepTreeBranch(testNode);
            return;
        }

        testNode.IsExpanded = true;
        var childNode = new StepTreeNodeViewModel(result.StepName, isGroup: false, testNode);
        childNode.ApplyResult(result);
        childNode.Refresh();
        testNode.Children.Add(childNode);
        testNode.ActualEvaluationCount++;
        RefreshStepTreeBranch(testNode);
    }

    private void RefreshStepTreeBranch(StepTreeNodeViewModel? node)
    {
        while (node != null)
        {
            node.Refresh();
            node = node.Parent;
        }
    }

    private void SetGroupExpanded(Group group, bool isExpanded)
    {
        if (!_groupTreeNodes.TryGetValue(group, out var node))
        {
            return;
        }

        if (node.KeepExpanded)
        {
            node.IsExpanded = true;
            return;
        }

        node.IsExpanded = isExpanded;
    }

    private void OnJumpToSelectedStepSnapshot(object sender, System.Windows.RoutedEventArgs e)
    {
        var index = GetLatestTimelineIndexForSelectedNode();
        if (!index.HasValue)
        {
            AddLog("Fuer den ausgewaehlten Testschritt ist noch kein Snapshot verfuegbar.");
            return;
        }

        SelectTimelineIndex(index.Value);
        AddLog($"Zu letztem Snapshot von '{SelectedStepTreeNode?.Title ?? "Testschritt"}' gesprungen.");
    }
}
