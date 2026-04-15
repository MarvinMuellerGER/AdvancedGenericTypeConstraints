using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using CurrentAnalyzer = AdvancedGenericTypeConstraints.Analyzers.AdvancedGenericTypeConstraintAnalyzer;

namespace AdvancedGenericTypeConstraints.Analyzers.Benchmarks;

[ShortRunJob]
[MemoryDiagnoser]
public class AnalyzerIntegrationBenchmark
{
    private const int ScenarioCount = 300;

    private Compilation _compilation = null!;
    private DiagnosticAnalyzer _currentAnalyzer = null!;
    private int _expectedDiagnosticCount;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = BenchmarkCompilationFactory.CreateCompilation(ScenarioCount);
        _currentAnalyzer = new CurrentAnalyzer();
        _expectedDiagnosticCount = Analyze();
    }

    [Benchmark]
    public int Current() => Analyze();

    private int Analyze()
    {
        var diagnostics = _compilation.WithAnalyzers([_currentAnalyzer])
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();

        if (diagnostics.Length != _expectedDiagnosticCount && _expectedDiagnosticCount is not 0)
        {
            throw new InvalidOperationException(
                $"Expected {_expectedDiagnosticCount} diagnostics, but analyzer returned {diagnostics.Length}.");
        }

        return diagnostics.Length;
    }
}
