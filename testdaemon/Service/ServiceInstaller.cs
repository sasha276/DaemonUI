using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace testdaemon.Service;

public static class ServiceInstaller
{
    private const string ServiceName = "DaemonAppd";

    private static bool RunElevated(string fileName, string arguments, out string error)
    {
        error = "";

        if (!OperatingSystem.IsWindows())
        {
            error = "Установка службы поддерживается только на Windows";
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true, 
                Verb = "runas",
            };
            using var proc = Process.Start(psi);
            if (proc == null) { error = "Не удалось запустить процесс"; return false; }

            proc.WaitForExit();
            if (proc.ExitCode != 0)
            {
                error = $"Код возврата {proc.ExitCode}";
                return false;
            }
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            error = "Запрос на повышение прав отклонён пользователем";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool Install(string appdFolder, out string error)
    {
        var exePath = Path.Combine(appdFolder, "appd.exe");
        if (!File.Exists(exePath))
        {
            error = $"appd.exe не найден в: {appdFolder}";
            return false;
        }
        return RunElevated(exePath, "install", out error);
    }

    public static bool Uninstall(string appdFolder, out string error)
    {
        var exePath = Path.Combine(appdFolder, "appd.exe");
        if (!File.Exists(exePath))
        {
            error = $"appd.exe не найден в: {appdFolder}";
            return false;
        }
        return RunElevated(exePath, "uninstall", out error);
    }

    public static bool Start(out string error) =>
        RunElevated("sc.exe", $"start {ServiceName}", out error);

    public static bool Stop(out string error) =>
        RunElevated("sc.exe", $"stop {ServiceName}", out error);

    public static bool Restart(out string error) =>
        RunElevated("shutdown.exe", "/r /t 0", out error);
}