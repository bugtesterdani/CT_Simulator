using Ct3xxProgramParser.Discovery;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Desktop;

public sealed class SampleProgramViewModel
{
    public SampleProgramViewModel(TestProgramInfo info)
    {
        Info = info;
    }

    public TestProgramInfo Info { get; }
    public string FolderName => Info.RelativeDirectory;
    public string FilePath => Info.FilePath;
    public string DisplayName => Info.DisplayName;
}
