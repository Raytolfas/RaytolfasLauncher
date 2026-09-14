// Copyright (C) 2026 Raytolfas
// This file is part of Raytolfas Launcher.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core.Version;

namespace RaytolfasLauncher
{
    public record JavaInfo(
        string Path,
        int? MajorVersion,
        bool Is64Bit,
        string Architecture,
        string FullVersion
    );

    public static class JavaRuntimeManager
    {
        private static readonly HttpClient httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true
        })
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        private static readonly ConcurrentDictionary<string, JavaInfo> infoCache = new(
            PlatformHelper.IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        static JavaRuntimeManager()
        {
            httpClient.DefaultRequestHeaders.Add("User-Agent", "RaytolfasLauncher/0.0.4.1");
        }

        public static int GetRequiredJavaMajorVersion(string? versionId, IVersion? version = null)
        {
            if (version?.JavaVersion != null && !string.IsNullOrWhiteSpace(version.JavaVersion.MajorVersion))
            {
                if (int.TryParse(version.JavaVersion.MajorVersion, out int parsedMajor) && parsedMajor > 0)
                    return parsedMajor;
            }

            if (string.IsNullOrWhiteSpace(versionId))
                return 21;

            string v = versionId.Trim();

            // Snapshot formats: e.g. 24w14a or 21w19a
            var snapshotMatch = Regex.Match(v, @"^(\d{2})w(\d{2})[a-z]$", RegexOptions.IgnoreCase);
            if (snapshotMatch.Success)
            {
                int year = int.Parse(snapshotMatch.Groups[1].Value);
                int week = int.Parse(snapshotMatch.Groups[2].Value);
                if (year > 24 || (year == 24 && week >= 14))
                    return 21;
                if (year > 21 || (year == 21 && week >= 19))
                    return 17;
                return 8;
            }

            // Semantic versions: e.g. 1.21.1, 1.20.4, 1.16.5
            var match = Regex.Match(v, @"^(\d+)\.(\d+)(?:\.(\d+))?");
            if (match.Success)
            {
                int major = int.Parse(match.Groups[1].Value);
                int minor = int.Parse(match.Groups[2].Value);
                int patch = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

                if (major == 1)
                {
                    if (minor > 20 || (minor == 20 && patch >= 5))
                        return 21;
                    if (minor >= 18)
                        return 17;
                    if (minor == 17)
                        return 16;
                    return 8;
                }
                else if (major >= 2)
                {
                    return 21;
                }
            }

            return 21;
        }

        public static JavaInfo DetectJavaInfo(string javawPath)
        {
            if (string.IsNullOrWhiteSpace(javawPath))
                return new JavaInfo("", null, true, "unknown", "");

            string normalizedPath = javawPath.Trim().Trim('"');
            string resolvedPath = PlatformHelper.ResolveSymlink(normalizedPath);

            if (infoCache.TryGetValue(resolvedPath, out var cached))
                return cached;

            int? majorVersion = null;
            bool is64Bit = true;
            string arch = "unknown";
            string fullVersion = "";

            // 1. Try reading from release file if available
            string? releaseVersion = TryReadReleaseFile(resolvedPath, "JAVA_VERSION");
            string? releaseArch = TryReadReleaseFile(resolvedPath, "OS_ARCH");

            if (!string.IsNullOrWhiteSpace(releaseVersion))
            {
                fullVersion = releaseVersion;
                majorVersion = ParseMajorVersionString(releaseVersion);
            }

            if (!string.IsNullOrWhiteSpace(releaseArch))
            {
                arch = releaseArch;
                if (arch.Contains("64", StringComparison.OrdinalIgnoreCase))
                    is64Bit = true;
                else if (arch.Contains("86", StringComparison.OrdinalIgnoreCase) || arch.Contains("32", StringComparison.OrdinalIgnoreCase))
                    is64Bit = false;
            }

            // 2. If version or arch is not determined, run java -version
            if (majorVersion == null || arch == "unknown")
            {
                var cmdResult = ProbeJavaProcess(resolvedPath);
                if (cmdResult != null)
                {
                    if (majorVersion == null)
                    {
                        majorVersion = cmdResult.MajorVersion;
                        fullVersion = cmdResult.FullVersion;
                    }

                    if (arch == "unknown")
                    {
                        arch = cmdResult.Architecture;
                        is64Bit = cmdResult.Is64Bit;
                    }
                }
            }

            if (arch == "unknown")
            {
                if (resolvedPath.IndexOf("(x86)", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    resolvedPath.IndexOf("\\x86\\", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    is64Bit = false;
                    arch = "x86";
                }
                else
                {
                    is64Bit = Environment.Is64BitOperatingSystem;
                    arch = is64Bit ? "x64" : "x86";
                }
            }

            var info = new JavaInfo(resolvedPath, majorVersion, is64Bit, arch, fullVersion);
            infoCache[resolvedPath] = info;
            infoCache[normalizedPath] = info;
            return info;
        }

        private static int? ParseMajorVersionString(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return null;
            string clean = version.Trim().Trim('"');
            string[] parts = clean.Split(new[] { '.', '_', '-', '+' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;

            if (parts[0] == "1" && parts.Length > 1 && int.TryParse(parts[1], out int legacyMajor))
                return legacyMajor;

            return int.TryParse(parts[0], out int major) ? major : null;
        }

        private static string? TryReadReleaseFile(string javaPath, string key)
        {
            try
            {
                var cur = new FileInfo(javaPath).Directory;
                for (int i = 0; i < 3 && cur != null; i++)
                {
                    string relPath = Path.Combine(cur.FullName, "release");
                    if (File.Exists(relPath))
                    {
                        string prefix = key + "=";
                        foreach (var line in File.ReadLines(relPath))
                        {
                            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            {
                                var parts = line.Split('=', 2);
                                if (parts.Length >= 2)
                                    return parts[1].Trim().Trim('"');
                            }
                        }
                    }
                    cur = cur.Parent;
                }
            }
            catch { }

            return null;
        }

        private static JavaInfo? ProbeJavaProcess(string javaPath)
        {
            try
            {
                if (!File.Exists(javaPath)) return null;

                var psi = new ProcessStartInfo
                {
                    FileName = javaPath,
                    Arguments = "-version",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return null;

                string output = proc.StandardError.ReadToEnd() + " " + proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(3000);

                int? major = null;
                string fullVer = "";
                var matchVer = Regex.Match(output, @"version\s+""?([0-9\._\-+]+)""?", RegexOptions.IgnoreCase);
                if (matchVer.Success)
                {
                    fullVer = matchVer.Groups[1].Value;
                    major = ParseMajorVersionString(fullVer);
                }

                bool is64 = true;
                string arch = "unknown";
                if (output.Contains("64-Bit", StringComparison.OrdinalIgnoreCase) ||
                    output.Contains("x86_64", StringComparison.OrdinalIgnoreCase) ||
                    output.Contains("amd64", StringComparison.OrdinalIgnoreCase) ||
                    output.Contains("aarch64", StringComparison.OrdinalIgnoreCase) ||
                    output.Contains("arm64", StringComparison.OrdinalIgnoreCase))
                {
                    is64 = true;
                    arch = (output.Contains("aarch64", StringComparison.OrdinalIgnoreCase) || output.Contains("arm64", StringComparison.OrdinalIgnoreCase))
                        ? "aarch64" : "x64";
                }
                else if (output.Contains("32-Bit", StringComparison.OrdinalIgnoreCase) ||
                         output.Contains("i386", StringComparison.OrdinalIgnoreCase) ||
                         output.Contains("armv7", StringComparison.OrdinalIgnoreCase))
                {
                    is64 = false;
                    arch = "x86";
                }

                return new JavaInfo(javaPath, major, is64, arch, fullVer);
            }
            catch
            {
                return null;
            }
        }

        public static bool IsJavaCompatible(int? javaMajor, int requiredMajor)
        {
            if (javaMajor == null) return false;

            if (requiredMajor >= 21)
                return javaMajor >= 21;
            if (requiredMajor == 17)
                return javaMajor >= 17;
            if (requiredMajor == 16)
                return javaMajor == 16 || javaMajor == 17;
            if (requiredMajor <= 8)
                return javaMajor <= 8;

            return javaMajor == requiredMajor;
        }

        public static string? FindCompatibleJava(int requiredMajor, IEnumerable<string> candidatePaths)
        {
            var detected = candidatePaths
                .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
                .Select(p => DetectJavaInfo(p))
                .ToList();

            // 1. Exact major match + 64-bit
            var exact64 = detected
                .Where(j => j.MajorVersion == requiredMajor && j.Is64Bit)
                .OrderByDescending(j => j.Path.Contains(".minecraft", StringComparison.OrdinalIgnoreCase) || j.Path.Contains("runtime", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
            if (exact64 != null) return exact64.Path;

            // 2. Compatible match + 64-bit
            var comp64 = detected
                .Where(j => IsJavaCompatible(j.MajorVersion, requiredMajor) && j.Is64Bit)
                .OrderBy(j => Math.Abs((j.MajorVersion ?? 0) - requiredMajor))
                .FirstOrDefault();
            if (comp64 != null) return comp64.Path;

            // 3. Exact major match (even if 32-bit)
            var exactAny = detected
                .Where(j => j.MajorVersion == requiredMajor)
                .FirstOrDefault();
            if (exactAny != null) return exactAny.Path;

            // 4. Any compatible match
            var compAny = detected
                .Where(j => IsJavaCompatible(j.MajorVersion, requiredMajor))
                .OrderBy(j => Math.Abs((j.MajorVersion ?? 0) - requiredMajor))
                .FirstOrDefault();
            if (compAny != null) return compAny.Path;

            return null;
        }

        public static async Task<string> DownloadAndInstallAdoptiumAsync(
            int requiredMajor,
            string minecraftPath,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            string os = PlatformHelper.IsWindows ? "windows" : PlatformHelper.IsMacOS ? "mac" : "linux";
            string arch = PlatformHelper.IsArm64 ? "aarch64" : PlatformHelper.IsX64 ? "x64" : "x32";

            string runtimeRootDir = Path.Combine(minecraftPath, "runtime", $"adoptium-java-{requiredMajor}-{arch}");
            Directory.CreateDirectory(runtimeRootDir);

            // Check if already downloaded
            string? existingExecutable = FindJavaExecutableInDirectory(runtimeRootDir);
            if (!string.IsNullOrWhiteSpace(existingExecutable) && File.Exists(existingExecutable))
            {
                PlatformHelper.SetExecutablePermission(existingExecutable);
                var existingInfo = DetectJavaInfo(existingExecutable);
                if (IsJavaCompatible(existingInfo.MajorVersion, requiredMajor))
                {
                    return existingExecutable;
                }
            }

            // Adoptium download URL
            string downloadUrl = $"https://api.adoptium.net/v3/binary/latest/{requiredMajor}/ga/{os}/{arch}/jre/hotspot/normal/adoptium";
            string tempArchiveFile = Path.Combine(runtimeRootDir, PlatformHelper.IsWindows ? "runtime.zip" : "runtime.tar.gz");

            using (var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                long? totalBytes = response.Content.Headers.ContentLength;

                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = new FileStream(tempArchiveFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                var buffer = new byte[81920];
                long totalRead = 0;
                int read;
                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                    totalRead += read;
                    if (totalBytes.HasValue && totalBytes.Value > 0)
                    {
                        int percent = (int)Math.Clamp((totalRead * 100) / totalBytes.Value, 0, 100);
                        progress?.Report(percent);
                    }
                }
            }

            // Extract archive
            if (PlatformHelper.IsWindows)
            {
                ZipFile.ExtractToDirectory(tempArchiveFile, runtimeRootDir, overwriteFiles: true);
            }
            else
            {
                using var fs = File.OpenRead(tempArchiveFile);
                using var gz = new GZipStream(fs, CompressionMode.Decompress);
                TarFile.ExtractToDirectory(gz, runtimeRootDir, overwriteFiles: true);
            }

            try
            {
                File.Delete(tempArchiveFile);
            }
            catch { }

            string? newExecutable = FindJavaExecutableInDirectory(runtimeRootDir);
            if (string.IsNullOrWhiteSpace(newExecutable) || !File.Exists(newExecutable))
                throw new FileNotFoundException($"Не удалось обнаружить исполняемый файл Java после распаковки в {runtimeRootDir}");

            PlatformHelper.SetExecutablePermission(newExecutable);

            // Also mark all files in bin directory as executable on Unix
            if (!PlatformHelper.IsWindows)
            {
                string? binDir = Path.GetDirectoryName(newExecutable);
                if (!string.IsNullOrWhiteSpace(binDir) && Directory.Exists(binDir))
                {
                    foreach (var file in Directory.GetFiles(binDir))
                    {
                        PlatformHelper.SetExecutablePermission(file);
                    }
                }
            }

            return newExecutable;
        }

        public static string? FindJavaExecutableInDirectory(string directory)
        {
            if (!Directory.Exists(directory)) return null;

            string targetName = PlatformHelper.IsWindows ? "javaw.exe" : "java";
            string altName = PlatformHelper.IsWindows ? "java.exe" : "java";

            // Direct check in bin/
            string directBin = Path.Combine(directory, "bin", targetName);
            if (File.Exists(directBin)) return directBin;

            string directAlt = Path.Combine(directory, "bin", altName);
            if (File.Exists(directAlt)) return directAlt;

            // Search in subdirectories (Adoptium archive creates a root dir like jdk-21.0.2+13-jre)
            try
            {
                foreach (var file in Directory.EnumerateFiles(directory, targetName, SearchOption.AllDirectories))
                {
                    if (file.EndsWith(Path.Combine("bin", targetName), StringComparison.OrdinalIgnoreCase))
                        return file;
                }

                if (PlatformHelper.IsWindows)
                {
                    foreach (var file in Directory.EnumerateFiles(directory, altName, SearchOption.AllDirectories))
                    {
                        if (file.EndsWith(Path.Combine("bin", altName), StringComparison.OrdinalIgnoreCase))
                            return file;
                    }
                }
            }
            catch { }

            return null;
        }
    }
}
