using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (!ShouldOpenUi(args))
        {
            return ClaudeCodeProcessWrapperProgram.Run(args);
        }

        ApplicationConfiguration.Initialize();

        ClaudeCodeSettingsState settings;
        try
        {
            settings = ClaudeCodeUserSettingsStore.LoadUserSettings();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to load Claude Code settings.\n\n{ex.Message}",
                "Claude Code Settings Editor",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            settings = ClaudeCodeUserSettingsStore.CreateEmptyUserSettings();
        }

        Application.Run(new MainForm(settings));
        return 0;
    }

    private static bool ShouldOpenUi(string[] args)
    {
        if (args.Length == 0)
        {
            return true;
        }

        return args.Any(arg =>
            arg.Equals("--ui", StringComparison.OrdinalIgnoreCase)
            || arg.Equals("--config", StringComparison.OrdinalIgnoreCase)
            || arg.Equals("/ui", StringComparison.OrdinalIgnoreCase));
    }
}

internal static class ClaudeCodeProcessWrapperProgram
{
    private static readonly Regex PlaceholderRegex = new(@"\$\{(?<name>[A-Za-z0-9_:\-]+)\}", RegexOptions.Compiled);

    public static int Run(string[] rawArgs)
    {
        string wrapperPath = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? AppContext.BaseDirectory;
        string wrapperDirectory = Path.GetDirectoryName(wrapperPath) ?? Environment.CurrentDirectory;

        string? bundledPath = TryExtractBundledPath(rawArgs, out string[] forwardedArgs);
        string configPath = ResolveConfigPath(wrapperPath);
        WrapperConfig config;

        try
        {
            config = LoadConfig(configPath);
        }
        catch (Exception ex)
        {
            return Fail($"Failed to load wrapper config: {ex.Message}", null);
        }

        string configDirectory = Path.GetDirectoryName(configPath) ?? wrapperDirectory;

        var placeholders = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CLAUDE_BUNDLED_PATH"] = bundledPath,
            ["CLAUDE_WRAPPER_EXE_PATH"] = wrapperPath,
            ["CLAUDE_WRAPPER_EXE_DIR"] = wrapperDirectory,
            ["CLAUDE_WRAPPER_CONFIG_PATH"] = configPath,
            ["CLAUDE_WRAPPER_CONFIG_DIR"] = configDirectory,
        };

        string command = ResolveCommand(config.Command, bundledPath, placeholders, configDirectory);
        string workingDirectory = ResolveWorkingDirectory(config.WorkingDirectory, placeholders, configDirectory);
        string? logFile = ResolveOptionalPath(config.LogFile, placeholders, configDirectory);
        ShellMode shellMode = ResolveShellMode(config.Shell, command);
        List<string> childArguments = config.Arguments
            .Select(argument => ExpandValue(argument, placeholders))
            .Concat(forwardedArgs)
            .ToList();

        var logger = new WrapperLogger(logFile);
        logger.Info($"Wrapper started. command={command}, shell={shellMode}, bundledPath={bundledPath ?? "<none>"}");

        try
        {
            using Process process = StartChildProcess(
                command,
                childArguments,
                shellMode,
                config,
                workingDirectory,
                placeholders,
                configPath,
                logger);

            bool childExited = false;

            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                TryKillProcess(process, logger, "Ctrl+C");
            };

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                if (!childExited)
                {
                    TryKillProcess(process, logger, "wrapper exit");
                }
            };

            Task stdinPump = PumpInputAsync(process, logger);
            Task stdoutPump = PumpOutputAsync(process.StandardOutput.BaseStream, Console.OpenStandardOutput(), logger, "stdout");
            Task stderrPump = PumpOutputAsync(process.StandardError.BaseStream, Console.OpenStandardError(), logger, "stderr");

            process.WaitForExit();
            childExited = true;

            try
            {
                stdinPump.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                logger.Info($"stdin task completed with error: {ex.Message}");
            }

            Task.WhenAll(stdoutPump, stderrPump).GetAwaiter().GetResult();
            logger.Info($"Child process exited with code {process.ExitCode}.");
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            logger.Info($"Launch failed: {ex}");
            return Fail($"Failed to launch Claude child process: {ex.Message}", logger);
        }
    }

    public static string ResolveConfigPath(string wrapperPath)
    {
        string? envPath = Environment.GetEnvironmentVariable("CLAUDE_WRAPPER_CONFIG");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(envPath));
        }

        string wrapperDirectory = Path.GetDirectoryName(wrapperPath) ?? Environment.CurrentDirectory;
        string wrapperName = Path.GetFileNameWithoutExtension(wrapperPath);

        string[] candidates =
        [
            Path.Combine(wrapperDirectory, $"{wrapperName}.json"),
            Path.Combine(wrapperDirectory, "config.json"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ClaudeCodeProcessWrapper",
                "config.json"),
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return candidates[0];
    }

    public static WrapperConfig LoadConfig(string? configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
        {
            return new WrapperConfig();
        }

        using FileStream stream = File.OpenRead(configPath);
        WrapperConfig? config = JsonSerializer.Deserialize<WrapperConfig>(stream, WrapperJsonOptions);
        return Normalize(config ?? new WrapperConfig());
    }

    public static void SaveConfig(string configPath, WrapperConfig config)
    {
        string? directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        WrapperConfig normalized = Normalize(config.Clone());
        using FileStream stream = File.Create(configPath);
        JsonSerializer.Serialize(stream, normalized, WrapperJsonOptions);
    }

    private static Process StartChildProcess(
        string command,
        IReadOnlyList<string> childArguments,
        ShellMode shellMode,
        WrapperConfig config,
        string workingDirectory,
        IReadOnlyDictionary<string, string?> placeholders,
        string configPath,
        WrapperLogger logger)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        if (!config.InheritEnvironment)
        {
            startInfo.Environment.Clear();
        }

        startInfo.Environment["CLAUDE_WRAPPER_EXE_PATH"] = placeholders["CLAUDE_WRAPPER_EXE_PATH"] ?? string.Empty;
        startInfo.Environment["CLAUDE_WRAPPER_EXE_DIR"] = placeholders["CLAUDE_WRAPPER_EXE_DIR"] ?? string.Empty;
        startInfo.Environment["CLAUDE_WRAPPER_CONFIG_PATH"] = configPath;
        startInfo.Environment["CLAUDE_WRAPPER_CONFIG_DIR"] = placeholders["CLAUDE_WRAPPER_CONFIG_DIR"] ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(placeholders["CLAUDE_BUNDLED_PATH"]))
        {
            startInfo.Environment["CLAUDE_WRAPPER_BUNDLED_PATH"] = placeholders["CLAUDE_BUNDLED_PATH"]!;
        }

        foreach ((string key, string value) in config.Environment)
        {
            startInfo.Environment[key] = ExpandValue(value, placeholders);
        }

        ApplyConfiguredEnvironment(startInfo, config.ApiBaseUrlEnvName, config.ApiBaseUrl, placeholders);
        ApplyConfiguredEnvironment(startInfo, config.ApiKeyEnvName, config.ApiKey, placeholders);
        ApplyConfiguredEnvironment(startInfo, config.ModelEnvName, config.Model, placeholders);

        switch (shellMode)
        {
            case ShellMode.None:
                foreach (string argument in childArguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }
                break;
            case ShellMode.Cmd:
                startInfo.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
                startInfo.ArgumentList.Add("/d");
                startInfo.ArgumentList.Add("/c");
                if (IsBatchScript(command))
                {
                    startInfo.ArgumentList.Add("call");
                }

                startInfo.ArgumentList.Add(command);
                foreach (string argument in childArguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }
                break;
            case ShellMode.PowerShell:
                startInfo.FileName = "powershell.exe";
                startInfo.ArgumentList.Add("-NoLogo");
                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-ExecutionPolicy");
                startInfo.ArgumentList.Add("Bypass");
                startInfo.ArgumentList.Add("-File");
                startInfo.ArgumentList.Add(command);
                foreach (string argument in childArguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }
                break;
            case ShellMode.Pwsh:
                startInfo.FileName = "pwsh.exe";
                startInfo.ArgumentList.Add("-NoLogo");
                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-File");
                startInfo.ArgumentList.Add(command);
                foreach (string argument in childArguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }
                break;
            default:
                throw new InvalidOperationException($"Unsupported shell mode: {shellMode}");
        }

        logger.Info($"Starting child process. file={startInfo.FileName}, args={string.Join(" ", startInfo.ArgumentList.Select(QuoteForLog))}");
        Process? process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Process.Start returned null.");
        }

        return process;
    }

    private static void ApplyConfiguredEnvironment(
        ProcessStartInfo startInfo,
        string? envName,
        string? value,
        IReadOnlyDictionary<string, string?> placeholders)
    {
        if (string.IsNullOrWhiteSpace(envName) || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string resolvedEnvName = ExpandValue(envName, placeholders).Trim();
        string resolvedValue = ExpandValue(value, placeholders);
        if (!string.IsNullOrWhiteSpace(resolvedEnvName))
        {
            startInfo.Environment[resolvedEnvName] = resolvedValue;
        }
    }

    private static WrapperConfig Normalize(WrapperConfig config)
    {
        config.Shell = string.IsNullOrWhiteSpace(config.Shell) ? "auto" : config.Shell;
        config.ApiBaseUrlEnvName = string.IsNullOrWhiteSpace(config.ApiBaseUrlEnvName) ? "ANTHROPIC_BASE_URL" : config.ApiBaseUrlEnvName;
        config.ApiKeyEnvName = string.IsNullOrWhiteSpace(config.ApiKeyEnvName) ? "ANTHROPIC_API_KEY" : config.ApiKeyEnvName;
        config.ModelEnvName = string.IsNullOrWhiteSpace(config.ModelEnvName) ? "ANTHROPIC_MODEL" : config.ModelEnvName;
        config.Arguments = config.Arguments?.Where(item => !string.IsNullOrWhiteSpace(item)).ToList() ?? [];
        config.Environment = config.Environment is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(
                config.Environment.Where(pair => !string.IsNullOrWhiteSpace(pair.Key)),
                StringComparer.OrdinalIgnoreCase);
        return config;
    }

    private static string ResolveCommand(
        string? configuredCommand,
        string? bundledPath,
        IReadOnlyDictionary<string, string?> placeholders,
        string configDirectory)
    {
        string command = string.IsNullOrWhiteSpace(configuredCommand)
            ? bundledPath ?? "claude"
            : ExpandValue(configuredCommand, placeholders);

        return ResolvePathLikeValue(command, configDirectory);
    }

    private static string ResolveWorkingDirectory(
        string? configuredWorkingDirectory,
        IReadOnlyDictionary<string, string?> placeholders,
        string configDirectory)
    {
        if (string.IsNullOrWhiteSpace(configuredWorkingDirectory))
        {
            return Environment.CurrentDirectory;
        }

        string resolved = ExpandValue(configuredWorkingDirectory, placeholders);
        return ResolvePathLikeValue(resolved, configDirectory);
    }

    private static string? ResolveOptionalPath(
        string? value,
        IReadOnlyDictionary<string, string?> placeholders,
        string configDirectory)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string resolved = ExpandValue(value, placeholders);
        return ResolvePathLikeValue(resolved, configDirectory);
    }

    private static string ResolvePathLikeValue(string value, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (Path.IsPathRooted(value))
        {
            return Path.GetFullPath(value);
        }

        if (value.Contains(Path.DirectorySeparatorChar) || value.Contains(Path.AltDirectorySeparatorChar) || value.StartsWith(".", StringComparison.Ordinal))
        {
            return Path.GetFullPath(Path.Combine(baseDirectory, value));
        }

        return value;
    }

    private static string ExpandValue(string value, IReadOnlyDictionary<string, string?> placeholders)
    {
        string expanded = PlaceholderRegex.Replace(value, match =>
        {
            string name = match.Groups["name"].Value;

            if (name.StartsWith("ENV:", StringComparison.OrdinalIgnoreCase))
            {
                string envName = name[4..];
                return Environment.GetEnvironmentVariable(envName) ?? string.Empty;
            }

            if (placeholders.TryGetValue(name, out string? placeholderValue))
            {
                return placeholderValue ?? string.Empty;
            }

            return Environment.GetEnvironmentVariable(name) ?? string.Empty;
        });

        return Environment.ExpandEnvironmentVariables(expanded);
    }

    private static string? TryExtractBundledPath(string[] rawArgs, out string[] forwardedArgs)
    {
        if (rawArgs.Length > 0 && LooksLikeBundledBinaryPath(rawArgs[0]))
        {
            forwardedArgs = rawArgs[1..];
            return Path.GetFullPath(rawArgs[0]);
        }

        forwardedArgs = rawArgs;
        return null;
    }

    private static bool LooksLikeBundledBinaryPath(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || candidate.StartsWith("-", StringComparison.Ordinal))
        {
            return false;
        }

        if (!Path.IsPathRooted(candidate) && !candidate.Contains(Path.DirectorySeparatorChar) && !candidate.Contains(Path.AltDirectorySeparatorChar))
        {
            return false;
        }

        try
        {
            return File.Exists(candidate);
        }
        catch
        {
            return false;
        }
    }

    private static ShellMode ResolveShellMode(string shell, string command)
    {
        if (Enum.TryParse(shell, ignoreCase: true, out ShellMode explicitMode) && explicitMode != ShellMode.Auto)
        {
            return explicitMode;
        }

        string extension = Path.GetExtension(command);
        return extension.ToLowerInvariant() switch
        {
            ".cmd" or ".bat" => ShellMode.Cmd,
            ".ps1" => ShellMode.PowerShell,
            _ => ShellMode.None,
        };
    }

    private static bool IsBatchScript(string command)
    {
        string extension = Path.GetExtension(command);
        return extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase);
    }

    private static string QuoteForLog(string value)
    {
        return value.Any(char.IsWhiteSpace) ? $"\"{value}\"" : value;
    }

    private static void TryKillProcess(Process process, WrapperLogger logger, string reason)
    {
        try
        {
            if (!process.HasExited)
            {
                logger.Info($"Killing child process because of {reason}.");
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            logger.Info($"Failed to kill child process: {ex.Message}");
        }
    }

    private static int Fail(string message, WrapperLogger? logger)
    {
        string fullMessage = $"[ClaudeCodeProcessWrapper] {message}";
        logger?.Info(fullMessage);
        Console.Error.WriteLine(fullMessage);
        return 1;
    }

    private static Task PumpInputAsync(Process process, WrapperLogger logger)
    {
        return Task.Run(async () =>
        {
            try
            {
                await Console.OpenStandardInput().CopyToAsync(process.StandardInput.BaseStream);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
            {
                logger.Info($"stdin pump stopped: {ex.Message}");
            }
            finally
            {
                try
                {
                    process.StandardInput.Close();
                }
                catch
                {
                    // Ignore shutdown races.
                }
            }
        });
    }

    private static Task PumpOutputAsync(Stream source, Stream destination, WrapperLogger logger, string label)
    {
        return Task.Run(async () =>
        {
            try
            {
                await source.CopyToAsync(destination);
                await destination.FlushAsync();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                logger.Info($"{label} pump stopped: {ex.Message}");
            }
        });
    }

    private static JsonSerializerOptions WrapperJsonOptions => _wrapperJsonOptions ??= CreateWrapperJsonOptions();

    private static JsonSerializerOptions? _wrapperJsonOptions;

    private static JsonSerializerOptions CreateWrapperJsonOptions()
    {
        return new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }
}

internal static class ClaudeCodeUserSettingsStore
{
    private static readonly HashSet<string> KnownEnvKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ANTHROPIC_BASE_URL",
        "ANTHROPIC_API_KEY",
        "ANTHROPIC_MODEL",
        "ANTHROPIC_REASONING_MODEL",
        "ANTHROPIC_DEFAULT_HAIKU_MODEL",
        "ANTHROPIC_DEFAULT_SONNET_MODEL",
        "ANTHROPIC_DEFAULT_OPUS_MODEL",
    };

    public static ClaudeCodeSettingsState LoadUserSettings()
    {
        string path = GetUserSettingsPath();
        JsonObject root = LoadRoot(path);
        JsonObject env = EnsureEnv(root);

        var state = new ClaudeCodeSettingsState
        {
            SettingsPath = path,
            Root = root,
            TopLevelModel = GetString(root, "model"),
            BaseUrl = GetString(env, "ANTHROPIC_BASE_URL"),
            ApiKey = GetString(env, "ANTHROPIC_API_KEY"),
            ApiModel = GetString(env, "ANTHROPIC_MODEL"),
            ReasoningModel = GetString(env, "ANTHROPIC_REASONING_MODEL"),
            DefaultHaikuModel = GetString(env, "ANTHROPIC_DEFAULT_HAIKU_MODEL"),
            DefaultSonnetModel = GetString(env, "ANTHROPIC_DEFAULT_SONNET_MODEL"),
            DefaultOpusModel = GetString(env, "ANTHROPIC_DEFAULT_OPUS_MODEL"),
        };

        foreach ((string key, JsonNode? valueNode) in env)
        {
            if (KnownEnvKeys.Contains(key))
            {
                continue;
            }

            string? value = NodeToString(valueNode);
            if (value is not null)
            {
                state.OtherEnvironment[key] = value;
            }
        }

        return state;
    }

    public static ClaudeCodeSettingsState CreateEmptyUserSettings()
    {
        return new ClaudeCodeSettingsState
        {
            SettingsPath = GetUserSettingsPath(),
            Root = new JsonObject(),
        };
    }

    public static void SaveUserSettings(ClaudeCodeSettingsState state)
    {
        JsonObject root = state.Root.DeepClone() as JsonObject ?? new JsonObject();
        JsonObject env = EnsureEnv(root);

        var desiredEnvironment = new Dictionary<string, string>(state.OtherEnvironment, StringComparer.OrdinalIgnoreCase);
        AddIfNotEmpty(desiredEnvironment, "ANTHROPIC_BASE_URL", state.BaseUrl);
        AddIfNotEmpty(desiredEnvironment, "ANTHROPIC_API_KEY", state.ApiKey);
        AddIfNotEmpty(desiredEnvironment, "ANTHROPIC_MODEL", state.ApiModel);
        AddIfNotEmpty(desiredEnvironment, "ANTHROPIC_REASONING_MODEL", state.ReasoningModel);
        AddIfNotEmpty(desiredEnvironment, "ANTHROPIC_DEFAULT_HAIKU_MODEL", state.DefaultHaikuModel);
        AddIfNotEmpty(desiredEnvironment, "ANTHROPIC_DEFAULT_SONNET_MODEL", state.DefaultSonnetModel);
        AddIfNotEmpty(desiredEnvironment, "ANTHROPIC_DEFAULT_OPUS_MODEL", state.DefaultOpusModel);

        foreach (string existingKey in env.Select(pair => pair.Key).ToList())
        {
            if (!desiredEnvironment.ContainsKey(existingKey))
            {
                env.Remove(existingKey);
            }
        }

        foreach ((string key, string value) in desiredEnvironment.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            env[key] = value;
        }

        if (env.Count == 0)
        {
            root.Remove("env");
        }
        else
        {
            root["env"] = env;
        }

        SetOrRemove(root, "model", state.TopLevelModel);

        string path = state.SettingsPath;
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        CreateBackupIfNeeded(path);
        string json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static string GetUserSettingsPath()
    {
        string? overriddenPath = Environment.GetEnvironmentVariable("CLAUDE_CODE_SETTINGS_PATH");
        if (!string.IsNullOrWhiteSpace(overriddenPath))
        {
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(overriddenPath));
        }

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".claude", "settings.json");
    }

    private static JsonObject LoadRoot(string path)
    {
        if (!File.Exists(path))
        {
            return new JsonObject();
        }

        string json = File.ReadAllText(path, Encoding.UTF8);
        JsonNode? parsed = JsonNode.Parse(
            json,
            nodeOptions: null,
            documentOptions: new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });

        if (parsed is JsonObject rootObject)
        {
            return rootObject;
        }

        throw new InvalidOperationException("Claude settings file must contain a JSON object.");
    }

    private static JsonObject EnsureEnv(JsonObject root)
    {
        if (root["env"] is JsonObject envObject)
        {
            return envObject;
        }

        var created = new JsonObject();
        root["env"] = created;
        return created;
    }

    private static string? GetString(JsonObject root, string propertyName)
    {
        return NodeToString(root[propertyName]);
    }

    private static string? NodeToString(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<string>(out string? stringValue))
            {
                return stringValue;
            }

            return jsonValue.ToJsonString();
        }

        return node.ToJsonString();
    }

    private static void SetOrRemove(JsonObject root, string propertyName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            root.Remove(propertyName);
            return;
        }

        root[propertyName] = value.Trim();
    }

    private static void AddIfNotEmpty(IDictionary<string, string> environment, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            environment[key] = value.Trim();
        }
    }

    private static void CreateBackupIfNeeded(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        string? settingsDirectory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(settingsDirectory))
        {
            return;
        }

        string backupDirectory = Path.Combine(settingsDirectory, "backups");
        Directory.CreateDirectory(backupDirectory);

        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        string backupFileName = $"{Path.GetFileNameWithoutExtension(path)}.{timestamp}{Path.GetExtension(path)}";
        string backupPath = Path.Combine(backupDirectory, backupFileName);
        File.Copy(path, backupPath, overwrite: false);
    }
}

internal sealed class ClaudeCodeSettingsState
{
    public string SettingsPath { get; init; } = string.Empty;

    public JsonObject Root { get; init; } = new();

    public string? BaseUrl { get; set; }

    public string? ApiKey { get; set; }

    public string? TopLevelModel { get; set; }

    public string? ApiModel { get; set; }

    public string? ReasoningModel { get; set; }

    public string? DefaultHaikuModel { get; set; }

    public string? DefaultSonnetModel { get; set; }

    public string? DefaultOpusModel { get; set; }

    public Dictionary<string, string> OtherEnvironment { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class WrapperConfig
{
    public string? Command { get; set; }

    public string Shell { get; set; } = "auto";

    public List<string> Arguments { get; set; } = [];

    public Dictionary<string, string> Environment { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool InheritEnvironment { get; set; } = true;

    public string? WorkingDirectory { get; set; }

    public string? LogFile { get; set; }

    public string? ApiBaseUrl { get; set; }

    public string ApiBaseUrlEnvName { get; set; } = "ANTHROPIC_BASE_URL";

    public string? ApiKey { get; set; }

    public string ApiKeyEnvName { get; set; } = "ANTHROPIC_API_KEY";

    public string? Model { get; set; }

    public string ModelEnvName { get; set; } = "ANTHROPIC_MODEL";

    public WrapperConfig Clone()
    {
        return new WrapperConfig
        {
            Command = Command,
            Shell = Shell,
            Arguments = [.. Arguments],
            Environment = new Dictionary<string, string>(Environment, StringComparer.OrdinalIgnoreCase),
            InheritEnvironment = InheritEnvironment,
            WorkingDirectory = WorkingDirectory,
            LogFile = LogFile,
            ApiBaseUrl = ApiBaseUrl,
            ApiBaseUrlEnvName = ApiBaseUrlEnvName,
            ApiKey = ApiKey,
            ApiKeyEnvName = ApiKeyEnvName,
            Model = Model,
            ModelEnvName = ModelEnvName,
        };
    }
}

internal enum ShellMode
{
    Auto,
    None,
    Cmd,
    PowerShell,
    Pwsh,
}

internal sealed class WrapperLogger
{
    private readonly string? _logFile;
    private readonly object _gate = new();

    public WrapperLogger(string? logFile)
    {
        _logFile = logFile;
    }

    public void Info(string message)
    {
        if (string.IsNullOrWhiteSpace(_logFile))
        {
            return;
        }

        try
        {
            string? directory = Path.GetDirectoryName(_logFile);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {message}{Environment.NewLine}";
            lock (_gate)
            {
                File.AppendAllText(_logFile, line, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch
        {
            // Logging must never block the wrapper.
        }
    }
}
