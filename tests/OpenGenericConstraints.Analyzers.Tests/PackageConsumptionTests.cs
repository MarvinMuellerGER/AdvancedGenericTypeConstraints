using System.Diagnostics;

namespace OpenGenericConstraints.Analyzers.Tests;

public class PackageConsumptionTests
{
    [Fact]
    public async Task ReportsCompilerError_When_ConsumerUsesPublishedPackageVersion()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), "OpenGenericConstraints.PackageConsumption", Guid.NewGuid().ToString("N"));
        var packageSource = Path.Combine(tempRoot, "packages");
        var consumerProjectDirectory = Path.Combine(tempRoot, "consumer");

        Directory.CreateDirectory(packageSource);
        Directory.CreateDirectory(consumerProjectDirectory);

        try
        {
            await RunDotNetCommandAsync(
                repositoryRoot,
                $"pack src/OpenGenericConstraints.Abstractions/OpenGenericConstraints.Abstractions.csproj -c Release -o \"{packageSource}\" -p:PackageVersion=0.1.0");

            await RunDotNetCommandAsync(
                repositoryRoot,
                $"pack src/OpenGenericConstraints.Analyzers/OpenGenericConstraints.Analyzers.csproj -c Release -o \"{packageSource}\" -p:PackageVersion=0.1.0");

            await File.WriteAllTextAsync(
                Path.Combine(consumerProjectDirectory, "NuGet.Config"),
                $$"""
                  <?xml version="1.0" encoding="utf-8"?>
                  <configuration>
                    <packageSources>
                      <clear />
                      <add key="local" value="{{packageSource}}" />
                      <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                    </packageSources>
                  </configuration>
                  """);

            await File.WriteAllTextAsync(
                Path.Combine(consumerProjectDirectory, "Consumer.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="OpenGenericConstraints.Abstractions" Version="0.1.0" />
                    <PackageReference Include="OpenGenericConstraints.Analyzers" Version="0.1.0" PrivateAssets="all" />
                  </ItemGroup>
                </Project>
                """);

            await File.WriteAllTextAsync(
                Path.Combine(consumerProjectDirectory, "Program.cs"),
                """
                using OpenGenericConstraints;

                public interface IHandleMessages<TMessage>
                {
                }

                public interface IFeatureRegistry
                {
                    void Register<[MustImplementOpenGeneric(typeof(IHandleMessages<>))] THandler>();
                }

                public sealed class MyHandler
                {
                }

                public static class Demo
                {
                    public static void Run(IFeatureRegistry registry)
                    {
                        registry.Register<MyHandler>();
                    }
                }
                """);

            var buildResult = await RunDotNetCommandAllowFailureAsync(consumerProjectDirectory, "build");

            Assert.NotEqual(0, buildResult.ExitCode);
            Assert.Contains("OGC001", buildResult.Output, StringComparison.Ordinal);
            Assert.Contains("Type 'MyHandler' must implement 'IHandleMessages<>'", buildResult.Output, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "OpenGenericConstraints.slnx")))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static async Task RunDotNetCommandAsync(string workingDirectory, string arguments)
    {
        var result = await RunDotNetCommandAllowFailureAsync(workingDirectory, arguments);

        Assert.True(
            result.ExitCode == 0,
            $"Expected `dotnet {arguments}` to succeed but it failed with exit code {result.ExitCode}.{Environment.NewLine}{result.Output}");
    }

    private static async Task<CommandResult> RunDotNetCommandAllowFailureAsync(string workingDirectory, string arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return new CommandResult(
            process.ExitCode,
            await standardOutputTask + await standardErrorTask);
    }

    private readonly record struct CommandResult(int ExitCode, string Output);
}
