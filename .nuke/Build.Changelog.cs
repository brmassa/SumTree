using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Nuke.Common;
using Serilog;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for the updating the Changelog.
/// </summary>
partial class Build
{
    [Parameter("Repository compare link")]
    public string RepositoryCompareLink = "https://github.com/brmassa/SumTree/compare/";

    [Parameter("Changelog file")] public string ChangelogFile { get; set; } = "CHANGELOG.md";

    private const string UnreleasedSection = "## [Unreleased][]";

    [GeneratedRegex(@"## v\[(\d+\.\d+\.\d+)\]")]
    private static partial Regex VersionRegex();

    private Target UpdateChangelog => td => td
        .DependsOn(CheckNewCommits)
        .ProceedAfterFailure()
        .Executes(() =>
        {
            if (!File.Exists(ChangelogFile))
            {
                throw new FileNotFoundException($"Error: File '{ChangelogFile}' not found.");
            }

            var fileContents = File.ReadAllText(ChangelogFile);

            if (IsVersionAlreadyInChangelog(VersionFull, fileContents))
            {
                throw new InvalidOperationException($"Error: Version '{VersionFull}' already exists in the changelog.");
            }

            var previousVersion = GetPreviousVersion();
            if (previousVersion == VersionFull)
            {
                throw new InvalidOperationException($"Version {VersionFull} is the current one");
            }

            var newVersionSection =
                $@"{Environment.NewLine}## v[{VersionFull}][] {DateTime.UtcNow:yyyy-MM-dd}{Environment.NewLine}";
            var linkReference =
                $@"[{VersionFull}]: {GetVersionLink($"v{previousVersion}", $"v{VersionFull}")}{Environment.NewLine}";
            var unreleasedLink = $@"[Unreleased]: {GetVersionLink($"v{VersionFull}", "HEAD")}";

            fileContents = InsertTextAtIndex(fileContents, newVersionSection, UnreleasedSection,
                UnreleasedSection.Length + 1);
            fileContents = InsertTextAtIndex(fileContents, linkReference, $"[{previousVersion}]:", 0);

            fileContents = UpdateUnreleasedLink(fileContents, unreleasedLink, previousVersion);

            File.WriteAllText(ChangelogFile, fileContents);

            Log.Information("Successfully inserted version '{versionFull}' into the changelog.", VersionFull);
        });

    private bool IsVersionAlreadyInChangelog(string versionFull, string fileContents) =>
        VersionRegex().Matches(fileContents).Any(match => match.Groups[1].Value == versionFull);

    private static string InsertTextAtIndex(string fileContents, string newText, string reference, int charDelta)
    {
        var linkInsertIndex = fileContents.LastIndexOf(reference, StringComparison.CurrentCulture);
        if (linkInsertIndex == -1)
        {
            throw new InvalidOperationException("Could not find the correct position to insert the new text.");
        }

        return fileContents.Insert(linkInsertIndex + charDelta, newText);
    }

    private string UpdateUnreleasedLink(string fileContents, string unreleasedLink, string previousVersion)
    {
        var oldUnreleasedLink = $@"[Unreleased]: {GetVersionLink($"v{previousVersion}", "HEAD")}";
        return fileContents.Replace(oldUnreleasedLink, unreleasedLink, StringComparison.InvariantCulture);
    }

    private string GetPreviousVersion()
    {
        var versionPattern = VersionRegex();
        var fileContents = File.ReadAllText(ChangelogFile);

        var versionMatches = versionPattern.Matches(fileContents);

        if (versionMatches.Count == 0)
        {
            return "0.0.0";
        }

        // Return the first match, which is the most recent version
        return versionMatches[0].Groups[1].ToString();
    }

    private string GetVersionLink(string previousVersion, string currentVersion) =>
        $"{RepositoryCompareLink}{previousVersion}...{currentVersion}";

    [CanBeNull] string ChangelogLatestVersion;

    [CanBeNull] string ChangelogUnreleased;

    private Target ExtractUnreleasedChangelog => td => td
        .Before(UpdateChangelog)
        .Executes(() =>
        {
            if (!File.Exists(ChangelogFile))
            {
                throw new FileNotFoundException($"Error: File '{ChangelogFile}' not found.");
            }

            var fileContents = File.ReadAllText(ChangelogFile);
            ChangelogUnreleased = GetUnreleasedChangelog(fileContents);

            Log.Information("Unreleased changelog:");
            Log.Information("{changelog}", ChangelogUnreleased);
        });

    private string GetUnreleasedChangelog(string fileContents)
    {
        var lines = fileContents.Split([Environment.NewLine], StringSplitOptions.None);
        var startIndex = -1;
        var endIndex = -1;

        // Find the Unreleased section
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Equals(UnreleasedSection, StringComparison.OrdinalIgnoreCase))
            {
                startIndex = i;
                break;
            }
        }

        if (startIndex == -1)
        {
            return "No Unreleased section found in changelog.";
        }

        // Find the end of Unreleased section (next ## heading)
        for (int i = startIndex + 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("## "))
            {
                endIndex = i;
                break;
            }
        }

        if (endIndex == -1)
        {
            endIndex = lines.Length;
        }

        // Extract the changelog content, excluding the Unreleased header
        var changelogLines = lines[(startIndex + 1)..endIndex]
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        return changelogLines.Length == 0
            ? "No changes in Unreleased section."
            : string.Join(Environment.NewLine, changelogLines);
    }

    private Target ExtractLatestChangelog => td => td
        .Executes(() =>
        {
            if (!File.Exists(ChangelogFile))
            {
                throw new FileNotFoundException($"Error: File '{ChangelogFile}' not found.");
            }

            var fileContents = File.ReadAllText(ChangelogFile);
            ChangelogLatestVersion = GetLatestVersionChangelog(fileContents);

            Log.Information("Latest version changelog:");
            Log.Information("{changelog}", ChangelogLatestVersion);
        });

    private string GetLatestVersionChangelog(string fileContents)
    {
        var lines = fileContents.Split([Environment.NewLine], StringSplitOptions.None);
        var startIndex = -1;
        var endIndex = -1;

        // Find the first version section (skip Unreleased)
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("## v[") && lines[i].Contains("][]"))
            {
                startIndex = i;
                break;
            }
        }

        if (startIndex == -1)
        {
            return "No version sections found in changelog.";
        }

        // Find the end of this version section (next ## heading or end of content sections)
        for (int i = startIndex + 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("## ") || lines[i].StartsWith("[") && lines[i].Contains("]: "))
            {
                endIndex = i;
                break;
            }
        }

        if (endIndex == -1)
        {
            endIndex = lines.Length;
        }

        // Extract the changelog content, excluding the version header
        var changelogLines = lines[(startIndex + 1)..endIndex]
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        return string.Join(Environment.NewLine, changelogLines);
    }
}