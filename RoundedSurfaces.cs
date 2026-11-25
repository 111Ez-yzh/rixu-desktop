using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shell;

internal static class RoundedSurfaces {
    static ResourceDictionary theme;

    internal static void Load(ResourceDictionary resources) {
        if(theme==null) {
            using(var stream=typeof(RoundedSurfaces).Assembly.GetManifestResourceStream("Surfaces.xaml"))
                theme=(ResourceDictionary)XamlReader.Load(stream);
        }
        if(!resources.MergedDictionaries.Contains(theme))resources.MergedDictionaries.Add(theme);
        // Tooltips and system-created popups can have a separate visual tree.
        var app=Application.Current;
        if(app!=null && !app.Resources.MergedDictionaries.Contains(theme))app.Resources.MergedDictionaries.Add(theme);
    }
    internal static void Frame(Window window) {
        window.Template=(ControlTemplate)window.FindResource("deskFrame");
        var radius=(CornerRadius)window.FindResource("windowFrameRadius");
        // With glass disabled, WPF passes this length to GDI as the corner ellipse's diameter.
        WindowChrome.SetWindowChrome(window,new WindowChrome {CornerRadius=new CornerRadius(radius.TopLeft*2),
            GlassFrameThickness=new Thickness(0),ResizeBorderThickness=new Thickness(window.ResizeMode==ResizeMode.NoResize?0:6),
            CaptionHeight=34,UseAeroCaptionButtons=false});
        window.CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand,(s,e)=>SystemCommands.MinimizeWindow(window)));
        window.CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand,(s,e)=>SystemCommands.MaximizeWindow(window)));
        window.CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand,(s,e)=>SystemCommands.RestoreWindow(window)));
        window.CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand,(s,e)=>SystemCommands.CloseWindow(window)));
    }
    internal static void Clip(FrameworkElement surface,double radius) {
        Action update=delegate {
            var geometry=new RectangleGeometry(new Rect(surface.RenderSize),radius,radius);
            geometry.Freeze();surface.Clip=geometry;
        };
        surface.SizeChanged+=delegate {update();};
        update();
    }
    internal static void Notice(Window owner,string title,string message) {
        var dialog=new Window {Title=title,Owner=owner,WindowStartupLocation=owner==null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            Width=420,SizeToContent=SizeToContent.Height,MaxHeight=Math.Max(260,SystemParameters.WorkArea.Height-40),ResizeMode=ResizeMode.NoResize,
            Background=owner==null || owner.AllowsTransparency ? new SolidColorBrush(Color.FromRgb(247,246,241)) : owner.Background,
            FontWeight=owner==null ? FontWeights.Medium : owner.FontWeight,FontSize=14};
        if(owner!=null) {dialog.Resources=owner.Resources;dialog.Language=owner.Language;}
        else {Load(dialog.Resources);dialog.Resources["ui.winClose"]="关闭 / Close";}
        dialog.SetResourceReference(Window.FontFamilyProperty,"deskFont");
        var panel=new DockPanel {Margin=new Thickness(20,4,20,18)};
        var ok=new Button {Content=dialog.TryFindResource("ui.confirm")??"OK",IsDefault=true,IsCancel=true,HorizontalAlignment=HorizontalAlignment.Right,
            Padding=new Thickness(16,6,16,6),Margin=new Thickness(0,15,0,0)};
        if(owner==null)ok.Style=(Style)dialog.FindResource("calendarAction");
        DockPanel.SetDock(ok,Dock.Bottom);panel.Children.Add(ok);
        panel.Children.Add(new ScrollViewer {VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            Content=new TextBlock {Text=message,TextWrapping=TextWrapping.Wrap}});
        dialog.Content=panel;Frame(dialog);ControlMotion.Enable(dialog);
        ok.Click+=delegate {dialog.Close();};dialog.ShowDialog();
    }
}
