using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Oee.Application.Production.Automation;

namespace Oee.Infrastructure.Production.Automation;

/// <summary>Lịch lưu ngoài wwwroot, ghi atomic và khóa file xuyên process để chịu được IIS recycle chồng lấp.</summary>
public sealed class JsonProductionPlanScheduleStore(IOptions<PlanAutomationStorageOptions> options,
    IHostEnvironment? environment = null) : IProductionPlanScheduleStore
{
    private string Root => Path.GetFullPath(options.Value.StoragePath, environment?.ContentRootPath ?? AppContext.BaseDirectory);
    public bool Exists => File.Exists(Path.Combine(Root, "schedule.json"));

    public async Task<IProductionPlanScheduleSession> OpenAsync(CancellationToken ct)
    {
        var root = Root;
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "schedule.json");
        var watch = Stopwatch.StartNew();
        FileStream lease;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                lease = new FileStream(Path.Combine(root, "schedule.lock"), FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.None, 1, FileOptions.Asynchronous);
                break;
            }
            catch (IOException) when (watch.Elapsed < TimeSpan.FromSeconds(10))
            { await Task.Delay(100, ct); }
        }
        try
        {
            ProductionPlanSchedule? schedule = null;
            if (File.Exists(path))
            {
                if (new FileInfo(path).Length > 65536) throw new InvalidDataException("Tệp lịch tự động không hợp lệ.");
                await using var file = File.OpenRead(path);
                schedule = await JsonSerializer.DeserializeAsync<ProductionPlanSchedule>(file, cancellationToken: ct)
                    ?? throw new InvalidDataException("Tệp lịch tự động trống hoặc bị lỗi.");
                if (schedule.Revision == Guid.Empty || schedule.SamplePlanId == Guid.Empty
                    || string.IsNullOrWhiteSpace(schedule.TimeZoneId))
                    throw new InvalidDataException("Tệp lịch tự động thiếu dữ liệu bắt buộc.");
            }
            return new Session(path, lease, schedule);
        }
        catch
        {
            await lease.DisposeAsync();
            throw;
        }
    }

    private sealed class Session(string path, FileStream lease, ProductionPlanSchedule? schedule) : IProductionPlanScheduleSession
    {
        public ProductionPlanSchedule? Schedule { get; private set; } = schedule;

        public async Task SaveAsync(ProductionPlanSchedule value, CancellationToken ct)
        {
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await JsonSerializer.SerializeAsync(file, value, cancellationToken: ct);
                    await file.FlushAsync(ct);
                    file.Flush(true);
                }
                File.Move(temporary, path, true);
                Schedule = value;
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public ValueTask DisposeAsync() => lease.DisposeAsync();
    }
}
