// Provides Connection Graph Window for the desktop application window logic.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Desktop.Views;

public partial class ConnectionGraphWindow : Window
{
    private sealed class DisplayBlock
    {
        /// <summary>
        /// Gets the group.
        /// </summary>
        public required NodeGroup Group { get; init; }
        /// <summary>
        /// Gets the title.
        /// </summary>
        public required string Title { get; init; }
        /// <summary>
        /// Gets the detail.
        /// </summary>
        public string? Detail { get; init; }
        /// <summary>
        /// Gets the module root.
        /// </summary>
        public string? ModuleRoot { get; init; }
    }

    private enum NodeGroup
    {
        Device,
        Wiring,
        TestSystem
    }

    private readonly IReadOnlyList<StepConnectionTrace> _traces;
    private readonly IReadOnlyList<MeasurementCurvePoint> _curvePoints;
    private readonly bool? _forcedSourceIsTestSystem;
    private Point? _dragStart;
    private Point _dragOrigin;
    private StepConnectionTrace? _selectedTrace;
    private StepConnectionTrace? _selectedRawTrace;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionGraphWindow"/> class.
    /// </summary>
    public ConnectionGraphWindow(
        string stepName,
        IReadOnlyList<StepConnectionTrace> traces,
        IReadOnlyList<MeasurementCurvePoint>? curvePoints = null,
        bool? forcedSourceIsTestSystem = null)
    {
        _traces = OrderTraces(traces);
        _curvePoints = curvePoints ?? Array.Empty<MeasurementCurvePoint>();
        _forcedSourceIsTestSystem = forcedSourceIsTestSystem;
        InitializeComponent();
        TitleTextBlock.Text = stepName;
        TraceListBox.ItemsSource = _traces;
        if (_traces.Count > 0)
        {
            TraceListBox.SelectedIndex = 0;
        }
        else
        {
            RenderSelectedTrace(null);
        }

        RenderCurve();
        UpdateStatus();
    }

    private void OnTraceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RenderSelectedTrace(TraceListBox.SelectedItem as StepConnectionTrace);
    }

    private void RenderSelectedTrace(StepConnectionTrace? trace)
    {
        var normalizedRawTrace = NormalizeTraceDirection(trace);
        var displayTrace = normalizedRawTrace;
        _selectedRawTrace = normalizedRawTrace;
        _selectedTrace = displayTrace;
        GraphScaleTransform.ScaleX = 1d;
        GraphScaleTransform.ScaleY = 1d;
        GraphTranslateTransform.X = 0d;
        GraphTranslateTransform.Y = 0d;
        GraphCanvas.Children.Clear();

        if (displayTrace == null || displayTrace.Nodes.Count == 0)
        {
            SelectedTraceTitleTextBlock.Text = "Keine Pfadinformation";
            SelectedTraceSummaryTextBlock.Text = "Fuer diesen Schritt liegt kein leitender Verbindungspfad vor.";
            TraceNodeSummaryItemsControl.ItemsSource = Array.Empty<string>();
            GraphCanvas.Width = 800;
            GraphCanvas.Height = 400;
            GraphCanvas.Children.Add(new TextBlock
            {
                Text = "Keine Verbindung fuer diesen Testschritt verfuegbar.",
                FontSize = 18,
                Foreground = Brush("#FF5E564A")
            });
            UpdateStatus();
            return;
        }

        SelectedTraceTitleTextBlock.Text = displayTrace.Title;
        SelectedTraceSummaryTextBlock.Text = $"Pfadlaenge: {displayTrace.Nodes.Count.ToString(CultureInfo.InvariantCulture)} Stationen";
        TraceNodeSummaryItemsControl.ItemsSource = displayTrace.Nodes.Select((node, index) => $"{index + 1}. {node}").ToList();
        var sourceIsTestSystem = ResolveSourceIsTestSystem(displayTrace);
        var displayBlocks = BuildDisplayBlocks(displayTrace, sourceIsTestSystem);

        const double leftMargin = 70;
        const double rightMargin = 70;
        const double topMargin = 40;
        const double laneHeaderHeight = 30;
        const double nodeWidth = 210;
        const double nodeHeight = 124;
        const double rowGap = 36;

        var laneNames = sourceIsTestSystem
            ? new[] { "Pruefsystem", "Verdrahtung / Baugruppe", "Geraet / Signal" }
            : new[] { "Geraet / Signal", "Verdrahtung / Baugruppe", "Pruefsystem" };
        var laneWidth = 310d;
        GraphCanvas.Width = leftMargin + rightMargin + laneNames.Length * laneWidth;
        var wiringBlockCount = Math.Max(1, displayBlocks.Count(block => block.Group == NodeGroup.Wiring));
        GraphCanvas.Height = topMargin + laneHeaderHeight + wiringBlockCount * (nodeHeight + rowGap) + 100;

        for (var laneIndex = 0; laneIndex < laneNames.Length; laneIndex++)
        {
            var laneLeft = leftMargin + laneIndex * laneWidth;
            var laneBorder = new Border
            {
                Width = laneWidth - 18,
                Height = GraphCanvas.Height - topMargin,
                Background = laneIndex switch
                {
                    0 => Brush("#FFF0F6FB"),
                    1 => Brush("#FFFBF4E8"),
                    _ => Brush("#FFF3F8EB")
                },
                BorderBrush = Brush("#FFE2D7C3"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14)
            };
            Canvas.SetLeft(laneBorder, laneLeft);
            Canvas.SetTop(laneBorder, topMargin);
            GraphCanvas.Children.Add(laneBorder);

            var laneTitle = new TextBlock
            {
                Text = laneNames[laneIndex],
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush("#FF4F4538")
            };
            Canvas.SetLeft(laneTitle, laneLeft + 14);
            Canvas.SetTop(laneTitle, topMargin + 12);
            GraphCanvas.Children.Add(laneTitle);
        }

        RenderDisplayBlocks(displayBlocks, sourceIsTestSystem, leftMargin, topMargin, laneHeaderHeight, laneWidth, nodeWidth, nodeHeight, rowGap);

        UpdateStatus();
    }

    private void RenderDisplayBlocks(
        IReadOnlyList<DisplayBlock> displayBlocks,
        bool sourceIsTestSystem,
        double leftMargin,
        double topMargin,
        double laneHeaderHeight,
        double laneWidth,
        double nodeWidth,
        double nodeHeight,
        double rowGap)
    {
        if (displayBlocks.Count == 0)
        {
            return;
        }

        var sourceBlock = displayBlocks.FirstOrDefault(block => block.Group == (sourceIsTestSystem ? NodeGroup.TestSystem : NodeGroup.Device));
        var sinkBlock = displayBlocks.LastOrDefault(block => block.Group == (sourceIsTestSystem ? NodeGroup.Device : NodeGroup.TestSystem));
        var wiringBlocks = displayBlocks.Where(block => block.Group == NodeGroup.Wiring).ToList();
        if (wiringBlocks.Count == 0)
        {
            wiringBlocks.Add(new DisplayBlock
            {
                Group = NodeGroup.Wiring,
                Title = "Direktverdrahtung",
                Detail = "-"
            });
        }

        var centers = new List<Point>();
        var renderOrder = new List<DisplayBlock>();
        var centerRow = (wiringBlocks.Count - 1) / 2d;

        if (sourceBlock != null)
        {
            renderOrder.Add(sourceBlock);
            centers.Add(DrawDisplayBlock(
                sourceBlock,
                1,
                GetLaneIndex(sourceBlock.Group, sourceIsTestSystem),
                0,
                leftMargin,
                topMargin,
                laneHeaderHeight,
                laneWidth,
                nodeWidth,
                nodeHeight,
                rowGap));
        }

        for (var index = 0; index < wiringBlocks.Count; index++)
        {
            renderOrder.Add(wiringBlocks[index]);
            centers.Add(DrawDisplayBlock(
                wiringBlocks[index],
                renderOrder.Count,
                GetLaneIndex(NodeGroup.Wiring, sourceIsTestSystem),
                index,
                leftMargin,
                topMargin,
                laneHeaderHeight,
                laneWidth,
                nodeWidth,
                nodeHeight,
                rowGap));
        }

        if (sinkBlock != null)
        {
            renderOrder.Add(sinkBlock);
            centers.Add(DrawDisplayBlock(
                sinkBlock,
                renderOrder.Count,
                GetLaneIndex(sinkBlock.Group, sourceIsTestSystem),
                Math.Max(0, wiringBlocks.Count - 1),
                leftMargin,
                topMargin,
                laneHeaderHeight,
                laneWidth,
                nodeWidth,
                nodeHeight,
                rowGap));
        }

        for (var index = 1; index < centers.Count; index++)
        {
            DrawConnector(centers[index - 1], centers[index]);
        }
    }

    private StepConnectionTrace? NormalizeTraceDirection(StepConnectionTrace? trace)
    {
        if (trace == null || trace.Nodes.Count <= 1)
        {
            return trace;
        }

        var normalizedNodes = trace.Nodes.ToList();
        var sourceIsTestSystem = ResolveSourceIsTestSystem(trace);
        var firstLane = GetLaneIndex(normalizedNodes[0], sourceIsTestSystem);
        var lastLane = GetLaneIndex(normalizedNodes[^1], sourceIsTestSystem);
        if (firstLane > lastLane)
        {
            normalizedNodes.Reverse();
            return new StepConnectionTrace(trace.Title, normalizedNodes);
        }

        return trace;
    }

    private static IReadOnlyList<StepConnectionTrace> OrderTraces(IReadOnlyList<StepConnectionTrace>? traces)
    {
        if (traces == null || traces.Count == 0)
        {
            return Array.Empty<StepConnectionTrace>();
        }

        return traces
            .OrderBy(GetTracePriority)
            .ThenBy(trace => trace.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int GetTracePriority(StepConnectionTrace trace)
    {
        var title = trace.Title ?? string.Empty;
        if (title.StartsWith("Ansteuerung", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Stimulus", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Pruefsystem", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (title.StartsWith("Waveform Response", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (title.StartsWith("Messpfad", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 3;
    }

    private Point DrawDisplayBlock(
        DisplayBlock block,
        int stepNumber,
        int laneIndex,
        double rowIndex,
        double leftMargin,
        double topMargin,
        double laneHeaderHeight,
        double laneWidth,
        double width,
        double height,
        double rowGap)
    {
        var laneLeft = leftMargin + laneIndex * laneWidth;
        var left = laneLeft + (laneWidth - width) / 2d - 10;
        var top = topMargin + laneHeaderHeight + 24 + rowIndex * (height + rowGap);
        var isClickableModule = CanOpenModule(block.ModuleRoot);
        var border = new Border
        {
            Width = width,
            Height = height,
            Background = block.Group switch
            {
                NodeGroup.Device => Brush("#FFD7EAF8"),
                NodeGroup.Wiring => Brush("#FFF6DFA4"),
                _ => Brush("#FFDDE9C7")
            },
            BorderBrush = Brush("#FF8B765A"),
            BorderThickness = new Thickness(1.2),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12),
            Cursor = isClickableModule ? Cursors.Hand : Cursors.Arrow,
            Tag = block.ModuleRoot
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = stepNumber.ToString(CultureInfo.InvariantCulture),
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#FF5C4F3D")
        });
        stack.Children.Add(new TextBlock
        {
            Text = block.Title,
            Margin = new Thickness(0, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#FF2F2A25")
        });
        if (!string.IsNullOrWhiteSpace(block.Detail))
        {
            stack.Children.Add(new TextBlock
            {
                Text = block.Detail,
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12.5,
                Foreground = Brush("#FF4F4538")
            });
        }

        border.Child = stack;
        if (isClickableModule)
        {
            border.MouseLeftButtonUp += OnNodeClicked;
        }
        Canvas.SetLeft(border, left);
        Canvas.SetTop(border, top);
        GraphCanvas.Children.Add(border);
        return new Point(left + width / 2d, top + height / 2d);
    }

    private void OnNodeClicked(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is not Border { Tag: string moduleRoot } || !CanOpenModule(moduleRoot))
        {
            return;
        }

        var subTrace = BuildSubmoduleTrace(moduleRoot);
        if (subTrace == null)
        {
            return;
        }

        var window = new ConnectionGraphWindow(
            $"{TitleTextBlock.Text} - {moduleRoot}",
            new[] { subTrace },
            _curvePoints,
            ResolveSourceIsTestSystem(_selectedRawTrace))
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private void DrawConnector(Point from, Point to)
    {
        var pathFigure = new PathFigure { StartPoint = from };
        var midY = (from.Y + to.Y) / 2d;
        pathFigure.Segments.Add(new BezierSegment(
            new Point(from.X, midY),
            new Point(to.X, midY),
            to,
            true));

        var path = new Path
        {
            Stroke = Brush("#FF6A5A43"),
            StrokeThickness = 2.4,
            Data = new PathGeometry(new[] { pathFigure })
        };
        GraphCanvas.Children.Add(path);

        var angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
        var arrowSize = 8d;
        var arrow = new Polygon
        {
            Fill = Brush("#FF6A5A43"),
            Points = new PointCollection
            {
                to,
                new Point(to.X - arrowSize * Math.Cos(angle - Math.PI / 6d), to.Y - arrowSize * Math.Sin(angle - Math.PI / 6d)),
                new Point(to.X - arrowSize * Math.Cos(angle + Math.PI / 6d), to.Y - arrowSize * Math.Sin(angle + Math.PI / 6d))
            }
        };
        GraphCanvas.Children.Add(arrow);
    }

    private static int GetLaneIndex(string node, bool sourceIsTestSystem)
    {
        return GetLaneIndex(ClassifyNodeGroup(node), sourceIsTestSystem);
    }

    private static int GetLaneIndex(NodeGroup group, bool sourceIsTestSystem)
    {
        return (group, sourceIsTestSystem) switch
        {
            (NodeGroup.TestSystem, true) => 0,
            (NodeGroup.Wiring, true) => 1,
            (NodeGroup.Device, true) => 2,
            (NodeGroup.Device, false) => 0,
            (NodeGroup.Wiring, false) => 1,
            (NodeGroup.TestSystem, false) => 2,
            _ => 1
        };
    }

    private static NodeGroup ClassifyNodeGroup(string node)
    {
        if (string.IsNullOrWhiteSpace(node))
        {
            return NodeGroup.Wiring;
        }

        var text = node.Trim();
        if (IsTestSystemLabel(text))
        {
            return NodeGroup.TestSystem;
        }

        if (IsDeviceEndpointLabel(text))
        {
            return NodeGroup.Device;
        }

        return NodeGroup.Wiring;
    }

    private static bool IsTestSystemLabel(string text)
    {
        foreach (var line in SplitLabelLines(text))
        {
            if (line.StartsWith("CT3.", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("UIF.", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Signal ", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("UIF_OUT", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("UIF_IN", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDeviceEndpointLabel(string text)
    {
        foreach (var line in SplitLabelLines(text))
        {
            if (line.StartsWith("DevicePort.", StringComparison.OrdinalIgnoreCase) ||
                line.Contains(".DevicePort.", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Device.", StringComparison.OrdinalIgnoreCase) ||
                line.Contains(".Device.", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> SplitLabelLines(string text) =>
        text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim());

    private static IReadOnlyList<DisplayBlock> BuildDisplayBlocks(StepConnectionTrace trace, bool sourceIsTestSystem)
    {
        if (trace.Nodes.Count == 0)
        {
            return Array.Empty<DisplayBlock>();
        }

        var result = new List<DisplayBlock>();
        var normalizedNodes = trace.Nodes.ToList();
        var sourceGroup = sourceIsTestSystem ? NodeGroup.TestSystem : NodeGroup.Device;
        var sinkGroup = sourceIsTestSystem ? NodeGroup.Device : NodeGroup.TestSystem;

        var sourceNode = normalizedNodes.FirstOrDefault(node => ClassifyNodeGroup(node) == sourceGroup) ?? normalizedNodes[0];
        result.Add(new DisplayBlock
        {
            Group = sourceGroup,
            Title = ExtractPrimaryPinLabel(sourceNode),
            Detail = ExtractEndpointDetail(sourceNode)
        });

        var firstSinkIndex = normalizedNodes.FindLastIndex(node => ClassifyNodeGroup(node) == sinkGroup);
        if (firstSinkIndex < 0)
        {
            firstSinkIndex = normalizedNodes.Count - 1;
        }

        var middleNodes = normalizedNodes
            .Skip(1)
            .Take(Math.Max(0, firstSinkIndex - 1))
            .Where(node => ClassifyNodeGroup(node) == NodeGroup.Wiring)
            .ToList();

        result.AddRange(BuildWiringBlocks(middleNodes));

        var sinkNode = normalizedNodes[firstSinkIndex];
        result.Add(new DisplayBlock
        {
            Group = sinkGroup,
            Title = ExtractPrimaryPinLabel(sinkNode),
            Detail = ExtractEndpointDetail(sinkNode)
        });

        return result;
    }

    private static IReadOnlyList<DisplayBlock> BuildWiringBlocks(IReadOnlyList<string> middleNodes)
    {
        if (middleNodes.Count == 0)
        {
            return Array.Empty<DisplayBlock>();
        }

        var blocks = new List<DisplayBlock>();
        var currentNodes = new List<string>();
        string? currentRoot = null;

        foreach (var node in middleNodes)
        {
            var root = GetModuleRootFromLabel(node);
            if (currentNodes.Count == 0)
            {
                currentNodes.Add(node);
                currentRoot = root;
                continue;
            }

            if (string.Equals(currentRoot, root, StringComparison.OrdinalIgnoreCase))
            {
                currentNodes.Add(node);
                continue;
            }

            blocks.Add(CreateWiringBlock(currentNodes, currentRoot));
            currentNodes = new List<string> { node };
            currentRoot = root;
        }

        if (currentNodes.Count > 0)
        {
            blocks.Add(CreateWiringBlock(currentNodes, currentRoot));
        }

        var filtered = blocks
            .Where(block => !string.IsNullOrWhiteSpace(block.ModuleRoot))
            .ToList();

        if (filtered.Count > 0)
        {
            return filtered;
        }

        return new[]
        {
            new DisplayBlock
            {
                Group = NodeGroup.Wiring,
                Title = "Direktverdrahtung",
                Detail = middleNodes.Count > 0
                    ? $"Pfad: {ExtractPrimaryPinLabel(middleNodes.First())} -> {ExtractPrimaryPinLabel(middleNodes.Last())}"
                    : "-"
            }
        };
    }

    private static DisplayBlock CreateWiringBlock(IReadOnlyList<string> nodes, string? moduleRoot)
    {
        var entryNode = SelectInterestingNode(nodes, moduleRoot, fromStart: true);
        var exitNode = SelectInterestingNode(nodes, moduleRoot, fromStart: false);
        var title = string.IsNullOrWhiteSpace(moduleRoot) ? "Verdrahtung" : moduleRoot;
        var detail = $"Eingang: {ExtractPrimaryPinLabel(entryNode)}\nAusgang: {ExtractPrimaryPinLabel(exitNode)}";
        return new DisplayBlock
        {
            Group = NodeGroup.Wiring,
            Title = title,
            Detail = detail,
            ModuleRoot = moduleRoot
        };
    }

    private static string SelectInterestingNode(IReadOnlyList<string> nodes, string? moduleRoot, bool fromStart)
    {
        if (nodes.Count == 0)
        {
            return string.Empty;
        }

        var ordered = fromStart ? nodes : nodes.Reverse();
        foreach (var node in ordered)
        {
            if (IsComponentTerminalNode(node, moduleRoot))
            {
                return node;
            }
        }

        foreach (var node in ordered)
        {
            if (!IsBoundaryNode(node))
            {
                return node;
            }
        }

        return fromStart ? nodes[0] : nodes[^1];
    }

    private static bool IsBoundaryNode(string node)
    {
        return node.Contains(".BoardPort.", StringComparison.OrdinalIgnoreCase) ||
               node.Contains(".DevicePort.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsComponentTerminalNode(string node, string? moduleRoot)
    {
        if (string.IsNullOrWhiteSpace(node) || string.IsNullOrWhiteSpace(moduleRoot))
        {
            return false;
        }

        var prefix = moduleRoot + ".";
        if (!node.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = node[prefix.Length..];
        var dotIndex = remainder.IndexOf('.');
        if (dotIndex <= 0 || dotIndex >= remainder.Length - 1)
        {
            return false;
        }

        var designator = remainder[..dotIndex];
        if (string.Equals(designator, "BoardPort", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(designator, "DevicePort", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return designator.Any(char.IsDigit);
    }

    private static string ExtractPrimaryPinLabel(string node)
    {
        if (string.IsNullOrWhiteSpace(node))
        {
            return "-";
        }

        var firstLine = SplitLabelLines(node).FirstOrDefault() ?? node.Trim();
        var parenthesesStart = firstLine.IndexOf(" (", StringComparison.Ordinal);
        var key = parenthesesStart > 0 ? firstLine[..parenthesesStart] : firstLine;
        var lastDot = key.LastIndexOf('.');
        return lastDot >= 0 && lastDot < key.Length - 1
            ? key[(lastDot + 1)..]
            : key;
    }

    private static string? ExtractEndpointDetail(string node)
    {
        if (string.IsNullOrWhiteSpace(node))
        {
            return null;
        }

        var firstLine = SplitLabelLines(node).FirstOrDefault() ?? node.Trim();
        var parenthesesStart = firstLine.IndexOf(" (", StringComparison.Ordinal);
        var key = parenthesesStart > 0 ? firstLine[..parenthesesStart] : firstLine;
        var displayLabel = parenthesesStart > 0 && firstLine.EndsWith(")", StringComparison.Ordinal)
            ? firstLine[(parenthesesStart + 2)..^1]
            : null;

        var parts = key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2)
        {
            var connector = parts[^2];
            var pin = parts[^1];
            if (!string.IsNullOrWhiteSpace(displayLabel) &&
                !string.Equals(displayLabel, pin, StringComparison.OrdinalIgnoreCase))
            {
                return $"{connector} / {displayLabel}";
            }

            return $"{connector} / {pin}";
        }

        return key;
    }

    private static bool IsActuationTrace(StepConnectionTrace trace)
    {
        var title = trace.Title ?? string.Empty;
        return title.StartsWith("Ansteuerung", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("Stimulus", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("Pruefsystem", StringComparison.OrdinalIgnoreCase);
    }

    private bool ResolveSourceIsTestSystem(StepConnectionTrace? trace)
    {
        if (_forcedSourceIsTestSystem.HasValue)
        {
            return _forcedSourceIsTestSystem.Value;
        }

        return trace != null && IsActuationTrace(trace);
    }

    private bool CanOpenModule(string? moduleRoot)
    {
        if (string.IsNullOrWhiteSpace(moduleRoot) || _selectedRawTrace == null)
        {
            return false;
        }

        var moduleNodes = _selectedRawTrace.Nodes
            .Where(node => NodeBelongsToModule(node, moduleRoot))
            .ToList();

        return IsExpandableSubmodule(moduleNodes);
    }

    private StepConnectionTrace? BuildSubmoduleTrace(string moduleRoot)
    {
        if (_selectedRawTrace == null)
        {
            return null;
        }

        var moduleNodes = _selectedRawTrace.Nodes
            .Where(node => NodeBelongsToModule(node, moduleRoot))
            .ToList();
        if (!IsExpandableSubmodule(moduleNodes))
        {
            return null;
        }

        var firstIndex = -1;
        var lastIndex = -1;
        for (var index = 0; index < _selectedRawTrace.Nodes.Count; index++)
        {
            if (!NodeBelongsToModule(_selectedRawTrace.Nodes[index], moduleRoot))
            {
                continue;
            }

            if (firstIndex < 0)
            {
                firstIndex = index;
            }

            lastIndex = index;
        }

        if (firstIndex < 0 || lastIndex < firstIndex)
        {
            return null;
        }

        var start = Math.Max(0, firstIndex - 1);
        var end = Math.Min(_selectedRawTrace.Nodes.Count - 1, lastIndex + 1);
        var nodes = _selectedRawTrace.Nodes
            .Skip(start)
            .Take(end - start + 1)
            .ToList();
        if (nodes.Count < 2)
        {
            return null;
        }

        return new StepConnectionTrace($"{moduleRoot} intern", nodes);
    }

    private static bool NodeBelongsToModule(string node, string moduleRoot)
    {
        return !string.IsNullOrWhiteSpace(node) &&
               node.StartsWith(moduleRoot + ".", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExpandableSubmodule(IReadOnlyList<string> moduleNodes)
    {
        if (moduleNodes.Count < 2)
        {
            return false;
        }

        var hasBoundaryNodes = moduleNodes.Any(IsBoundaryNode);
        if (!hasBoundaryNodes)
        {
            return false;
        }

        var hasInternalNodes = moduleNodes.Any(node => !IsBoundaryNode(node));
        return hasInternalNodes;
    }

    private static string? GetModuleRootFromLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        foreach (var line in label
                     .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                     .Select(item => item.Trim()))
        {
            var keyPart = line.Split(' ', 2, StringSplitOptions.TrimEntries)[0];
            var dotIndex = keyPart.IndexOf('.');
            if (dotIndex <= 0)
            {
                continue;
            }

            var root = keyPart[..dotIndex];
            if (string.Equals(root, "CT3", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(root, "UIF", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return root;
        }

        return null;
    }

    private void RenderCurve()
    {
        CurveCanvas.Children.Clear();
        CurveCanvas.Width = 1100;

        if (_curvePoints.Count == 0 || _curvePoints.All(point => !point.Value.HasValue))
        {
            CurveCanvas.Children.Add(new TextBlock
            {
                Text = "Keine Kurvenpunkte fuer diesen Testschritt vorhanden.",
                Foreground = Brush("#FF5E564A"),
                FontSize = 16
            });
            return;
        }

        const double left = 48;
        const double top = 18;
        const double width = 980;
        const double height = 120;
        CurveCanvas.Height = 180;

        var numericPoints = _curvePoints.Where(point => point.Value.HasValue).ToList();
        var minTime = numericPoints.Min(point => point.TimeMs);
        var maxTime = Math.Max(minTime + 1, numericPoints.Max(point => point.TimeMs));
        var minValue = numericPoints.Min(point => point.Value!.Value);
        var maxValue = Math.Max(minValue + 0.001, numericPoints.Max(point => point.Value!.Value));

        CurveCanvas.Children.Add(new Line { X1 = left, Y1 = top + height, X2 = left + width, Y2 = top + height, Stroke = Brush("#FF768191"), StrokeThickness = 1.4 });
        CurveCanvas.Children.Add(new Line { X1 = left, Y1 = top, X2 = left, Y2 = top + height, Stroke = Brush("#FF768191"), StrokeThickness = 1.4 });

        var polyline = new Polyline
        {
            Stroke = Brush("#FF2E6F95"),
            StrokeThickness = 2.5
        };

        foreach (var point in numericPoints)
        {
            var x = left + (point.TimeMs - minTime) / (double)(maxTime - minTime) * width;
            var y = top + height - ((point.Value!.Value - minValue) / (maxValue - minValue) * height);
            polyline.Points.Add(new Point(x, y));

            var dot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = Brush("#FF2E6F95"),
                ToolTip = $"{point.Label}: {point.Value?.ToString("0.###", CultureInfo.InvariantCulture)} {point.Unit} @ {point.TimeMs} ms"
            };
            Canvas.SetLeft(dot, x - 4);
            Canvas.SetTop(dot, y - 4);
            CurveCanvas.Children.Add(dot);
        }

        CurveCanvas.Children.Add(polyline);
        AddCurveLabel(left - 10, top - 4, maxValue.ToString("0.###", CultureInfo.InvariantCulture));
        AddCurveLabel(left - 10, top + height - 10, minValue.ToString("0.###", CultureInfo.InvariantCulture));
        AddCurveLabel(left, top + height + 6, $"{minTime} ms");
        AddCurveLabel(left + width - 40, top + height + 6, $"{maxTime} ms");
    }

    private void AddCurveLabel(double left, double top, string text)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = Brush("#FF5E564A"),
            FontSize = 12
        };
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, top);
        CurveCanvas.Children.Add(label);
    }

    private void OnViewportMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var factor = e.Delta > 0 ? 1.12 : 1d / 1.12;
        var nextScale = Math.Clamp(GraphScaleTransform.ScaleX * factor, 0.35, 3.5);
        GraphScaleTransform.ScaleX = nextScale;
        GraphScaleTransform.ScaleY = nextScale;
        UpdateStatus();
    }

    private void OnViewportMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindClickableModuleBorder(e.OriginalSource as DependencyObject) != null)
        {
            return;
        }

        _dragStart = e.GetPosition(this);
        _dragOrigin = new Point(GraphTranslateTransform.X, GraphTranslateTransform.Y);
        Mouse.Capture((UIElement)sender);
    }

    private void OnViewportMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart == null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(this);
        var delta = current - _dragStart.Value;
        GraphTranslateTransform.X = _dragOrigin.X + delta.X;
        GraphTranslateTransform.Y = _dragOrigin.Y + delta.Y;
    }

    private void OnViewportMouseLeftButtonUp(object sender, MouseEventArgs e)
    {
        _dragStart = null;
        if (Mouse.Captured != null)
        {
            Mouse.Capture(null);
        }
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        StatusTextBlock.Text =
            $"Pfade: {_traces.Count.ToString(CultureInfo.InvariantCulture)} | Ausgewaehlt: {_selectedTrace?.Nodes.Count.ToString(CultureInfo.InvariantCulture) ?? "0"} Stationen | Kurvenpunkte: {_curvePoints.Count.ToString(CultureInfo.InvariantCulture)} | Zoom: {(GraphScaleTransform.ScaleX * 100d).ToString("0", CultureInfo.InvariantCulture)} %";
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static Border? FindClickableModuleBorder(DependencyObject? source)
    {
        var current = source;
        while (current != null)
        {
            if (current is Border { Tag: string moduleRoot } border && !string.IsNullOrWhiteSpace(moduleRoot))
            {
                return border;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static SolidColorBrush Brush(string color) =>
        new((Color)ColorConverter.ConvertFromString(color));
}
