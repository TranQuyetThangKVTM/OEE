using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Oee.Application.Production.LineOff;
using Oee.Domain.Oee;
using Oee.Infrastructure.Production.Automation;

namespace Oee.Infrastructure.Production.LineOff;

public sealed class JsonLineOffCalendarStore(
    IOptions<PlanAutomationStorageOptions> options,
    IHostEnvironment? environment = null) : ILineOffCalendarStore
{
    private string Root => Path.GetFullPath(options.Value.StoragePath, environment?.ContentRootPath ?? AppContext.BaseDirectory);

    public async Task<LineOffCalendar> ReadAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(Root, "line-off-periods.json");
        FileStream file;
        try
        {
            file = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous);
        }
        catch (FileNotFoundException) { return LineOffCalendar.Empty; }
        catch (DirectoryNotFoundException) { return LineOffCalendar.Empty; }
        await using var ownedFile = file;
        if (file.Length > 4 * 1024 * 1024) throw new InvalidDataException("Lịch Off quá lớn.");
        var calendar = await JsonSerializer.DeserializeAsync<LineOffCalendar>(file, cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Lịch Off bị lỗi.");
        Validate(calendar);
        return calendar;
    }

    public async Task<ILineOffCalendarSession> OpenAsync(CancellationToken cancellationToken)
    {
        var root = Root;
        Directory.CreateDirectory(root);
        var watch = Stopwatch.StartNew();
        FileStream lease;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                lease = new FileStream(Path.Combine(root, "line-off-periods.lock"), FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.None, 1, FileOptions.Asynchronous);
                break;
            }
            catch (IOException) when (watch.Elapsed < TimeSpan.FromSeconds(10))
            { await Task.Delay(100, cancellationToken); }
        }
        try { return new Session(Path.Combine(root, "line-off-periods.json"), lease, await ReadAsync(cancellationToken)); }
        catch { await lease.DisposeAsync(); throw; }
    }

    private static void Validate(LineOffCalendar calendar)
    {
        if (calendar.Revision == Guid.Empty || calendar.Periods is null || calendar.Periods.Count > 10000
            || calendar.Periods.Select(x => x.Id).Distinct().Count() != calendar.Periods.Count)
            throw new InvalidDataException("Lịch Off không hợp lệ.");
        foreach (var entry in calendar.Periods)
        {
            if (entry.Id == Guid.Empty || entry.WorkCenterId == Guid.Empty || string.IsNullOrWhiteSpace(entry.TimeZoneId)
                || entry.Note?.Length > 500 || entry.End - entry.Start > TimeSpan.FromDays(366))
                throw new InvalidDataException("Khoảng Off không hợp lệ.");
            OeeTimeWindowValue.Create(entry.Start, entry.End);
        }
    }

    private sealed class Session(string path, FileStream lease, LineOffCalendar calendar) : ILineOffCalendarSession
    {
        public LineOffCalendar Calendar { get; private set; } = calendar;

        public async Task SaveAsync(LineOffCalendar value, CancellationToken cancellationToken)
        {
            Validate(value);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await JsonSerializer.SerializeAsync(file, value, cancellationToken: cancellationToken);
                    if (file.Length > 4 * 1024 * 1024) throw new InvalidDataException("Lịch Off quá lớn.");
                    await file.FlushAsync(cancellationToken);
                    file.Flush(true);
                }
                File.Move(temporary, path, true);
                Calendar = value;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public ValueTask DisposeAsync() => lease.DisposeAsync();
    }
}
