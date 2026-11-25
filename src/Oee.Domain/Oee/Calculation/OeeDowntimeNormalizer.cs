namespace Oee.Domain.Oee;

/// <summary>Cắt khoảng dừng theo kỳ, loại bản trùng và xử lý phần chồng lấn theo thứ tự hiện tại.</summary>
internal static class OeeDowntimeNormalizer
{
    /// <summary>Trả các đoạn không chồng lấn và bổ sung cờ bất thường vào danh sách được cung cấp.</summary>
    public static List<OeeDowntimeInputValue> Normalize(
        IEnumerable<OeeDowntimeInputValue> source, DateTimeOffset start, DateTimeOffset end, List<string> anomalies)
    {
        // LINQ này chạy in-memory trong Domain: lọc event hợp lệ, cắt biên kỳ rồi sắp theo thời gian.
        var ordered = source
            .Where(item => item.EndAt > item.StartAt && item.EndAt > start && item.StartAt < end)
            .Select(item => item with { StartAt = item.StartAt < start ? start : item.StartAt, EndAt = item.EndAt > end ? end : item.EndAt })
            .OrderBy(item => item.StartAt).ThenBy(item => item.EndAt).ToArray();
        var result = new List<OeeDowntimeInputValue>();
        foreach (var item in ordered)
        {
            if (result.Count > 0 && item == result[^1])
            {
                anomalies.Add("DuplicateDowntime");
                continue;
            }
            if (result.Count > 0 && item.StartAt < result[^1].EndAt)
            {
                anomalies.Add("OverlappingDowntime");
                var previous = result[^1];
                if (item.EndAt > previous.EndAt) result.Add(item with { StartAt = previous.EndAt });
                continue;
            }
            result.Add(item);
        }
        return result;
    }
}
