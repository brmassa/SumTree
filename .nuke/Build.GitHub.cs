using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
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
    public readonly string GitHubToken;

    [Parameter("GitHub repository")] public readonly string GitHubRepository = "brmassa/SumTree";

    [Parameter("GitHub API URL")] private static readonly string GitHubApiBaseUrl = "https://api.github.com";

    private static string Date =>
        DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Creates a release in the GitHub repository.
    /// </summary>
    /// <see href="https://docs.github.com/en/rest/releases/releases#create-a-release"/>
    public Target GitHubCreateRelease => td => td
        .DependsOn(GitHubCreateTag, ExtractChangelogUnreleased)
        .OnlyWhenStatic(() => HasNewCommits)
        .Requires(() => GitHubToken)
        .Executes(async () =>
        {
            try
            {
                using var httpClient = HttpClientGitHubToken();
                var message = $"{ChangelogUnreleased}";
                var release = $"{TagName} / {Date}";
                var response = await httpClient.PostAsJsonAsync(
                    GitHubApiUrl($"repos/{GitHubRepository}/releases"),
                    new
                    {
                        tag_name = TagName,
                        name = release,
                        body = message,
                        draft = false,
                        prerelease = false
                    }).ConfigureAwait(false);

                _ = response.EnsureSuccessStatusCode();
                Log.Information(
                    "Release {release} created with the description '{message}'",
                    release, message);
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "{StatusCode}: {Message}", ex.StatusCode,
                    ex.Message);
                throw;
            }
        });

    /// <summary>
    /// Creates a tag in the GitHub repository.
    /// </summary>
    /// <see href="https://docs.github.com/en/rest/git/refs#create-a-reference"/>
    private Target GitHubCreateTag => td => td
        .DependsOn(CheckNewCommits, GitHubCreateCommit)
        .OnlyWhenStatic(() => HasNewCommits)
        .Requires(() => GitHubToken)
        .Executes(async () =>
        {
            try
            {
                using var httpClient = HttpClientGitHubToken();
                var message = $"Automatic tag creation: '{TagName}' in {Date}";
                var response = await httpClient.PostAsJsonAsync(
                    GitHubApiUrl($"repos/{GitHubRepository}/git/refs"),
                    new
                    {
                        @ref = $"refs/tags/{TagName}",
                        sha = GitTasks.GitCurrentCommit()
                    }).ConfigureAwait(false);

                _ = response.EnsureSuccessStatusCode();
                Log.Information(
                    "Tag {tag} created with the message '{message}'",
                    TagName, message);
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "{StatusCode}: {Message}", ex.StatusCode,
                    ex.Message);
                throw;
            }
        });

    private Target GitHubCreateCommit => td => td
        .DependsOn(CheckNewCommits, UpdateProjectVersions, UpdateChangelog)
        .OnlyWhenStatic(() => HasNewCommits)
        .Executes(() =>
        {
            // var commitMessage = $"chore: Automatic commit creation in {Date} [skip ci]";

            // Configure git user for CI/CD environment
            GitTasks.Git("config --global user.name `GitHub Actions` ");
            GitTasks.Git("config --global user.email `actions@github.com` ");

            // Use Git commands to commit changes locally
            GitTasks.Git("add .");
            GitTasks.Git($"""commit -m "chore: Automatic commit creation in {Date} [skip ci]" """);
            GitTasks.Git("push origin HEAD");

            Log.Information(
                "Commit in branch {branch} created and pushed",
                Repository?.Branch ?? "main");
        });

    /// <summary>
    /// Creates an HTTP client and set the authentication header.
    /// </summary>
    private HttpClient HttpClientGitHubToken()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {GitHubToken}");
        httpClient.DefaultRequestHeaders.Add("User-Agent", "SumTree-Build-System");
        return httpClient;
    }

    /// <summary>
    /// Generate the GitHub API URL.
    /// </summary>
    /// <param name="url">The URL to append to the base URL.</param>
    /// <returns></returns>
    private string GitHubApiUrl(string url)
    {
        var apiUrl = $"{GitHubApiBaseUrl}/{url}";
        Log.Information("GitHub API call: {url}", apiUrl);
        return apiUrl;
    }
}