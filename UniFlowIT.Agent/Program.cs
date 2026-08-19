using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Microsoft.Win32;
using UniFlowIT.Agent;

const string Version = "1.0.9";
var config = AgentStore.Load();
AgentStore.ApplyProtocolArguments(args, config);
AgentStore.Save(config);
AgentStore.Log($"Inicializando UniFlowIT Agent {Version}. Configurado={(!string.IsNullOrWhiteSpace(config.Token))}.");

using var cts = new CancellationTokenSource();
using var syncLock = new SemaphoreSlim(1, 1);
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

var listener = new HttpListener();
listener.Prefixes.Add("http://127.0.0.1:17891/");
try
{
    listener.Start();
    AgentStore.Log("Agente escutando em http://127.0.0.1:17891/.");
}
catch (Exception error)
{
    AgentStore.Log($"Falha ao iniciar o agente: {error}");
    return;
}

_ = Task.Run(() => SyncLoopAsync(config, syncLock, cts.Token), cts.Token);

while (!cts.IsCancellationRequested)
{
    var context = await listener.GetContextAsync().WaitAsync(cts.Token);
    _ = Task.Run(() => HandleAsync(context, config, syncLock, cts.Token), cts.Token);
}

static async Task HandleAsync(HttpListenerContext context, AgentConfig config, SemaphoreSlim syncLock, CancellationToken cancellationToken)
{
    AddCors(context.Response);

    if (context.Request.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = 204;
        context.Response.Close();
        return;
    }

    try
    {
        if (context.Request.Url?.AbsolutePath == "/health")
        {
            var installedFromRegistry = AgentStore.IsInstalledInWindows();
            await WriteJsonAsync(context.Response, new
            {
                installed = installedFromRegistry,
                installedFromRegistry,
                name = "UniFlowIT Agent",
                running = true,
                version = Version,
                config.AgentId
            });
            return;
        }

        if (context.Request.Url?.AbsolutePath == "/register" && context.Request.HttpMethod == "POST")
        {
            var request = await JsonSerializer.DeserializeAsync<RegisterRequest>(context.Request.InputStream, JsonOptions.Default, cancellationToken);
            if (request is not null)
            {
                config.ApiUrl = string.IsNullOrWhiteSpace(request.ApiUrl) ? config.ApiUrl : request.ApiUrl;
                config.Token = string.IsNullOrWhiteSpace(request.Token) ? config.Token : request.Token;
                config.EmpresaId = request.EmpresaId;
                config.UsuarioId = request.UsuarioId;
                AgentStore.Save(config);
            }

            var equipamento = await RegisterAsync(config, syncLock, cancellationToken);
            await WriteJsonAsync(context.Response, new { ok = true, equipamento });
            return;
        }

        if (context.Request.Url?.AbsolutePath == "/remote" && context.Request.HttpMethod == "POST")
        {
            var request = await JsonSerializer.DeserializeAsync<RemoteRequest>(context.Request.InputStream, JsonOptions.Default, cancellationToken);
            var password = string.IsNullOrWhiteSpace(request?.Password) ? config.RustDeskPassword : request.Password;
            OpenRustDesk(request?.RustDeskId ?? string.Empty, password);
            await WriteJsonAsync(context.Response, new { ok = true });
            return;
        }

        context.Response.StatusCode = 404;
        await WriteJsonAsync(context.Response, new { message = "Endpoint nao encontrado." });
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        await WriteJsonAsync(context.Response, new { message = ex.Message });
    }
}

static async Task SyncLoopAsync(AgentConfig config, SemaphoreSlim syncLock, CancellationToken cancellationToken)
{
    await TryRegisterAsync(config, syncLock, cancellationToken);

    using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));
    while (await timer.WaitForNextTickAsync(cancellationToken))
    {
        await TryRegisterAsync(config, syncLock, cancellationToken);
    }
}

static async Task TryRegisterAsync(AgentConfig config, SemaphoreSlim syncLock, CancellationToken cancellationToken)
{
    try
    {
        if (!string.IsNullOrWhiteSpace(config.Token))
        {
            await RegisterAsync(config, syncLock, cancellationToken);
        }
    }
    catch (Exception ex)
    {
        AgentStore.Log($"Sincronizacao pendente: {ex.Message}");
    }
}

static async Task<object> RegisterAsync(AgentConfig config, SemaphoreSlim syncLock, CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(config.Token))
    {
        throw new InvalidOperationException("Token de usuario ausente.");
    }

    await syncLock.WaitAsync(cancellationToken);
    try
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.Token);
        RustDeskProvisioner.EnsureRunning(config);
        var payload = HardwareSnapshot.Capture(config.AgentId, config.EquipamentoId, config.EmpresaId, Version, config.RustDeskPassword);
        var response = await http.PostAsJsonAsync($"{config.ApiUrl.TrimEnd('/')}/agent/equipamento", payload, JsonOptions.Default, cancellationToken);
        response.EnsureSuccessStatusCode();

        var sync = await response.Content.ReadFromJsonAsync<AgentEquipmentSyncResponse>(JsonOptions.Default, cancellationToken);
        if (sync?.Equipamento is not null)
        {
            var previousId = config.EquipamentoId;
            config.EquipamentoId = sync.Equipamento.Id;
            AgentStore.Save(config);

            if (sync.Created || previousId != sync.Equipamento.Id)
            {
                AgentStore.Log($"Equipamento sincronizado com novo cadastro. Id={sync.Equipamento.Id}, Patrimonio={sync.Equipamento.Patrimonio}.");
            }

            return sync.Equipamento;
        }

        return new { };
    }
    finally
    {
        syncLock.Release();
    }
}

static void OpenRustDesk(string rustDeskId, string password)
{
    if (string.IsNullOrWhiteSpace(rustDeskId))
    {
        throw new InvalidOperationException("RustDesk ID ausente.");
    }

    var rustDeskExe = RustDeskProvisioner.FindRustDeskExecutable();
    if (!string.IsNullOrWhiteSpace(rustDeskExe))
    {
        var arguments = string.IsNullOrWhiteSpace(password)
            ? $"--connect {rustDeskId}"
            : $"--connect {rustDeskId} --password \"{password.Replace("\"", "\\\"")}\"";
        Process.Start(new ProcessStartInfo
        {
            FileName = rustDeskExe,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        return;
    }

    Process.Start(new ProcessStartInfo
    {
        FileName = string.IsNullOrWhiteSpace(password)
            ? $"rustdesk://{Uri.EscapeDataString(rustDeskId)}"
            : $"rustdesk://{Uri.EscapeDataString(rustDeskId)}?password={Uri.EscapeDataString(password)}",
        UseShellExecute = true
    });
}

static void AddCors(HttpListenerResponse response)
{
    response.Headers["Access-Control-Allow-Origin"] = "*";
    response.Headers["Access-Control-Allow-Headers"] = "content-type";
    response.Headers["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS";
}

static async Task WriteJsonAsync(HttpListenerResponse response, object value)
{
    response.ContentType = "application/json; charset=utf-8";
    var json = JsonSerializer.Serialize(value, JsonOptions.Default);
    var bytes = Encoding.UTF8.GetBytes(json);
    response.ContentLength64 = bytes.Length;
    await response.OutputStream.WriteAsync(bytes);
    response.Close();
}

internal sealed record RegisterRequest(string ApiUrl, string Token, int? EmpresaId, int? UsuarioId, string? UsuarioNome);
internal sealed record RemoteRequest(string RustDeskId, string? Password);
internal sealed record AgentEquipmentSyncResponse(bool Created, AgentEquipmentResponse? Equipamento);
internal sealed record AgentEquipmentResponse(int Id, string Patrimonio, string Hostname);

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}

internal static class HardwareSnapshot
{
    public static object Capture(string agentId, int? equipamentoId, int? empresaId, string version, string rustDeskPassword)
    {
        return new
        {
            agentId,
            equipamentoId,
            empresaId,
            agentVersion = version,
            patrimonio = Environment.MachineName,
            hostname = Environment.MachineName,
            processador = GetProcessorName(),
            gpu = GetGpuName(),
            memoriaGb = GetMemoryGb(),
            memoriaLivreGb = GetFreeMemoryGb(),
            discoGb = GetDiskGb(),
            discoLivreGb = GetFreeDiskGb(),
            discos = GetDisks(),
            ip = GetLocalIp(),
            sistemaOperacional = OperatingSystem.IsWindows() ? $"{Environment.OSVersion.VersionString}" : System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            rustDeskId = GetRustDeskId(),
            rustDeskPassword
        };
    }

    private static string GetProcessorName()
    {
        var fromPowerShell = RunPowerShell("(Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty Name)");
        return string.IsNullOrWhiteSpace(fromPowerShell)
            ? Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Processador nao identificado"
            : fromPowerShell;
    }

    private static int GetMemoryGb()
    {
        var value = RunPowerShell("(Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory");
        return long.TryParse(value, out var bytes) ? (int)Math.Ceiling(bytes / 1024d / 1024d / 1024d) : 0;
    }

    private static int GetFreeMemoryGb()
    {
        var value = RunPowerShell("(Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory");
        return long.TryParse(value, out var kib) ? (int)Math.Floor(kib / 1024d / 1024d) : 0;
    }

    private static int GetDiskGb()
    {
        return DriveInfo.GetDrives()
            .Where(drive => drive.IsReady && drive.DriveType == DriveType.Fixed)
            .Sum(drive => (int)Math.Ceiling(drive.TotalSize / 1024d / 1024d / 1024d));
    }

    private static int GetFreeDiskGb()
    {
        return DriveInfo.GetDrives()
            .Where(drive => drive.IsReady && drive.DriveType == DriveType.Fixed)
            .Sum(drive => (int)Math.Floor(drive.AvailableFreeSpace / 1024d / 1024d / 1024d));
    }

    private static IEnumerable<object> GetDisks()
    {
        return DriveInfo.GetDrives()
            .Where(drive => drive.IsReady && drive.DriveType == DriveType.Fixed)
            .Select(drive => new
            {
                nome = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? drive.Name.TrimEnd('\\') : $"{drive.Name.TrimEnd('\\')} - {drive.VolumeLabel}",
                unidade = drive.Name,
                totalGb = (int)Math.Ceiling(drive.TotalSize / 1024d / 1024d / 1024d),
                livreGb = (int)Math.Floor(drive.AvailableFreeSpace / 1024d / 1024d / 1024d)
            })
            .ToArray();
    }

    private static string GetGpuName()
    {
        var fromPowerShell = RunPowerShell("(Get-CimInstance Win32_VideoController | Select-Object -First 1 -ExpandProperty Name)");
        return string.IsNullOrWhiteSpace(fromPowerShell) ? "GPU nao identificada" : fromPowerShell;
    }

    private static string GetLocalIp()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(item => item.OperationalStatus == OperationalStatus.Up && item.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(item => item.GetIPProperties().UnicastAddresses)
            .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(address => address.Address.ToString())
            .FirstOrDefault() ?? string.Empty;
    }

    private static string GetRustDeskId()
    {
        var configRoots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RustDesk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RustDesk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "RustDesk")
        };

        var paths = configRoots
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*.toml", SearchOption.AllDirectories))
            .Concat(configRoots.Select(root => Path.Combine(root, "RustDesk.toml")))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths.Where(File.Exists))
        {
            var content = File.ReadAllText(path);
            var match = Regex.Match(content, @"(?im)^\s*(?:id|rustdesk_id)\s*=\s*[""']?([A-Za-z0-9_-]{6,})[""']?");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return GetRustDeskIdFromExecutable();
    }

    private static string GetRustDeskIdFromExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RustDesk", "rustdesk.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "RustDesk", "rustdesk.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "RustDesk", "rustdesk.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RustDesk", "rustdesk.exe")
        };

        foreach (var path in candidates.Where(File.Exists))
        {
            foreach (var argument in new[] { "--get-id", "--id", "--get-id | more" })
            {
                var output = argument.Contains('|')
                    ? RunProcess("cmd.exe", $"/c \"\"{path}\" {argument}\"")
                    : RunProcess(path, argument);
                var match = Regex.Match(output, @"\b([0-9]{6,12})\b");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
        }

        return string.Empty;
    }

    private static string RunProcess(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null || !process.WaitForExit(2500))
            {
                return string.Empty;
            }

            return $"{process.StandardOutput.ReadToEnd()} {process.StandardError.ReadToEnd()}";
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string RunPowerShell(string command)
    {
        if (!OperatingSystem.IsWindows())
        {
            return string.Empty;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            return process?.StandardOutput.ReadToEnd().Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}

internal static class AgentStore
{
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UniFlowIT", "Agent");
    private static readonly string ConfigPath = Path.Combine(DirectoryPath, "agent-config.json");

    public static AgentConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            return new AgentConfig();
        }

        var config = JsonSerializer.Deserialize<AgentConfig>(File.ReadAllText(ConfigPath), JsonOptions.Default) ?? new AgentConfig();
        return string.IsNullOrWhiteSpace(config.AgentId) ? config.withAgentId() : config;
    }

    public static void Save(AgentConfig config)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOptions.Default));
    }

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            File.AppendAllText(Path.Combine(DirectoryPath, "agent.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never block the agent.
        }
    }

    public static bool IsInstalledInWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\UniFlowIT Agent");
            var installLocation = key?.GetValue("InstallLocation")?.ToString();
            var exePath = string.IsNullOrWhiteSpace(installLocation)
                ? string.Empty
                : Path.Combine(installLocation, "UniFlowIT.Agent.exe");

            return !string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath);
        }
        catch
        {
            return false;
        }
    }

    public static void ApplyProtocolArguments(string[] args, AgentConfig config)
    {
        var raw = args.FirstOrDefault(arg => arg.StartsWith("uniflowit-agent://", StringComparison.OrdinalIgnoreCase));
        if (raw is null || !Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            return;
        }

        var query = ParseQuery(uri.Query);
        config.ApiUrl = query.GetValueOrDefault("apiUrl") ?? config.ApiUrl;
        config.Token = query.GetValueOrDefault("token") ?? config.Token;
        config.EmpresaId = int.TryParse(query.GetValueOrDefault("empresaId"), out var empresaId) ? empresaId : config.EmpresaId;
        config.UsuarioId = int.TryParse(query.GetValueOrDefault("usuarioId"), out var usuarioId) ? usuarioId : config.UsuarioId;
    }

    private static AgentConfig withAgentId(this AgentConfig config)
    {
        config.AgentId = Guid.NewGuid().ToString("N");
        return config;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        return query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => Uri.UnescapeDataString(parts[1].Replace('+', ' ')),
                StringComparer.OrdinalIgnoreCase);
    }
}

internal static class RustDeskProvisioner
{
    public static async Task EnsureAsync(AgentConfig config, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    public static string FindRustDeskExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RustDesk", "rustdesk.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "RustDesk", "rustdesk.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "RustDesk", "rustdesk.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RustDesk", "rustdesk.exe")
        };

        var fromPath = FindExecutableInPath("rustdesk.exe");
        return candidates.Concat([fromPath])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .FirstOrDefault(File.Exists) ?? string.Empty;
    }

    public static void EnsureRunning(AgentConfig config)
    {
        var rustDeskExe = FindRustDeskExecutable();
        if (string.IsNullOrWhiteSpace(rustDeskExe))
        {
            AgentStore.Log("RustDesk nao encontrado para iniciar o daemon.");
            return;
        }

        if (Process.GetProcessesByName("rustdesk").Length > 0)
        {
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(config.RustDeskPassword))
            {
                RunRustDeskCommand(rustDeskExe, $"--password \"{config.RustDeskPassword}\"");
                RunRustDeskCommand(rustDeskExe, "--option approve-mode password");
                RunRustDeskCommand(rustDeskExe, "--option verification-method use-permanent-password");
                RunRustDeskCommand(rustDeskExe, "--option allow-only-conn-window-open N");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = rustDeskExe,
                Arguments = "--service",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            AgentStore.Log("Daemon do RustDesk iniciado em segundo plano.");
        }
        catch (Exception error)
        {
            AgentStore.Log($"Nao foi possivel iniciar o daemon do RustDesk: {error.Message}");
        }
    }

    private static void RunRustDeskCommand(string rustDeskExe, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = rustDeskExe,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            process?.WaitForExit(8000);
        }
        catch (Exception error)
        {
            AgentStore.Log($"Falha ao aplicar configuracao RustDesk '{arguments}': {error.Message}");
        }
    }

    private static async Task InstallRustDeskSilentlyAsync(CancellationToken cancellationToken)
    {
        var winget = FindExecutableInPath("winget.exe");
        if (!string.IsNullOrWhiteSpace(winget))
        {
            var result = await RunProcessAsync(
                winget,
                "install --id RustDesk.RustDesk -e --silent --accept-package-agreements --accept-source-agreements",
                TimeSpan.FromMinutes(4),
                cancellationToken);
            AgentStore.Log($"Instalacao RustDesk via winget finalizada. ExitCode={result.ExitCode}. Output={result.Output.Trim()}");

            if (!string.IsNullOrWhiteSpace(FindRustDeskExecutable()))
            {
                return;
            }
        }
        else
        {
            AgentStore.Log("winget.exe nao encontrado. Usando fallback de download direto do RustDesk.");
        }

        await InstallRustDeskFromGitHubAsync(cancellationToken);
    }

    private static async Task InstallRustDeskFromGitHubAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("UniFlowIT-Agent/1.0");

            var releaseJson = await http.GetStringAsync("https://api.github.com/repos/rustdesk/rustdesk/releases/latest", cancellationToken);
            using var document = JsonDocument.Parse(releaseJson);
            var downloadUrl = document.RootElement
                .GetProperty("assets")
                .EnumerateArray()
                .Select(asset => asset.TryGetProperty("browser_download_url", out var url) ? url.GetString() ?? string.Empty : string.Empty)
                .FirstOrDefault(url =>
                    url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    && url.Contains("x86_64", StringComparison.OrdinalIgnoreCase)
                    && !url.Contains("portable", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                AgentStore.Log("Nao encontrei instalador EXE x86_64 do RustDesk no release latest.");
                return;
            }

            var installerPath = Path.Combine(Path.GetTempPath(), "rustdesk-uniflowit-install.exe");
            AgentStore.Log($"Baixando RustDesk: {downloadUrl}");
            await using (var stream = await http.GetStreamAsync(downloadUrl, cancellationToken))
            await using (var file = File.Create(installerPath))
            {
                await stream.CopyToAsync(file, cancellationToken);
            }

            var result = await RunProcessAsync(installerPath, "--silent-install", TimeSpan.FromMinutes(4), cancellationToken);
            AgentStore.Log($"Instalacao RustDesk via download direto finalizada. ExitCode={result.ExitCode}. Output={result.Output.Trim()}");
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        }
        catch (Exception error)
        {
            AgentStore.Log($"Falha no fallback de instalacao direta do RustDesk: {error.Message}");
        }
    }

    private static void ConfigureRustDesk(string rustDeskExe, string password)
    {
        foreach (var arguments in new[]
        {
            "--install-service",
            $"--password \"{password.Replace("\"", "\\\"")}\""
        })
        {
            try
            {
                var result = RunProcessSync(rustDeskExe, arguments, TimeSpan.FromSeconds(12));
                AgentStore.Log($"RustDesk comando '{arguments}' executado. ExitCode={result.ExitCode}. Output={result.Output.Trim()}");
            }
            catch (Exception error)
            {
                AgentStore.Log($"Falha ao configurar RustDesk com '{arguments}': {error.Message}");
            }
        }
    }

    private static string GeneratePassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
        Span<byte> bytes = stackalloc byte[18];
        RandomNumberGenerator.Fill(bytes);
        return new string(bytes.ToArray().Select(value => alphabet[value % alphabet.Length]).ToArray());
    }

    private static string FindExecutableInPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim(), fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // Ignore invalid PATH entries.
            }
        }

        return string.Empty;
    }

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(string fileName, string arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process is null)
        {
            return (-1, string.Empty);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }
        }

        return (process.HasExited ? process.ExitCode : -1, $"{await process.StandardOutput.ReadToEndAsync(cancellationToken)} {await process.StandardError.ReadToEndAsync(cancellationToken)}");
    }

    private static (int ExitCode, string Output) RunProcessSync(string fileName, string arguments, TimeSpan timeout)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process is null)
        {
            return (-1, string.Empty);
        }

        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }
        }

        return (process.HasExited ? process.ExitCode : -1, $"{process.StandardOutput.ReadToEnd()} {process.StandardError.ReadToEnd()}");
    }
}
