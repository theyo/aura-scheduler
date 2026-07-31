using System.Runtime.InteropServices;

namespace AuraScheduler.UI.Infrastructure
{
    /// <summary>
    /// Extracts the icon embedded in this exe's PE resources (via &lt;ApplicationIcon&gt;)
    /// at runtime, so callers never depend on a loose icon.ico file.
    /// </summary>
    internal static class AppIcon
    {
        private const int SM_CXSMICON = 49;
        private const uint DI_NORMAL = 0x0003;
        private const uint BLACKNESS = 0x00000042;

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public uint bmiColors;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
            [MarshalAs(UnmanagedType.Bool)] public bool fIcon;
            public uint xHotspot;
            public uint yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ExtractIconEx(string lpszFile, int nIconIndex,
            IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyHeight, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, uint diFlags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        private static extern IntPtr CreateIconIndirect(ref ICONINFO piconinfo);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateBitmap(int nWidth, int nHeight, uint nPlanes, uint nBitCount, IntPtr lpBits);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool PatBlt(IntPtr hdc, int x, int y, int width, int height, uint rop);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateSolidBrush(uint colorRef);

        [DllImport("gdi32.dll")]
        private static extern bool Ellipse(IntPtr hdc, int left, int top, int right, int bottom);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern bool FillRect(IntPtr hdc, ref RECT lprc, IntPtr hbr);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        public static IntPtr ExtractSmallIcon() => Extract(large: false);

        public static IntPtr ExtractLargeIcon() => Extract(large: true);

        /// <summary>
        /// Creates a small, in-memory copy of the application icon with a red update badge.
        /// The returned HICON is owned by the caller and must be released with DestroyIcon.
        /// </summary>
        public static IntPtr CreateUpdateIcon()
        {
            var baseIcon = ExtractSmallIcon();
            if (baseIcon == IntPtr.Zero)
                return IntPtr.Zero;

            var size = Math.Max(16, GetSystemMetrics(SM_CXSMICON));
            var screenDc = GetDC(IntPtr.Zero);
            var colorBitmap = IntPtr.Zero;
            var maskBitmap = IntPtr.Zero;
            var colorDc = IntPtr.Zero;
            var oldColorBitmap = IntPtr.Zero;
            var redBrush = IntPtr.Zero;
            var whiteBrush = IntPtr.Zero;
            var result = IntPtr.Zero;

            try
            {
                var bitmapInfo = new BITMAPINFO
                {
                    bmiHeader = new BITMAPINFOHEADER
                    {
                        biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                        biWidth = size,
                        biHeight = -size,
                        biPlanes = 1,
                        biBitCount = 32,
                        biCompression = 0,
                    },
                };

                colorBitmap = CreateDIBSection(screenDc, ref bitmapInfo, 0, out var bits, IntPtr.Zero, 0);
                maskBitmap = CreateBitmap(size, size, 1, 1, IntPtr.Zero);
                colorDc = CreateCompatibleDC(screenDc);
                if (colorBitmap == IntPtr.Zero || maskBitmap == IntPtr.Zero || colorDc == IntPtr.Zero)
                    return IntPtr.Zero;

                // DIB sections are not guaranteed to be zeroed by GDI.
                Marshal.Copy(new byte[size * size * 4], 0, bits, size * size * 4);
                oldColorBitmap = SelectObject(colorDc, colorBitmap);
                DrawIconEx(colorDc, 0, 0, baseIcon, size, size, 0, IntPtr.Zero, DI_NORMAL);

                var badgeSize = Math.Max(8, size / 2);
                var badgeLeft = size - badgeSize;
                redBrush = CreateSolidBrush(0x000000FF); // red in COLORREF BGR format
                var badgeRect = new RECT { left = badgeLeft, top = 0, right = size, bottom = badgeSize };
                var oldBrush = SelectObject(colorDc, redBrush);
                Ellipse(colorDc, badgeRect.left, badgeRect.top, badgeRect.right, badgeRect.bottom);
                SelectObject(colorDc, oldBrush);

                whiteBrush = CreateSolidBrush(0x00FFFFFF);
                oldBrush = SelectObject(colorDc, whiteBrush);
                var lineWidth = Math.Max(1, size / 10);
                var lineRect = new RECT
                {
                    left = badgeLeft + (badgeSize - lineWidth) / 2,
                    top = Math.Max(1, badgeSize / 5),
                    right = badgeLeft + (badgeSize + lineWidth) / 2,
                    bottom = badgeSize * 3 / 5,
                };
                FillRect(colorDc, ref lineRect, whiteBrush);
                var dotSize = Math.Max(1, lineWidth);
                var dotRect = new RECT
                {
                    left = badgeLeft + (badgeSize - dotSize) / 2,
                    top = badgeSize * 3 / 4,
                    right = badgeLeft + (badgeSize + dotSize) / 2,
                    bottom = badgeSize * 3 / 4 + dotSize,
                };
                FillRect(colorDc, ref dotRect, whiteBrush);
                SelectObject(colorDc, oldBrush);

                // GDI drawing into a 32-bit DIB writes RGB but leaves alpha at zero.
                // Make the circular badge pixels opaque so the shell does not treat
                // the newly drawn badge as fully transparent.
                var pixels = new byte[size * size * 4];
                Marshal.Copy(bits, pixels, 0, pixels.Length);
                var radius = badgeSize / 2.0;
                var centerX = badgeLeft + radius;
                var centerY = radius;
                for (var y = 0; y < badgeSize; y++)
                {
                    for (var x = badgeLeft; x < size; x++)
                    {
                        var dx = x + 0.5 - centerX;
                        var dy = y + 0.5 - centerY;
                        if ((dx * dx) + (dy * dy) <= radius * radius)
                            pixels[((y * size) + x) * 4 + 3] = 0xFF;
                    }
                }
                Marshal.Copy(pixels, 0, bits, pixels.Length);

                var previousBitmap = SelectObject(colorDc, maskBitmap);
                PatBlt(colorDc, 0, 0, size, size, BLACKNESS);
                SelectObject(colorDc, previousBitmap);

                var iconInfo = new ICONINFO { fIcon = true, hbmColor = colorBitmap, hbmMask = maskBitmap };
                result = CreateIconIndirect(ref iconInfo);
                return result;
            }
            finally
            {
                if (oldColorBitmap != IntPtr.Zero)
                    SelectObject(colorDc, oldColorBitmap);

                if (redBrush != IntPtr.Zero)
                    DeleteObject(redBrush);

                if (whiteBrush != IntPtr.Zero)
                    DeleteObject(whiteBrush);

                if (colorDc != IntPtr.Zero)
                    DeleteDC(colorDc);

                if (colorBitmap != IntPtr.Zero)
                    DeleteObject(colorBitmap);

                if (maskBitmap != IntPtr.Zero)
                    DeleteObject(maskBitmap);

                if (screenDc != IntPtr.Zero)
                    ReleaseDC(IntPtr.Zero, screenDc);

                DestroyIcon(baseIcon);
            }
        }

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
