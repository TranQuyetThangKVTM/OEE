using System.Text.Json;
using System.Text.Json.Nodes;
using Oee.Application.Contracts.Audit;

namespace Oee.Infrastructure.Audit;

/// <summary>
/// Chuyển JSON audit nhiều phiên bản sang các field before/after thân thiện cho dialog MudBlazor.
/// </summary>
/// <remarks>
/// Mapper không query entity hiện tại nên audit vẫn mở sau khi entity đổi tên/xóa. Dữ liệu nhạy cảm
/// được loại bỏ đệ quy trước khi tạo field và trước cả raw JSON. JSON cũ lỗi/thiếu trường trả kết quả
/// rỗng thay vì làm hỏng trang audit.
/// </remarks>
internal static class AuditPresentationMapper
{
    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password", "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "AccessToken",
        "RefreshToken", "ApiKey", "Secret", "ConnectionString", "Cookie", "Authorization",
        "ClientSecret", "CertificatePassword", "PrivateKey",
    };

    /// <summary>Map AuditLog sang detail DTO và chỉ kèm raw JSON khi caller đủ capability.</summary>
    public static AuditLogDetailResponse Map(
        Domain.Platform.AuditLogEntity log,
        bool includeRawJson)
    {
        var sanitized = Sanitize(log.Changes);
        return new AuditLogDetailResponse(
            log.Id,
            log.Timestamp,
            log.UserId,
            log.UserName,
            log.Action,
            AuditPresentationCatalog.ActionName(log.Action),
            log.EntityType,
            AuditPresentationCatalog.EntityName(log.EntityType),
            log.EntityId,
            log.EntitySubject,
            log.IpAddress,
            ToFields(sanitized),
            includeRawJson ? Pretty(sanitized) : null);
    }

    /// <summary>Hỗ trợ cấu trúc Before/After và cấu trúc field-level của các phiên bản audit cũ.</summary>
    private static AuditFieldChangeResponse[] ToFields(JsonObject? root)
    {
        if (root is null)
        {
            return [];
        }

        if (TryObject(root, "Before", out var beforeRoot) && TryObject(root, "After", out var afterRoot))
        {
            return beforeRoot.Select(pair => pair.Key)
                .Concat(afterRoot.Select(pair => pair.Key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(name => !IsSensitive(name))
                .Select(name => Field(name, beforeRoot[name], afterRoot[name]))
                .Where(field => !string.Equals(field.Before, field.After, StringComparison.Ordinal))
                .ToArray();
        }

        if (TryObject(root, "Before", out beforeRoot))
        {
            return beforeRoot.Where(pair => !IsSensitive(pair.Key))
                .Select(pair => Field(pair.Key, pair.Value, null)).ToArray();
        }

        if (TryObject(root, "After", out afterRoot))
        {
            return afterRoot.Where(pair => !IsSensitive(pair.Key))
                .Select(pair => Field(pair.Key, null, pair.Value)).ToArray();
        }

        return root
            .Where(pair => !IsSensitive(pair.Key))
            .Select(pair =>
            {
                if (pair.Value is JsonObject change)
                {
                    var before = Find(change, "Before", "From", "Old");
                    var after = Find(change, "After", "To", "New");
                    if (before is not null || after is not null)
                    {
                        return Field(pair.Key, before, after);
                    }
                }

                return Field(pair.Key, null, pair.Value);
            })
            .Where(field => !string.Equals(field.Before, field.After, StringComparison.Ordinal))
            .ToArray();
    }

    private static AuditFieldChangeResponse Field(string name, JsonNode? before, JsonNode? after)
        => new(name, AuditPresentationCatalog.FieldName(name), Display(before), Display(after));

    private static string? Display(JsonNode? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonValue scalar && scalar.TryGetValue<bool>(out var boolean))
        {
            return boolean ? "Có" : "Không";
        }

        return value is JsonValue ? value.ToString() : value.ToJsonString();
    }

    /// <summary>Parse JSON và xóa key nhạy cảm; JSON không hợp lệ được coi là không có nội dung.</summary>
    private static JsonObject? Sanitize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var root = JsonNode.Parse(json) as JsonObject;
            if (root is not null)
            {
                RemoveSensitive(root);
            }

            return root;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Duyệt đệ quy object/array để secret lồng sâu cũng không lọt ra raw view.
    private static void RemoveSensitive(JsonObject value)
    {
        foreach (var key in value.Select(pair => pair.Key).ToArray())
        {
            if (IsSensitive(key))
            {
                value.Remove(key);
                continue;
            }

            if (value[key] is JsonObject child)
            {
                RemoveSensitive(child);
            }
            else if (value[key] is JsonArray array)
            {
                foreach (var item in array.OfType<JsonObject>())
                {
                    RemoveSensitive(item);
                }
            }
        }
    }

    private static bool IsSensitive(string name) => SensitiveNames.Contains(name)
        || name.Contains("Password", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Token", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Secret", StringComparison.OrdinalIgnoreCase)
        || name.Contains("PrivateKey", StringComparison.OrdinalIgnoreCase);

    private static JsonNode? Find(JsonObject value, params string[] names)
        => value.FirstOrDefault(pair => names.Contains(pair.Key, StringComparer.OrdinalIgnoreCase)).Value;

    private static bool TryObject(JsonObject value, string name, out JsonObject result)
    {
        result = value.FirstOrDefault(pair => pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value as JsonObject ?? new();
        return result.Count > 0;
    }

    private static string? Pretty(JsonObject? value)
        => value?.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

}
