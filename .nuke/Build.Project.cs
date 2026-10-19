namespace Build;

/// <summary>
/// The only build file that differs between the MASS4 repositories besides the feature partials: it names this
/// project and wires its features into the shared build. Names of projects, repositories and products live here.
/// </summary>
sealed partial class Build
{
    const string ProductName = "SumTree";
    const string DefaultGithubRepository = "brmassa/SumTree";
    const string DefaultGitlabProjectPath = "brmassa/sumtree";
    const string ReleaseBotEmail = "massa+github@brunomassa.com";
    const string NuGetPackagePrefix = "com.BrunoMassa.";
    const string PackageAuthors = "Bruno Massa";
    const string PackageCopyrightOwner = "Bruno Massa";
    const string PackageProjectUrl = "https://github.com/brmassa/SumTree";
    const string PackageRepositoryUrl = "https://github.com/brmassa/SumTree.git";
    const string PackageTags = "sumtree prefix-sum tree treemap treeset data-structure";

    const bool DefaultPublishSelfContained = false;
    const bool DefaultPublishSingleFile = false;
    const bool DefaultPublishTrimmed = false;

    const string DefaultConfiguration = "Release";
    const string TestReportArgument = "--report-trx";

    IEnumerable<Project> ReleaseProjects => PackableProjects;
    IEnumerable<Project> VersionedProjects => PackableProjects;

    AbsolutePath PublishOutput(Project project) => PublishDir / project.Name;
    Dictionary<string, object> MsBuildProperties => [];
    Target[] PackDependencies => [Publish];
    Target[] ReleaseCommitDependencies => [UpdateProjectVersions];

    [Parameter("Framework for published release archives (default: net10.0). NuGet packages include all target frameworks.")]
    readonly string PublishFramework = "net10.0";

    partial void ConfigurePublish(Project project, ref DotNetPublishSettings settings) =>
        settings = settings.SetFramework(PublishFramework);
}
