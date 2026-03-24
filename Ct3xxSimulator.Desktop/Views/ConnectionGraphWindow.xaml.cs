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
    private readonly IReadOnlyList<StepConnectionTrace> _traces;
    private readonly IReadOnlyList<MeasurementCurvePoint> _curvePoints;
    private Point? _dragStart;
    private Point _dragOrigin;

    public ConnectionGraphWindow(string stepName, IReadOnlyList<StepConnectionTrace> traces, IReadOnlyList<MeasurementCurvePoint>? curvePoints = null)
    {
        _traces = traces ?? Array.Empty<StepConnectionTrace>();
        _curvePoints = curvePoints ?? Array.Empty<MeasurementCurvePoint>();
        InitializeComponent();
        TitleTextBlock.Text = stepName;
        RenderGraph();
        RenderCurve();
        UpdateStatus();
    }

    private void RenderGraph()
    {
        GraphCanvas.Children.Clear();

        const double leftMargin = 50;
        const double topMargin = 36;
        const double rowHeight = 170;
        const double nodeWidth = 190;
        const double nodeHeight = 64;
        const double nodeGap = 68;
        const double titleGap = 34;

        var maxNodeCount = Math.Max(1, _traces.Count == 0 ? 0 : _traces.Max(trace => trace.Nodes.Count));
        GraphCanvas.Width = leftMargin * 2 + maxNodeCount * nodeWidth + Math.Max(0, maxNodeCount - 1) * nodeGap;
        GraphCanvas.Height = topMargin * 2 + Math.Max(1, _traces.Count) * rowHeight;

        if (_traces.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = "Keine Verbindung fuer diesen Testschritt verfuegbar.",
                FontSize = 18,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5E564A"))
            };

            Canvas.SetLeft(empty, leftMargin);
            Canvas.SetTop(empty, topMargin);
            GraphCanvas.Children.Add(empty);
            return;
        }

        for (var traceIndex = 0; traceIndex < _traces.Count; traceIndex++)
        {
            var trace = _traces[traceIndex];
            var rowTop = topMargin + traceIndex * rowHeight;

            var titleBlock = new TextBlock
            {
                Text = trace.Title,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3D3528"))
            };
            Canvas.SetLeft(titleBlock, leftMargin);
            Canvas.SetTop(titleBlock, rowTop);
            GraphCanvas.Children.Add(titleBlock);

            var nodeTop = rowTop + titleGap;
            for (var nodeIndex = 0; nodeIndex < trace.Nodes.Count; nodeIndex++)
            {
                var nodeLeft = leftMargin + nodeIndex * (nodeWidth + nodeGap);
                if (nodeIndex < trace.Nodes.Count - 1)
                {
                    DrawConnection(nodeLeft + nodeWidth, nodeTop + nodeHeight / 2d, nodeGap);
                }

                DrawNode(trace.Nodes[nodeIndex], nodeLeft, nodeTop, nodeWidth, nodeHeight, nodeIndex);
            }
        }
    }

    private void DrawNode(string label, double left, double top, double width, double height, int nodeIndex)
    {
        var fillColor = nodeIndex == 0
            ? "#FFDDE9C7"
            : nodeIndex % 2 == 0
                ? "#FFF6DFA4"
                : "#FFE4D2B6";

        var border = new Border
        {
            Width = width,
            Height = height,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fillColor)),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF8B765A")),
            BorderThickness = new Thickness(1.2),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10)
        };

        border.Child = new TextBlock
        {
            Text = label,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            FontWeight = FontWeights.Medium,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2F2A25")),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        Canvas.SetLeft(border, left);
        Canvas.SetTop(border, top);
        GraphCanvas.Children.Add(border);
    }

    private void DrawConnection(double startX, double centerY, double gapWidth)
    {
        var line = new Line
        {
            X1 = startX,
            Y1 = centerY,
            X2 = startX + gapWidth - 20,
            Y2 = centerY,
            Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6A5A43")),
            StrokeThickness = 2.2,
            SnapsToDevicePixels = true
        };
        GraphCanvas.Children.Add(line);

        var arrow = new Polygon
        {
            Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6A5A43")),
            Points = new PointCollection
            {
                new(startX + gapWidth - 20, centerY - 6),
                new(startX + gapWidth - 20, centerY + 6),
                new(startX + gapWidth - 4, centerY)
            }
        };
        GraphCanvas.Children.Add(arrow);
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
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5E564A")),
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

        var axisBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF768191"));
        CurveCanvas.Children.Add(new Line { X1 = left, Y1 = top + height, X2 = left + width, Y2 = top + height, Stroke = axisBrush, StrokeThickness = 1.4 });
        CurveCanvas.Children.Add(new Line { X1 = left, Y1 = top, X2 = left, Y2 = top + height, Stroke = axisBrush, StrokeThickness = 1.4 });

        var polyline = new Polyline
        {
            Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2E6F95")),
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
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2E6F95")),
                ToolTip = $"{point.Label}: {point.Value?.ToString("0.###", CultureInfo.InvariantCulture)} {point.Unit} @ {point.TimeMs} ms"
            };
            Canvas.SetLeft(dot, x - 4);
            Canvas.SetTop(dot, y - 4);
            CurveCanvas.Children.Add(dot);
        }

        CurveCanvas.Children.Add(polyline);
        AddCurveLabel(left - 10, top - 4, $"{maxValue.ToString("0.###", CultureInfo.InvariantCulture)}");
        AddCurveLabel(left - 10, top + height - 10, $"{minValue.ToString("0.###", CultureInfo.InvariantCulture)}");
        AddCurveLabel(left, top + height + 6, $"{minTime} ms");
        AddCurveLabel(left + width - 40, top + height + 6, $"{maxTime} ms");
    }

    private void AddCurveLabel(double left, double top, string text)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5E564A")),
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
            $"Traces: {_traces.Count.ToString(CultureInfo.InvariantCulture)} | Punkte: {_curvePoints.Count.ToString(CultureInfo.InvariantCulture)} | Zoom: {(GraphScaleTransform.ScaleX * 100d).ToString("0", CultureInfo.InvariantCulture)} % | Offset: {GraphTranslateTransform.X.ToString("0", CultureInfo.InvariantCulture)} / {GraphTranslateTransform.Y.ToString("0", CultureInfo.InvariantCulture)}";
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
