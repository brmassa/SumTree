using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;

namespace SumTree.Nuke;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for the library publish process (NuGet packaging).
/// </summary>
partial class Build
{
    [Parameter("Output directory for NuGet packages (default: ./packages)")]
    public readonly AbsolutePath PackageDirectory;

    private AbsolutePath PackageDir => PackageDirectory ?? RootDirectory / "packages";

    [Parameter("Include symbols package (default: true)")] public readonly bool IncludeSymbols = true;

    [Parameter("Include source package (default: true)")] public readonly bool IncludeSource = true;

    [Parameter] [Secret] string NugetApiKey;

    /// <summary>
    /// Creates NuGet packages for the library
    /// </summary>
    private Target Pack => td => td
        .DependsOn(Compile, ExtractChangelogLatestVersion)
        .Produces(PackageDir / "*.zip")
        .Executes(() =>
        {
            // Escape the release notes string
            var escapedReleaseNotes = (ChangelogLatestVersion ?? string.Empty)
                                      .Replace("\"", "\\\"")
                                      .Replace("\r", "")
                                      .Replace("\n", "\\n")
                                      .Replace(",", "_");
            
            DotNetTasks.DotNetPack(s => s
                .SetProject(Solution.SumTree)
                .SetConfiguration(ConfigurationSet)
                .SetOutputDirectory(PackageDir)
                .SetVersion(CurrentVersion)
                .SetAssemblyVersion(CurrentVersion)
                .SetInformationalVersion(CurrentVersion)
                .SetPackageReleaseNotes(escapedReleaseNotes)
                .SetNoBuild(true)
                .SetNoRestore(true)
                .SetIncludeSymbols(IncludeSymbols)
                .SetIncludeSource(IncludeSource)
                .SetSymbolPackageFormat(DotNetSymbolPackageFormat.snupkg)
            );
        });

    /// <summary>
    /// Publishes NuGet packages to nuget.org
    /// </summary>
    private Target Publish => td => td
        .DependsOn(Pack)
        .Requires(() => !string.IsNullOrEmpty(NugetApiKey))
        .Executes(() =>
        {
            var packageFiles = PackageDir.GlobFiles("*.nupkg")
                .Where(x => !x.Name.EndsWith(".symbols.nupkg"));

            foreach (var packageFile in packageFiles)
            {
                DotNetTasks.DotNetNuGetPush(s => s
                    .SetTargetPath(packageFile)
                    .SetSource("https://api.nuget.org/v3/index.json")
                    .SetApiKey(NugetApiKey)
                    .SetSkipDuplicate(true)
                );
            }
        });
}