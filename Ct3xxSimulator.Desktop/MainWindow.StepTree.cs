// Provides Main Window Step Tree for the desktop application support code.
using System;
using System.Collections.Generic;
using System.Linq;
using Ct3xxProgramParser.Model;
using Ct3xxSimulator.Desktop.ViewModels;

namespace Ct3xxSimulator.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// Executes BuildStepTree.
    /// </summary>
    private void BuildStepTree(Ct3xxProgram program)
    {
        StepTreeRootNodes.Clear();
        _stepTreeNodes.Clear();
        _groupTreeNodes.Clear();
        _treeNodeTests.Clear();
        _treeNodeGroups.Clear();
        _breakpointTests.Clear();
        _breakpointGroups.Clear();
        SelectedStepTreeNode = null;

        foreach (var item in program.RootItems)
        {
            AddSequenceNodeToTree(item, null, StepTreeRootNodes, "root");
        }

        if (program.DutLoop != null)
        {
            var loopNode = new StepTreeNodeViewModel(program.DutLoop.Name ?? "DUT Loop", isGroup: true, "dutloop");
            loopNode.KeepExpanded = true;
            loopNode.IsExpanded = true;
            loopNode.GroupMode = "Test Loop";
            loopNode.HasBreakpoint = _breakpointNodeKeys.Contains(loopNode.NodeKey);
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
                AddSequenceNodeToTree(item, loopNode, loopNode.Children, loopNode.NodeKey);
            }

            StepTreeRootNodes.Add(loopNode);
        }
    }

    /// <summary>
    /// Executes RebuildFlatStepTreeFromResults.
    /// </summary>
    private void RebuildFlatStepTreeFromResults()
    {
        StepTreeRootNodes.Clear();
        _stepTreeNodes.Clear();
        _groupTreeNodes.Clear();
        _treeNodeTests.Clear();
        _treeNodeGroups.Clear();
        SelectedStepTreeNode = null;

        foreach (var step in StepResults)
        {
            var node = new StepTreeNodeViewModel(step.StepName, isGroup: false, $"result:{StepTreeRootNodes.Count}");
            node.ApplyResult(step);
            node.Refresh();
            StepTreeRootNodes.Add(node);
        }
    }

    /// <summary>
    /// Executes RebuildStepTreeForTimelineIndex.
    /// </summary>
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

    /// <summary>
    /// Executes SelectBestStepNodeForTimelineIndex.
    /// </summary>
    private void SelectBestStepNodeForTimelineIndex(int timelineIndex, string? currentStep)
    {
        var candidates = EnumerateVisibleNodes(StepTreeRootNodes)
            .Where(node => node.Result != null)
            .Select(node => new
            {
                Node = node,
                LatestIndex = GetLatestTimelineIndexForNode(node)
            })
            .Where(item => item.LatestIndex.HasValue && item.LatestIndex.Value <= timelineIndex)
            .ToList();

        if (candidates.Count == 0)
        {
            SelectedStepTreeNode = null;
            return;
        }

        var exactNameMatch = candidates
            .Where(item => !string.IsNullOrWhiteSpace(currentStep) &&
                           string.Equals(item.Node.Title, currentStep, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.LatestIndex!.Value)
            .FirstOrDefault();
        if (exactNameMatch != null)
        {
            SelectedStepTreeNode = exactNameMatch.Node;
            return;
        }

        var latest = candidates
            .OrderByDescending(item => item.LatestIndex!.Value)
            .First();
        SelectedStepTreeNode = latest.Node;
    }

    /// <summary>
    /// Executes EnumerateVisibleNodes.
    /// </summary>
    private static IEnumerable<StepTreeNodeViewModel> EnumerateVisibleNodes(IEnumerable<StepTreeNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;

            foreach (var child in EnumerateVisibleNodes(node.Children))
            {
                yield return child;
            }
        }
    }

    /// <summary>
    /// Executes GetLatestTimelineIndexForSelectedNode.
    /// </summary>
    private int? GetLatestTimelineIndexForSelectedNode()
    {
        return GetLatestTimelineIndexForNode(SelectedStepTreeNode);
    }

    /// <summary>
    /// Executes GetLatestTimelineIndexForNode.
    /// </summary>
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

    /// <summary>
    /// Executes RebuildFlatStepTreeFromResults.
    /// </summary>
    private void RebuildFlatStepTreeFromResults(int timelineIndex)
    {
        StepTreeRootNodes.Clear();
        _stepTreeNodes.Clear();
        _groupTreeNodes.Clear();
        _treeNodeTests.Clear();
        _treeNodeGroups.Clear();
        SelectedStepTreeNode = null;

        foreach (var step in StepResults.Where(step => IsResultVisibleAtTimeline(step, timelineIndex)))
        {
            var node = new StepTreeNodeViewModel(step.StepName, isGroup: false, $"result:{StepTreeRootNodes.Count}");
            node.ApplyResult(step);
            node.Refresh();
            StepTreeRootNodes.Add(node);
        }
    }

    /// <summary>
    /// Executes IsResultVisibleAtTimeline.
    /// </summary>
    private bool IsResultVisibleAtTimeline(StepResultViewModel result, int timelineIndex)
    {
        if (!result.TimelineIndex.HasValue)
        {
            return timelineIndex >= _timeline.Count - 1;
        }

        return result.TimelineIndex.Value <= timelineIndex;
    }

    /// <summary>
    /// Executes AddSequenceNodeToTree.
    /// </summary>
    private void AddSequenceNodeToTree(SequenceNode item, StepTreeNodeViewModel? parent, ICollection<StepTreeNodeViewModel> target, string parentKey)
    {
        var nodeIndex = target.Count;
        switch (item)
        {
            case Group group:
                var groupKey = $"{parentKey}/group:{nodeIndex}:{group.Name ?? group.Id ?? "group"}";
                var groupNode = new StepTreeNodeViewModel(group.Name ?? group.Id ?? "Gruppe", isGroup: true, groupKey, parent);
                groupNode.IsExpanded = true;
                groupNode.HasBreakpoint = _breakpointNodeKeys.Contains(groupKey);
                ConfigureGroupPresentation(groupNode, group);
                _groupTreeNodes[group] = groupNode;
                _treeNodeGroups[groupNode] = group;
                if (groupNode.HasBreakpoint)
                {
                    _breakpointGroups.Add(group);
                }
                target.Add(groupNode);
                foreach (var child in group.Items)
                {
                    AddSequenceNodeToTree(child, groupNode, groupNode.Children, groupKey);
                }

                break;

            case Test test:
                var testKey = $"{parentKey}/test:{nodeIndex}:{test.Parameters?.Name ?? test.Name ?? test.Id ?? "test"}";
                var testNode = new StepTreeNodeViewModel(test.Parameters?.Name ?? test.Name ?? test.Id ?? "Test", isGroup: false, testKey, parent)
                {
                    ExpectedEvaluationCount = EstimateEvaluationCount(test)
                };
                testNode.HasBreakpoint = _breakpointNodeKeys.Contains(testKey);
                _stepTreeNodes[test] = testNode;
                _treeNodeTests[testNode] = test;
                if (testNode.HasBreakpoint)
                {
                    _breakpointTests.Add(test);
                }
                target.Add(testNode);
                foreach (var child in test.Items)
                {
                    AddSequenceNodeToTree(child, testNode, testNode.Children, testKey);
                }
                break;
        }
    }

    /// <summary>
    /// Executes ConfigureGroupPresentation.
    /// </summary>
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

    /// <summary>
    /// Executes EstimateEvaluationCount.
    /// </summary>
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

    /// <summary>
    /// Executes ApplyEvaluationToStepTree.
    /// </summary>
    private void ApplyEvaluationToStepTree(Test test, StepResultViewModel result)
    {
        if (!_stepTreeNodes.TryGetValue(test, out var testNode))
        {
            var fallbackNode = new StepTreeNodeViewModel(result.StepName, isGroup: false, $"fallback:{StepTreeRootNodes.Count}");
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
        var childNode = new StepTreeNodeViewModel(result.StepName, isGroup: false, $"{testNode.NodeKey}/result:{testNode.Children.Count}", testNode);
        childNode.ApplyResult(result);
        childNode.Refresh();
        testNode.Children.Add(childNode);
        testNode.ActualEvaluationCount++;
        RefreshStepTreeBranch(testNode);
    }

    /// <summary>
    /// Executes RefreshStepTreeBranch.
    /// </summary>
    private void RefreshStepTreeBranch(StepTreeNodeViewModel? node)
    {
        while (node != null)
        {
            node.Refresh();
            node = node.Parent;
        }
    }

    /// <summary>
    /// Executes SetGroupExpanded.
    /// </summary>
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

    /// <summary>
    /// Executes OnJumpToSelectedStepSnapshot.
    /// </summary>
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

    /// <summary>
    /// Executes TryGetSelectedNodeTest.
    /// </summary>
    private bool TryGetSelectedNodeTest(out Test test)
    {
        if (SelectedStepTreeNode != null && _treeNodeTests.TryGetValue(SelectedStepTreeNode, out var selectedTest))
        {
            test = selectedTest;
            return true;
        }

        test = null!;
        return false;
    }

    /// <summary>
    /// Executes TryGetSelectedNodeGroup.
    /// </summary>
    private bool TryGetSelectedNodeGroup(out Group group)
    {
        if (SelectedStepTreeNode != null && _treeNodeGroups.TryGetValue(SelectedStepTreeNode, out var selectedGroup))
        {
            group = selectedGroup;
            return true;
        }

        group = null!;
        return false;
    }

    /// <summary>
    /// Executes ToggleBreakpointForSelectedNode.
    /// </summary>
    private void ToggleBreakpointForSelectedNode()
    {
        if (SelectedStepTreeNode == null)
        {
            return;
        }

        if (TryGetSelectedNodeTest(out var test))
        {
            if (_breakpointTests.Contains(test))
            {
                _breakpointTests.Remove(test);
                _breakpointNodeKeys.Remove(SelectedStepTreeNode.NodeKey);
                SelectedStepTreeNode.HasBreakpoint = false;
                AddLog($"Breakpoint entfernt: {SelectedStepTreeNode.Title}");
            }
            else
            {
                _breakpointTests.Add(test);
                _breakpointNodeKeys.Add(SelectedStepTreeNode.NodeKey);
                SelectedStepTreeNode.HasBreakpoint = true;
                AddLog($"Breakpoint gesetzt: {SelectedStepTreeNode.Title}");
            }
        }
        else if (TryGetSelectedNodeGroup(out var group))
        {
            if (_breakpointGroups.Contains(group))
            {
                _breakpointGroups.Remove(group);
                _breakpointNodeKeys.Remove(SelectedStepTreeNode.NodeKey);
                SelectedStepTreeNode.HasBreakpoint = false;
                AddLog($"Gruppen-Breakpoint entfernt: {SelectedStepTreeNode.Title}");
            }
            else
            {
                _breakpointGroups.Add(group);
                _breakpointNodeKeys.Add(SelectedStepTreeNode.NodeKey);
                SelectedStepTreeNode.HasBreakpoint = true;
                AddLog($"Gruppen-Breakpoint gesetzt: {SelectedStepTreeNode.Title}");
            }
        }

        OnPropertyChanged(nameof(CanToggleBreakpoint));
        OnPropertyChanged(nameof(BreakpointButtonText));
    }
}
