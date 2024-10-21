using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Business;

public static class BusinessUtils
{
    public const string EventRecipientHeader = "migr-eventrecipient";
    public const string EventPubsubName = "pubsub";
    public const string EventTopicPrefix = "elcpmig_";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LoggerConfiguration NewLoggerConfiguration()
    {
        return new LoggerConfiguration()
            .Filter.ByExcluding("SourceContext like 'System.Net.Http.HttpClient.%Handler' and EventId.Id in [102, 103]");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ExpressionTemplate MakeExpressionTemplate(bool colored)
    {
        return new ExpressionTemplate(
            """
            {UtcDateTime(@t):O} {@l:u4} {SourceContext}{#if IsDefined(EventId)} ({Coalesce(EventId.Id, 0)}){#end}
            {#if HasScope()}{#each k,v in Scope()}=> {k} = {v}{#delimit} {#end}
            {#end}{@m}
            {@x}

            """,
            nameResolver: ScopeFunctionNameResolver.Instance,
            theme: colored ? TemplateTheme.Literate : null
        );
    }

    private sealed class ScopeFunctionNameResolver : NameResolver
    {
        public static readonly NameResolver Instance = new ScopeFunctionNameResolver();

        private static readonly MethodInfo GetScopeMethod =
            typeof(ScopeFunctionNameResolver).GetMethod(nameof(GetScope), BindingFlags.NonPublic | BindingFlags.Static)!;

        private static readonly MethodInfo HasScopeMethod =
            typeof(ScopeFunctionNameResolver).GetMethod(nameof(HasScope), BindingFlags.NonPublic | BindingFlags.Static)!;

        private ScopeFunctionNameResolver() { }

        public override bool TryResolveFunctionName(string name, [NotNullWhen(true)] out MethodInfo? implementation)
        {
            implementation = name.Equals("scope", StringComparison.OrdinalIgnoreCase) ? GetScopeMethod
                : name.Equals("hasscope", StringComparison.OrdinalIgnoreCase) ? HasScopeMethod
                : null;
            return implementation is not null;
        }

        private static IEnumerable<KeyValuePair<string, LogEventPropertyValue>> CoreGetScope(LogEvent logEvent)
        {
            bool seen = false;

            return logEvent.Properties
                .SkipWhile(
                    x =>
                    {
                        bool skip = !seen;
                        seen = x.Key.Equals("SourceContext", StringComparison.OrdinalIgnoreCase);
                        return skip;
                    }
                );
        }

        private static LogEventPropertyValue GetScope(LogEvent logEvent)
        {
            return new StructureValue(CoreGetScope(logEvent).Select(static x => new LogEventProperty(x.Key, x.Value)).Reverse());
        }

        private static LogEventPropertyValue HasScope(LogEvent logEvent)
        {
            return new ScalarValue(CoreGetScope(logEvent).Any());
        }
    }
}
