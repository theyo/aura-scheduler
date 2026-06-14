using System.Runtime.InteropServices;

namespace AuraScheduler.UI.Infrastructure
{
    /// <summary>
    /// Extracts the icon embedded in this exe's PE resources (via &lt;ApplicationIcon&gt;)
    /// at runtime, so callers never depend on a loose icon.ico file.
    /// </summary>
    internal static class AppIcon
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ExtractIconEx(string lpszFile, int nIconIndex,
            IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

        public static IntPtr ExtractSmallIcon() => Extract(large: false);

        public static IntPtr ExtractLargeIcon() => Extract(large: true);

        private static IntPtr Extract(bool large)
        {
            var icons = new IntPtr[1];
            var exePath = Environment.ProcessPath ?? string.Empty;
            var extracted = large
                ? ExtractIconEx(exePath, 0, icons, null, 1)
                : ExtractIconEx(exePath, 0, null, icons, 1);
            return extracted == 1 ? icons[0] : IntPtr.Zero;
        }
    }
}
