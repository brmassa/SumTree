using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;

namespace SumTree.Nuke;

/// <summary>
/// This is the main build file for the project.
/// </summary>
[ShutdownDotNetAfterServerBuild]
// 1. Continuous Integration - runs on every commit
[GitHubActions(
    "continuous-integration",
    GitHubActionsImage.UbuntuLatest,
    FetchDepth = 0,
    AutoGenerate = false, // Disable because it's too buggy
    On = [GitHubActionsTrigger.Push, GitHubActionsTrigger.PullRequest],
    OnPushBranches = ["main", "develop"],
    OnPullRequestBranches = ["main"],
    InvokedTargets = [nameof(TestReport), nameof(Pack)],
    PublishArtifacts = true
)]

// 2. Weekly Release - Friday 13:00 UTC (10:00 BRT)
[GitHubActions(
    "weekly-release",
    GitHubActionsImage.UbuntuLatest,
    FetchDepth = 0,
    AutoGenerate = false, // Disable because it's too buggy
    OnCronSchedule = "0 13 * * 5", // Friday 13:00 UTC
    On = [GitHubActionsTrigger.WorkflowDispatch],
    InvokedTargets = [nameof(Test), nameof(GitHubCreateRelease)],
    ImportSecrets = [nameof(GitHubToken)],
    PublishArtifacts = true
)]

// 3. Auto Publish on Tag - triggers when a tag is created
[GitHubActions(
    "publish-on-tag",
    GitHubActionsImage.UbuntuLatest,
    FetchDepth = 0,
    AutoGenerate = false, // Disable because it's too buggy
    OnPushTags = ["v*"],
    On = [GitHubActionsTrigger.WorkflowDispatch],
    InvokedTargets = [nameof(Restore), nameof(Compile), nameof(Pack), nameof(Publish)],
    ImportSecrets = [nameof(NugetApiKey)],
    PublishArtifacts = true
)]

// 4. Manual Publish
[GitHubActions(
    "manual-publish",
    GitHubActionsImage.UbuntuLatest,
    On = [GitHubActionsTrigger.WorkflowDispatch],
    FetchDepth = 0,
    AutoGenerate = false, // Disable because it's too buggy
    InvokedTargets = [nameof(Restore), nameof(Compile), nameof(Pack), nameof(Publish)],
    ImportSecrets = [nameof(NugetApiKey)],
    PublishArtifacts = true
)]
internal sealed partial class Build : NukeBuild
{
    /// <summary>
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode
    /// </summary>
    public static int Main() => Execute<Build>(x => x.Pack);
}
