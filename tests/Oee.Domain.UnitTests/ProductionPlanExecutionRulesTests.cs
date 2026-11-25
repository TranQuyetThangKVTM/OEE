using Oee.Domain.Common;
using Oee.Domain.Entities.Production;

namespace Oee.Domain.UnitTests;

public sealed class ProductionPlanExecutionRulesTests
{
    [Theory]
    [InlineData(-1, false, 0, ProductionPlanExecutionStatusEnum.Pending)]
    [InlineData(0, false, 0, ProductionPlanExecutionStatusEnum.InProgress)]
    [InlineData(7, true, 150, ProductionPlanExecutionStatusEnum.InProgress)]
    [InlineData(8, false, 0, ProductionPlanExecutionStatusEnum.MissingOutput)]
    [InlineData(9, true, 0, ProductionPlanExecutionStatusEnum.CompletedUnderTarget)]
    [InlineData(8, true, 99, ProductionPlanExecutionStatusEnum.CompletedUnderTarget)]
    [InlineData(8, true, 100, ProductionPlanExecutionStatusEnum.CompletedOnTarget)]
    [InlineData(9, true, 150, ProductionPlanExecutionStatusEnum.CompletedOnTarget)]
    public void UsesHalfOpenWindowAndDistinguishesMissingFromRecordedZero(int hoursFromStart,
        bool hasOutput, int actual, ProductionPlanExecutionStatusEnum expected)
    {
        var start = new DateTimeOffset(2026, 10, 19, 22, 0, 0, TimeSpan.FromHours(7));
        var result = ProductionPlanExecutionRules.GetStatus(start, start.AddHours(8), 100m, actual,
            hasOutput, start.AddHours(hoursFromStart).ToUniversalTime());
        Assert.Equal(expected, result);
    }
}
