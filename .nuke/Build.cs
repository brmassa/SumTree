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
    GitHubActionsImage.UbuntuLatest,
    FetchDepth = 0,
    AutoGenerate = false,
    On = [GitHubActionsTrigger.Push, GitHubActionsTrigger.PullRequest],
    InvokedTargets = [nameof(TestReport), nameof(Compile), nameof(Restore), nameof(Pack)]
)]

// 2. Weekly Release - Friday 10 AM EST (2 PM UTC)
[GitHubActions(
    "weekly-release",
    GitHubActionsImage.UbuntuLatest,
    GitHubActionsImage.UbuntuLatest,
    FetchDepth = 0,
    AutoGenerate = false,
    OnCronSchedule = "0 16 * * 4", // Thursday 16:00 UTC = 13:00 BRT
    OnPushBranches = ["main"],
    InvokedTargets = [nameof(Test), nameof(GitHubCreateRelease)]
)]

// 3. Auto Publish on Tag - triggers when a tag is created
[GitHubActions(
    "publish-on-tag",
    GitHubActionsImage.UbuntuLatest,
    FetchDepth = 0,
    AutoGenerate = false,
    OnPushTags = ["v*"],
    OnPushBranches = ["main"],
    InvokedTargets = [nameof(Pack), nameof(Publish)],
    ImportSecrets = [nameof(NugetApiKey)]
)]

// 4. Manual Publish
[GitHubActions(
    "manual-publish",
    GitHubActionsImage.UbuntuLatest,
    On = [GitHubActionsTrigger.WorkflowDispatch],
    FetchDepth = 0,
    AutoGenerate = false,
    InvokedTargets = [nameof(Pack), nameof(Publish)],
    ImportSecrets = [nameof(NugetApiKey)]
)]
internal sealed partial class Build : NukeBuild
{
    private static int Main() => Execute<Build>(x => x.Pack);
}