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
    readonly Dictionary<string,string> pages=new Dictionary<string,string>();
    readonly Dictionary<Place,TextBlock> stamps=new Dictionary<Place,TextBlock>();
    readonly TravelClock dial=new TravelClock();
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
        bool on=page=="World" && !data.ListOnly && !data.DigitalFace && SystemParameters.ClientAreaAnimation && window.WindowState!=WindowState.Minimized && window.IsVisible;
        if(on==sweeping)return;
        sweeping=on;
        if(on){lastFrame=TimeSpan.Zero;CompositionTarget.Rendering+=Sweep;}
        else {CompositionTarget.Rendering-=Sweep;dial.Rest();}
    }
    void Sweep(object sender,EventArgs args) {
        var frame=((RenderingEventArgs)args).RenderingTime;
        double elapsed=(frame-lastFrame).TotalSeconds;
        if(elapsed<(RenderCapability.Tier==0?.041:.028))return;
        lastFrame=frame;
        var utc=DateTime.UtcNow;
        if(Math.Abs((utc-dialUtc).TotalSeconds)>2)return;
        dial.ShowTime(utc+dialOffset);dial.Advance(elapsed);
    }
    void SetupPages() {
        window.IsVisibleChanged+=delegate {UpdateSweep();};
        window.StateChanged+=delegate {UpdateSweep();};
        System.ComponentModel.PropertyChangedEventHandler animationPreference=(s,e)=>{
            if(e.PropertyName=="ClientAreaAnimation"){dial.ApplyMotionPreference();UpdateSweep();}
        };
        SystemParameters.StaticPropertyChanged+=animationPreference;
        window.Closed+=delegate {CompositionTarget.Rendering-=Sweep;SystemParameters.StaticPropertyChanged-=animationPreference;};
        var photograph=new ImageBrush(LoadArtwork("Cover.jpg")) {Stretch=Stretch.UniformToFill};
        photograph.Freeze();
        Element<Rectangle>(window,"Cover").Fill=photograph;
        ClipHomeFrost();
        Element<Image>(window,"Signature").Source=LoadArtwork("SanmuMark.png");
        Button("Compact").Click+=delegate {ToggleWidget(page);};
        Button("OnlyWidgets").Click+=delegate {window.WindowState=WindowState.Minimized;};
        Button("MiniPlaces").Click+=delegate {ChooseWidgetPlaces(window);};
        selected=catalog.Restore(data.Zones); active=selected.FirstOrDefault(p=>p.Key==data.ActiveCity)??selected[0];
        BuildLaunchers();
        Button("Back").Click+=delegate {Navigate("Home");};
        window.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape && page!="Home"){Navigate("Home");e.Handled=true;}};
        Element<Grid>(window,"DialHost").Children.Add(dial);
        var picker=Element<ComboBox>(window,"DialDesignPicker");
        picker.SelectedValuePath="Tag";
        foreach(var design in DialDesign.All) {
            var sample=new StackPanel {Orientation=Orientation.Horizontal};
            sample.Children.Add(new InstrumentArt {Design=design,Width=22,Height=22,Margin=new Thickness(0,0,5,0)});
            var label=new TextBlock {VerticalAlignment=VerticalAlignment.Center};
            label.SetResourceReference(TextBlock.TextProperty,"ui.dial"+design.Id);sample.Children.Add(label);
            var item=new ComboBoxItem {Tag=design.Id,Content=sample};
            item.SetResourceReference(AutomationProperties.NameProperty,"ui.dial"+design.Id);picker.Items.Add(item);
        }
        dial.Design=DialDesign.Find(data.ClockDesign);data.ClockDesign=dial.Design.Id;
        picker.SelectedValue=data.ClockDesign;
        picker.SelectionChanged+=delegate {
            var selectedItem=picker.SelectedItem as ComboBoxItem;
            if(selectedItem==null)return;
            dial.Design=DialDesign.Find((string)selectedItem.Tag);
            data.ClockDesign=dial.Design.Id;RefreshClockWidget();Save();
        };
        Button("Analog").Click+=delegate {SetFace(false);};
        Button("Digital").Click+=delegate {SetFace(true);};
        Button("ListOnly").Click+=delegate {SetFace(false,true);};
        StyleFace();
    }
    void BuildLaunchers() {
        Element<Panel>(window,"Launchers").Children.Clear();
        pages.Clear();widgetToggles.Clear();summaries.Clear();
        pages.Add("Home",T("homeTitle"));
        AddPage("Payroll",T("payTitle"),T("payCaption"),"M4 4 Q4 2 6 2 H18 Q20 2 20 4 V20 Q20 22 18 22 H6 Q4 22 4 20 Z M8 6 H16 M16 14 V18 M16 10 h.01 M12 10 h.01 M8 10 h.01 M12 14 h.01 M8 14 h.01 M12 18 h.01 M8 18 h.01");
        AddPage("World",T("worldTitle"),T("worldCaption"),"M12 10 v2.2 l1.6 1 M16.13 7.66 l-.81 -4.05 a2 2 0 0 0 -2 -1.61 h-2.68 a2 2 0 0 0 -2 1.61 l-.78 4.05 M7.88 16.36 l.8 4 a2 2 0 0 0 2 1.61 h2.72 a2 2 0 0 0 2 -1.61 l.81 -4.05 M18 12 A6 6 0 1 1 6 12 A6 6 0 1 1 18 12");
        AddPage("TodoPage",T("todoTitle"),T("todoCaption"),"M13 5 H21 M13 12 H21 M13 19 H21 M3 17 l2 2 4 -4 M3 7 l2 2 4 -4");
    }
    static ImageSource LoadArtwork(string resource) {
        using(var stream=typeof(DayDesk).Assembly.GetManifestResourceStream(resource)) {
            var image=new System.Windows.Media.Imaging.BitmapImage();
            image.BeginInit();image.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.StreamSource=stream;image.EndInit();image.Freeze();
            return image;
        }
    }
    void ClipHomeFrost() {
        var backdrop=Element<Grid>(window,"HomeFrost");
        var cutouts=new GeometryGroup();backdrop.Clip=cutouts;
        // Both panes sample the same photograph at the same scale as the sharp cover.
        foreach(var name in new[]{"DeskGlass","DateGlass"}) {
            var pane=Element<Border>(window,name);
            var radius=pane.CornerRadius.TopLeft;
            var cutout=new RectangleGeometry {RadiusX=radius,RadiusY=radius};
            cutouts.Children.Add(cutout);
            pane.LayoutUpdated+=delegate {
                if(!pane.IsVisible || !pane.IsArrangeValid)return;
                var bounds=new Rect(pane.TranslatePoint(new Point(),backdrop),pane.RenderSize);
                if(cutout.Rect!=bounds)cutout.Rect=bounds;
            };
        }
    }
    readonly Dictionary<string,TextBlock> summaries=new Dictionary<string,TextBlock>();
    void AddPage(string id,string title,string caption,string iconData) {
        pages.Add(id,title);
        var tile=new Button {Padding=new Thickness(12,9,10,9),Background=Paint("#7AFFFFFF"),HorizontalContentAlignment=HorizontalAlignment.Stretch,ToolTip=caption};
        AutomationProperties.SetName(tile,title);
        var row=new DockPanel();
        var arrow=new Path {Data=Geometry.Parse("M1,1 L5,5 L1,9"),Stroke=Paint("#536279"),StrokeThickness=1.4,
            StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,
            Width=8,Height=11,Margin=new Thickness(4,0,0,0),VerticalAlignment=VerticalAlignment.Center};
        DockPanel.SetDock(arrow,Dock.Right);row.Children.Add(arrow);
        var icon=new Path {Data=Geometry.Parse(iconData),Stroke=Paint("#394B65"),StrokeThickness=1.6,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Width=24,Height=24,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,9,0)};
        row.Children.Add(icon);
        var labels=new StackPanel();
        labels.Children.Add(new TextBlock {Text=title,FontSize=20,Foreground=Paint("#1D2736"),FontWeight=FontWeights.Bold,TextTrimming=TextTrimming.CharacterEllipsis});
        var status=new TextBlock {FontSize=13,FontWeight=FontWeights.Medium,Foreground=Paint("#394758"),Margin=new Thickness(0,3,0,0),TextTrimming=TextTrimming.CharacterEllipsis};
        status.SetBinding(FrameworkElement.ToolTipProperty,new System.Windows.Data.Binding("Text"){Source=status});
        labels.Children.Add(status);summaries[id]=status;
        row.Children.Add(labels);
        tile.Content=row;
        var slide=new TranslateTransform();arrow.RenderTransform=slide;
        Action<bool> hover=over=>{double old=slide.X;slide.X=over?3:0;Ease(slide,TranslateTransform.XProperty,old,slide.X,160);};
        tile.MouseEnter+=delegate {hover(true);};tile.MouseLeave+=delegate {hover(false);};
        tile.Click+=delegate {Navigate(id);};
        var entry=new DockPanel {Margin=new Thickness(0,3,0,5)};
        var toggle=new CheckBox {Content=T("widget"),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(10,0,0,0),ToolTip=F("widgetFor",title)};
        AutomationProperties.SetName(toggle,F("widgetFor",title));
        widgetToggles[id]=toggle;
        toggle.Click+=delegate {ToggleWidget(id);};
        DockPanel.SetDock(toggle,Dock.Right);entry.Children.Add(toggle);entry.Children.Add(tile);
        Element<Panel>(window,"Launchers").Children.Add(entry);
    }
    void Navigate(string id) {
        page=id;SyncWidgetControls();
        foreach(var key in pages.Keys) Element<FrameworkElement>(window,key).Visibility=key==id?Visibility.Visible:Visibility.Collapsed;
        Element<DockPanel>(window,"PageBar").Visibility=id=="Home"?Visibility.Collapsed:Visibility.Visible;
        Say("Heading",pages[id]); Refresh();
        UpdateSweep(); Appear(Element<FrameworkElement>(window,id));
    }
    void StyleFace() {
        Element<Grid>(window,"DialHost").Visibility=data.DigitalFace?Visibility.Collapsed:Visibility.Visible;
        Element<Viewbox>(window,"DigitalHost").Visibility=data.DigitalFace?Visibility.Visible:Visibility.Collapsed;
        Element<Image>(window,"DigitalFlag").Visibility=Element<Viewbox>(window,"DigitalHost").Visibility;
        var hero=Element<Border>(window,"ClockHero");
        hero.Visibility=data.ListOnly?Visibility.Collapsed:Visibility.Visible;
        hero.Background=(Brush)window.Resources[data.DigitalFace?"digitalStage":"analogStage"];
        Element<StackPanel>(window,"DialDesignControl").Visibility=data.DigitalFace||data.ListOnly?Visibility.Collapsed:Visibility.Visible;
        Element<StackPanel>(window,"ClockCaption").HorizontalAlignment=data.DigitalFace?HorizontalAlignment.Center:HorizontalAlignment.Stretch;
        Element<TextBlock>(window,"CityTitle").Foreground=data.DigitalFace?Brushes.White:Paint("#272D32");
        Element<TextBlock>(window,"CityDate").Foreground=Paint(data.DigitalFace?"#BEC4D9":"#545C60");
        var listPanel=Element<Border>(window,"ClockListPanel");
        Grid.SetColumn(listPanel,data.ListOnly?0:2);
        Grid.SetColumnSpan(listPanel,data.ListOnly?3:1);
        string mode=data.ListOnly?"ListOnly":data.DigitalFace?"Digital":"Analog";
        foreach(var name in new[]{"Analog","Digital","ListOnly"}) {
            bool chosen=name==mode;
            Button(name).Background=chosen?Paint("#3C537F"):Paint("#E9EAF0");
            Button(name).Foreground=chosen?Brushes.White:Paint("#3C3D48");
        }
    }
    void SetFace(bool digital,bool listOnly=false) {
        if(digital==data.DigitalFace && listOnly==data.ListOnly)return;
        data.DigitalFace=digital;data.ListOnly=listOnly;
        StyleFace();DrawClocks();UpdateSweep();UpdateClocks(DateTime.UtcNow);
        Appear(Element<FrameworkElement>(window,listOnly?"ClockListPanel":digital?"DigitalHost":"DialHost"));Save();
    }
    void DrawClocks() {
        var flag=ClockFlags.For(active.Code);
        dial.Flag=flag;
        Element<Image>(window,"DigitalFlag").Source=flag;
        RefreshClockWidget();
        var list=Element<StackPanel>(window,"Clocks");list.Children.Clear();stamps.Clear();
        foreach(var p in selected) {
            Place place=p;
            var row=new Button {Background=p==active?Paint("#E6EBF5"):Brushes.White,HorizontalContentAlignment=HorizontalAlignment.Stretch,Padding=new Thickness(10,8,10,8),Margin=new Thickness(0,1,0,2),ToolTip=PlaceTitle(p)};
            row.Template=(ControlTemplate)window.Resources["listAction"];
            var dock=new DockPanel();
            var time=new TextBlock {VerticalAlignment=VerticalAlignment.Center,FontSize=18,FontWeight=FontWeights.Medium};
            DockPanel.SetDock(time,Dock.Right);dock.Children.Add(time);
            var labels=new StackPanel();
            labels.Children.Add(new TextBlock {Text=CityName(p),FontSize=16,TextTrimming=TextTrimming.CharacterEllipsis});
            labels.Children.Add(new TextBlock {Text=CountryName(p),FontSize=12,Foreground=Paint("#676A78")});
            dock.Children.Add(labels); row.Content=ClockFlags.BehindRow(dock,p.Code,false,data.ListOnly);list.Children.Add(row);stamps[p]=time;
            row.Click+=delegate {active=place;data.ActiveCity=place.Key;DrawClocks();UpdateClocks(DateTime.UtcNow);Save();};
        }
    }
    void UpdateClocks(DateTime utc) {
        foreach(var pair in stamps)pair.Value.Text=(utc+catalog.Offset(pair.Key,utc)).ToString(data.ListOnly?"HH:mm:ss":"HH:mm");
        var offset=catalog.Offset(active,utc);var local=utc+offset;dialUtc=utc;dialOffset=offset;
        Say("CityTitle",PlaceTitle(active));Say("CityDate",local.ToString("yyyy.MM.dd   ")+"UTC"+(offset<TimeSpan.Zero?"−":"+")+offset.Duration().ToString(@"hh\:mm"));
        Say("DigitalTime",local.ToString("HH:mm:ss"));
        if(!data.DigitalFace && !data.ListOnly && window.WindowState!=WindowState.Minimized)dial.ShowTime(local);
    }
    void EditZones() {
        var layout=new DockPanel {Margin=new Thickness(22)};
        var dialog=Dialog(T("editPlaces"),layout,610);
        var save=ActionButton(T("savePlaces"));DockPanel.SetDock(save,Dock.Bottom);layout.Children.Add(save);
        var hint=new TextBlock {Text=T("searchHint"),FontSize=21,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,10)};
        DockPanel.SetDock(hint,Dock.Top);layout.Children.Add(hint);
        var search=new TextBox {Margin=new Thickness(0,0,0,10)};AutomationProperties.SetName(search,hint.Text);DockPanel.SetDock(search,Dock.Top);layout.Children.Add(search);
        var list=new ListBox();layout.Children.Add(list);
        var chosen=new HashSet<Place>(selected);
        Action filter=delegate {
            list.Items.Clear();string query=search.Text.Trim();
            foreach(var p in catalog.Places.Where(p=>p.Matches(query) || CityName(p).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0).OrderByDescending(p=>chosen.Contains(p)).ThenBy(p=>CountryName(p)).ThenBy(p=>CityName(p))) {
                Place place=p;var check=new CheckBox {Content=PlaceTitle(p),IsEnabled=p.Zone!="-",ToolTip=p.Zone,IsChecked=chosen.Contains(p),Margin=new Thickness(4,6,4,6)};
                check.Click+=delegate {if(check.IsChecked==true)chosen.Add(place);else chosen.Remove(place);};
                list.Items.Add(check);
            }
            if(list.Items.Count==0)list.Items.Add(T("noMatches"));
        };
        search.TextChanged+=delegate {filter();};filter();
        save.Click+=delegate {
            if(chosen.Count==0){RoundedSurfaces.Notice(dialog,dialog.Title,T("needPlace"));return;}
            selected=selected.Where(chosen.Contains).Concat(catalog.Places.Where(p=>chosen.Contains(p) && !selected.Contains(p))).ToList();
            if(!selected.Contains(active))active=selected[0];data.ActiveCity=active.Key;
            data.Zones=selected.Select(p=>p.Code=="WIN"?p.Zone:p.Key).ToArray();DrawClocks();UpdateClocks(DateTime.UtcNow);Save();dialog.Close();
        };
        dialog.Loaded+=delegate {search.Focus();};dialog.ShowDialog();
    }
}
