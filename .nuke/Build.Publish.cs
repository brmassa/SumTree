using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Serilog;

namespace SumTree.Nuke;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for the library publish process (NuGet packaging).
/// </summary>
partial class Build
{
    [Parameter("Output directory for NuGet packages (default: ./packages)")]
    readonly AbsolutePath _packageDirectory;

    private AbsolutePath PackageDir => _packageDirectory ?? RootDirectory / "packages";

    [Parameter("Include symbols package (default: true)")]
    readonly bool _includeSymbols = true;

    [Parameter("Include source package (default: true)")]
    readonly bool _includeSource = true;

    [Parameter("Override version for packaging")]
    readonly string _versionOverride;

    [Parameter]
    [Secret]
    readonly string _nugetApiKey;

    [Parameter("NuGet source URL (default: nuget.org)")]
    readonly string _nugetSource = "https://api.nuget.org/v3/index.json";

    [Parameter("Skip duplicate packages when publishing")]
    readonly bool _skipDuplicate = true;

    [Parameter("Perform dry run (validate but don't publish)")]
    readonly bool _dryRun;

    /// <summary>
    /// Creates NuGet packages for the library
    /// </summary>
    Target Pack => _ => _
        .DependsOn(Compile, ExtractChangelogLatestVersion, ValidatePackaging)
        .Produces(PackageDir / "*.nupkg")
        .Executes(() =>
        {
            Log.Information("Creating NuGet packages...");

            // Ensure package directory exists
            PackageDir.CreateDirectory();

            // Determine version to use
            var packageVersion = _versionOverride ?? VersionFull;
            Log.Information("Using version: {Version}", packageVersion);

            // Prepare release notes
            var releaseNotes = PrepareReleaseNotes();
            Log.Information("Release notes prepared ({Length} characters)", releaseNotes.Length);

            try
            {
                DotNetTasks.DotNetPack(s => s
                    .SetProject(_solution.SumTree)
                    .SetConfiguration(ConfigurationSet)
                    .SetOutputDirectory(PackageDir)
                    .SetVersion(packageVersion)
                    .SetAssemblyVersion(packageVersion)
                    .SetInformationalVersion(packageVersion)
                    .SetPackageReleaseNotes(releaseNotes)
                    .SetNoBuild(true)
                    .SetNoRestore(true)
                    .SetIncludeSymbols(_includeSymbols)
                    .SetIncludeSource(_includeSource)
                    .SetSymbolPackageFormat(DotNetSymbolPackageFormat.snupkg)
                    .SetVerbosity(DotNetVerbosity.normal)
                );

                // Verify packages were created
                var packageFiles = PackageDir.GlobFiles("*.nupkg");
                if (!packageFiles.Any())
                {
                    throw new InvalidOperationException("No NuGet packages were created");
                }

                Log.Information("Successfully created {Count} package(s):", packageFiles.Count);
                foreach (var package in packageFiles)
                {
                    var size = new System.IO.FileInfo(package).Length;
                    Log.Information("  - {Name} ({Size:N0} bytes)", package.Name, size);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create NuGet packages");
                throw;
            }
        });

    /// <summary>
    /// Publishes NuGet packages to the configured source
    /// </summary>
    Target Publish => _ => _
        .DependsOn(Pack, ValidatePublishing)
        .Requires(() => !string.IsNullOrEmpty(_nugetApiKey) || _dryRun)
        .Executes(() =>
        {
            var packageFiles = PackageDir.GlobFiles("*.nupkg")
                .Where(x => !x.Name.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (!packageFiles.Any())
            {
                Log.Warning("No packages found to publish in {Directory}", PackageDir);
                return;
            }

            Log.Information("Found {Count} package(s) to publish:", packageFiles.Length);
            foreach (var package in packageFiles)
            {
                Log.Information("  - {Name}", package.Name);
            }

            if (_dryRun)
            {
                Log.Information("DRY RUN: Would publish packages to {Source}", _nugetSource);
                return;
            }

            Log.Information("Publishing packages to {Source}...", _nugetSource);

            var publishedCount = 0;
            var skippedCount = 0;
            var failedCount = 0;

            foreach (var packageFile in packageFiles)
            {
                try
                {
                    Log.Information("Publishing {Package}...", packageFile.Name);

                    DotNetTasks.DotNetNuGetPush(s => s
                        .SetTargetPath(packageFile)
                        .SetSource(_nugetSource)
                        .SetApiKey(_nugetApiKey)
                        .SetSkipDuplicate(_skipDuplicate)
                        .SetTimeout(300) // 5 minutes timeout
                    );

                    Log.Information("Successfully published {Package}", packageFile.Name);
                    publishedCount++;
                }
                catch (Exception ex) when (ex.Message.Contains("409") || ex.Message.Contains("already exists"))
                {
                    Log.Warning("Package {Package} already exists, skipping", packageFile.Name);
                    skippedCount++;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to publish {Package}", packageFile.Name);
                    failedCount++;

                    if (!_skipDuplicate)
                    {
                        throw;
                    }
                }
            }

            // Summary
            Log.Information("Publishing summary:");
            Log.Information("  - Published: {Published}", publishedCount);
            Log.Information("  - Skipped: {Skipped}", skippedCount);
            Log.Information("  - Failed: {Failed}", failedCount);

            if (failedCount > 0 && publishedCount == 0)
            {
                throw new InvalidOperationException($"Failed to publish any packages ({failedCount} failures)");
            }
        });

    /// <summary>
    /// Validates that packaging can proceed
    /// </summary>
    Target ValidatePackaging => _ => _
        .Before(Pack)
        .Executes(() =>
        {
            Log.Information("Validating packaging prerequisites...");

            if (_solution.SumTree == null)
            {
                throw new InvalidOperationException("Main project (SumTree) not found in solution");
            }

            // Check if project file exists
            if (!_solution.SumTree.Path.FileExists())
            {
                throw new InvalidOperationException($"Project file not found: {_solution.SumTree.Path}");
            }

            // Validate version format
            var versionToUse = _versionOverride ?? VersionFull;
            if (string.IsNullOrWhiteSpace(versionToUse))
            {
                throw new InvalidOperationException("No valid version available for packaging");
            }

            if (!IsValidSemanticVersion(versionToUse))
            {
                throw new InvalidOperationException($"Invalid version format: {versionToUse}");
            }

            Log.Information("Packaging validation completed successfully");
            Log.Information("  - Project: {Project}", _solution.SumTree.Name);
            Log.Information("  - Version: {Version}", versionToUse);
            Log.Information("  - Output: {Directory}", PackageDir);
        });

    /// <summary>
    /// Validates that publishing can proceed
    /// </summary>
    Target ValidatePublishing => _ => _
        .Before(Publish)
        .Executes(() =>
        {
            Log.Information("Validating publishing prerequisites...");

            if (_dryRun)
            {
                Log.Information("Dry run mode - skipping API key validation");
                return;
            }

            if (string.IsNullOrWhiteSpace(_nugetApiKey))
            {
                throw new InvalidOperationException("NuGet API key is required for publishing");
            }

            if (string.IsNullOrWhiteSpace(_nugetSource))
            {
                throw new InvalidOperationException("NuGet source URL is required");
            }

            // Validate API key format (basic check)
            if (_nugetApiKey.Length < 10)
            {
                throw new InvalidOperationException("NuGet API key appears to be invalid (too short)");
            }

            Log.Information("Publishing validation completed successfully");
            Log.Information("  - Source: {Source}", _nugetSource);
            Log.Information("  - Skip duplicates: {Skip}", _skipDuplicate);
        });

    /// <summary>
    /// Prepares release notes for the package, escaping special characters
    /// </summary>
    private string PrepareReleaseNotes()
    {
        var rawNotes = _changelogLatestVersion ?? "No release notes available.";

        // Escape special characters for MSBuild
        var escapedNotes = rawNotes
            .Replace("\"", "\\\"")
            .Replace("\r\n", "\\n")
            .Replace("\r", "\\n")
            .Replace("\n", "\\n")
            .Replace(",", "&#44;")
            .Replace(";", "&#59;");

        // Truncate if too long (NuGet has limits)
        const int maxLength = 4000;
        if (escapedNotes.Length > maxLength)
        {
            escapedNotes = escapedNotes.Substring(0, maxLength - 50) + "... (truncated)";
            Log.Warning("Release notes truncated to {Length} characters", escapedNotes.Length);
        }

        return escapedNotes;
    }

    /// <summary>
    /// Validates if a string is a valid semantic version
    /// </summary>
    private static bool IsValidSemanticVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var parts = version.Split('.');
        if (parts.Length < 2 || parts.Length > 4)
        {
            return false;
        }

        // Check that first 3 parts are valid integers
        for (var i = 0; i < Math.Min(3, parts.Length); i++)
        {
            if (!int.TryParse(parts[i], out var number) || number < 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Lists all packages in the package directory
    /// </summary>
    Target ListPackages => _ => _
        .Executes(() =>
        {
            if (!PackageDir.DirectoryExists())
            {
                Log.Information("Package directory does not exist: {Directory}", PackageDir);
                return;
            }

            var packages = PackageDir.GlobFiles("*.nupkg");
            if (!packages.Any())
            {
                Log.Information("No packages found in {Directory}", PackageDir);
                return;
            }

            Log.Information("Found {Count} package(s) in {Directory}:", packages.Count, PackageDir);
            foreach (var package in packages.OrderBy(p => p.Name))
            {
                var info = new System.IO.FileInfo(package);
                Log.Information("  - {Name} ({Size:N0} bytes, {Modified:yyyy-MM-dd HH:mm:ss})",
                    package.Name, info.Length, info.LastWriteTime);
            }
        });
}
