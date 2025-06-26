using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using Serilog;

namespace SumTree.Nuke;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for the build process.
/// </summary>
partial class Build
{
    private Target Clean => s => s
        .Executes(() =>
        {
            Solution.SumTree.Directory.GlobDirectories("**/bin", "**/obj", "**/output").ForEach((path) =>
                path.DeleteDirectory()
            );
            Solution.SumTree_Tests.Directory.GlobDirectories("**/bin", "**/obj", "**/output").ForEach((path) =>
                path.DeleteDirectory()
            );
            (RootDirectory / "packages").DeleteDirectory();
            CoverageDirectory.DeleteDirectory();
        });

    private Target Restore => td => td
        .After(Clean)
        .Executes(() =>
        {
            _ = DotNetTasks.DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    private Target Compile => td => td
        .After(Restore)
        .Executes(() =>
        {
            Log.Debug("Configuration {Configuration}", ConfigurationSet);
            Log.Debug("configuration {configuration}", Configuration);
            _ = DotNetTasks.DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(ConfigurationSet)
                .EnableNoRestore()
            );
        });
}