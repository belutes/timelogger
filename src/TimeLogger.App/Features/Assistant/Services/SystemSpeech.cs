using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TimeLogger.App.Features.Assistant.Services;

public static class SystemSpeech
{
    public static bool IsSupported => OperatingSystem.IsMacOS() || OperatingSystem.IsWindows();

    public static async Task SpeakAsync(string text, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true };
        if (OperatingSystem.IsMacOS()) start.FileName = "/usr/bin/say";
        else if (OperatingSystem.IsWindows())
        {
            start.FileName = "powershell.exe";
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-Command");
            start.ArgumentList.Add("[Console]::InputEncoding = [System.Text.Encoding]::UTF8; Add-Type -AssemblyName System.Speech; $voice = New-Object System.Speech.Synthesis.SpeechSynthesizer; try { $voice.Speak([Console]::In.ReadToEnd()) } finally { $voice.Dispose() }");
        }
        else throw new PlatformNotSupportedException("Read aloud is available on macOS and Windows.");

        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new InvalidOperationException("The system reader could not start.");
        using var registration = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        });
        var errorTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.StandardInput.WriteAsync(text.AsMemory(), cancellationToken);
            process.StandardInput.Close();
            await process.WaitForExitAsync(cancellationToken);
            await errorTask;
            if (process.ExitCode != 0) throw new InvalidOperationException("The system reader could not read this response.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }
}
