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

#if !(NET || NETSTANDARD2_1_OR_GREATER)
    public static Task CopyToAsync(this Stream source, Stream destination, CancellationToken cancellationToken)
    {
        int GetCopyBufferSize()
        {
            const int defaultCopyBufferSize = 81920;

            if (!source.CanSeek)
            {
                return defaultCopyBufferSize;
            }

            long length = source.Length;
            long position = source.Position;
            if (length <= position)
            {
                return 1;
            }

            long remaining = length - position;
            return remaining > 0 ? (int)Math.Min(defaultCopyBufferSize, remaining) : defaultCopyBufferSize;
        }

        return source.CopyToAsync(destination, GetCopyBufferSize(), cancellationToken);
    }
#endif
}
