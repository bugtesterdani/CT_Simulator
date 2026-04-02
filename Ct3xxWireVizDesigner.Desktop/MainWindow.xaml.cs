using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Ct3xxWireVizDesigner.Core.Model;
using Ct3xxWireVizDesigner.Core.Serialization;
using Ct3xxWireVizDesigner.Core.WireViz;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace Ct3xxWireVizDesigner.Desktop;

/// <summary>
/// Interaction logic for MainWindow.xaml.
/// </summary>
public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await WebView.EnsureCoreWebView2Async();
        WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
        WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        var htmlPath = ResolveWebUiPath();
        if (htmlPath == null)
        {
            MessageBox.Show("Web UI assets not found.", "CT3xx WireViz Designer", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var uri = new Uri(htmlPath).AbsoluteUri + $"?v={DateTime.UtcNow.Ticks}";
        WebView.CoreWebView2.Navigate(uri);
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeElement))
            {
                return;
            }

            var type = typeElement.GetString();
            switch (type)
            {
                case "request-open-graph":
                    HandleOpenGraph();
                    break;
                case "request-save-graph":
                    HandleSaveGraph(root);
                    break;
                case "request-import-wireviz":
                    HandleImportWireViz();
                    break;
                case "request-export-wireviz":
                    HandleExportWireViz(root);
                    break;
                case "request-export-wireviz-preview":
                    HandleExportWireVizPreview(root);
                    break;
            }
        }
        catch (Exception ex)
        {
            SendStatus($"Error: {ex.Message}");
        }
    }

    private void HandleOpenGraph()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Block Graph (*.block.json)|*.block.json|JSON (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var json = File.ReadAllText(dialog.FileName);
        var graph = BlockGraphJson.Deserialize(json);
        PostGraphMessage("graph-loaded", graph);
    }

    private void HandleSaveGraph(JsonElement root)
    {
        if (!root.TryGetProperty("graph", out var graphElement))
        {
            return;
        }

        var graph = graphElement.Deserialize<BlockGraph>(JsonOptions) ?? new BlockGraph();
        var dialog = new SaveFileDialog
        {
            FileName = "graph.block.json",
            Filter = "Block Graph (*.block.json)|*.block.json|JSON (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, BlockGraphJson.Serialize(graph));
        SendStatus("Graph saved.");
    }

    private void HandleImportWireViz()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "WireViz YAML (*.yaml;*.yml)|*.yaml;*.yml|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var yaml = File.ReadAllText(dialog.FileName);
        var graph = WireVizBlockMapper.ImportFromYaml(yaml, dialog.FileName);
        PostGraphMessage("wireviz-imported", graph);
    }

    private void HandleExportWireViz(JsonElement root)
    {
        if (!root.TryGetProperty("graph", out var graphElement))
        {
            return;
        }

        var full = root.TryGetProperty("full", out var fullElement) && fullElement.ValueKind == JsonValueKind.True;
        var compress = root.TryGetProperty("compress", out var compressElement) && compressElement.ValueKind == JsonValueKind.True;
        var graph = graphElement.Deserialize<BlockGraph>(JsonOptions) ?? new BlockGraph();
        var yaml = WireVizBlockMapper.ExportToYaml(graph, full);

        var dialog = new SaveFileDialog
        {
            FileName = compress ? "wireviz.yaml.gz" : "wireviz.yaml",
            Filter = compress
                ? "WireViz YAML (gzip) (*.yaml.gz)|*.yaml.gz|All files (*.*)|*.*"
                : "WireViz YAML (*.yaml;*.yml)|*.yaml;*.yml|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (compress)
        {
            using var fileStream = File.Create(dialog.FileName);
            using var gzip = new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionLevel.Optimal);
            using var writer = new StreamWriter(gzip);
            writer.Write(yaml);
            PostYamlMessage("wireviz-exported", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(yaml)));
        }
        else
        {
            File.WriteAllText(dialog.FileName, yaml);
            PostYamlMessage("wireviz-exported", yaml);
        }
        SendStatus("WireViz exported.");
    }

    private void HandleExportWireVizPreview(JsonElement root)
    {
        if (!root.TryGetProperty("graph", out var graphElement))
        {
            return;
        }

        var graph = graphElement.Deserialize<BlockGraph>(JsonOptions) ?? new BlockGraph();
        var yaml = WireVizBlockMapper.ExportToYaml(graph);
        PostYamlMessage("wireviz-preview", yaml);
    }

    private void PostGraphMessage(string type, BlockGraph graph)
    {
        var message = JsonSerializer.Serialize(new { type, graph }, JsonOptions);
        WebView.CoreWebView2.PostWebMessageAsJson(message);
    }

    private void PostYamlMessage(string type, string yaml)
    {
        var message = JsonSerializer.Serialize(new { type, yaml }, JsonOptions);
        WebView.CoreWebView2.PostWebMessageAsJson(message);
    }

    private void SendStatus(string text)
    {
        var message = JsonSerializer.Serialize(new { type = "status", text }, JsonOptions);
        WebView.CoreWebView2.PostWebMessageAsJson(message);
    }

    private static string? ResolveWebUiPath()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.CurrentDirectory, "Ct3xxWireVizDesigner.Web", "wwwroot", "index.html"),
            Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var dir = new DirectoryInfo(Environment.CurrentDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Ct3xxWireVizDesigner.Web", "wwwroot", "index.html");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
