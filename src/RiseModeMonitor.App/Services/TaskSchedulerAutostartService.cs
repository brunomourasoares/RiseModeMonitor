using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
using RiseModeMonitor.Core.Services;

namespace RiseModeMonitor.Services;

/// <summary>
/// Starts the app at logon through a Task Scheduler task with "highest privileges".
/// This is the standard way to autostart an elevated app without a UAC prompt
/// (a Startup-folder shortcut or Run key would trigger UAC or be blocked).
/// Must be called from an elevated process.
/// </summary>
public sealed class TaskSchedulerAutostartService : IAutostartService
{
    private const string TaskName = "RiseModeMonitor";

    public bool IsEnabled() => Run("/Query", "/TN", TaskName) == 0;

    public bool SetEnabled(bool enabled)
    {
        if (!enabled)
            return !IsEnabled() || Run("/Delete", "/TN", TaskName, "/F") == 0;

        var exe = Environment.ProcessPath;
        // Running through "dotnet RiseModeMonitor.dll" would register dotnet.exe, which is wrong.
        if (string.IsNullOrEmpty(exe) || Path.GetFileName(exe).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            return false;

        var xmlPath = Path.Combine(Path.GetTempPath(), $"RiseModeMonitor-{Guid.NewGuid():N}.xml");
        try
        {
            // schtasks requires the task XML to be UTF-16.
            File.WriteAllText(xmlPath, BuildTaskXml(exe, WindowsIdentity.GetCurrent().Name), Encoding.Unicode);
            return Run("/Create", "/TN", TaskName, "/XML", xmlPath, "/F") == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            try { File.Delete(xmlPath); } catch (IOException) { }
        }
    }

    private static string BuildTaskXml(string exePath, string userId)
    {
        var user = SecurityElement.Escape(userId);
        var exe = SecurityElement.Escape(exePath);
        var dir = SecurityElement.Escape(Path.GetDirectoryName(exePath) ?? string.Empty);

        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Description>Starts Rise Mode Monitor when the user logs on.</Description>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{user}</UserId>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{user}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>false</AllowHardTerminate>
                <StartWhenAvailable>true</StartWhenAvailable>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Enabled>true</Enabled>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{exe}</Command>
                  <Arguments>--minimized</Arguments>
                  <WorkingDirectory>{dir}</WorkingDirectory>
                </Exec>
              </Actions>
            </Task>
            """;
    }

    private static int Run(params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in args) psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi);
            if (process is null) return -1;

            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Win32Exception)
        {
            return -1;
        }
    }
}
