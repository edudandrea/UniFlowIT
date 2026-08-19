using System.Diagnostics;
using System.Drawing;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Win32;

const string AgentName = "UniFlowIT Agent";
const string Version = "1.0.9";

var apiBase = GetArgument(args, "--apiBase")
    ?? Environment.GetEnvironmentVariable("UNIFLOWIT_API_BASE")
    ?? "http://localhost:5151/api";
var token = GetArgument(args, "--token") ?? string.Empty;
var empresaId = GetArgument(args, "--empresaId") ?? string.Empty;
var usuarioId = GetArgument(args, "--usuarioId") ?? string.Empty;
var usuarioNome = GetArgument(args, "--usuarioNome") ?? string.Empty;
var installDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "UniFlowIT",
    "Agent");
var logPath = Path.Combine(installDir, "install.log");

try
{
    await InstallAsync(apiBase.TrimEnd('/'), token, empresaId, usuarioId, usuarioNome, installDir, logPath);
    ShowInstallerDialog(
        success: true,
        title: "Instalacao concluida",
        message: "UniFlowIT Agent instalado com sucesso.",
        details: "A instalacao foi concluida e o equipamento sera sincronizado automaticamente.");
}
catch (Exception error)
{
    Log(logPath, $"Falha na instalacao: {error}");
    ShowInstallerDialog(
        success: false,
        title: "Falha na instalacao",
        message: "Nao foi possivel instalar o UniFlowIT Agent.",
        details: $"Detalhe: {error.Message}\nLog: {logPath}");
}

static async Task InstallAsync(string apiBase, string token, string empresaId, string usuarioId, string usuarioNome, string installDir, string logPath)
{
    var zipPath = Path.Combine(Path.GetTempPath(), "UniFlowIT.Agent.zip");
    var downloadUrl = $"{apiBase}/agent/download/windows";

    Directory.CreateDirectory(installDir);
    Log(logPath, $"Iniciando instalacao. API: {apiBase}");

    Log(logPath, $"Baixando pacote: {downloadUrl}");
    using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) })
    await using (var stream = await http.GetStreamAsync(downloadUrl))
    await using (var file = File.Create(zipPath))
    {
        await stream.CopyToAsync(file);
    }
    Log(logPath, $"Pacote baixado: {zipPath}");

    StopAgent(installDir);
    await Task.Delay(800);

    if (Directory.Exists(installDir))
    {
        Directory.Delete(installDir, recursive: true);
    }

    Directory.CreateDirectory(installDir);
    ZipFile.ExtractToDirectory(zipPath, installDir, overwriteFiles: true);
    Log(logPath, $"Pacote extraido em: {installDir}");

    var installedExe = Path.Combine(installDir, "UniFlowIT.Agent.exe");
    if (!File.Exists(installedExe))
    {
        throw new FileNotFoundException("UniFlowIT.Agent.exe nao encontrado.", installedExe);
    }

    var uninstallScript = Path.Combine(installDir, "uninstall-agent.ps1");
    var rustDeskPassword = GenerateRustDeskPassword();
    await File.WriteAllTextAsync(uninstallScript, CreateUninstallScript(installDir));
    await WriteAgentConfigAsync(installDir, apiBase, token, empresaId, usuarioId, usuarioNome, rustDeskPassword);

    RegisterStartup(installedExe);
    RegisterProtocol(installedExe);
    RegisterUninstall(installDir, installedExe, uninstallScript);
    await EnsureRustDeskInstalledAsync(logPath, rustDeskPassword);
    StartHidden(installedExe);
    await EnsureAgentStartedAsync(logPath);
    Log(logPath, "Instalacao concluida com sucesso.");
}

static async Task WriteAgentConfigAsync(string installDir, string apiBase, string token, string empresaId, string usuarioId, string usuarioNome, string rustDeskPassword)
{
    if (string.IsNullOrWhiteSpace(token))
    {
        return;
    }

    var configPath = Path.Combine(installDir, "agent-config.json");
    var config = new
    {
        agentId = Guid.NewGuid().ToString("N"),
        apiUrl = apiBase,
        token,
        empresaId = int.TryParse(empresaId, out var empresa) ? empresa : (int?)null,
        usuarioId = int.TryParse(usuarioId, out var usuario) ? usuario : (int?)null,
        usuarioNome,
        rustDeskPassword
    };
    await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    }));
}

static string? GetArgument(string[] args, string name)
{
    for (var index = 0; index < args.Length; index++)
    {
        if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
        {
            return args[index + 1];
        }

        if (args[index].StartsWith($"{name}=", StringComparison.OrdinalIgnoreCase))
        {
            return args[index][(name.Length + 1)..];
        }
    }

    return null;
}

static void StopAgent(string installDir)
{
    var normalizedInstallDir = Path.GetFullPath(installDir).TrimEnd('\\');

    foreach (var process in Process.GetProcessesByName("UniFlowIT.Agent"))
    {
        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(4000);
        }
        catch
        {
            // Best effort.
        }
    }

    foreach (var process in EnumerateAgentProcesses(normalizedInstallDir))
    {
        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(4000);
        }
        catch
        {
            // Best effort.
        }
    }
}

static IEnumerable<Process> EnumerateAgentProcesses(string installDir)
{
    foreach (var process in Process.GetProcesses())
    {
        if (process.Id == Environment.ProcessId)
        {
            process.Dispose();
            continue;
        }

        var executablePath = string.Empty;
        try
        {
            executablePath = process.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            // Some system processes deny MainModule access.
        }

        var matchesInstallDir = executablePath.StartsWith(installDir, StringComparison.OrdinalIgnoreCase)
            || process.ProcessName.Contains("UniFlowIT.Agent", StringComparison.OrdinalIgnoreCase);

        if (matchesInstallDir)
        {
            yield return process;
        }
        else
        {
            try
            {
                process.Dispose();
            }
            catch
            {
            }
        }
    }
}

static void RegisterStartup(string installedExe)
{
    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
    key?.SetValue(AgentName, $"\"{installedExe}\"", RegistryValueKind.String);
}

static void RegisterProtocol(string installedExe)
{
    using var protocolKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\uniflowit-agent");
    protocolKey?.SetValue(string.Empty, "URL:UniFlowIT Agent", RegistryValueKind.String);
    protocolKey?.SetValue("URL Protocol", string.Empty, RegistryValueKind.String);

    using var commandKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\uniflowit-agent\shell\open\command");
    commandKey?.SetValue(string.Empty, $"\"{installedExe}\" \"%1\"", RegistryValueKind.String);
}

static void RegisterUninstall(string installDir, string installedExe, string uninstallScript)
{
    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\UniFlowIT Agent");
    if (key is null)
    {
        return;
    }

    key.SetValue("DisplayName", AgentName, RegistryValueKind.String);
    key.SetValue("DisplayVersion", Version, RegistryValueKind.String);
    key.SetValue("Publisher", "UniFlowIT", RegistryValueKind.String);
    key.SetValue("InstallLocation", installDir, RegistryValueKind.String);
    key.SetValue("DisplayIcon", installedExe, RegistryValueKind.String);
    key.SetValue("UninstallString", $"powershell.exe -ExecutionPolicy Bypass -File \"{uninstallScript}\"", RegistryValueKind.String);
    key.SetValue("QuietUninstallString", $"powershell.exe -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{uninstallScript}\"", RegistryValueKind.String);
    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
}

static string CreateUninstallScript(string installDir)
{
    return $$"""
$ErrorActionPreference = "SilentlyContinue"
$installDir = "{{installDir}}"

Get-Process | Where-Object { $_.ProcessName -eq "UniFlowIT.Agent" } | Stop-Process -Force
Get-Process | Where-Object { $_.ProcessName -eq "rustdesk" } | Stop-Process -Force
Get-Service | Where-Object { $_.Name -like "*RustDesk*" -or $_.DisplayName -like "*RustDesk*" } | Stop-Service -Force

$winget = Get-Command winget.exe -ErrorAction SilentlyContinue
if ($winget) {
    & $winget.Source uninstall --id RustDesk.RustDesk -e --silent --disable-interactivity --accept-source-agreements | Out-Null
}

$uninstallRoots = @(
    "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
)
foreach ($entry in Get-ItemProperty $uninstallRoots -ErrorAction SilentlyContinue) {
    if ($entry.DisplayName -like "*RustDesk*" -and $entry.UninstallString) {
        Start-Process -FilePath "cmd.exe" -ArgumentList "/c", $entry.UninstallString, "/S" -WindowStyle Hidden -Wait
    }
}

Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "UniFlowIT Agent" -ErrorAction SilentlyContinue
Remove-Item -LiteralPath "HKCU:\Software\Classes\uniflowit-agent" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\UniFlowIT Agent" -Recurse -Force -ErrorAction SilentlyContinue
Set-Location $env:TEMP
Remove-Item -LiteralPath $installDir -Recurse -Force -ErrorAction SilentlyContinue
""";
}

static void StartHidden(string installedExe)
{
    Process.Start(new ProcessStartInfo
    {
        FileName = installedExe,
        UseShellExecute = true,
        WindowStyle = ProcessWindowStyle.Hidden
    });
}

static async Task EnsureRustDeskInstalledAsync(string logPath, string password)
{
    if (!OperatingSystem.IsWindows())
    {
        return;
    }

    var winget = FindExecutableInPath("winget.exe");
    if (!string.IsNullOrWhiteSpace(winget))
    {
        Log(logPath, "Iniciando instalacao silenciosa do RustDesk via winget.");
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = winget,
            Arguments = "install --id RustDesk.RustDesk -e --silent --disable-interactivity --accept-package-agreements --accept-source-agreements",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (process is not null)
        {
            await process.WaitForExitAsync();
            var output = $"{await process.StandardOutput.ReadToEndAsync()} {await process.StandardError.ReadToEndAsync()}".Trim();
            Log(logPath, $"Instalacao via winget finalizada. ExitCode={process.ExitCode}. {output}");
            if (process.ExitCode is 0 or -1978335189)
            {
                var installedByWinget = FindRustDeskExecutable();
                if (!string.IsNullOrWhiteSpace(installedByWinget))
                {
                    await ConfigureRustDeskAsync(installedByWinget, password, logPath);
                    return;
                }
            }
        }

        Log(logPath, "winget nao concluiu a instalacao. Usando o instalador oficial do RustDesk.");
    }

    await InstallRustDeskFromGitHubAsync(logPath);
    var installedExe = FindRustDeskExecutable();
    if (string.IsNullOrWhiteSpace(installedExe))
    {
        throw new InvalidOperationException("RustDesk foi instalado, mas o executável nao foi encontrado.");
    }

    await ConfigureRustDeskAsync(installedExe, password, logPath);
}

static async Task InstallRustDeskFromGitHubAsync(string logPath)
{
    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("UniFlowIT-Agent/1.0.3");
    var releaseJson = await http.GetStringAsync("https://api.github.com/repos/rustdesk/rustdesk/releases/latest");
    using var document = JsonDocument.Parse(releaseJson);
    var downloadUrl = document.RootElement
        .GetProperty("assets")
        .EnumerateArray()
        .Select(asset => asset.TryGetProperty("browser_download_url", out var url) ? url.GetString() ?? string.Empty : string.Empty)
        .FirstOrDefault(url => url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            && url.Contains("x86_64", StringComparison.OrdinalIgnoreCase)
            && !url.Contains("portable", StringComparison.OrdinalIgnoreCase));

    if (string.IsNullOrWhiteSpace(downloadUrl))
    {
        throw new InvalidOperationException("Instalador oficial do RustDesk nao encontrado.");
    }

    var installerPath = Path.Combine(Path.GetTempPath(), "rustdesk-uniflowit-install.exe");
    Log(logPath, $"Baixando instalador oficial do RustDesk: {downloadUrl}");
    await using (var stream = await http.GetStreamAsync(downloadUrl))
    await using (var file = File.Create(installerPath))
    {
        await stream.CopyToAsync(file);
    }

    using var rustdesk = Process.Start(new ProcessStartInfo
    {
        FileName = installerPath,
        Arguments = "--silent-install",
        UseShellExecute = false,
        CreateNoWindow = true
    });

    if (rustdesk is null)
    {
        throw new InvalidOperationException("Nao foi possivel iniciar o instalador oficial do RustDesk.");
    }

    await rustdesk.WaitForExitAsync();
    Log(logPath, $"Instalacao oficial do RustDesk finalizada. ExitCode={rustdesk.ExitCode}.");

    if (rustdesk.ExitCode != 0)
    {
        throw new InvalidOperationException($"A instalacao do RustDesk falhou. Codigo: {rustdesk.ExitCode}.");
    }
}

static async Task ConfigureRustDeskAsync(string rustDeskExe, string password, string logPath)
{
    var serviceResult = await RunRustDeskCommandAsync(rustDeskExe, "--install-service");
    Log(logPath, $"Servico do RustDesk verificado. ExitCode={serviceResult.ExitCode}. {serviceResult.Output}");

    var passwordResult = await RunRustDeskCommandAsync(rustDeskExe, $"--password \"{password}\"");
    Log(logPath, $"Senha do RustDesk configurada em segundo plano. ExitCode={passwordResult.ExitCode}. {passwordResult.Output}");
    if (passwordResult.ExitCode != 0)
    {
        throw new InvalidOperationException($"Nao foi possivel configurar a senha do RustDesk. Codigo: {passwordResult.ExitCode}.");
    }

    foreach (var option in new[]
    {
        (Name: "approve-mode", Value: "password"),
        (Name: "verification-method", Value: "use-permanent-password"),
        (Name: "allow-only-conn-window-open", Value: "N")
    })
    {
        var optionResult = await RunRustDeskCommandAsync(rustDeskExe, $"--option {option.Name} {option.Value}");
        Log(logPath, $"Opcao RustDesk {option.Name}={option.Value}. ExitCode={optionResult.ExitCode}. {optionResult.Output}");
    }

    using var daemon = Process.Start(new ProcessStartInfo
    {
        FileName = rustDeskExe,
        Arguments = "--service",
        UseShellExecute = false,
        CreateNoWindow = true,
        WindowStyle = ProcessWindowStyle.Hidden
    });
    Log(logPath, "Daemon do RustDesk iniciado em segundo plano.");
}

static async Task<(int ExitCode, string Output)> RunRustDeskCommandAsync(string rustDeskExe, string arguments)
{
    using var process = Process.Start(new ProcessStartInfo
    {
        FileName = rustDeskExe,
        Arguments = arguments,
        UseShellExecute = false,
        CreateNoWindow = true,
        WindowStyle = ProcessWindowStyle.Hidden,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    });

    if (process is null)
    {
        throw new InvalidOperationException("Nao foi possivel iniciar o RustDesk em segundo plano.");
    }

    var outputTask = process.StandardOutput.ReadToEndAsync();
    var errorTask = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    var output = $"{await outputTask} {await errorTask}".Trim();
    return (process.ExitCode, output);
}

static string FindRustDeskExecutable()
{
    var candidates = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RustDesk", "rustdesk.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "RustDesk", "rustdesk.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "RustDesk", "rustdesk.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RustDesk", "rustdesk.exe")
    };

    return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
}

static string GenerateRustDeskPassword()
{
    const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
    Span<byte> bytes = stackalloc byte[16];
    RandomNumberGenerator.Fill(bytes);
    return new string(bytes.ToArray().Select(value => alphabet[value % alphabet.Length]).ToArray());
}

static string FindExecutableInPath(string fileName)
{
    var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
    return path
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .Select(directory => Path.Combine(directory.Trim(), fileName))
        .FirstOrDefault(File.Exists) ?? string.Empty;
}

static async Task EnsureAgentStartedAsync(string logPath)
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
    for (var attempt = 1; attempt <= 8; attempt++)
    {
        try
        {
            var response = await http.GetAsync("http://127.0.0.1:17891/health");
            var body = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode && AgentHealthIsCurrent(body))
            {
                Log(logPath, "Health check do agente OK.");
                return;
            }

            Log(logPath, $"Health check tentativa {attempt}: resposta de agente antigo ou invalido.");
        }
        catch (Exception error)
        {
            Log(logPath, $"Health check tentativa {attempt}: {error.Message}");
        }

        await Task.Delay(600);
    }

    throw new InvalidOperationException("O agente foi instalado, mas nao respondeu ao health check local.");
}

static bool AgentHealthIsCurrent(string body)
{
    try
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("installedFromRegistry", out var installed)
                && installed.ValueKind == JsonValueKind.True
                && document.RootElement.TryGetProperty("version", out var version)
                && string.Equals(version.GetString(), "1.0.9", StringComparison.OrdinalIgnoreCase);
    }
    catch
    {
        return false;
    }
}

static void Log(string logPath, string message)
{
    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
    }
    catch
    {
        // Logging must never block installation.
    }
}

static void ShowInstallerDialog(bool success, string title, string message, string details)
{
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);

    var accent = success ? Color.FromArgb(16, 185, 129) : Color.FromArgb(239, 68, 68);
    var accentSoft = success ? Color.FromArgb(8, 97, 75) : Color.FromArgb(111, 36, 50);
    using var form = new Form
    {
        Text = AgentName,
        StartPosition = FormStartPosition.CenterScreen,
        FormBorderStyle = FormBorderStyle.FixedDialog,
        MaximizeBox = false,
        MinimizeBox = false,
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
        ClientSize = new Size(560, 330),
        BackColor = Color.FromArgb(8, 18, 32),
        Font = new Font("Segoe UI", 10f, FontStyle.Regular),
        ForeColor = Color.White
    };

    var header = new Panel
    {
        Dock = DockStyle.Top,
        Height = 88,
        BackColor = Color.FromArgb(12, 22, 38)
    };
    form.Controls.Add(header);

    header.Controls.Add(new Label
    {
        Text = "UNIFLOWIT AGENT",
        AutoSize = false,
        Location = new Point(28, 18),
        Size = new Size(300, 22),
        ForeColor = Color.FromArgb(244, 194, 222),
        Font = new Font("Segoe UI", 9f, FontStyle.Bold)
    });

    header.Controls.Add(new Label
    {
        Text = title,
        AutoSize = false,
        Location = new Point(28, 38),
        Size = new Size(360, 38),
        ForeColor = Color.White,
        Font = new Font("Segoe UI", 20f, FontStyle.Bold)
    });

    var close = new Button
    {
        Text = "x",
        Location = new Point(494, 24),
        Size = new Size(42, 42),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(241, 245, 249),
        ForeColor = Color.FromArgb(82, 96, 116),
        Font = new Font("Segoe UI", 14f, FontStyle.Bold),
        Cursor = Cursors.Hand
    };
    close.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
    close.Click += (_, _) => form.Close();
    header.Controls.Add(close);

    var body = new Panel
    {
        Location = new Point(0, 88),
        Size = new Size(560, 180),
        BackColor = Color.FromArgb(28, 39, 58)
    };
    form.Controls.Add(body);

    var smilePanel = new Panel
    {
        Location = new Point(28, 26),
        Size = new Size(94, 94),
        BackColor = accentSoft
    };
    smilePanel.Paint += (_, args) =>
    {
        args.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(accent);
        using var pen = new Pen(Color.FromArgb(222, 255, 244), 4);
        args.Graphics.FillEllipse(brush, 12, 12, 70, 70);
        args.Graphics.DrawEllipse(pen, 12, 12, 70, 70);
        using var eyeBrush = new SolidBrush(Color.FromArgb(8, 18, 32));
        args.Graphics.FillEllipse(eyeBrush, 34, 38, 7, 7);
        args.Graphics.FillEllipse(eyeBrush, 56, 38, 7, 7);
        var smileY = success ? 52 : 62;
        var startAngle = success ? 20 : 200;
        var sweepAngle = success ? 140 : 140;
        args.Graphics.DrawArc(new Pen(Color.FromArgb(8, 18, 32), 4), 34, smileY, 30, 20, startAngle, sweepAngle);
    };
    body.Controls.Add(smilePanel);

    body.Controls.Add(new Label
    {
        Text = message,
        AutoSize = false,
        Location = new Point(146, 30),
        Size = new Size(375, 34),
        ForeColor = Color.White,
        Font = new Font("Segoe UI", 13f, FontStyle.Bold)
    });

    body.Controls.Add(new Label
    {
        Text = details,
        AutoSize = false,
        Location = new Point(146, 72),
        Size = new Size(370, 78),
        ForeColor = Color.FromArgb(185, 202, 221),
        Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
    });

    var footer = new Panel
    {
        Dock = DockStyle.Bottom,
        Height = 62,
        BackColor = Color.FromArgb(10, 18, 32)
    };
    form.Controls.Add(footer);

    var ok = new Button
    {
        Text = success ? "Ok, continuar" : "Entendi",
        Location = new Point(376, 12),
        Size = new Size(150, 38),
        FlatStyle = FlatStyle.Flat,
        BackColor = success ? Color.FromArgb(37, 99, 235) : Color.FromArgb(15, 23, 42),
        ForeColor = Color.White,
        Font = new Font("Segoe UI", 10f, FontStyle.Bold),
        Cursor = Cursors.Hand
    };
    ok.FlatAppearance.BorderColor = success ? Color.FromArgb(14, 165, 233) : Color.FromArgb(71, 85, 105);
    ok.Click += (_, _) => form.Close();
    footer.Controls.Add(ok);

    form.AcceptButton = ok;
    form.ShowDialog();
}
