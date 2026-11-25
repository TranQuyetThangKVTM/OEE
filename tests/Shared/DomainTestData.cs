using System.Collections;
using System.Reflection;
using Oee.Domain.Common;
using Oee.Domain.Entities.Production;

namespace Oee.Tests.Fixtures;

/// <summary>
/// Chỉ dùng để dựng dữ liệu lịch sử/không hợp lệ cho test SQL và projection.
/// Test hành vi entity phải gọi factory thật; production không được tham chiếu helper này.
/// </summary>
internal static class DomainTestData
{
    public static T Create<T>(params (string Property, object? Value)[] values) where T : Entity
    {
        var entity = typeof(T) == typeof(ProductionPlanEntity)
            ? (T)(Entity)ProductionPlanEntity.Create(Guid.NewGuid(), Guid.NewGuid(), "FIXTURE",
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero), 100)
            : (T)(Activator.CreateInstance(typeof(T), nonPublic: true) ?? throw new InvalidOperationException("Cannot create test entity."));
        foreach (var (property, value) in values) Set(entity, property, value);
        return entity;
    }

    public static void Set(Entity entity, string propertyName, object? value)
    {
        var property = entity.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Unknown fixture property {propertyName}.");
        // Reflection trên derived type không trả private setter được khai báo ở lớp base.
        property = property.DeclaringType!.GetProperty(propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!;
        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (value is not null && !targetType.IsInstanceOfType(value))
        {
            // Giữ implicit numeric conversion của object initializer cũ (100 -> decimal/long).
            if (targetType.IsEnum) value = Enum.ToObject(targetType, value);
            else if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(targetType))
                value = Convert.ChangeType(value, targetType, System.Globalization.CultureInfo.InvariantCulture);
        }
        if (property.SetMethod is not null) { property.SetValue(entity, value); return; }
        if (entity is SampleProductionPlanEntity && propertyName == nameof(SampleProductionPlanEntity.Items))
        {
            var field = typeof(SampleProductionPlanEntity).GetField("items", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var list = (IList)field.GetValue(entity)!;
            list.Clear();
            foreach (var item in (IEnumerable)value!) list.Add(item);
            return;
        }
        throw new InvalidOperationException($"Fixture property {propertyName} has no setter.");
    }
}
