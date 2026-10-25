using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ReVibranceGUI.Services;

/// <summary>
/// Information about an available application update.
/// </summary>
public sealed record UpdateInfo
{
    public required Version Version { get; init; }
    public required string TagName { get; init; }
    public required string HtmlUrl { get; init; }
    public required string ReleaseNotes { get; init; }
    public required string ZipUrl { get; init; }
    public string? Sha256Url { get; init; }
    public long ZipSizeBytes { get; init; }
}

/// <summary>
/// Progress reporting payload during update download and verification.
/// </summary>
public sealed record UpdateProgress
{
    public double Percentage { get; init; }
    public long BytesDownloaded { get; init; }
    public long? TotalBytes { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
}

/// <summary>
/// Service contract for checking, downloading, verifying, and applying application updates.
/// </summary>
public interface IUpdateService
{
    Task<UpdateInfo?> CheckForUpdateAsync(Version currentVersion, CancellationToken ct = default);
    Task<string> DownloadAndPrepareUpdateAsync(UpdateInfo update, string? baseUpdatesDir = null, IProgress<UpdateProgress>? progress = null, CancellationToken ct = default);
    void ApplyUpdateAndRestart(string stagingDir, string? targetDir = null, string? exeName = null);
}

/// <summary>
/// Native in-app auto-updater communicating with GitHub Releases.
/// Downloads portable release packages, verifies SHA256 integrity, extracts to staging,
/// and launches a detached PowerShell helper to atomically overwrite and restart the app.
/// </summary>
public class UpdateService : IUpdateService, IDisposable
{
    private const string DefaultReleasesApiUrl = "https://api.github.com/repos/roscocheeta/re-vibrance-gui/releases/latest";
    private readonly HttpClient _httpClient;
    private readonly bool _disposeClient;
    private bool _disposed;

    public UpdateService(HttpClient? httpClient = null)
    {
        if (httpClient != null)
        {
            _httpClient = httpClient;
            _disposeClient = false;
        }
        else
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ReVibranceGUI-Updater/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
            _disposeClient = true;
        }
    }

    /// <summary>
    /// Checks the latest GitHub release and determines if a newer version is available.
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync(Version currentVersion, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, DefaultReleasesApiUrl);
            if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            {
                request.Headers.UserAgent.ParseAdd($"ReVibranceGUI-Updater/{currentVersion}");
            }

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn($"Update check returned status code: {response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ParseReleaseJson(json, currentVersion);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Logger.Warn("Failed to check for updates", ex);
            return null;
        }
    }

    /// <summary>
    /// Parses GitHub release JSON payload to find newer valid versions with zip assets.
    /// </summary>
    public static UpdateInfo? ParseReleaseJson(string json, Version currentVersion)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("tag_name", out var tagElement))
        {
            return null;
        }

        string rawTag = tagElement.GetString() ?? string.Empty;
        string cleanTag = rawTag.TrimStart('v', 'V');
        if (!Version.TryParse(cleanTag, out var releaseVersion))
        {
            return null;
        }

        if (releaseVersion <= currentVersion)
        {
            return null;
        }

        string htmlUrl = root.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() ?? string.Empty : string.Empty;
        string body = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? string.Empty : string.Empty;

        string? zipUrl = null;
        long zipSize = 0;
        string? sha256Url = null;

        if (root.TryGetProperty("assets", out var assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assetsEl.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var nameEl) || !asset.TryGetProperty("browser_download_url", out var dlEl))
                {
                    continue;
                }

                string name = nameEl.GetString() ?? string.Empty;
                string downloadUrl = dlEl.GetString() ?? string.Empty;

                if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    zipUrl = downloadUrl;
                    if (asset.TryGetProperty("size", out var sizeEl) && sizeEl.TryGetInt64(out var size))
                    {
                        zipSize = size;
                    }
                }
                else if (name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase))
                {
                    sha256Url = downloadUrl;
                }
            }
        }

        if (string.IsNullOrEmpty(zipUrl))
        {
            return null;
        }

        return new UpdateInfo
        {
            Version = releaseVersion,
            TagName = rawTag,
            HtmlUrl = htmlUrl,
            ReleaseNotes = body,
            ZipUrl = zipUrl,
            Sha256Url = sha256Url,
            ZipSizeBytes = zipSize
        };
    }

    /// <summary>
    /// Extracts the 64-character SHA256 hex string from standard checksum file contents.
    /// </summary>
    public static string? ExtractSha256Hash(string sha256FileContent)
    {
        if (string.IsNullOrWhiteSpace(sha256FileContent))
        {
            return null;
        }

        var tokens = sha256FileContent.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            if (token.Length == 64 && token.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
            {
                return token.ToLowerInvariant();
            }
        }

        return null;
    }

    /// <summary>
    /// Computes and verifies the SHA256 checksum of an in-memory byte array against expected file content.
    /// </summary>
    public static bool VerifyChecksum(byte[] fileData, string sha256FileContent)
    {
        var expected = ExtractSha256Hash(sha256FileContent);
        if (string.IsNullOrEmpty(expected))
        {
            return false;
        }

        var hashBytes = SHA256.HashData(fileData);
        var actual = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Computes and verifies the SHA256 checksum of a file on disk against expected file content.
    /// </summary>
    public static async Task<bool> VerifyFileChecksumAsync(string filePath, string sha256FileContent, CancellationToken ct = default)
    {
        var expected = ExtractSha256Hash(sha256FileContent);
        if (string.IsNullOrEmpty(expected))
        {
            return false;
        }

        using var stream = File.OpenRead(filePath);
        var hashBytes = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        var actual = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Downloads the update package and checksum, verifies integrity, and extracts into a staging directory.
    /// </summary>
    public async Task<string> DownloadAndPrepareUpdateAsync(
        UpdateInfo update,
        string? baseUpdatesDir = null,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken ct = default)
    {
        baseUpdatesDir ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ReVibranceGUI",
            "updates");

        Directory.CreateDirectory(baseUpdatesDir);

        string? expectedSha256 = null;
        if (!string.IsNullOrEmpty(update.Sha256Url))
        {
            progress?.Report(new UpdateProgress
            {
                Percentage = 0,
                BytesDownloaded = 0,
                TotalBytes = update.ZipSizeBytes,
                StatusMessage = "Fetching checksum..."
            });

            try
            {
                using var shaResponse = await _httpClient.GetAsync(update.Sha256Url, ct).ConfigureAwait(false);
                if (shaResponse.IsSuccessStatusCode)
                {
                    var rawSha = await shaResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    expectedSha256 = ExtractSha256Hash(rawSha);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to download checksum file, proceeding with caution", ex);
            }
        }

        string zipFilePath = Path.Combine(baseUpdatesDir, $"update-{update.TagName}.zip");
        if (File.Exists(zipFilePath))
        {
            File.Delete(zipFilePath);
        }

        // Stream download with progress reporting
        using (var response = await _httpClient.GetAsync(update.ZipUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength ?? (update.ZipSizeBytes > 0 ? update.ZipSizeBytes : null);
            using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var fileStream = new FileStream(zipFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
                totalRead += bytesRead;

                double pct = totalBytes.HasValue && totalBytes.Value > 0
                    ? (double)totalRead / totalBytes.Value * 100.0
                    : 0.0;

                progress?.Report(new UpdateProgress
                {
                    Percentage = Math.Min(pct, 100.0),
                    BytesDownloaded = totalRead,
                    TotalBytes = totalBytes,
                    StatusMessage = totalBytes.HasValue
                        ? $"Downloading: {totalRead / (1024.0 * 1024.0):F1} MB / {totalBytes.Value / (1024.0 * 1024.0):F1} MB"
                        : $"Downloading: {totalRead / (1024.0 * 1024.0):F1} MB"
                });
            }
        }

        // Verify SHA256 Checksum if available
        if (!string.IsNullOrEmpty(expectedSha256))
        {
            progress?.Report(new UpdateProgress
            {
                Percentage = 100,
                BytesDownloaded = update.ZipSizeBytes,
                TotalBytes = update.ZipSizeBytes,
                StatusMessage = "Verifying package integrity..."
            });

            bool verified = await VerifyFileChecksumAsync(zipFilePath, expectedSha256, ct).ConfigureAwait(false);
            if (!verified)
            {
                try { File.Delete(zipFilePath); } catch { }
                throw new InvalidDataException("Package integrity verification failed! The downloaded file's SHA256 checksum does not match.");
            }
        }

        // Extract to staging directory
        progress?.Report(new UpdateProgress
        {
            Percentage = 100,
            BytesDownloaded = update.ZipSizeBytes,
            TotalBytes = update.ZipSizeBytes,
            StatusMessage = "Extracting update..."
        });

        string stagingDir = Path.Combine(baseUpdatesDir, "staging");
        if (Directory.Exists(stagingDir))
        {
            Directory.Delete(stagingDir, recursive: true);
        }
        Directory.CreateDirectory(stagingDir);

        ZipFile.ExtractToDirectory(zipFilePath, stagingDir, overwriteFiles: true);

        // Normalize if package root is contained inside a single folder
        if (!File.Exists(Path.Combine(stagingDir, "ReVibranceGUI.exe")))
        {
            var nestedExe = Directory.GetFiles(stagingDir, "ReVibranceGUI.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (nestedExe != null)
            {
                var nestedDir = Path.GetDirectoryName(nestedExe);
                if (!string.IsNullOrEmpty(nestedDir) && nestedDir != stagingDir)
                {
                    foreach (var file in Directory.GetFiles(nestedDir))
                    {
                        var dest = Path.Combine(stagingDir, Path.GetFileName(file));
                        File.Move(file, dest, overwrite: true);
                    }
                    foreach (var dir in Directory.GetDirectories(nestedDir))
                    {
                        var dest = Path.Combine(stagingDir, Path.GetFileName(dir));
                        if (!Directory.Exists(dest))
                        {
                            Directory.Move(dir, dest);
                        }
                    }
                }
            }
        }

        return stagingDir;
    }

    /// <summary>
    /// Tests if a directory is writable by current process credentials without elevation.
    /// </summary>
    public static bool IsDirectoryWritable(string directoryPath)
    {
        try
        {
            var testPath = Path.Combine(directoryPath, $".perm_test_{Guid.NewGuid():N}.tmp");
            using (var fs = File.Create(testPath, 1, FileOptions.DeleteOnClose))
            {
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Generates the standalone PowerShell script to wait for the host app to exit,
    /// copy extracted files over the installation directory, and restart the application.
    /// </summary>
    public static string BuildUpdateScript(int parentPid, string stagingDir, string targetDir, string exeName)
    {
        string escapedStaging = stagingDir.TrimEnd('\\', '/').Replace("'", "''");
        string escapedTarget = targetDir.TrimEnd('\\', '/').Replace("'", "''");
        string escapedExeName = exeName.Replace("'", "''");

        var sb = new StringBuilder();
        sb.AppendLine("# ReVibranceGUI Auto-Update Script");
        sb.AppendLine($"$parentPid = {parentPid}");
        sb.AppendLine($"$stagingDir = '{escapedStaging}'");
        sb.AppendLine($"$targetDir = '{escapedTarget}'");
        sb.AppendLine($"$exePath = '{escapedTarget}\\{escapedExeName}'");
        sb.AppendLine();
        sb.AppendLine("# 1. Wait for parent application to exit");
        sb.AppendLine("try {");
        sb.AppendLine("    $parent = Get-Process -Id $parentPid -ErrorAction SilentlyContinue");
        sb.AppendLine("    if ($parent) {");
        sb.AppendLine("        $null = $parent.WaitForExit(15000)");
        sb.AppendLine("    }");
        sb.AppendLine("} catch { }");
        sb.AppendLine();
        sb.AppendLine("# 2. Force terminate if still lingering");
        sb.AppendLine("try {");
        sb.AppendLine("    Stop-Process -Id $parentPid -Force -ErrorAction SilentlyContinue");
        sb.AppendLine("} catch { }");
        sb.AppendLine();
        sb.AppendLine("Start-Sleep -Milliseconds 750");
        sb.AppendLine();
        sb.AppendLine("# 3. Overwrite target application files with retry loop");
        sb.AppendLine("$maxRetries = 5");
        sb.AppendLine("$retryCount = 0");
        sb.AppendLine("$copied = $false");
        sb.AppendLine("while (-not $copied -and $retryCount -lt $maxRetries) {");
        sb.AppendLine("    try {");
        sb.AppendLine("        Copy-Item -Path \"$stagingDir\\*\" -Destination $targetDir -Recurse -Force -ErrorAction Stop");
        sb.AppendLine("        $copied = $true");
        sb.AppendLine("    } catch {");
        sb.AppendLine("        $retryCount++");
        sb.AppendLine("        Start-Sleep -Seconds 1");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# 4. Launch the updated application");
        sb.AppendLine("Start-Process -FilePath $exePath -WorkingDirectory $targetDir");
        sb.AppendLine();
        sb.AppendLine("# 5. Clean up staging files and updater script");
        sb.AppendLine("try { Remove-Item -Path $stagingDir -Recurse -Force -ErrorAction SilentlyContinue } catch { }");
        sb.AppendLine("try { Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue } catch { }");

        return sb.ToString();
    }

    /// <summary>
    /// Generates updater script, spawns PowerShell helper, and terminates current process to unlock files.
    /// Elevates via UAC if the installation folder is not user-writable.
    /// </summary>
    public void ApplyUpdateAndRestart(string stagingDir, string? targetDir = null, string? exeName = null)
    {
        targetDir ??= AppContext.BaseDirectory;
        exeName ??= Path.GetFileName(Environment.ProcessPath ?? "ReVibranceGUI.exe");
        int parentPid = Environment.ProcessId;

        bool writable = IsDirectoryWritable(targetDir);

        string updatesDir = Path.GetDirectoryName(stagingDir) ?? stagingDir;
        string scriptPath = Path.Combine(updatesDir, "apply-update.ps1");
        string scriptContent = BuildUpdateScript(parentPid, stagingDir, targetDir, exeName);
        File.WriteAllText(scriptPath, scriptContent, Encoding.UTF8);

        string pwsh = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell\\v1.0\\powershell.exe");

        if (!File.Exists(pwsh))
        {
            pwsh = "powershell.exe";
        }

        var psi = new ProcessStartInfo
        {
            FileName = pwsh,
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\""
        };

        if (!writable)
        {
            // Directory requires administrative privileges (e.g. Program Files)
            psi.Verb = "runas";
            psi.UseShellExecute = true;
        }
        else
        {
            // User-writable directory (e.g. AppData, Desktop, Portable folder)
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
        }

        Logger.Info($"Spawning updater script: {scriptPath} (Elevated: {!writable})");
        Process.Start(psi);

        // Terminate parent application cleanly so updater can overwrite locked binaries
        Environment.Exit(0);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_disposeClient)
            {
                _httpClient.Dispose();
            }
            _disposed = true;
        }
    }
}
