using System;
using System.Linq;
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
    /// <summary>
    /// Cleans all build outputs and temporary directories.
    /// </summary>
    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            Log.Information("Cleaning build outputs...");

            var directoriesToClean = new[]
            {
                _solution.SumTree?.Directory,
                _solution.SumTree_Tests?.Directory,
                _solution.SumTree_Benchmarks?.Directory
            }.Where(d => d != null);

            foreach (var directory in directoriesToClean)
            {
                directory.GlobDirectories("**/bin", "**/obj", "**/output")
                    .ForEach(path =>
                    {
                        Log.Debug("Deleting directory: {Directory}", path);
                        path.DeleteDirectory();
                    });
            }

            // Clean package and coverage directories
            var rootDirectoriesToClean = new[]
            {
                RootDirectory / "packages",
                CoverageDirectory,
                RootDirectory / "TestResults"
            };

            foreach (var directory in rootDirectoriesToClean)
            {
                if (directory.DirectoryExists())
                {
                    Log.Debug("Deleting root directory: {Directory}", directory);
                    directory.DeleteDirectory();
                }
            }

            Log.Information("Clean completed successfully");
        });

    /// <summary>
    /// Restores NuGet packages for the solution.
    /// </summary>
    Target Restore => _ => _
        .After(Clean)
        .Executes(() =>
        {
            Log.Information("Restoring NuGet packages...");

            DotNetTasks.DotNetRestore(s => s
                .SetProjectFile(_solution)
                .SetVerbosity(DotNetVerbosity.normal)
                .SetNoCache(IsServerBuild) // Disable cache on CI/CD for consistency
            );

            Log.Information("Package restoration completed successfully");
        });

    /// <summary>
    /// Compiles the solution.
    /// </summary>
    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            Log.Information("Compiling solution with configuration: {Configuration}", ConfigurationSet);

            DotNetTasks.DotNetBuild(s => s
                .SetProjectFile(_solution)
                .SetConfiguration(ConfigurationSet)
                .EnableNoRestore()
                .SetVerbosity(DotNetVerbosity.normal)
                .AddProperty("TreatWarningsAsErrors", IsServerBuild.ToString())
                .AddProperty("WarningsAsErrors", "")
                .AddProperty("WarningsNotAsErrors", "CS1591") // Allow missing XML documentation warnings
            );

            Log.Information("Compilation completed successfully");
        });

    /// <summary>
    /// Validates that all required projects are present in the solution.
    /// </summary>
    Target ValidateSolution => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            Log.Information("Validating solution structure...");

            var requiredProjects = new[]
            {
                (_solution.SumTree, "SumTree"),
                (_solution.SumTree_Tests, "SumTree.Tests")
            };

            var missingProjects = requiredProjects
                .Where(p => p.Item1 == null)
                .Select(p => p.Item2)
                .ToList();

            if (missingProjects.Any())
            {
                Log.Error("Missing required projects: {Projects}", string.Join(", ", missingProjects));
                throw new InvalidOperationException($"Solution validation failed. Missing projects: {string.Join(", ", missingProjects)}");
            }

            Log.Information("Solution validation completed successfully");
            Log.Information("Found projects:");
            Log.Information("  - Main: {Project}", _solution.SumTree?.Name ?? "Not found");
            Log.Information("  - Tests: {Project}", _solution.SumTree_Tests?.Name ?? "Not found");

            if (_solution.SumTree_Benchmarks != null)
            {
                Log.Information("  - Benchmarks: {Project}", _solution.SumTree_Benchmarks.Name);
            }
        });

    /// <summary>
    /// Full build pipeline that includes cleaning, restoring, and compiling.
    /// </summary>
    public Target FullBuild => _ => _
        .DependsOn(ValidateSolution, Clean, Restore, Compile)
        .Executes(() => Log.Information("Full build pipeline completed successfully"));
}
