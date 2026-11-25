namespace Oee.Infrastructure.IntegrationTests;

public sealed class SqlServerTheoryAttribute : TheoryAttribute
{
    public SqlServerTheoryAttribute() => Skip = SqlServerFactAttribute.ConfigurationSkipReason;
}
