using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.Git;
using Nuke.Common.Tools.GitVersion;
using Serilog;

namespace SumTree.Nuke;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for the versioning using GitVersion.
/// </summary>
partial class Build
{
    [GitRepository] private readonly GitRepository Repository;

    [GitVersion] private readonly GitVersion GitVersion;

    /// <summary>
    /// The current version, using GitVersion.
    /// </summary>
    private string VersionFull => GitVersion?.SemVer ?? "1.0.0";

    /// <summary>
    /// The current version for use in build systems, using GitVersion.
    /// </summary>
    private string CurrentVersion => GitVersion?.AssemblySemVer ?? "1.0.0.0";

    private string VersionMajor => GitVersion?.Major.ToString(CultureInfo.InvariantCulture) ?? "1";

    private string VersionMajorMinor =>
        $"{GitVersion?.Major ?? 1}.{GitVersion?.Minor ?? 0}";

    /// <summary>
    /// The version in a format that can be used as a tag.
    /// </summary>
    private string TagName => $"v{VersionFull}";

    /// <summary>
    /// Checks if there are new commits since the last tag.
    /// </summary>
    private bool HasNewCommits => GitVersion?.CommitsSinceVersionSource != "0";

    private string CurrentTag => $"v{VersionFull}";

    private string CurrentFullVersion => VersionFull;

    /// <summary>
    /// Prints the current version.
    /// </summary>
    private Target ShowCurrentVersion => td => td
        .Executes(() =>
        {
            Log.Information("Current version:  {Version}", CurrentFullVersion);
            Log.Information("Current tag:      {Version}", CurrentTag);
            Log.Information("Next version:     {Version}", VersionFull);
        });

    /// <summary>
    /// Checks if there are new commits since the last tag.
    /// If there are no new commits, the whole publish process is skipped.
    /// </summary>
    private Target CheckNewCommits => td => td
        .DependsOn(ShowCurrentVersion)
        .Executes(() =>
        {
            Log.Information("Next version:    {Version}", TagName);

            // If there are no new commits since the last tag, skip tag creation
            // Nuke will stop here and not execute any of the following targets
            Log.Information(HasNewCommits
                ? $"There are {GitVersion?.CommitsSinceVersionSource ?? "unknown"} new commits since last tag."
                : "No new commits since last tag. Skipping tag creation.");
        });

    /// <summary>
    /// Update each project Version
    /// </summary>
    private Target UpdateProjectVersions => td => td
        .DependsOn(CheckNewCommits)
        .Executes(() =>
        {
            Log.Information("Projects: {ProjectsCount}",
                Solution.Projects.Count);
            List<string> projectsVersioned = [Solution.SumTree];
            Solution.Projects
                // Filter logic
                .Where(p => projectsVersioned.Contains(p.Path))
                .ToList()
                .ForEach(project =>
                {
                    Log.Information(
                        "{project}:\tfrom {version} to {VersionFull}",
                        project.Name,
                        project.GetProperty("Version"), VersionFull);
                    var msbuildProject = project.GetMSBuildProject();
                    msbuildProject.SetProperty("Version", VersionFull);
                    msbuildProject.Save(project.Path);
                });
        });

    public Target CreateCommit => td => td
        .DependsOn(CheckNewCommits, UpdateProjectVersions)
        .OnlyWhenStatic(() => HasNewCommits)
        .Executes(() =>
        {
            try
            {
                // Add all the changes to the current branch
                GitTasks.Git("add -A");

                // Commit the changes to the current branch
                GitTasks.Git("config --global user.name \"github-actions\"");
                GitTasks.Git("config --global user.email \"github-actions@github.com\"");
                GitTasks.Git($"commit -m \"chore: Automatic commit creation: {Date}\"");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating commit");
                throw;
            }
        });
}