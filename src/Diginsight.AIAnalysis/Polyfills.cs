using System.ComponentModel;
#if !NET7_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif

// ReSharper disable once CheckNamespace
namespace System.IO;

[EditorBrowsable(EditorBrowsableState.Never)]
internal static class Polyfills
{
#if !NET7_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<string> ReadToEndAsync(this TextReader reader, CancellationToken cancellationToken) => reader.ReadToEndAsync();
#endif
}
