using System.Net;

namespace Diginsight.Analyzer.Entities;

public static class AnalysisExceptions
{
    public static AnalysisException MalformedGuid(string what) =>
        new ($"'{what}' is not a well-formed GUID", HttpStatusCode.BadRequest, nameof(MalformedGuid));

    public static AnalysisException MalformedGuid_Property(string propertyPath) => MalformedGuid($"Property `{propertyPath}`");

    public static AnalysisException NotSupportedYet(string what) =>
        new ($"{what} is not supported yet", HttpStatusCode.NotImplemented, nameof(NotSupportedYet));

    public static AnalysisException EnumNotDefined<T>(Type enumType, T enumValue)
        where T : Enum =>
        new ($"Enum '{enumType.ToString()}' has no such member '{Convert.ToInt64(enumValue):D}'", HttpStatusCode.BadRequest, nameof(EnumNotDefined));

    public static AnalysisException InputNotPositive(string name) =>
        new ($"Input `{name}` must be positive", HttpStatusCode.BadRequest, nameof(InputNotPositive));

    public static AnalysisException InputGreaterThan(string name, double value) =>
        new ($"Input `{name}` must be less than or equal to {value:R}", HttpStatusCode.BadRequest, nameof(InputGreaterThan));

    public static AnalysisException DownstreamException(string message, AnalysisException? innerException = null) =>
        new (message, HttpStatusCode.BadGateway, nameof(DownstreamException), innerException);

    public static AnalysisException DownstreamException(ref AnalysisException.InterpolatedStringHandler handler, AnalysisException? innerException = null) =>
        new (ref handler, HttpStatusCode.BadGateway, nameof(DownstreamException), innerException);
}
