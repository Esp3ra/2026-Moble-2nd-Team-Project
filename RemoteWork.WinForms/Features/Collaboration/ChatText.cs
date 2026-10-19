using System.Buffers;
using System.Text;

namespace RemoteWork.WinForms.Features.Collaboration;

// Server Python len() counts Unicode scalar values, not UTF-16 code units.
internal static class ChatText
{
    public static bool WithinLimit(string text, int maximum)
    {
        ReadOnlySpan<char> remaining = text.AsSpan();
        int count = 0;
        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(remaining, out _, out int consumed) != OperationStatus.Done
                || ++count > maximum) return false;
            remaining = remaining[consumed..];
        }
        return true;
    }
}
