using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;

public partial class DayDesk {
    Window widget;
    TextBlock compactAmount;
    StackPanel compactList;
    readonly Dictionary<Place,TextBlock> compactTimes=new Dictionary<Place,TextBlock>();
    readonly Dictionary<Place,InstrumentArt> compactDials=new Dictionary<Place,InstrumentArt>();
    readonly Dictionary<string,Point> positions=new Dictionary<string,Point>();

    List<Place> WidgetPlaces() {
        var ids=new HashSet<string>(data.CompactZones??new string[0]);
        var places=selected.Where(p=>ids.Contains(p.Key)).ToList();
        if(places.Count==0) places.Add(active);
        return places;
    }
    void ChooseWidgetPlaces(Window owner) {
        var panel=new DockPanel {Margin=new Thickness(20)};
        var dialog=Dialog("小窗显示哪些时钟",panel,440);
        dialog.Owner=owner;
        var save=ActionButton("保存");DockPanel.SetDock(save,Dock.Bottom);panel.Children.Add(save);
        var list=new StackPanel();
        panel.Children.Add(new ScrollViewer {Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        var chosen=new HashSet<Place>(WidgetPlaces());
        foreach(var p in selected) {
            var place=p;
            var check=new CheckBox {Content=p.Title,Margin=new Thickness(2,7,2,7),IsChecked=chosen.Contains(p)};
            check.Click+=delegate {if(check.IsChecked==true)chosen.Add(place);else chosen.Remove(place);};
            list.Children.Add(check);
        }
        save.Click+=delegate {
            if(chosen.Count==0){MessageBox.Show(dialog,"请至少保留一个小窗时钟。");return;}
            data.CompactZones=selected.Where(chosen.Contains).Select(p=>p.Key).ToArray();
            Save();dialog.Close();
            if(widget!=null){BuildWidget();RefreshWidget(DateTime.UtcNow);}
        };
        dialog.ShowDialog();
    }
    void OpenWidget() {
        if(widget!=null || page=="Home")return;
        var mini=new Window {Title=pages[page]+" · 日序",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,
            AllowsTransparency=true,Background=Brushes.Transparent,Topmost=true,SizeToContent=SizeToContent.Height,
            Resources=window.Resources,FontFamily=window.FontFamily,FontSize=14,ShowInTaskbar=true,WindowStartupLocation=WindowStartupLocation.Manual};
        widget=mini;
        BuildWidget();
        var source=PresentationSource.FromVisual(window);
        var handle=new WindowInteropHelper(window).Handle;
        var monitor=System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        var scale=source.CompositionTarget.TransformFromDevice;
        var corner=scale.Transform(new Point(monitor.Left,monitor.Top));
        var edge=scale.Transform(new Point(monitor.Right,monitor.Bottom));
        Point position;
        if(!positions.TryGetValue(page,out position))position=new Point(edge.X-mini.Width-18,corner.Y+22);
        mini.ContentRendered+=delegate {
            mini.Left=Math.Max(corner.X,Math.Min(mini.Left,edge.X-mini.ActualWidth));
            mini.Top=Math.Max(corner.Y,Math.Min(mini.Top,edge.Y-mini.ActualHeight));
        };
        mini.Left=position.X;mini.Top=position.Y;
        mini.Closed+=delegate {
            positions[page]=new Point(mini.Left,mini.Top);
            widget=null;compactAmount=null;compactList=null;compactTimes.Clear();compactDials.Clear();
            if(!window.Dispatcher.HasShutdownStarted){window.Show();window.Activate();Refresh();}
        };
        mini.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){e.Handled=true;mini.Close();}};
        var menu=new ContextMenu();
        var expand=new MenuItem {Header="返回完整窗口"};expand.Click+=delegate {mini.Close();};menu.Items.Add(expand);
        if(page=="World"){var change=new MenuItem {Header="选择小窗地点"};change.Click+=delegate {ChooseWidgetPlaces(mini);};menu.Items.Add(change);}
        mini.ContextMenu=menu;
        Point? pressed=null;
        mini.MouseLeftButtonDown+=(s,e)=>{
            if(e.ClickCount==2){e.Handled=true;mini.Close();return;}
            pressed=e.GetPosition(mini);
        };
        mini.MouseLeftButtonUp+=delegate {pressed=null;};
        mini.MouseMove+=(s,e)=>{
            if(!pressed.HasValue || e.LeftButton!=MouseButtonState.Pressed)return;
            var delta=e.GetPosition(mini)-pressed.Value;
            if(Math.Abs(delta.X)<SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y)<SystemParameters.MinimumVerticalDragDistance)return;
            pressed=null;mini.DragMove();
        };
        RefreshWidget(DateTime.UtcNow);mini.Show();window.Hide();
    }
    Button WidgetAction(string text,string help,Action action) {
        var b=new Button {Content=text,ToolTip=help,Padding=new Thickness(6,2,6,2),Background=Brushes.Transparent,Foreground=Paint("#555C52")};
        AutomationProperties.SetName(b,help);b.Click+=delegate {action();};return b;
    }
    void BuildWidget() {
        compactTimes.Clear();compactDials.Clear();compactAmount=null;
        bool pay=page=="Payroll", clock=page=="World";
        widget.Width=pay?174:clock?236:260;
        var shell=new Border {Background=Paint(pay?"#222821":clock?"#F4F4ED":"#F3EAC9"),
            CornerRadius=new CornerRadius(pay?22:clock?12:4),BorderThickness=new Thickness(1),
            BorderBrush=Paint(pay?"#41473C":clock?"#B7BCB0":"#C9BC96"),Padding=pay?new Thickness(12,3,12,3):new Thickness(13,10,13,12)};
        widget.Content=shell;
        if(pay) {
            shell.Height=43;shell.ToolTip="今日预计已赚 · 拖动移动，双击展开";
            compactAmount=new TextBlock {Foreground=Paint("#F0F5E8"),FontFamily=new FontFamily("Segoe UI Semibold"),FontSize=21};
            shell.Child=new Viewbox {Child=compactAmount,StretchDirection=StretchDirection.DownOnly};return;
        }
        var body=new DockPanel();shell.Child=body;
        var heading=new DockPanel {Margin=new Thickness(0,0,0,8)};DockPanel.SetDock(heading,Dock.Top);body.Children.Add(heading);
        var expand=WidgetAction("↗","返回完整窗口",()=>widget.Close());DockPanel.SetDock(expand,Dock.Right);heading.Children.Add(expand);
        if(clock){var choose=WidgetAction("地点","选择小窗地点",()=>ChooseWidgetPlaces(widget));DockPanel.SetDock(choose,Dock.Right);heading.Children.Add(choose);}
        heading.Children.Add(new TextBlock {Text=clock?"世界时钟":"待办便签",VerticalAlignment=VerticalAlignment.Center,FontWeight=FontWeights.SemiBold});
        compactList=new StackPanel();
        body.Children.Add(new ScrollViewer {Content=compactList,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxHeight=clock?300:205});
        if(clock) {
            foreach(var p in WidgetPlaces()) {
                var row=new DockPanel {Margin=new Thickness(0,4,0,4),ToolTip=p.Title};
                var time=new TextBlock {FontFamily=new FontFamily("Consolas"),FontSize=16,VerticalAlignment=VerticalAlignment.Center};
                DockPanel.SetDock(time,Dock.Right);row.Children.Add(time);compactTimes[p]=time;
                if(!data.DigitalFace){var face=new InstrumentArt(){Width=36,Height=36,Margin=new Thickness(0,0,7,0)};row.Children.Add(face);compactDials[p]=face;}
                row.Children.Add(new TextBlock {Text=p.City,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis});
                compactList.Children.Add(row);
            }
        } else FillNote();
    }
    void FillNote() {
        compactList.Children.Clear();
        if(data.Tasks.Count==0)compactList.Children.Add(new TextBlock {Text="暂无待办",Foreground=Paint("#706B5C"),Margin=new Thickness(0,8,0,10)});
        foreach(var t in data.Tasks.OrderBy(t=>t.Done)) {
            var task=t;
            var label=new TextBlock {Text=task.Title,TextWrapping=TextWrapping.Wrap,MaxWidth=190};
            if(task.Done)label.TextDecorations=TextDecorations.Strikethrough;
            var check=new CheckBox {Content=label,IsChecked=task.Done,Margin=new Thickness(0,5,0,5)};
            check.Click+=delegate {task.Done=check.IsChecked==true;DrawTasks();FillNote();Save();};
            compactList.Children.Add(check);
        }
    }
    void RefreshWidget(DateTime utc) {
        if(widget==null)return;
        if(compactAmount!=null)compactAmount.Text=Element<TextBlock>(window,"Amount").Text;
        foreach(var pair in compactTimes) {
            var local=utc+catalog.Offset(pair.Key,utc);pair.Value.Text=local.ToString("HH:mm:ss");
            InstrumentArt face;
            if(compactDials.TryGetValue(pair.Key,out face)){face.FaceTime=local;face.InvalidateVisual();}
        }
    }
}

