using System.Diagnostics;
using System.Reflection;

internal class Program
{
    private const  string             AppName = "DontBanMyPluginsApp";
    private static FileSystemWatcher? _watcher;
    private static Mutex?             _mutex;

    private static void Main(string[] _)
    {
        _mutex = new Mutex(true, $"Global\\{AppName}", out var createdNew);

        if(!createdNew)
        {
            ShowNotification("程序已在运行", "监控程序已经在运行中，无需重复启动。");
            return;
        }

        try
        {
            var isFirstRun = !IsTaskExists();

            if(isFirstRun) SetStartup();

            StartSilentMonitoring();
        }
        finally
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }

    private static void StartSilentMonitoring()
    {
        var targetDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XIVLauncherCN", "dalamudAssets");

        if(!Directory.Exists(targetDirectory)) return;

        ScanAndProcessExistingFiles(targetDirectory);

        _watcher = new FileSystemWatcher(targetDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter          = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Attributes,
            Filter                = "cheatplugin.json"
        };

        _watcher.Created             += OnFileDetected;
        _watcher.Changed             += OnFileDetected;
        _watcher.EnableRaisingEvents =  true;

        using var waitHandle = new ManualResetEvent(false);
        waitHandle.WaitOne();
    }

    private static void ScanAndProcessExistingFiles(string directory)
    {
        try
        {
            var files = Directory.GetFiles(directory, "cheatplugin.json", SearchOption.AllDirectories);
            foreach (var file in files) ProcessFile(file);
        }
        catch
        {
            // ignored
        }
    }

    private static void ProcessFile(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists) return;

            if ((fileInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                fileInfo.Attributes &= ~FileAttributes.ReadOnly;

            using var writer = new StreamWriter(fileInfo.FullName,        false);
            writer.Write("[]");
        }
        catch
        {
            // ignored
        }
    }

    private static void OnFileDetected(object sender, FileSystemEventArgs e)
    {
        ProcessFile(e.FullPath);
    }

    private static void SetStartup()
    {
        try
        {
            var executablePath = Assembly.GetExecutingAssembly().Location;
            if(executablePath.EndsWith(".dll"))
                executablePath = Process.GetCurrentProcess().MainModule?.FileName ?? executablePath;

            var taskPath = executablePath.Replace("\"", "\\\"");
            string[] cmdParts =
            [
                "schtasks",
                "/create",
                $"/tn \"{AppName}\"",
                $"/tr \"\\\"{taskPath}\\\"\"",
                "/sc onlogon",
                "/rl highest",
                "/f"
            ];
            var command = string.Join(" ", cmdParts);

            var processInfo = new ProcessStartInfo
            {
                FileName               = "cmd.exe",
                Arguments              = $"/c {command}",
                CreateNoWindow         = true,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                Verb                   = "runas"
            };

            using var process = Process.Start(processInfo);
            process?.WaitForExit();
        }
        catch
        {
            // ignored
        }
    }

    private static bool IsTaskExists()
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName               = "schtasks",
                Arguments              = $"/query /tn \"{AppName}\" /fo list",
                CreateNoWindow         = true,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true
            };

            using var process = Process.Start(processInfo);
            if(process == null) return false;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output.Contains($"任务名称: {AppName}") || output.Contains($"TaskName: {AppName}");
        }
        catch
        {
            return false;
        }
    }

    private static void ShowNotification(string title, string message)
    {
        try
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch
        {
            // ignored
        }
    }
}