using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using ReVibranceGUI.Services;
using Xunit;

namespace ReVibranceGUI.Tests;

public class UpdateServiceTests
{
    private const string SampleReleaseJson = """
    {
      "tag_name": "v1.0.8",
      "html_url": "https://github.com/roscocheeta/re-vibrance-gui/releases/tag/v1.0.8",
      "body": "## What's Changed\n* Added in-app auto-updater\n* Fixed DPI scaling",
      "assets": [
        {
          "name": "ReVibranceGUI-v1.0.8.zip",
          "browser_download_url": "https://github.com/roscocheeta/re-vibrance-gui/releases/download/v1.0.8/ReVibranceGUI-v1.0.8.zip",
          "size": 10485760
        },
        {
          "name": "ReVibranceGUI-v1.0.8.zip.sha256",
          "browser_download_url": "https://github.com/roscocheeta/re-vibrance-gui/releases/download/v1.0.8/ReVibranceGUI-v1.0.8.zip.sha256",
          "size": 95
        }
      ]
    }
    """;

    [Fact]
    public void ParseReleaseJson_ValidNewerRelease_ReturnsUpdateInfo()
    {
        var currentVersion = new Version(1, 0, 7);
        var update = UpdateService.ParseReleaseJson(SampleReleaseJson, currentVersion);

        Assert.NotNull(update);
        Assert.Equal(new Version(1, 0, 8), update.Version);
        Assert.Equal("v1.0.8", update.TagName);
        Assert.Equal("https://github.com/roscocheeta/re-vibrance-gui/releases/tag/v1.0.8", update.HtmlUrl);
        Assert.Equal("https://github.com/roscocheeta/re-vibrance-gui/releases/download/v1.0.8/ReVibranceGUI-v1.0.8.zip", update.ZipUrl);
        Assert.Equal("https://github.com/roscocheeta/re-vibrance-gui/releases/download/v1.0.8/ReVibranceGUI-v1.0.8.zip.sha256", update.Sha256Url);
        Assert.Equal(10485760, update.ZipSizeBytes);
        Assert.Contains("Added in-app auto-updater", update.ReleaseNotes);
    }

    [Theory]
    [InlineData("1.0.8")]
    [InlineData("1.0.9")]
    [InlineData("2.0.0")]
    public void ParseReleaseJson_CurrentOrNewerAlreadyInstalled_ReturnsNull(string currentVerStr)
    {
        var currentVersion = new Version(currentVerStr);
        var update = UpdateService.ParseReleaseJson(SampleReleaseJson, currentVersion);

        Assert.Null(update);
    }

    [Fact]
    public void ParseReleaseJson_ReleaseMissingZipAsset_ReturnsNull()
    {
        var jsonNoZip = """
        {
          "tag_name": "v1.0.9",
          "html_url": "https://github.com/roscocheeta/re-vibrance-gui/releases/tag/v1.0.9",
          "body": "Notes",
          "assets": [
            {
              "name": "checksums.txt",
              "browser_download_url": "https://github.com/.../checksums.txt",
              "size": 50
            }
          ]
        }
        """;

        var update = UpdateService.ParseReleaseJson(jsonNoZip, new Version(1, 0, 7));
        Assert.Null(update);
    }

    [Fact]
    public void ParseReleaseJson_InvalidJsonOrMissingTagName_ReturnsNull()
    {
        var jsonNoTag = """{ "body": "no tag" }""";
        var update = UpdateService.ParseReleaseJson(jsonNoTag, new Version(1, 0, 7));
        Assert.Null(update);
    }

    [Theory]
    [InlineData("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855  ReVibranceGUI-v1.0.8.zip", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData("   e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855\r\n", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    public void ExtractSha256Hash_ValidFormats_ExtractsNormalizedHash(string input, string expected)
    {
        var result = UpdateService.ExtractSha256Hash(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a sha256 hash")]
    [InlineData("1234567890abcdef")] // too short (16 chars)
    public void ExtractSha256Hash_InvalidInputs_ReturnsNull(string input)
    {
        var result = UpdateService.ExtractSha256Hash(input);
        Assert.Null(result);
    }

    [Fact]
    public void VerifyChecksum_MatchesPayload_ReturnsTrue()
    {
        byte[] payload = Encoding.UTF8.GetBytes("ReVibranceGUI-AutoUpdate-Payload-Test-String");
        byte[] hash = SHA256.HashData(payload);
        string sha256File = $"{Convert.ToHexString(hash)}  package.zip";

        bool verified = UpdateService.VerifyChecksum(payload, sha256File);
        Assert.True(verified);
    }

    [Fact]
    public void VerifyChecksum_Mismatch_ReturnsFalse()
    {
        byte[] payload = Encoding.UTF8.GetBytes("ReVibranceGUI-AutoUpdate-Payload-Test-String");
        byte[] wrongPayload = Encoding.UTF8.GetBytes("Tampered-Payload");
        byte[] hash = SHA256.HashData(wrongPayload);
        string sha256File = $"{Convert.ToHexString(hash)}  package.zip";

        bool verified = UpdateService.VerifyChecksum(payload, sha256File);
        Assert.False(verified);
    }

    [Fact]
    public void BuildUpdateScript_ContainsSafeEscapingAndExpectedSteps()
    {
        int pid = 9876;
        string staging = @"C:\Users\Ross Cheetah\AppData\Local\ReVibranceGUI\updates\staging";
        string target = @"C:\Program Files\ReVibranceGUI App";
        string exe = "ReVibranceGUI.exe";

        string script = UpdateService.BuildUpdateScript(pid, staging, target, exe);

        Assert.Contains("$parentPid = 9876", script);
        Assert.Contains("$stagingDir = 'C:\\Users\\Ross Cheetah\\AppData\\Local\\ReVibranceGUI\\updates\\staging'", script);
        Assert.Contains("$targetDir = 'C:\\Program Files\\ReVibranceGUI App'", script);
        Assert.Contains("$exePath = 'C:\\Program Files\\ReVibranceGUI App\\ReVibranceGUI.exe'", script);
        Assert.Contains("WaitForExit", script);
        Assert.Contains("Copy-Item", script);
        Assert.Contains("Start-Process -FilePath $exePath -WorkingDirectory $targetDir", script);
        Assert.Contains("Remove-Item", script);
    }

    [Fact]
    public void IsDirectoryWritable_TempDirectory_ReturnsTrue()
    {
        string tempDir = Path.GetTempPath();
        bool writable = UpdateService.IsDirectoryWritable(tempDir);
        Assert.True(writable);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WithMockHttp_FindsNewVersion()
    {
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleReleaseJson, Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(mockHandler);
        using var service = new UpdateService(httpClient);

        var update = await service.CheckForUpdateAsync(new Version(1, 0, 0));

        Assert.NotNull(update);
        Assert.Equal(new Version(1, 0, 8), update.Version);
    }

    [Fact]
    public async Task DownloadAndPrepareUpdateAsync_DownloadsExtractsAndVerifiesSha256()
    {
        string tempBase = Path.Combine(Path.GetTempPath(), $"ReVibrance_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempBase);

        try
        {
            // 1. Create a mock zip containing a dummy ReVibranceGUI.exe
            byte[] zipBytes;
            using (var ms = new MemoryStream())
            {
                using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var entry = archive.CreateEntry("ReVibranceGUI.exe");
                    using var es = entry.Open();
                    byte[] dummyExeContent = Encoding.UTF8.GetBytes("MOCK_EXE_BINARY_DATA");
                    es.Write(dummyExeContent, 0, dummyExeContent.Length);
                }
                zipBytes = ms.ToArray();
            }

            // 2. Compute SHA256 of the mock zip
            byte[] hashBytes = SHA256.HashData(zipBytes);
            string sha256Content = $"{Convert.ToHexString(hashBytes)}  ReVibranceGUI-v1.0.8.zip";

            // 3. Mock HTTP client to serve zip and sha256
            var mockHandler = new MockHttpMessageHandler(req =>
            {
                if (req.RequestUri != null && req.RequestUri.AbsoluteUri.EndsWith(".sha256"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(sha256Content, Encoding.UTF8, "text/plain")
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(zipBytes)
                };
            });

            using var httpClient = new HttpClient(mockHandler);
            using var service = new UpdateService(httpClient);

            var updateInfo = new UpdateInfo
            {
                Version = new Version(1, 0, 8),
                TagName = "v1.0.8",
                HtmlUrl = "https://github.com/...",
                ReleaseNotes = "Test Notes",
                ZipUrl = "https://github.com/.../ReVibranceGUI-v1.0.8.zip",
                Sha256Url = "https://github.com/.../ReVibranceGUI-v1.0.8.zip.sha256",
                ZipSizeBytes = zipBytes.Length
            };

            var progressReports = new List<UpdateProgress>();
            var progress = new Progress<UpdateProgress>(p => progressReports.Add(p));

            string stagingDir = await service.DownloadAndPrepareUpdateAsync(
                updateInfo,
                baseUpdatesDir: tempBase,
                progress: progress);

            Assert.True(Directory.Exists(stagingDir));
            string extractedExe = Path.Combine(stagingDir, "ReVibranceGUI.exe");
            Assert.True(File.Exists(extractedExe));
            Assert.Equal("MOCK_EXE_BINARY_DATA", File.ReadAllText(extractedExe));
        }
        finally
        {
            if (Directory.Exists(tempBase))
            {
                try { Directory.Delete(tempBase, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public async Task DownloadAndPrepareUpdateAsync_ChecksumMismatch_ThrowsInvalidDataException()
    {
        string tempBase = Path.Combine(Path.GetTempPath(), $"ReVibrance_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempBase);

        try
        {
            byte[] zipBytes = Encoding.UTF8.GetBytes("MOCK_ZIP_BYTES");
            string wrongSha256 = "0000000000000000000000000000000000000000000000000000000000000000  file.zip";

            var mockHandler = new MockHttpMessageHandler(req =>
            {
                if (req.RequestUri != null && req.RequestUri.AbsoluteUri.EndsWith(".sha256"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(wrongSha256, Encoding.UTF8, "text/plain")
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(zipBytes)
                };
            });

            using var httpClient = new HttpClient(mockHandler);
            using var service = new UpdateService(httpClient);

            var updateInfo = new UpdateInfo
            {
                Version = new Version(1, 0, 8),
                TagName = "v1.0.8",
                HtmlUrl = "https://github.com/...",
                ReleaseNotes = "Test Notes",
                ZipUrl = "https://github.com/.../ReVibranceGUI-v1.0.8.zip",
                Sha256Url = "https://github.com/.../ReVibranceGUI-v1.0.8.zip.sha256",
                ZipSizeBytes = zipBytes.Length
            };

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                service.DownloadAndPrepareUpdateAsync(updateInfo, baseUpdatesDir: tempBase));
        }
        finally
        {
            if (Directory.Exists(tempBase))
            {
                try { Directory.Delete(tempBase, recursive: true); } catch { }
            }
        }
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
