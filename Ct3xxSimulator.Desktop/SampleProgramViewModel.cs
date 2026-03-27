// Provides Sample Program View Model for the desktop application support code.
using Ct3xxProgramParser.Discovery;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Desktop;

/// <summary>
/// Represents the sample program view model.
/// </summary>
public sealed class SampleProgramViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SampleProgramViewModel"/> class.
    /// </summary>
    public SampleProgramViewModel(TestProgramInfo info)
    {
        Info = info;
    }

    /// <summary>
    /// Gets the info.
    /// </summary>
    public TestProgramInfo Info { get; }
    /// <summary>
    /// Gets the folder name.
    /// </summary>
    public string FolderName => Info.RelativeDirectory;
    /// <summary>
    /// Gets the file path.
    /// </summary>
    public string FilePath => Info.FilePath;
    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string DisplayName => Info.DisplayName;
}
