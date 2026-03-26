using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace Ct3xxProgramParser.Model;

[XmlRoot("CT3xxProgram")]
public class Ct3xxProgram
{
    [XmlAttribute("HashAlg")] public string? HashAlgorithm { get; set; }
    [XmlAttribute("Id")] public string? Id { get; set; }
    [XmlAttribute("Rev")] public string? Revision { get; set; }
    [XmlAttribute("ProgramGUID")] public string? ProgramGuid { get; set; }
    [XmlAttribute("ProgramAuthor")] public string? ProgramAuthor { get; set; }
    [XmlAttribute("ProgramVersion")] public string? ProgramVersion { get; set; }
    [XmlAttribute("ProgramComment")] public string? ProgramComment { get; set; }
    [XmlAttribute("DUTName")] public string? DutName { get; set; }
    [XmlAttribute("DUTRevision")] public string? DutRevision { get; set; }
    [XmlAttribute("DUTVariant")] public string? DutVariant { get; set; }
    [XmlAttribute("DUTCode")] public string? DutCode { get; set; }
    [XmlAttribute("DUTComment")] public string? DutComment { get; set; }
    [XmlAttribute("TestTicket")] public string? TestTicket { get; set; }
    [XmlAttribute("LogTarget")] public string? LogTarget { get; set; }
    [XmlAttribute("LogPrefix")] public string? LogPrefix { get; set; }
    [XmlAttribute("LogSuffix")] public string? LogSuffix { get; set; }
    [XmlAttribute("LotCode")] public string? LotCode { get; set; }
    [XmlAttribute("FixtureCode")] public string? FixtureCode { get; set; }
    [XmlAttribute("HandlingCode")] public string? HandlingCode { get; set; }
    [XmlAttribute("BoardIndex")] public string? BoardIndex { get; set; }
    [XmlAttribute("Discharge")] public string? Discharge { get; set; }

    [XmlElement("OperatorScreen")] public OperatorScreen? OperatorScreen { get; set; }
    [XmlElement("UserButton")] public UserButtonPanel? UserButtons { get; set; }
    [XmlElement("Table")] public List<Table> Tables { get; set; } = new();
    [XmlElement("Group", typeof(Group))]
    [XmlElement("Test", typeof(Test))]
    public List<SequenceNode> RootItems { get; set; } = new();
    [XmlElement("DUTLoop")] public DutLoop? DutLoop { get; set; }
    [XmlElement("Application")] public ApplicationInfo? Application { get; set; }
    [XmlElement("Hash")] public HashInfo? Hash { get; set; }
}

public class OperatorScreen
{
    [XmlElement("Display1")] public DisplayLine? Display1 { get; set; }
    [XmlElement("Display2")] public DisplayLine? Display2 { get; set; }
    [XmlElement("Display3")] public DisplayLine? Display3 { get; set; }
    [XmlElement("Display4")] public DisplayLine? Display4 { get; set; }
}

public class DisplayLine
{
    [XmlAttribute("Title")] public string? Title { get; set; }
    [XmlAttribute("Text")] public string? Text { get; set; }
}

public class UserButtonPanel
{
    [XmlElement("Button1")] public UserButton? Button1 { get; set; }
    [XmlElement("Button2")] public UserButton? Button2 { get; set; }
    [XmlElement("Button3")] public UserButton? Button3 { get; set; }
    [XmlElement("Button4")] public UserButton? Button4 { get; set; }
    [XmlElement("Button5")] public UserButton? Button5 { get; set; }
    [XmlElement("Button6")] public UserButton? Button6 { get; set; }
    [XmlElement("Button7")] public UserButton? Button7 { get; set; }
}

public class UserButton
{
    [XmlAttribute("Enable")] public string? Enable { get; set; }
    [XmlAttribute("Text")] public string? Text { get; set; }
    [XmlAttribute("Library")] public string? Library { get; set; }
    [XmlAttribute("Function")] public string? Function { get; set; }
}

[XmlInclude(typeof(Group))]
[XmlInclude(typeof(Test))]
[XmlInclude(typeof(Table))]
public abstract class SequenceNode
{
}

[XmlType("Group")]
public class Group : SequenceNode
{
    [XmlAttribute("Id")] public string? Id { get; set; }
    [XmlAttribute("Rev")] public string? Revision { get; set; }
    [XmlAttribute("Name")] public string? Name { get; set; }
    [XmlAttribute("Disabled")] public string? Disabled { get; set; }
    [XmlAttribute("ExecCondition")] public string? ExecCondition { get; set; }
    [XmlAttribute("RepeatCondition")] public string? RepeatCondition { get; set; }
    [XmlAttribute("ExecMode")] public string? ExecMode { get; set; }
    [XmlAttribute("LogLoops")] public string? LogLoops { get; set; }
    [XmlAttribute("LoopCnt")] public string? LoopCount { get; set; }
    [XmlAttribute("LogPrefix")] public string? LogPrefix { get; set; }
    [XmlAttribute("LogSuffix")] public string? LogSuffix { get; set; }

    [XmlElement("Group", typeof(Group))]
    [XmlElement("Test", typeof(Test))]
    [XmlElement("Table", typeof(Table))]
    public List<SequenceNode> Items { get; set; } = new();
}

[XmlType("Test")]
public class Test : SequenceNode
{
    [XmlAttribute("Id")] public string? Id { get; set; }
    [XmlAttribute("Rev")] public string? Revision { get; set; }
    [XmlAttribute("Disabled")] public string? Disabled { get; set; }
    [XmlAttribute("LogFlags")] public string? LogFlags { get; set; }
    [XmlAttribute("Split")] public string? Split { get; set; }
    [XmlAttribute("Name")] public string? Name { get; set; }
    [XmlAttribute("File")] public string? File { get; set; }
    [XmlAttribute("Digest")] public string? Digest { get; set; }

    [XmlElement("Parameters")] public TestParameters? Parameters { get; set; }
    [XmlElement("Debug")] public List<DebugNode> Debug { get; set; } = new();
    [XmlElement("Group", typeof(Group))]
    [XmlElement("Test", typeof(Test))]
    [XmlElement("Table", typeof(Table))]
    public List<SequenceNode> Items { get; set; } = new();
}

public class TestParameters
{
    [XmlAttribute("Name")] public string? Name { get; set; }
    [XmlAttribute("DrawingReference")] public string? DrawingReference { get; set; }
    [XmlAttribute("Message")] public string? Message { get; set; }
    [XmlAttribute("Image")] public string? Image { get; set; }
    [XmlAttribute("Mode")] public string? Mode { get; set; }
    [XmlAttribute("Options")] public string? Options { get; set; }
    [XmlAttribute("OptionsVariable")] public string? OptionsVariable { get; set; }
    [XmlAttribute("Function")] public string? Function { get; set; }
    [XmlAttribute("Library")] public string? Library { get; set; }
    [XmlAttribute("PASSText")] public string? PassText { get; set; }
    [XmlAttribute("FAILText")] public string? FailText { get; set; }
    [XmlAttribute("AutoOk")] public string? AutoOk { get; set; }
    [XmlAttribute("QuitCondition")] public string? QuitCondition { get; set; }
    [XmlAttribute("OnText")] public string? OnText { get; set; }
    [XmlAttribute("OffText")] public string? OffText { get; set; }
    [XmlAttribute("ResPassText")] public string? ResultPassText { get; set; }
    [XmlAttribute("ResFailText")] public string? ResultFailText { get; set; }
    [XmlAttribute("ResErrorText")] public string? ResultErrorText { get; set; }
    [XmlAttribute("ResultArray")] public string? ResultArray { get; set; }
    [XmlAttribute("NumberOfResults")] public string? NumberOfResults { get; set; }

    [XmlElement("Table")] public List<Table> Tables { get; set; } = new();
    [XmlElement("AcqChannel1")] public AcquisitionChannel? AcquisitionChannel1 { get; set; }
    [XmlElement("AcqChannel2")] public AcquisitionChannel? AcquisitionChannel2 { get; set; }
    [XmlElement("AcqChannel3")] public AcquisitionChannel? AcquisitionChannel3 { get; set; }
    [XmlElement("StiChannel1")] public StimulusChannel? StimulusChannel1 { get; set; }
    [XmlElement("StiChannel2")] public StimulusChannel? StimulusChannel2 { get; set; }
    [XmlAnyElement] public XmlElement[]? ExtraElements { get; set; }
    [XmlAnyAttribute] public XmlAttribute[]? AdditionalAttributes { get; set; }
}

public class AcquisitionChannel
{
    [XmlAttribute("Source")] public string? Source { get; set; }
    [XmlAttribute("Range")] public string? Range { get; set; }
    [XmlAttribute("Filter")] public string? Filter { get; set; }
    [XmlAttribute("UpperVoltageLimit")] public string? UpperVoltageLimit { get; set; }
    [XmlAttribute("LowerVoltageLimit")] public string? LowerVoltageLimit { get; set; }
    [XmlAnyAttribute] public XmlAttribute[]? AdditionalAttributes { get; set; }
}

public class StimulusChannel
{
    [XmlAttribute("Target")] public string? Target { get; set; }
    [XmlAttribute("Source")] public string? Source { get; set; }
    [XmlAttribute("Mode")] public string? Mode { get; set; }
    [XmlAttribute("Current")] public string? Current { get; set; }
    [XmlAttribute("Voltage")] public string? Voltage { get; set; }
    [XmlAttribute("VoltageLimit")] public string? VoltageLimit { get; set; }
    [XmlAttribute("Power")] public string? Power { get; set; }
    [XmlAttribute("Delay")] public string? Delay { get; set; }
}

[XmlType("Table")]
public class Table : SequenceNode
{
    [XmlAttribute("Id")] public string? Id { get; set; }
    [XmlAttribute("Rev")] public string? Revision { get; set; }
    [XmlAttribute("File")] public string? File { get; set; }
    [XmlAttribute("Digest")] public string? Digest { get; set; }
    [XmlAttribute("IfcFile")] public string? InterfaceFile { get; set; }
    [XmlAttribute("IfcDigest")] public string? InterfaceDigest { get; set; }
    [XmlAttribute("Length")] public string? Length { get; set; }

    [XmlElement("Variable")] public List<VariableDefinition> Variables { get; set; } = new();
    [XmlElement("Record")] public List<Record> Records { get; set; } = new();
    [XmlElement("File")] public List<TableFile> Files { get; set; } = new();
    [XmlElement("Library")] public List<LibraryDefinition> Libraries { get; set; } = new();
}

public class LibraryDefinition
{
    [XmlAttribute("HashAlg")] public string? HashAlgorithm { get; set; }
    [XmlAttribute("Id")] public string? Id { get; set; }
    [XmlAttribute("Rev")] public string? Revision { get; set; }
    [XmlAttribute("Name")] public string? Name { get; set; }
    [XmlAttribute("LibExternal")] public string? IsExternal { get; set; }

    [XmlElement("Function")] public List<LibraryFunction> Functions { get; set; } = new();
    [XmlElement("Hash")] public HashInfo? Hash { get; set; }
}

public class LibraryFunction
{
    [XmlAttribute("HashAlg")] public string? HashAlgorithm { get; set; }
    [XmlAttribute("Id")] public string? Id { get; set; }
    [XmlAttribute("Rev")] public string? Revision { get; set; }
    [XmlAttribute("Name")] public string? Name { get; set; }
    [XmlAttribute("Disabled")] public string? Disabled { get; set; }
    [XmlAttribute("ExecCondition")] public string? ExecCondition { get; set; }
    [XmlAttribute("LogPrefix")] public string? LogPrefix { get; set; }
    [XmlAttribute("LogSuffix")] public string? LogSuffix { get; set; }

    [XmlElement("Table", typeof(Table))] public List<Table> Tables { get; set; } = new();
    [XmlElement("Group", typeof(Group))]
    [XmlElement("Test", typeof(Test))]
    public List<SequenceNode> Items { get; set; } = new();
    [XmlElement("Hash")] public HashInfo? Hash { get; set; }
}

public class VariableDefinition
{
    [XmlAttribute("Id")] public string? Id { get; set; }
    [XmlAttribute("Rev")] public string? Revision { get; set; }
    [XmlAttribute("Index")] public string? Index { get; set; }
    [XmlAttribute("Name")] public string? Name { get; set; }
    [XmlAttribute("Type")] public string? Type { get; set; }
    [XmlAttribute("Initial")] public string? Initial { get; set; }
}

public class Record
{
    [XmlAttribute("Id")] public string? Id { get; set; }
    [XmlAttribute("Rev")] public string? Revision { get; set; }
    [XmlAttribute("Index")] public string? Index { get; set; }
    [XmlAttribute("Disabled")] public string? Disabled { get; set; }
    [XmlAttribute("Variable")] public string? Variable { get; set; }
    [XmlAttribute("Text")] public string? Text { get; set; }
    [XmlAttribute("Destination")] public string? Destination { get; set; }
    [XmlAttribute("Expression")] public string? Expression { get; set; }
    [XmlAttribute("Type")] public string? Type { get; set; }
    [XmlAttribute("LowerLimit")] public string? LowerLimit { get; set; }
    [XmlAttribute("UpperLimit")] public string? UpperLimit { get; set; }
    [XmlAttribute("DrawingReference")] public string? DrawingReference { get; set; }
    [XmlAttribute("Unit")] public string? Unit { get; set; }
    [XmlAttribute("TP")] public string? TestPoint { get; set; }
    [XmlAttribute("D")] public string? D { get; set; }
    [XmlAttribute("V")] public string? Voltage { get; set; }
    [XmlAttribute("T")] public string? Time { get; set; }
    [XmlAttribute("R")] public string? Resistance { get; set; }
    [XmlAttribute("K")] public string? SwitchState { get; set; }
    [XmlAttribute("DestinationVariable")] public string? DestinationVariable { get; set; }
    [XmlAttribute("Device")] public string? Device { get; set; }
    [XmlAttribute("Control")] public string? Control { get; set; }
    [XmlAttribute("Direction")] public string? Direction { get; set; }
    [XmlAttribute("Command")] public string? Command { get; set; }
    [XmlAnyAttribute] public XmlAttribute[]? AdditionalAttributes { get; set; }
}

public class TableFile
{
    [XmlAttribute("Id")] public string? Id { get; set; }
    [XmlAttribute("Rev")] public string? Revision { get; set; }
    [XmlAttribute("Index")] public string? Index { get; set; }
    [XmlAttribute("Disabled")] public string? Disabled { get; set; }
    [XmlAttribute("Name")] public string? Name { get; set; }
    [XmlAttribute("Digest")] public string? Digest { get; set; }
}

public class DutLoop
{
    [XmlAttribute("Id")] public string? Id { get; set; }
    [XmlAttribute("Rev")] public string? Revision { get; set; }
    [XmlAttribute("Name")] public string? Name { get; set; }
    [XmlAttribute("Disabled")] public string? Disabled { get; set; }
    [XmlAttribute("ExecCondition")] public string? ExecCondition { get; set; }
    [XmlAttribute("LoopCnt")] public string? LoopCount { get; set; }
    [XmlAttribute("LogPrefix")] public string? LogPrefix { get; set; }
    [XmlAttribute("LogSuffix")] public string? LogSuffix { get; set; }
    [XmlAttribute("ErrorCnt")] public string? ErrorCount { get; set; }
    [XmlAttribute("FailCnt")] public string? FailCount { get; set; }
    [XmlAttribute("PassCnt")] public string? PassCount { get; set; }

    [XmlElement("Group", typeof(Group))]
    [XmlElement("Test", typeof(Test))]
    [XmlElement("Table", typeof(Table))]
    public List<SequenceNode> Items { get; set; } = new();
}

public class ApplicationInfo
{
    [XmlAttribute("Id")] public string? Id { get; set; }
    [XmlAttribute("Rev")] public string? Revision { get; set; }
    [XmlAttribute("Name")] public string? Name { get; set; }
    [XmlAttribute("Version")] public string? Version { get; set; }

    [XmlElement("Table")] public List<Table> Tables { get; set; } = new();
}

public class HashInfo
{
    [XmlAttribute("Val")] public string? Value { get; set; }
}

public class DebugNode
{
    [XmlAttribute("Id")] public string? Id { get; set; }
    [XmlAttribute("Rev")] public string? Revision { get; set; }
    [XmlAttribute("Disabled")] public string? Disabled { get; set; }
    [XmlAttribute("Mask")] public string? Mask { get; set; }
    [XmlAttribute("Mode")] public string? Mode { get; set; }
    [XmlAttribute("DisplayKind")] public string? DisplayKind { get; set; }
    [XmlAttribute("Display")] public string? Display { get; set; }
    [XmlAttribute("RangeLowerLimit")] public string? RangeLowerLimit { get; set; }
    [XmlAttribute("RangeUpperLimit")] public string? RangeUpperLimit { get; set; }
    [XmlAttribute("LowerLimit")] public string? LowerLimit { get; set; }
    [XmlAttribute("UpperLimit")] public string? UpperLimit { get; set; }
}
