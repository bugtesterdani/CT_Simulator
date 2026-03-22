using Ct3xxWireVizParser.Model;
using Ct3xxWireVizParser.Parsing;

namespace Ct3xxWireVizParser.Tests;

[TestClass]
public sealed class WireVizParserTests
{
    private const string SampleYaml = """
metadata:
  title: Demo 01
  description: Sample harness
options:
  fontname: arial
  bgcolor: WH
tweak:
  append: rankdir=LR
connectors:
  TESTSYS:
    type: D-Sub
    bgcolor: WH
    subtype: female
    pinlabels: [X301.1, X301.2, X301.3, X301.4, X301.5, X301.6, X301.7, X301.8, X301.9]
    ct3xx_signals: [DCD, RX, TX, DTR, GND, DSR, RTS, CTS, RI]
  CONN_A:
    type: Molex KK 254
    bgcolor: YE
    subtype: female
    pinlabels: [GND, RX, TX]
  DevicePort:
    type: Molex KK 254
    bgcolor: YE
    subtype: female
    pinlabels: [GND, RX, TX]
cables:
  W1:
    gauge: 0.25 mm2
    length: 0.2
    color_code: DIN
    wirecount: 3
    shield: true
bundles:
  B1:
    category: sleeve
connections:
  -
    - TESTSYS: [5, 2, 3]
    - W1: [1, 2, 3]
    - DevicePort: [1, 3, 2]
  -
    - TESTSYS: 5
    - W1: s
additional_bom_items:
  - description: Heatshrink
    qty: 1
custom_section:
  arbitrary:
    nested: [1, true, text]
""";

    [TestMethod]
    public void ShouldParseKnownWireVizSections()
    {
        var parser = new WireVizParser();

        var document = parser.Parse(SampleYaml);

        Assert.AreEqual("Demo 01", document.Metadata!.Properties["title"].AsString());
        Assert.AreEqual(3, document.Connectors.Count);
        Assert.AreEqual(1, document.Cables.Count);
        Assert.AreEqual(1, document.Bundles.Count);
        Assert.AreEqual(2, document.Connections.Count);
        Assert.AreEqual(1, document.AdditionalBomItems.Count);
        Assert.AreEqual("D-Sub", document.Connectors["TESTSYS"].Properties["type"].AsString());
        Assert.AreEqual("X301.1", document.Connectors["TESTSYS"].Properties["pinlabels"].Items[0].AsString());
        Assert.AreEqual("DCD", document.Connectors["TESTSYS"].Properties["ct3xx_signals"].Items[0].AsString());
        Assert.AreEqual("0.2", document.Cables["W1"].Properties["length"].AsString());
        Assert.AreEqual(WireVizConnectorRole.TestSystem, document.ConnectorDefinitions["TESTSYS"].Role);
        Assert.AreEqual(WireVizConnectorRole.Harness, document.ConnectorDefinitions["CONN_A"].Role);
        Assert.AreEqual(WireVizConnectorRole.Device, document.ConnectorDefinitions["DevicePort"].Role);
    }

    [TestMethod]
    public void ShouldPreserveUnknownSectionsAndNestedValues()
    {
        var parser = new WireVizParser();

        var document = parser.Parse(SampleYaml);

        var custom = document.GetSection("custom_section");
        Assert.IsNotNull(custom);
        Assert.AreEqual(WireVizValueKind.Mapping, custom.Kind);
        Assert.AreEqual("true", custom.Properties["arbitrary"].Properties["nested"].Items[1].AsString());
        Assert.AreEqual("text", custom.Properties["arbitrary"].Properties["nested"].Items[2].AsString());
    }
}
