using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Oee.Application.Production.Automation;

namespace Oee.Infrastructure.Production.Automation;

public sealed class AutomaticShiftReportFiles(IOptions<PlanAutomationStorageOptions> options,
    IHostEnvironment? environment = null) : IAutomaticShiftReportFiles
{
    private string DirectoryPath => Path.Combine(Path.GetFullPath(options.Value.StoragePath,
        environment?.ContentRootPath ?? AppContext.BaseDirectory), "Reports");
    private string FilePath(Guid id) => Path.Combine(DirectoryPath, id.ToString("N") + ".pdf");

    public async Task SaveAsync(Guid id, byte[] content, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0 || content.Length > 50 * 1024 * 1024)
            throw new InvalidDataException("Nội dung PDF trống hoặc vượt quá 50 MB.");
        Directory.CreateDirectory(DirectoryPath);
        var path = FilePath(id);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, content, ct);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public Task<byte[]> ReadAsync(Guid id, CancellationToken ct) => File.ReadAllBytesAsync(FilePath(id), ct);
    public Task DeleteAsync(Guid id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        File.Delete(FilePath(id));
        return Task.CompletedTask;
    }
}
