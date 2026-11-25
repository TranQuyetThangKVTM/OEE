using Oee.Domain.Common;

namespace Oee.Domain.UnitTests;

public sealed class DomainGuardTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    [InlineData("ABCD")]
    public void RequiredKeepsExistingFailuresAndInfersTheParameter(string? value)
    {
        var inferred = Assert.Throws<ArgumentException>(() => DomainGuard.Required(value, 3));
        var explicitName = Assert.Throws<ArgumentException>(() => DomainGuard.Required(value, 3, nameof(value)));

        Assert.Equal(nameof(value), inferred.ParamName);
        Assert.Equal(explicitName.Message, inferred.Message);
        Assert.Contains("is required and cannot exceed 3 characters.", inferred.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StringsAreTrimmedBeforeCheckingTheMaximumLength()
    {
        const string value = " ABC ";

        Assert.Equal("ABC", DomainGuard.Required(value, 3));
        Assert.Equal("ABC", DomainGuard.Optional(value, 3));

        var inferred = Assert.Throws<ArgumentException>(() => DomainGuard.Optional(value, 2));
        var explicitName = Assert.Throws<ArgumentException>(() => DomainGuard.Optional(value, 2, nameof(value)));

        Assert.Equal(nameof(value), inferred.ParamName);
        Assert.Equal(explicitName.Message, inferred.Message);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(" \t ", null)]
    [InlineData(" A ", "A")]
    public void OptionalKeepsNullWhitespaceAndValidValueBehavior(string? value, string? expected)
    {
        Assert.Equal(expected, DomainGuard.Optional(value, 1));
    }

    [Fact]
    public void RequiredIdRejectsEmptyAndPreservesTheSuppliedId()
    {
        var id = Guid.Empty;
        var exception = Assert.Throws<ArgumentException>(() => DomainGuard.RequiredId(id));

        Assert.Equal(nameof(id), exception.ParamName);

        id = Guid.NewGuid();
        Assert.Equal(id, DomainGuard.RequiredId(id));
    }

    [Fact]
    public void OptionalIdForwardsTheOriginalParameterName()
    {
        Guid? id = Guid.Empty;
        var exception = Assert.Throws<ArgumentException>(() => DomainGuard.OptionalId(id));

        Assert.Equal(nameof(id), exception.ParamName);
        Assert.Null(DomainGuard.OptionalId(null));

        id = Guid.NewGuid();
        Assert.Equal(id, DomainGuard.OptionalId(id));
    }

    [Fact]
    public void DefinedRejectsAnUnknownValueAndAcceptsAKnownValue()
    {
        var status = (RecordStatusEnum)99;
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => DomainGuard.Defined(status));

        Assert.Equal(nameof(status), exception.ParamName);
        Assert.Equal(RecordStatusEnum.Active, DomainGuard.Defined(RecordStatusEnum.Active));
    }

    [Fact]
    public void NumericGuardsKeepTheirBoundariesAndInferParameterNames()
    {
        var quantity = -1m;
        var negative = Assert.Throws<ArgumentOutOfRangeException>(() => DomainGuard.NonNegative(quantity));
        Assert.Equal(nameof(quantity), negative.ParamName);

        quantity = 0m;
        var zero = Assert.Throws<ArgumentOutOfRangeException>(() => DomainGuard.Positive(quantity));
        Assert.Equal(nameof(quantity), zero.ParamName);

        var ratio = 1.01m;
        var outside = Assert.Throws<ArgumentOutOfRangeException>(() => DomainGuard.Ratio(ratio));
        Assert.Equal(nameof(ratio), outside.ParamName);

        Assert.Equal(0m, DomainGuard.NonNegative(0m));
        Assert.Equal(1m, DomainGuard.Positive(1m));
        Assert.Equal(0m, DomainGuard.Ratio(0m));
        Assert.Equal(1m, DomainGuard.Ratio(1m));
    }

    [Fact]
    public void ExplicitNamesArePreservedForLabelsAndComplexExpressions()
    {
        var request = new { Code = "", Availability = (decimal?)1.01m };

        var labeled = Assert.Throws<ArgumentException>(() =>
            DomainGuard.Required(request.Code, 32, "Enterprise code"));

        var member = Assert.Throws<ArgumentException>(() =>
            DomainGuard.Required(request.Code, 32, nameof(request.Code)));

        var coalesced = Assert.Throws<ArgumentOutOfRangeException>(() =>
            DomainGuard.Ratio(request.Availability ?? 0, nameof(request.Availability)));

        Assert.Equal("Enterprise code", labeled.ParamName);
        Assert.Equal("Code", member.ParamName);
        Assert.Equal("Availability", coalesced.ParamName);
    }
}
