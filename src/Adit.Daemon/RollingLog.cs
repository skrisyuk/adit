using Serilog;

namespace Adit.Daemon;

public static class RollingLog
{
    public static Serilog.Core.Logger Create(string path, long sizeLimit = 20 * 1024 * 1024, int retainedFiles = 10)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(path,
                fileSizeLimitBytes: sizeLimit,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: retainedFiles,
                buffered: false,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
