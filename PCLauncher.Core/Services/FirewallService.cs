using System;
using System.Diagnostics;

namespace PCLauncher.Core.Services;

public interface IFirewallService
{
    bool IsFirewallRuleConfigured(int port);
    bool AddFirewallRule(int port);
}

public class FirewallService : IFirewallService
{
    private const string RuleName = "PC Launcher Server";

    public bool IsFirewallRuleConfigured(int port)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall show rule name=\"{RuleName}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);

            return process.ExitCode == 0 && output.Contains(RuleName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public bool AddFirewallRule(int port)
    {
        try
        {
            // First remove existing rule if any to update port
            var deletePsi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall delete rule name=\"{RuleName}\"",
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            try
            {
                using var delProc = Process.Start(deletePsi);
                delProc?.WaitForExit(3000);
            }
            catch { }

            // Add new inbound rule restricted to private and domain networks
            var addPsi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall add rule name=\"{RuleName}\" dir=in action=allow protocol=TCP localport={port} profile=private,domain description=\"Allows LAN phones to connect to PC Launcher Server\"",
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var addProc = Process.Start(addPsi);
            addProc?.WaitForExit(10000);
            return addProc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
