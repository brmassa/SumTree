namespace Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--editor-session-diagnostics")
        {
            EditorSessionDiagnostics.Run(args.Length > 1 ? args[1] : "artifacts/editor-performance/editor-sessions.json");
            return;
        }

        if (args is ["--editor-validate"])
        {
            EditorDiagnostics.ValidateAll();
            return;
        }

        if (args.Length > 0 && args[0] == "--editor-diagnostics")
        {
            EditorDiagnostics.Run(args.Length > 1 ? args[1] : "docs/internal/benchmarks/editor-diagnostics.json");
            return;
        }

        var config = DefaultConfig.Instance.AddExporter(RPlotExporter.Default);
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        if (summaries.Any(summary => summary.HasCriticalValidationErrors || summary.Reports.Any(report => !report.Success)))
        {
            Environment.ExitCode = 1;
        }
    }
}
