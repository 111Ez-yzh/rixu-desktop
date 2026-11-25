using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class ClockFlags {
    static readonly ZipArchive archive=new ZipArchive(typeof(ClockFlags).Assembly.GetManifestResourceStream("Flags.zip"));
    static readonly Dictionary<string,BitmapSource> images=new Dictionary<string,BitmapSource>();

    internal static BitmapSource For(string code) {
        BitmapSource cached;
        if(images.TryGetValue(code,out cached))return cached;
        var entry=archive.GetEntry(code+".png")??archive.GetEntry("globe.png");
        using(var stream=entry.Open())
        using(var buffer=new MemoryStream()) {
            stream.CopyTo(buffer);
            buffer.Position=0;
            var bitmap=new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption=BitmapCacheOption.OnLoad;
            bitmap.StreamSource=buffer;
            bitmap.EndInit();
            bitmap.Freeze();
            images.Add(code,bitmap);
            return bitmap;
        }
    }

    // Keep the flag between the two text columns; even in a narrow widget it
    // yields space to the city label and never intercepts clicks or dragging.
    internal static Grid BehindRow(UIElement content,string code,bool compact,bool listOnly) {
        var grid=new Grid { ClipToBounds=true };
        RoundedSurfaces.Clip(grid,compact?8:12);
        var flag=new Image {
            Source=For(code), Width=compact?40:listOnly?72:54,
            Height=compact?40:listOnly?72:54, Stretch=Stretch.Uniform,
            Opacity=compact?.31:.39, IsHitTestVisible=false,
            HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center,
            OpacityMask=new LinearGradientBrush(Color.FromArgb(155,255,255,255),Colors.White,0)
        };
        RenderOptions.SetBitmapScalingMode(flag,BitmapScalingMode.HighQuality);
        grid.Children.Add(flag);
        grid.Children.Add(content);
        return grid;
    }
}
