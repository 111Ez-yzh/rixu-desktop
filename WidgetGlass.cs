using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

internal static class WidgetGlass {
    [StructLayout(LayoutKind.Sequential)]
    struct Accent { public int State,Flags,Tint,Animation; }
    [StructLayout(LayoutKind.Sequential)]
    struct CompositionAttribute { public int Attribute; public IntPtr Data; public IntPtr Size; }
    [StructLayout(LayoutKind.Sequential)]
    struct Bounds { public int Left,Top,Right,Bottom; }

    [DllImport("user32.dll")] static extern bool SetWindowCompositionAttribute(IntPtr window,ref CompositionAttribute attribute);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out Bounds bounds);
    [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr window,IntPtr region,bool repaint);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int left,int top,int right,int bottom,int ellipseWidth,int ellipseHeight);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr handle);

    internal static void Attach(Window window) {
        window.SourceInitialized+=delegate {
            var source=(HwndSource)PresentationSource.FromVisual(window);
            ApplyPreferences(window,source.Handle);
            source.AddHook(delegate(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam,ref bool handled) {
                // WM_SETTINGCHANGE, WM_THEMECHANGED, WM_DWMCOMPOSITIONCHANGED
                if(msg==0x001A || msg==0x031A || msg==0x031E)ApplyPreferences(window,hwnd);
                return IntPtr.Zero;
            });
            RoundToContent(window);
        };
        window.SizeChanged+=delegate {window.Dispatcher.BeginInvoke(new Action(()=>RoundToContent(window)));};
        window.DpiChanged+=delegate {window.Dispatcher.BeginInvoke(new Action(()=>RoundToContent(window)));};
    }
    static void ApplyPreferences(Window window,IntPtr handle) {
        var preference=Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","EnableTransparency",1);
        SetMaterial(window,handle,!SystemParameters.HighContrast && !Object.Equals(preference,0));
    }
    static void SetMaterial(Window window,IntPtr handle,bool allowTransparency) {
        // The widget uses a layered HWND for its custom silhouette. The accent
        // policy blurs the scene behind that HWND even when it isn't focused.
        const int acrylicBlurBehind=4, accentPolicy=19;
        var accent=new Accent {State=allowTransparency?acrylicBlurBehind:0,Tint=0x02999897};
        int bytes=Marshal.SizeOf(typeof(Accent));
        var pointer=Marshal.AllocHGlobal(bytes);
        try {
            Marshal.StructureToPtr(accent,pointer,false);
            var attribute=new CompositionAttribute {Attribute=accentPolicy,Data=pointer,Size=new IntPtr(bytes)};
            if(SetWindowCompositionAttribute(handle,ref attribute) && allowTransparency) {
                window.Background=Brushes.Transparent;
                return;
            }
        } catch(EntryPointNotFoundException) { /* Older systems use the opaque surface. */ }
        finally {Marshal.FreeHGlobal(pointer);}
        window.Background=new SolidColorBrush(Color.FromRgb(231,236,243));
    }
    // Clip the compositor material too, including the much rounder earnings capsule.
    static void RoundToContent(Window window) {
        var source=PresentationSource.FromVisual(window) as HwndSource;
        var shell=window.Content as Border;
        Bounds bounds;
        if(source==null || source.CompositionTarget==null || shell==null || !GetWindowRect(source.Handle,out bounds))return;
        var dpi=source.CompositionTarget.TransformToDevice;
        double diameter=shell.CornerRadius.TopLeft*2;
        var region=CreateRoundRectRgn(0,0,bounds.Right-bounds.Left+1,bounds.Bottom-bounds.Top+1,
            (int)Math.Round(diameter*dpi.M11),(int)Math.Round(diameter*dpi.M22));
        if(region!=IntPtr.Zero && SetWindowRgn(source.Handle,region,true)==0)DeleteObject(region);
    }
}
