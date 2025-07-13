using System.Diagnostics;

namespace RazorX.Integration.Tests;

[TestClass]
public static class Initialize {

    private static Process _process = null!;

    [AssemblyInitialize]
    public async static Task AssemblyInitialize(TestContext testContext) {
        _process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = "dotnet",
                Arguments = "run",
                WorkingDirectory = "../../../../RazorX.Integration",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        _process.Start();
        await Task.Delay(2000);
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup() {
        if (_process is not null && !_process.HasExited) {
            _process.Kill(true);
            _process.WaitForExit();
        }
    }
}