using System.Diagnostics;
using DayKeep.Core;

namespace DayKeep.Infrastructure;

public static class ExternalLinks
{
    public static void Open(Uri uri)
    {
        var safe = WebLink.Parse(uri.AbsoluteUri);
        Process.Start(new ProcessStartInfo(safe.AbsoluteUri) { UseShellExecute = true });
    }
}
