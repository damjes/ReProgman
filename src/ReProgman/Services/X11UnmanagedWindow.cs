using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace ReProgman.Services;

/// <summary>
/// Removes an X11 window from window-manager control when a WM ignores the
/// normal no-decoration hint. The trade-off is that the WM no longer provides
/// task switching or native move/resize handling for this window.
/// </summary>
public static class X11UnmanagedWindow
{
    private static readonly nuint OverrideRedirectMask = (nuint)(1UL << 9);

    public static bool TryEnable(Window window)
    {
        var handle = window.TryGetPlatformHandle();
        if (handle?.HandleDescriptor != "XID") return false;

        var display = XOpenDisplay(null);
        if (display == IntPtr.Zero) return false;

        try
        {
            var windowId = new UIntPtr(unchecked((ulong)handle.Handle.ToInt64()));
            XUnmapWindow(display, windowId);
            XSync(display, false);

            var attributes = new XSetWindowAttributes { OverrideRedirect = 1 };
            XChangeWindowAttributes(display, windowId, OverrideRedirectMask, ref attributes);
            XMapWindow(display, windowId);
            XRaiseWindow(display, windowId);
            XSync(display, false);
            return true;
        }
        finally
        {
            XCloseDisplay(display);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XSetWindowAttributes
    {
        public IntPtr BackgroundPixmap;
        public UIntPtr BackgroundPixel;
        public IntPtr BorderPixmap;
        public UIntPtr BorderPixel;
        public int BitGravity;
        public int WinGravity;
        public int BackingStore;
        public UIntPtr BackingPlanes;
        public UIntPtr BackingPixel;
        public int SaveUnder;
        public nint EventMask;
        public nint DoNotPropagateMask;
        public int OverrideRedirect;
        public UIntPtr Colormap;
        public UIntPtr Cursor;
    }

    [DllImport("libX11.so.6", CharSet = CharSet.Ansi)]
    private static extern IntPtr XOpenDisplay(string? displayName);

    [DllImport("libX11.so.6")]
    private static extern int XUnmapWindow(IntPtr display, UIntPtr window);

    [DllImport("libX11.so.6")]
    private static extern int XChangeWindowAttributes(IntPtr display, UIntPtr window, nuint valueMask, ref XSetWindowAttributes attributes);

    [DllImport("libX11.so.6")]
    private static extern int XMapWindow(IntPtr display, UIntPtr window);

    [DllImport("libX11.so.6")]
    private static extern int XRaiseWindow(IntPtr display, UIntPtr window);

    [DllImport("libX11.so.6")]
    private static extern int XSync(IntPtr display, bool discard);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);
}
