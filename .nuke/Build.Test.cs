using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.ReportGenerator;
using Serilog;

namespace SumTree.Nuke;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for the build process.
/// </summary>
partial class Build
{
    private static AbsolutePath CoverageDirectory => RootDirectory / "coverage";
    private static AbsolutePath CoverageReportDirectory => CoverageDirectory / "report";
    private static AbsolutePath CoverageReportSummaryDirectory => CoverageReportDirectory / "Summary.txt";

    private AbsolutePath GetCoverageResultFile()
    {
        // Find the coverage.cobertura.xml file in the subdirectories
        var coverageFiles = CoverageDirectory.GlobFiles("**/coverage.cobertura.xml");
        return coverageFiles.FirstOrDefault() ?? CoverageDirectory / "coverage.xml";
    }

    private Target Test => td => td
        .After(Compile)
        .Executes(() =>
        {
            _ = CoverageDirectory.CreateDirectory();
            DotNetTasks.DotNetTest(settings => settings
                .SetProjectFile(Solution.SumTree_Tests)
                .SetConfiguration(ConfigurationSet)
                .SetResultsDirectory(CoverageDirectory)
                .SetDataCollector("XPlat Code Coverage")
                .SetLoggers("trx")
                .AddProperty("CollectCoverage", "true")
                .AddProperty("CoverletOutputFormat", "cobertura")
                .AddProperty("ExcludeByFile", "**/*.g.cs")
            );
        });

    public Target TestReport => td => td
        .DependsOn(Test)
        .Executes(() =>
        {
            var coverageResultFile = GetCoverageResultFile();
            if (!coverageResultFile.FileExists())
            {
                Log.Error("Coverage file not found: {0}", coverageResultFile);
                return;
            }

            _ = CoverageReportDirectory.CreateDirectory();
            _ = ReportGeneratorTasks.ReportGenerator(s => s
                .SetTargetDirectory(CoverageReportDirectory)
                .SetReportTypes(ReportTypes.Html, ReportTypes.TextSummary)
                .SetReports(coverageResultFile)
            );
            var summaryText = CoverageReportSummaryDirectory.ReadAllLines();
            Log.Information(string.Join(Environment.NewLine, summaryText));
        });
}
