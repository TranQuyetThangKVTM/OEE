namespace Oee.Web.Endpoints;
internal static class EndpointConcurrency
{
    internal static byte[]? ReadIfMatch(HttpContext context)
    {
        var value = context.Request.Headers.IfMatch.ToString().Trim('"');
        if (string.IsNullOrWhiteSpace(value))
            return null;
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
