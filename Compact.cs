using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;

public partial class DayDesk {
    Window widgetWindow;
    Point? lastWidgetPosition;
    string activeWidget;
    readonly Dictionary<string,CheckBox> widgetToggles=new Dictionary<string,CheckBox>();
    bool closingWorkspace;
    TextBlock compactAmount;
    StackPanel clockRows;
    StackPanel noteTasks;
    readonly Dictionary<Place,TextBlock> compactTimes=new Dictionary<Place,TextBlock>();
    readonly Dictionary<Place,InstrumentArt> compactDials=new Dictionary<Place,InstrumentArt>();

    List<Place> WidgetPlaces() {
        var ids=new HashSet<string>(data.CompactZones??new string[0]);
        var places=selected.Where(p=>ids.Contains(p.Key)).ToList();
        if(places.Count==0) places.Add(active);
        return places;
    }
    void ChooseWidgetPlaces(Window owner) {
        var panel=new DockPanel {Margin=new Thickness(20)};
        var dialog=Dialog(T("widgetPlacesTitle"),panel,440);
        dialog.Owner=owner;
        var save=ActionButton(T("save"));DockPanel.SetDock(save,Dock.Bottom);panel.Children.Add(save);
        var list=new StackPanel();
        panel.Children.Add(new ScrollViewer {Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        var chosen=new HashSet<Place>(WidgetPlaces());
        foreach(var p in selected) {
            var place=p;
            var check=new CheckBox {Content=PlaceTitle(p),Margin=new Thickness(2,7,2,7),IsChecked=chosen.Contains(p)};
            check.Click+=delegate {if(check.IsChecked==true)chosen.Add(place);else chosen.Remove(place);};
            list.Children.Add(check);
        }
        save.Click+=delegate {
            if(chosen.Count==0){RoundedSurfaces.Notice(dialog,dialog.Title,T("needWidgetPlace"));return;}
            data.CompactZones=selected.Where(chosen.Contains).Select(p=>p.Key).ToArray();
            Save();dialog.Close();
            RefreshClockWidget();
        };
        dialog.ShowDialog();
    }
    void RefreshClockWidget() {
        if(clockRows==null)return;
        clockRows.Children.Clear();
        compactTimes.Clear();compactDials.Clear();
        foreach(var p in WidgetPlaces()) {
            var row=new DockPanel {Margin=new Thickness(0,4,0,4),ToolTip=PlaceTitle(p)};
            var time=new TextBlock {FontSize=16,VerticalAlignment=VerticalAlignment.Center};
            DockPanel.SetDock(time,Dock.Right);row.Children.Add(time);compactTimes[p]=time;
            if(!data.DigitalFace && !data.ListOnly){var face=new InstrumentArt(){Design=dial.Design,Width=36,Height=36,Margin=new Thickness(0,0,7,0)};row.Children.Add(face);compactDials[p]=face;}
            row.Children.Add(new TextBlock {Text=CityName(p),VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis});
            clockRows.Children.Add(ClockFlags.BehindRow(row,p.Code,true,data.ListOnly));
        }
        RefreshWidget(DateTime.UtcNow);
    }
    bool WidgetSelected(string id) {
        return data.OpenWidgets!=null && data.OpenWidgets.Contains(id);
    }
    void SyncWidgetControls() {
        foreach(var item in widgetToggles)item.Value.IsChecked=WidgetSelected(item.Key);
        Button("Compact").Content=WidgetSelected(page) ? T("widgetClose") : T("widgetOpen");
        Button("OnlyWidgets").IsEnabled=widgetWindow!=null;
    }
    void ToggleWidget(string id) {
        bool adding=!WidgetSelected(id);
        data.OpenWidgets=widgetToggles.Keys.Where(key=>key==id ? adding : WidgetSelected(key)).ToArray();
        DisplayWidgets();SyncWidgetControls();Save();
    }
    void RestoreWidgets(object sender,EventArgs e) {
        window.ContentRendered-=RestoreWidgets;
        data.OpenWidgets=widgetToggles.Keys.Where(WidgetSelected).ToArray();
        DisplayWidgets();SyncWidgetControls();
    }
    void ReturnToPage(string id) {
        ToggleWidget(id);
        if(window.WindowState==WindowState.Minimized)window.WindowState=WindowState.Normal;
        Navigate(id);window.Activate();
    }
    Rect WidgetWorkArea(Window mini) {
        var target=mini.IsVisible?mini:window;
        var source=PresentationSource.FromVisual(target);
        var monitor=System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(target).Handle).WorkingArea;
        var scale=source.CompositionTarget.TransformFromDevice;
        return new Rect(scale.Transform(new Point(monitor.Left,monitor.Top)),scale.Transform(new Point(monitor.Right,monitor.Bottom)));
    }
    static void PlaceWidget(Window mini,Rect area,Point anchor,double height) {
        mini.Left=Math.Max(area.Left,Math.Min(anchor.X,area.Right-mini.Width));
        mini.Top=Math.Max(area.Top,Math.Min(anchor.Y,area.Bottom-height));
    }
    Window CreateWidgetWindow() {
        var mini=new Window {WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,
            AllowsTransparency=true,Background=Brushes.Transparent,Topmost=true,SizeToContent=SizeToContent.Height,UseLayoutRounding=true,
            Language=window.Language,Resources=window.Resources,FontFamily=window.FontFamily,FontWeight=window.FontWeight,FontSize=14,ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};
        TextOptions.SetTextFormattingMode(mini,TextFormattingMode.Display);
        WidgetGlass.Attach(mini);ControlMotion.Enable(mini);
        mini.Closed+=delegate {
            lastWidgetPosition=new Point(mini.Left,mini.Top);
            widgetWindow=null;activeWidget=null;ClearWidgetParts();
            if(!closingWorkspace && data.OpenWidgets.Length>0) {
                data.OpenWidgets=new string[0];SyncWidgetControls();Save();
            }
        };
        mini.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){e.Handled=true;ReturnToPage(activeWidget);}};
        mini.SizeChanged+=delegate {if(mini.IsVisible)PlaceWidget(mini,WidgetWorkArea(mini),new Point(mini.Left,mini.Top),mini.ActualHeight);};
        Point? pressed=null;
        mini.PreviewMouseLeftButtonDown+=(s,e)=>{
            pressed=null;
            // Leave buttons, task checkboxes and scrollbars in charge of their own gestures.
            for(var hit=e.OriginalSource as DependencyObject;hit!=null && hit!=mini;) {
                if(hit is ButtonBase || hit is ScrollBar)return;
                var section=hit as Border;
                if(section!=null && section.Tag is string) {
                    activeWidget=(string)section.Tag;
                    if(e.ClickCount==2){e.Handled=true;ReturnToPage(activeWidget);return;}
                    break;
                }
                hit=hit is Visual?VisualTreeHelper.GetParent(hit):LogicalTreeHelper.GetParent(hit);
            }
            pressed=e.GetPosition(mini);
        };
        mini.PreviewMouseLeftButtonUp+=delegate {pressed=null;};
        mini.PreviewMouseMove+=(s,e)=>{
            if(!pressed.HasValue || e.LeftButton!=MouseButtonState.Pressed)return;
            var delta=e.GetPosition(mini)-pressed.Value;
            if(Math.Abs(delta.X)<SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y)<SystemParameters.MinimumVerticalDragDistance)return;
            pressed=null;mini.DragMove();
            if(!mini.IsVisible)return;
            var area=WidgetWorkArea(mini);
            mini.MaxHeight=area.Height;PlaceWidget(mini,area,new Point(mini.Left,mini.Top),mini.ActualHeight);
        };
        return mini;
    }
    void ClearWidgetParts() {
        compactAmount=null;clockRows=null;noteTasks=null;
        compactTimes.Clear();compactDials.Clear();
    }
    void DisplayWidgets() {
        if(data.OpenWidgets==null || data.OpenWidgets.Length==0) {
            if(widgetWindow!=null)widgetWindow.Close();
            return;
        }
        if(widgetWindow==null)widgetWindow=CreateWidgetWindow();
        var mini=widgetWindow;
        var ids=data.OpenWidgets;
        bool group=ids.Length>1;
        var area=WidgetWorkArea(mini);
        if(!WidgetSelected(activeWidget))activeWidget=ids[0];
        mini.Language=window.Language;
        mini.Title=String.Join(" · ",ids.Select(id=>pages[id]))+" · "+T("brand");
        mini.Width=group?280:ids[0]=="Payroll"?174:ids[0]=="World"?236:260;
        var anchor=mini.IsVisible?new Point(mini.Left,mini.Top):lastWidgetPosition??new Point(area.Right-mini.Width-18,area.Top+22);
        ClearWidgetParts();
        var stack=new StackPanel();
        foreach(var id in ids) {
            if(stack.Children.Count>0)stack.Children.Add(new Border {Height=1,Margin=new Thickness(14,0,14,0),Background=Paint("#27737E90")});
            stack.Children.Add(BuildWidgetSection(id,group));
        }
        bool pill=!group && ids[0]=="Payroll";
        var shell=new Border {CornerRadius=new CornerRadius(pill?22:19),BorderThickness=new Thickness(1),
            BorderBrush=(Brush)window.Resources["widgetEdge"],Background=pill?Paint("#D7293141"):(Brush)window.Resources["widgetFrost"],
            Child=new ScrollViewer {Content=stack,Focusable=false,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}};
        RoundedSurfaces.Clip(shell,shell.CornerRadius.TopLeft);
        mini.MaxHeight=area.Height;mini.Content=shell;
        if(clockRows!=null)RefreshClockWidget();else RefreshWidget(DateTime.UtcNow);
        shell.Measure(new Size(mini.Width,mini.MaxHeight));
        PlaceWidget(mini,area,anchor,shell.DesiredSize.Height);
        if(!mini.IsVisible)mini.Show();
    }
    ContextMenu WidgetMenu(string id) {
        var menu=new ContextMenu {Resources=window.Resources};ControlMotion.Enable(menu);
        var expand=new MenuItem {Header=T("returnFull")};expand.Click+=delegate {ReturnToPage(id);};menu.Items.Add(expand);
        if(id=="World"){var change=new MenuItem {Header=T("chooseMiniPlaces")};change.Click+=delegate {ChooseWidgetPlaces(widgetWindow);};menu.Items.Add(change);}
        var close=new MenuItem {Header=T("widgetClose")};close.Click+=delegate {ToggleWidget(id);};menu.Items.Add(close);
        return menu;
    }
    Button WidgetAction(string name,string path,string help,string label,Action action) {
        var icon=new System.Windows.Shapes.Path {Data=Geometry.Parse(path),Stroke=Paint("#344255"),StrokeThickness=1.55,
            StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Width=15,Height=15,VerticalAlignment=VerticalAlignment.Center};
        var content=new StackPanel {Orientation=Orientation.Horizontal};
        content.Children.Add(icon);
        if(label!=null)content.Children.Add(new TextBlock {Text=label,Margin=new Thickness(3,0,0,0),VerticalAlignment=VerticalAlignment.Center});
        var button=new Button {Name=name,Content=content,Style=(Style)window.Resources["widgetAction"],ToolTip=help};
        AutomationProperties.SetName(button,help);button.Click+=delegate {action();};return button;
    }
    Border BuildWidgetSection(string id,bool group) {
        bool pay=id=="Payroll", clock=id=="World";
        var shell=new Border {Tag=id,Background=pay||clock?Brushes.Transparent:Paint("#29FFE3A6"),ContextMenu=WidgetMenu(id),
            Padding=pay&&!group?new Thickness(12,3,12,3):new Thickness(13,10,13,12)};
        AutomationProperties.SetName(shell,pages[id]);
        shell.PreviewMouseDown+=delegate {activeWidget=id;};
        shell.GotKeyboardFocus+=delegate {activeWidget=id;};
        if(pay) {
            shell.ToolTip=T("miniPayTip");
            compactAmount=new TextBlock {Foreground=Paint("#F5F7FD"),FontWeight=FontWeights.SemiBold,FontSize=21};
            var amount=new Viewbox {Child=compactAmount,StretchDirection=StretchDirection.DownOnly};
            if(group) {
                var bar=new DockPanel();
                var capsule=new Border {CornerRadius=new CornerRadius(17),Background=Paint("#D42F394A"),Padding=new Thickness(12,2,12,3),MaxWidth=142,Child=amount};
                DockPanel.SetDock(capsule,Dock.Right);bar.Children.Add(capsule);
                bar.Children.Add(new TextBlock {Text=T("miniEarned"),FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
                shell.Child=bar;
            } else {shell.Height=41;shell.Child=amount;}
            return shell;
        }
        var body=new DockPanel();shell.Child=body;
        var heading=new DockPanel {Margin=new Thickness(0,0,0,8)};DockPanel.SetDock(heading,Dock.Top);body.Children.Add(heading);
        var actions=new StackPanel {Orientation=Orientation.Horizontal};
        if(clock) {
            var choose=WidgetAction("WidgetPlaces","M7.5,14 C6,12 2.7,8.8 2.7,6 A4.8,4.8 0 1 1 12.3,6 C12.3,8.8 9,12 7.5,14 Z M9,6 A1.5,1.5 0 1 1 6,6 A1.5,1.5 0 1 1 9,6 Z",T("chooseMiniPlaces"),T("locations"),()=>ChooseWidgetPlaces(widgetWindow));
            choose.Padding=new Thickness(8,0,6,0);actions.Children.Add(choose);
            actions.Children.Add(new Border {Width=1,Height=12,Background=Paint("#26536170"),VerticalAlignment=VerticalAlignment.Center});
        }
        actions.Children.Add(WidgetAction("Expand"+id,"M3.5,11.5 L11.5,3.5 M4.5,3.5 H11.5 V10.5",T("returnFull"),null,()=>ReturnToPage(id)));
        var toolbar=new Border {Child=actions,CornerRadius=new CornerRadius(16),BorderThickness=new Thickness(1),
            BorderBrush=(Brush)window.Resources["widgetEdge"],Background=(Brush)window.Resources["widgetToolbar"]};
        DockPanel.SetDock(toolbar,Dock.Right);heading.Children.Add(toolbar);
        heading.Children.Add(new TextBlock {Text=clock?T("worldTitle"):T("note"),VerticalAlignment=VerticalAlignment.Center,Foreground=Paint("#202C3B"),FontWeight=FontWeights.SemiBold});
        var compactList=new StackPanel();
        body.Children.Add(new ScrollViewer {Content=compactList,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxHeight=clock?300:205});
        if(clock)clockRows=compactList;
        else {noteTasks=compactList;FillNote();}
        return shell;
    }
    void FillNote(Todo changedTask=null) {
        if(noteTasks==null)return;
        noteTasks.Children.Clear();
        if(data.Tasks.All(t=>t.Archived))noteTasks.Children.Add(new TextBlock {Text=T("noTasks"),Foreground=Paint("#706B5C"),Margin=new Thickness(0,8,0,10)});
        foreach(var task in OrderedTasks())noteTasks.Children.Add(TaskCheck(task,true,task==changedTask));
    }
    void RefreshWidget(DateTime utc) {

        if(compactAmount!=null)compactAmount.Text=Element<TextBlock>(window,"Amount").Text;
        foreach(var pair in compactTimes) {
            var local=utc+catalog.Offset(pair.Key,utc);pair.Value.Text=local.ToString("HH:mm:ss");
            InstrumentArt face;
            if(compactDials.TryGetValue(pair.Key,out face)){face.FaceTime=local;face.InvalidateVisual();}
        }
    }
}
