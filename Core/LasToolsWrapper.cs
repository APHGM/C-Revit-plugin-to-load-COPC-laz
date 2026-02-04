using System;
using System.Diagnostics;
using System.IO;

namespace RvtLoadLaz.Core
{
    /// <summary>
    /// Wrapper for LAStools laszip.exe command line utility
    /// Converts LAZ (compressed) to LAS (uncompressed) for safe reading
    /// </summary>
    public static class LasToolsWrapper
    {
        private static string GetLasZipExePath()
        {
            // LAStools executable in Tools folder
            string exePath = Path.Combine(
                Path.GetDirectoryName(typeof(LasToolsWrapper).Assembly.Location),
                "Tools",
                "laszip.exe"
            );

            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException($"laszip.exe not found at: {exePath}");
            }

            return exePath;
        }

        /// <summary>
        /// Convert LAZ file to uncompressed LAS file
        /// </summary>
        /// <param name="lazPath">Input LAZ file path</param>
        /// <param name="lasPath">Output LAS file path</param>
        /// <returns>True if successful</returns>
        public static bool ConvertLazToLas(string lazPath, string lasPath, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                if (!File.Exists(lazPath))
                {
                    errorMessage = $"Input file not found: {lazPath}";
                    return false;
                }

                string laszipExe = GetLasZipExePath();

                // Build command: laszip.exe -i input.laz -o output.las
                var startInfo = new ProcessStartInfo
                {
                    FileName = laszipExe,
                    Arguments = $"-i \"{lazPath}\" -o \"{lasPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(laszipExe)
                };

                Trace.WriteLine($"🔧 Converting LAZ to LAS...");
                Trace.WriteLine($"   Command: {startInfo.FileName} {startInfo.Arguments}");

                using (var process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        errorMessage = $"laszip.exe failed with exit code {process.ExitCode}\n{error}";
                        Trace.WriteLine($"❌ Conversion failed: {errorMessage}");
                        return false;
                    }

                    if (!File.Exists(lasPath))
                    {
                        errorMessage = "Output LAS file was not created";
                        return false;
                    }

                    Trace.WriteLine($"✓ Conversion successful: {new FileInfo(lasPath).Length:N0} bytes");
                    return true;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Exception during conversion: {ex.Message}";
                Trace.WriteLine($"❌ {errorMessage}");
                return false;
            }
        }

        /// <summary>
        /// Get info about LAZ file without converting
        /// </summary>
        public static LasFileInfo GetFileInfo(string lazPath)
        {
            // For now, we'll get this by reading the header
            // LAStools also has lasinfo.exe but we can read header directly
            return LasReader.ReadHeader(lazPath);
        }
    }
}
