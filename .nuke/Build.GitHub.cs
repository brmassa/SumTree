using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Nuke.Common;
using Nuke.Common.Tools.Git;
using Serilog;

namespace SumTree.Nuke;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible integrating the GitHub CI/CD.
/// </summary>
partial class Build
{
    /// <summary>
    /// The GitHub CI/CD variables are injected by Nuke.
    /// </summary>
    [Parameter("GitHub token")]
    readonly string GitHubToken;

    [Parameter("GitHub repository")]
    readonly string GitHubRepository = "brmassa/SumTree";

    [Parameter("GitHub API URL")]
    static readonly string GitHubApiBaseUrl = "https://api.github.com";

    [Parameter("Skip CI checks (add [skip ci] to commit message)")]
    readonly bool SkipCi = true;

    static string Date =>
        DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Creates a release in the GitHub repository.
    /// </summary>
    /// <see href="https://docs.github.com/en/rest/releases/releases#create-a-release"/>
    public Target GitHubCreateRelease => _ => _
        .DependsOn(GitHubCreateTag, ExtractChangelogUnreleased)
        .OnlyWhenStatic(() => HasNewCommits)
        .Requires(() => GitHubToken)
        .Executes(async () =>
        {
            if (string.IsNullOrWhiteSpace(ChangelogUnreleased) || ChangelogUnreleased == "No changes in Unreleased section.")
            {
                Log.Warning("No unreleased changes found in changelog. Skipping release creation.");
                return;
            }

            var releaseName = $"{TagName} / {Date}";
            Log.Information("Creating GitHub release: {Release}", releaseName);

            try
            {
                using var httpClient = CreateHttpClientWithAuth();
                var releaseData = new
                {
                    tag_name = TagName,
                    name = releaseName,
                    body = ChangelogUnreleased,
                    draft = false,
                    prerelease = IsPrerelease(TagName),
                    generate_release_notes = false
                };

                var response = await httpClient.PostAsJsonAsync(
                    GitHubApiUrl($"repos/{GitHubRepository}/releases"),
                    releaseData).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    Log.Information("Successfully created release: {Release}", releaseName);
                    var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Log.Debug("GitHub API response: {Response}", responseContent);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Log.Error("Failed to create release. Status: {Status}, Response: {Response}",
                        response.StatusCode, errorContent);

                    if (response.StatusCode == HttpStatusCode.UnprocessableEntity && errorContent.Contains("already_exists"))
                    {
                        Log.Warning("Release {Release} already exists, continuing...", releaseName);
                        return;
                    }

                    response.EnsureSuccessStatusCode();
                }
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "HTTP error creating GitHub release: {StatusCode}: {Message}",
                    ex.Data["StatusCode"], ex.Message);
                throw;
            }
            catch (TaskCanceledException ex)
            {
                Log.Error(ex, "Timeout creating GitHub release");
                throw new InvalidOperationException("GitHub API request timed out", ex);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error creating GitHub release");
                throw;
            }
        });

    /// <summary>
    /// Creates a tag in the GitHub repository.
    /// </summary>
    /// <see href="https://docs.github.com/en/rest/git/refs#create-a-reference"/>
    Target GitHubCreateTag => _ => _
        .DependsOn(CheckNewCommits, GitHubCreateCommit)
        .OnlyWhenStatic(() => HasNewCommits)
        .Requires(() => GitHubToken)
        .Executes(async () =>
        {
            // Check if tag already exists
            if (await TagExistsAsync(TagName).ConfigureAwait(false))
            {
                Log.Warning("Tag {Tag} already exists, skipping tag creation", TagName);
                return;
            }

            var currentSha = GitTasks.GitCurrentCommit();
            Log.Information("Creating tag {Tag} at commit {Sha}", TagName, currentSha[..8]);

            try
            {
                using var httpClient = CreateHttpClientWithAuth();
                var tagData = new
                {
                    @ref = $"refs/tags/{TagName}",
                    sha = currentSha
                };

                var response = await httpClient.PostAsJsonAsync(
                    GitHubApiUrl($"repos/{GitHubRepository}/git/refs"),
                    tagData).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    Log.Information("Successfully created tag: {Tag}", TagName);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Log.Error("Failed to create tag. Status: {Status}, Response: {Response}",
                        response.StatusCode, errorContent);

                    if (response.StatusCode == HttpStatusCode.UnprocessableEntity && errorContent.Contains("already exists"))
                    {
                        Log.Warning("Tag {Tag} already exists, continuing...", TagName);
                        return;
                    }

                    response.EnsureSuccessStatusCode();
                }
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "HTTP error creating GitHub tag: {StatusCode}: {Message}",
                    ex.Data["StatusCode"], ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error creating GitHub tag");
                throw;
            }
        });

    /// <summary>
    /// Creates and pushes a commit with version updates and changelog changes.
    /// </summary>
    Target GitHubCreateCommit => _ => _
        .DependsOn(CheckNewCommits, UpdateProjectVersions, UpdateChangelog)
        .OnlyWhenStatic(() => HasNewCommits)
        .Executes(() =>
        {
            var commitMessage = SkipCi
                ? $"chore: Automatic version bump to {VersionFull} [skip ci]"
                : $"chore: Automatic version bump to {VersionFull}";

            Log.Information("Creating commit with message: {Message}", commitMessage);

            try
            {
                // Configure git user for CI/CD environment
                ConfigureGitUser();

                // Check if there are any changes to commit
                var status = GitTasks.Git("status --porcelain").Select(x => x.Text).ToList();
                if (!status.Any())
                {
                    Log.Information("No changes to commit, skipping commit creation");
                    return;
                }

                Log.Information("Found {Count} changed file(s):", status.Count);
                foreach (var change in status.Take(10)) // Limit output
                {
                    Log.Information("  {Change}", change);
                }

                if (status.Count > 10)
                {
                    Log.Information("  ... and {More} more", status.Count - 10);
                }

                // Stage all changes
                GitTasks.Git("add .");

                // Create commit
                GitTasks.Git($"commit -m \"{commitMessage}\"");

                // Push to remote
                var currentBranch = Repository?.Branch ?? "main";
                Log.Information("Pushing commit to branch: {Branch}", currentBranch);
                GitTasks.Git($"push origin {currentBranch}");

                Log.Information("Successfully created and pushed commit");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create or push commit");

                // Try to get more details about the error
                try
                {
                    var gitStatus = GitTasks.Git("status").Select(x => x.Text);
                    Log.Information("Git status: {Status}", string.Join(Environment.NewLine, gitStatus));
                }
                catch
                {
                    // Ignore errors getting status
                }

                throw;
            }
        });

    /// <summary>
    /// Validates GitHub integration prerequisites.
    /// </summary>
    Target ValidateGitHubIntegration => _ => _
        .Before(GitHubCreateCommit)
        .Executes(() =>
        {
            Log.Information("Validating GitHub integration prerequisites...");

            // Check git configuration
            try
            {
                var remoteOutputs = GitTasks.Git("remote get-url origin");
                var remoteUrl = remoteOutputs.Any() ? remoteOutputs.First().Text : "Not found";
                Log.Information("Git remote URL: {Url}", remoteUrl);

                var branchOutputs = GitTasks.Git("branch --show-current");
                var currentBranch = Repository?.Branch ?? (branchOutputs.Any() ? branchOutputs.First().Text : "Unknown");
                Log.Information("Current branch: {Branch}", currentBranch);

                var commitOutputs = GitTasks.Git("log -1 --oneline");
                var lastCommit = commitOutputs.Any() ? commitOutputs.First().Text : "Not found";
                Log.Information("Last commit: {Commit}", lastCommit);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not retrieve git information");
            }

            // Validate GitHub token if provided
            if (!string.IsNullOrWhiteSpace(GitHubToken))
            {
                if (GitHubToken.Length < 20)
                {
                    Log.Warning("GitHub token appears to be too short");
                }
                else
                {
                    Log.Information("GitHub token is available ({Length} characters)", GitHubToken.Length);
                }
            }

            // Validate repository format
            if (!GitHubRepository.Contains("/"))
            {
                throw new InvalidOperationException($"Invalid GitHub repository format: {GitHubRepository}. Expected format: owner/repo");
            }

            Log.Information("GitHub integration validation completed");
        });

    /// <summary>
    /// Creates an HTTP client and sets the authentication header.
    /// </summary>
    HttpClient CreateHttpClientWithAuth()
    {
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(2)
        };

        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {GitHubToken}");
        httpClient.DefaultRequestHeaders.Add("User-Agent", "SumTree-Build-System/1.0");
        httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

        return httpClient;
    }

    /// <summary>
    /// Generates the GitHub API URL.
    /// </summary>
    /// <param name="endpoint">The API endpoint to append to the base URL.</param>
    /// <returns>Complete GitHub API URL</returns>
    string GitHubApiUrl(string endpoint)
    {
        var apiUrl = $"{GitHubApiBaseUrl}/{endpoint.TrimStart('/')}";
        Log.Debug("GitHub API URL: {Url}", apiUrl);
        return apiUrl;
    }

    /// <summary>
    /// Configures git user information for commits.
    /// </summary>
    void ConfigureGitUser()
    {
        try
        {
            // Use GitHub Actions bot identity
            GitTasks.Git("config user.name \"github-actions[bot]\"");
            GitTasks.Git("config user.email \"github-actions[bot]@users.noreply.github.com\"");

            Log.Debug("Configured git user for GitHub Actions");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not configure git user");
            throw;
        }
    }

    /// <summary>
    /// Checks if a tag already exists in the repository.
    /// </summary>
    /// <param name="tagName">The tag name to check</param>
    /// <returns>True if the tag exists, false otherwise</returns>
    async Task<bool> TagExistsAsync(string tagName)
    {
        try
        {
            using var httpClient = CreateHttpClientWithAuth();
            var response = await httpClient.GetAsync(
                GitHubApiUrl($"repos/{GitHubRepository}/git/refs/tags/{tagName}")
            ).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                Log.Debug("Tag {Tag} exists in GitHub", tagName);
                return true;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Log.Debug("Tag {Tag} does not exist in GitHub", tagName);
                return false;
            }

            Log.Warning("Unexpected response checking tag existence: {Status}", response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not check if tag {Tag} exists, assuming it doesn't", tagName);
            return false;
        }
    }

    /// <summary>
    /// Determines if a version should be marked as prerelease.
    /// </summary>
    /// <param name="version">The version string</param>
    /// <returns>True if this should be a prerelease</returns>
    static bool IsPrerelease(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        var versionLower = version.ToLowerInvariant();
        return versionLower.Contains("alpha") ||
               versionLower.Contains("beta") ||
               versionLower.Contains("rc") ||
               versionLower.Contains("preview") ||
               versionLower.Contains("pre");
    }
}
