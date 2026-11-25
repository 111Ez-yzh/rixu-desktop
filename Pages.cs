using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Automation;

public partial class DayDesk {
    readonly WorldCatalog catalog=new WorldCatalog();
    readonly Dictionary<string,string> pages=new Dictionary<string,string> {{"Home","我的工作台"}};
    readonly Dictionary<Place,TextBlock> stamps=new Dictionary<Place,TextBlock>();
    readonly InstrumentArt dial=new InstrumentArt();
    List<Place> selected; Place active;
    string page;
    static Brush Paint(string hex) {return (Brush)new BrushConverter().ConvertFromString(hex);}
    static void Ease(Animatable target,DependencyProperty property,double from,double to,int ms) {
        if(!SystemParameters.ClientAreaAnimation) return;
        target.BeginAnimation(property,new DoubleAnimation(from,to,TimeSpan.FromMilliseconds(ms)) {
            EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop});
    }
    void Appear(FrameworkElement el) {
        if(!SystemParameters.ClientAreaAnimation)return;
        var move=new TranslateTransform(); el.RenderTransform=move;
        Ease(move,TranslateTransform.YProperty,14,0,320);
        el.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(230)) {FillBehavior=FillBehavior.Stop});
    }
    TimeSpan lastFrame;
    DateTime dialUtc;
    TimeSpan dialOffset;
    bool sweeping;
    void UpdateSweep() {
        bool on=page=="World" && !data.DigitalFace && SystemParameters.ClientAreaAnimation && window.WindowState!=WindowState.Minimized && window.IsVisible;
        if(on==sweeping)return;
        sweeping=on;
        if(on)CompositionTarget.Rendering+=Sweep;
        else CompositionTarget.Rendering-=Sweep;
    }
    void Sweep(object sender,EventArgs args) {
        var frame=((RenderingEventArgs)args).RenderingTime;
        if(frame-lastFrame<TimeSpan.FromMilliseconds(28))return;
        lastFrame=frame;
        var utc=DateTime.UtcNow;
        if(Math.Abs((utc-dialUtc).TotalSeconds)>2)return;
        dial.FaceTime=utc+dialOffset;dial.InvalidateVisual();
    }
    void SetupPages() {
        window.IsVisibleChanged+=delegate {UpdateSweep();};
        window.StateChanged+=delegate {UpdateSweep();};
        window.Closed+=delegate {CompositionTarget.Rendering-=Sweep;};
        LoadCover();
        Button("Compact").Click+=delegate {OpenWidget();};
        Button("MiniPlaces").Click+=delegate {ChooseWidgetPlaces(window);};
        selected=catalog.Restore(data.Zones); active=selected.FirstOrDefault(p=>p.Key==data.ActiveCity)??selected[0];
        AddPage("Payroll","收入计算器","按秒查看今日收入","M4 4 Q4 2 6 2 H18 Q20 2 20 4 V20 Q20 22 18 22 H6 Q4 22 4 20 Z M8 6 H16 M16 14 V18 M16 10 h.01 M12 10 h.01 M8 10 h.01 M12 14 h.01 M8 14 h.01 M12 18 h.01 M8 18 h.01");
        AddPage("World","世界时钟","查看各地时间与表盘","M12 10 v2.2 l1.6 1 M16.13 7.66 l-.81 -4.05 a2 2 0 0 0 -2 -1.61 h-2.68 a2 2 0 0 0 -2 1.61 l-.78 4.05 M7.88 16.36 l.8 4 a2 2 0 0 0 2 1.61 h2.72 a2 2 0 0 0 2 -1.61 l.81 -4.05 M18 12 A6 6 0 1 1 6 12 A6 6 0 1 1 18 12");
        AddPage("TodoPage","待办清单","整理任务，开始专注","M13 5 H21 M13 12 H21 M13 19 H21 M3 17 l2 2 4 -4 M3 7 l2 2 4 -4");
        Button("Back").Click+=delegate {Navigate("Home");};
        window.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape && page!="Home"){Navigate("Home");e.Handled=true;}};
        Element<Grid>(window,"DialHost").Children.Add(dial);
        Button("Analog").Click+=delegate {SetFace(false);};
        Button("Digital").Click+=delegate {SetFace(true);};
        StyleFace();
    }
    void LoadCover() {
        using(var stream=typeof(DayDesk).Assembly.GetManifestResourceStream("Cover.jpg")) {
            var image=new System.Windows.Media.Imaging.BitmapImage();
            image.BeginInit();image.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.StreamSource=stream;image.EndInit();image.Freeze();
            Element<Image>(window,"Cover").Source=image;
        }
    }
    readonly Dictionary<string,TextBlock> summaries=new Dictionary<string,TextBlock>();
    void AddPage(string id,string title,string caption,string iconData) {
        pages.Add(id,title);
        var tile=new Button {Padding=new Thickness(3,19,5,19),Background=Brushes.Transparent,HorizontalContentAlignment=HorizontalAlignment.Stretch,ToolTip=caption};
        AutomationProperties.SetName(tile,title);
        var row=new DockPanel();
        var arrow=new TextBlock {Text="↗",FontFamily=new FontFamily("Segoe UI Symbol"),FontSize=20,Foreground=Paint("#63745E"),VerticalAlignment=VerticalAlignment.Center};
        DockPanel.SetDock(arrow,Dock.Right);row.Children.Add(arrow);
        var icon=new Path {Data=Geometry.Parse(iconData),Stroke=Paint("#5D675A"),StrokeThickness=1.6,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Width=24,Height=24,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,15,0)};
        row.Children.Add(icon);
        var labels=new StackPanel();
        labels.Children.Add(new TextBlock {Text=title,FontSize=23,FontWeight=FontWeights.Medium});
        var status=new TextBlock {FontSize=13,Foreground=Paint("#66695F"),Margin=new Thickness(0,4,0,0),TextTrimming=TextTrimming.CharacterEllipsis};
        labels.Children.Add(status);summaries[id]=status;
        row.Children.Add(labels);
        tile.Content=new Border {Child=row,BorderThickness=new Thickness(0,0,0,1),BorderBrush=Paint("#D4D4C9"),Padding=new Thickness(0,0,0,12)};
        var slide=new TranslateTransform();arrow.RenderTransform=slide;
        Action<bool> hover=over=>{double old=slide.X;slide.X=over?3:0;Ease(slide,TranslateTransform.XProperty,old,slide.X,160);tile.Background=over?Paint("#EEEFE6"):Brushes.Transparent;};
        tile.MouseEnter+=delegate {hover(true);};tile.MouseLeave+=delegate {hover(false);};
        tile.Click+=delegate {Navigate(id);};
        Element<Panel>(window,"Launchers").Children.Add(tile);
    }
    void Navigate(string id) {
        page=id;
        foreach(var key in pages.Keys) Element<FrameworkElement>(window,key).Visibility=key==id?Visibility.Visible:Visibility.Collapsed;
        Element<DockPanel>(window,"PageBar").Visibility=id=="Home"?Visibility.Collapsed:Visibility.Visible;
        Say("Heading",pages[id]); if(id=="World")UpdateClocks(DateTime.UtcNow);
        UpdateSweep(); Appear(Element<FrameworkElement>(window,id));
    }
    void StyleFace() {
        Element<Grid>(window,"DialHost").Visibility=data.DigitalFace?Visibility.Collapsed:Visibility.Visible;
        Element<Viewbox>(window,"DigitalHost").Visibility=data.DigitalFace?Visibility.Visible:Visibility.Collapsed;
        foreach(var name in new[]{"Analog","Digital"}) {
            bool chosen=(name=="Digital")==data.DigitalFace;
            Button(name).Background=chosen?Paint("#333E34"):Paint("#E7E8DE");
            Button(name).Foreground=chosen?Brushes.White:Paint("#3C3D48");
        }
    }
    void SetFace(bool digital) {
        if(digital==data.DigitalFace)return;
        data.DigitalFace=digital;StyleFace();UpdateSweep();UpdateClocks(DateTime.UtcNow);
        Appear(Element<FrameworkElement>(window,digital?"DigitalHost":"DialHost"));Save();
    }
    void DrawClocks() {
        var list=Element<StackPanel>(window,"Clocks");list.Children.Clear();stamps.Clear();
        foreach(var p in selected) {
            Place place=p;
            var row=new Button {Background=p==active?Paint("#E8EBE1"):Brushes.White,HorizontalContentAlignment=HorizontalAlignment.Stretch,Padding=new Thickness(10,8,10,8),Margin=new Thickness(0,1,0,2),ToolTip=p.Title};
            var dock=new DockPanel();
            var time=new TextBlock {VerticalAlignment=VerticalAlignment.Center,FontSize=18,FontWeight=FontWeights.Medium};
            DockPanel.SetDock(time,Dock.Right);dock.Children.Add(time);
            var labels=new StackPanel();
            labels.Children.Add(new TextBlock {Text=p.City,FontSize=16,TextTrimming=TextTrimming.CharacterEllipsis});
            labels.Children.Add(new TextBlock {Text=p.Country,FontSize=12,Foreground=Paint("#676A78")});
            dock.Children.Add(labels); row.Content=dock;list.Children.Add(row);stamps[p]=time;
            row.Click+=delegate {active=place;data.ActiveCity=place.Key;DrawClocks();UpdateClocks(DateTime.UtcNow);Save();};
        }
    }
    void UpdateClocks(DateTime utc) {
        foreach(var pair in stamps)pair.Value.Text=(utc+catalog.Offset(pair.Key,utc)).ToString("HH:mm");
        var offset=catalog.Offset(active,utc);var local=utc+offset;dialUtc=utc;dialOffset=offset;
        Say("CityTitle",active.Title);Say("CityDate",local.ToString("yyyy.MM.dd   ")+"UTC"+(offset<TimeSpan.Zero?"−":"+")+offset.Duration().ToString(@"hh\:mm"));
        Say("DigitalTime",local.ToString("HH:mm:ss"));
        if(!data.DigitalFace){dial.FaceTime=local;dial.InvalidateVisual();}
    }
    void EditZones() {
        var layout=new DockPanel {Margin=new Thickness(22)};
        var dialog=Dialog("管理地点",layout,610);
        var save=ActionButton("保存地点");DockPanel.SetDock(save,Dock.Bottom);layout.Children.Add(save);
        var hint=new TextBlock {Text="搜索国家、地区或城市",FontSize=21,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,10)};
        DockPanel.SetDock(hint,Dock.Top);layout.Children.Add(hint);
        var search=new TextBox {Margin=new Thickness(0,0,0,10)};AutomationProperties.SetName(search,hint.Text);DockPanel.SetDock(search,Dock.Top);layout.Children.Add(search);
        var list=new ListBox();layout.Children.Add(list);
        var chosen=new HashSet<Place>(selected);
        Action filter=delegate {
            list.Items.Clear();string query=search.Text.Trim();
            foreach(var p in catalog.Places.Where(p=>p.Matches(query)).OrderByDescending(p=>chosen.Contains(p)).ThenBy(p=>p.Country).ThenBy(p=>p.City)) {
                Place place=p;var check=new CheckBox {Content=p.Title,IsEnabled=p.Zone!="-",ToolTip=p.Zone,IsChecked=chosen.Contains(p),Margin=new Thickness(4,6,4,6)};
                check.Click+=delegate {if(check.IsChecked==true)chosen.Add(place);else chosen.Remove(place);};
                list.Items.Add(check);
            }
            if(list.Items.Count==0)list.Items.Add("没有匹配的地点");
        };
        search.TextChanged+=delegate {filter();};filter();
        save.Click+=delegate {
            if(chosen.Count==0){MessageBox.Show(dialog,"请至少选择一个地点。");return;}
            selected=selected.Where(chosen.Contains).Concat(catalog.Places.Where(p=>chosen.Contains(p) && !selected.Contains(p))).ToList();
            if(!selected.Contains(active))active=selected[0];data.ActiveCity=active.Key;
            data.Zones=selected.Select(p=>p.Code=="WIN"?p.Zone:p.Key).ToArray();DrawClocks();UpdateClocks(DateTime.UtcNow);Save();dialog.Close();
        };
        dialog.Loaded+=delegate {search.Focus();};dialog.ShowDialog();
    }
}



