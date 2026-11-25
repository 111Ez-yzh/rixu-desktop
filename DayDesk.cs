using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Serialization;

public class DeskData {
    public Pay Pay = new Pay();
    public List<Todo> Tasks = new List<Todo>();
    public string[] Zones = {"China Standard Time","Tokyo Standard Time","GMT Standard Time","Eastern Standard Time"};
    public string UiLanguage="zh-CN";
    public string ClockDesign="Titanium";
    public bool Pinned; public bool DigitalFace; public bool ListOnly; public string ActiveCity; public string[] CompactZones; public string[] OpenWidgets;
}
public partial class DayDesk {
    readonly Window window;
    readonly DeskData data;
    readonly string path;
    readonly DispatcherTimer timer = new DispatcherTimer();
    DateTime? focusEnd;
    int remaining = 1500;
    static T Element<T>(Window w,string name) where T:FrameworkElement { return (T)w.FindName(name); }
    void Say(string name,string value) { Element<TextBlock>(window,name).Text = value; }
    Button Button(string name) { return Element<Button>(window,name); }
    public DayDesk(Window view, DeskData state, string file) {
        window=view; data=state; path=file;
        RoundedSurfaces.Load(window.Resources);RoundedSurfaces.Frame(window);
        RoundedSurfaces.Clip(Element<Grid>(window,"CoverPanel"),24);
        ControlMotion.Enable(window);
        SetupLanguage();
        var pin = Element<CheckBox>(window,"Pin");
        pin.IsChecked=window.Topmost=data.Pinned;
        pin.Click += delegate { window.Topmost=data.Pinned=pin.IsChecked == true; Save(); };
        Button("Settings").Click += delegate { EditPay(); };
        Button("Zones").Click += delegate { EditZones(); };
        Button("PlanTask").Click += delegate { EditTask(null); };
        Button("TaskHistory").Click += delegate { ShowTaskHistory(); };
        Button("AddTask").Click += delegate { AddTask(); };
        Element<TextBox>(window,"TaskEntry").KeyDown += (s,e) => { if(e.Key==Key.Enter && !e.ImeProcessedKey.Equals(Key.Enter)) { AddTask(); e.Handled=true; } };
        Button("StartFocus").Click += delegate {
            if(focusEnd.HasValue) { remaining=SecondsLeft(); focusEnd=null; }
            else { if(remaining<=0) remaining=1500; focusEnd=DateTime.UtcNow.AddSeconds(remaining); }
            Refresh();
        };
        Button("ResetFocus").Click += delegate { remaining=1500; focusEnd=null; Refresh(); };
        SetupPages(); DrawTasks(); DrawClocks(); Navigate("Home");
        timer.Interval=TimeSpan.FromSeconds(1); timer.Tick += delegate { Refresh(); }; timer.Start();
        window.ContentRendered+=RestoreWidgets;
        window.Closed += delegate {
            closingWorkspace=true;timer.Stop();
            if(widgetWindow!=null)widgetWindow.Close();
        };
    }
    void Save() {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp=path+".tmp";
            using(var stream=File.Create(temp)) new XmlSerializer(typeof(DeskData)).Serialize(stream,data);
            if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
        } catch(Exception ex) { RoundedSurfaces.Notice(widgetWindow!=null && widgetWindow.IsActive?widgetWindow:window,T("brand"),T("saveError")+" "+ex.Message); }
    }
    void AddTask() {
        var input=Element<TextBox>(window,"TaskEntry");
        string title=input.Text.Trim();
        if(title.Length==0) { input.Focus(); return; }
        data.Tasks.Add(new Todo {Title=title}); input.Clear(); DrawTasks(); Save(); input.Focus();
    }
    void DrawTasks(Todo changedTask=null) {
        var list=Element<StackPanel>(window,"Tasks"); list.Children.Clear();
        foreach(var task in OrderedTasks()) {
            var row=new DockPanel {Margin=new Thickness(0,4,0,4)};
            var remove=new Button {Content=task.Done?T("archiveTask"):T("delete"),ToolTip=task.Done?T("archiveHint"):T("deleteTip")};
            DockPanel.SetDock(remove,Dock.Right);row.Children.Add(remove);
            var edit=new Button {Content=T("editTask"),Margin=new Thickness(0,0,6,0)};
            DockPanel.SetDock(edit,Dock.Right);row.Children.Add(edit);
            row.Children.Add(TaskCheck(task,false,task==changedTask));list.Children.Add(row);
            edit.Click += delegate { EditTask(task); };
            remove.Click += delegate {
                if(task.Done)task.Archived=true;
                else data.Tasks.Remove(task);
                DrawTasks();Save();
            };
        }
        FillNote(changedTask);
        Say("TaskCount",F("pendingCount",data.Tasks.Count(t=>!t.Done)));
        Element<TextBlock>(window,"Empty").Visibility=data.Tasks.All(t=>t.Archived) ? Visibility.Visible : Visibility.Hidden;
    }
    int SecondsLeft() { return Math.Max(0,(int)Math.Ceiling((focusEnd.Value-DateTime.UtcNow).TotalSeconds)); }
    void Refresh() {
        var utc=DateTime.UtcNow; var now=utc.ToLocalTime(); var p=data.Pay;
        var rate=p.ForDay(now);
        var currency=PayCurrency.ForCode(p.Currency);
        decimal earned=rate.Earned(now);
        if(page=="World") UpdateClocks(utc);
        Say("DateLabel",now.ToString(EnglishUI?"ddd, MMM d":"M 月 d 日  dddd",UiCulture));
        Say("Amount",currency.Format(p.Monthly==0 ? (decimal?)null : earned,UiCulture,true));
        Say("Rate",p.Monthly==0 ? T("rateEmpty") : rate.MonthDays==0 ? T("noWorkThisMonth") :
            F("rates",currency.Format(rate.PerSecond,UiCulture,true),currency.Format(rate.DailyPay,UiCulture)));
        int minute=now.Hour*60+now.Minute;
        string shift=!rate.IsWorkday ? T("offDay") : minute<p.Start ? T("beforeWork") : minute>=p.End ? T("afterWork") : minute>=p.Lunch && minute<rate.LunchEnd ? T("lunch") : T("working");
        Say("Shift",shift);
        if(page=="Home") {
            Say("IssueMonth",now.ToString(EnglishUI?"MMMM yyyy":"yyyy 年 M 月",UiCulture));
            Say("IssueDay",now.ToString("dd"));Say("LocalTime",now.ToString("HH:mm:ss"));
            summaries["Payroll"].Text=p.Monthly>0 ? F("paySummary",shift,currency.Format(earned,UiCulture)) : T("setupPay");
            summaries["World"].Text=F("worldSummary",selected.Count,CityName(active));
            summaries["TodoPage"].Text=F("taskSummary",data.Tasks.Count(t=>!t.Done),data.Tasks.Count(t=>t.Done && !t.Archived));
        }
        RefreshWidget(utc);
        Say("Countdown",rate.IsWorkday && minute<p.End ? F("countdown",(now.Date.AddMinutes(p.End)-now).ToString(@"hh\:mm\:ss")) : "");
        if(focusEnd.HasValue) {
            remaining=SecondsLeft();
            if(remaining==0) { focusEnd=null; System.Media.SystemSounds.Asterisk.Play(); }
        }
        Say("FocusTime",TimeSpan.FromSeconds(remaining).ToString(@"mm\:ss"));
        Say("FocusLabel",remaining==0 ? T("focusDone") : T("focus"));
        Button("StartFocus").Content=focusEnd.HasValue ? T("pause") : remaining==1500 || remaining==0 ? T("startFocus") : T("resume");
    }
    Window Dialog(string title,FrameworkElement content,int height) {
        var dialog=new Window {Language=window.Language,Resources=window.Resources, Background=window.Background,Foreground=window.Foreground, Title=title,Owner=window,Content=content,Width=460,Height=height,
            MinWidth=400,MinHeight=360,FontFamily=window.FontFamily,FontWeight=window.FontWeight,FontSize=14,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        RoundedSurfaces.Frame(dialog);ControlMotion.Enable(dialog);return dialog;
    }
    static Button ActionButton(string label) { return new Button {Content=label,Padding=new Thickness(14,7,14,7),HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,6,0,4)}; }
    static TextBox Field(Panel panel,string label,string value) {
        var row=new DockPanel {Margin=new Thickness(0,5,0,5)};
        row.Children.Add(new TextBlock {Text=label,Width=180,VerticalAlignment=VerticalAlignment.Center});
        var box=new TextBox {Text=value,Padding=new Thickness(6,4,6,4)};
        row.Children.Add(box); panel.Children.Add(row); return box;
    }
    static string Clock(int minutes) { return TimeSpan.FromMinutes(minutes).ToString(@"hh\:mm"); }
    static int Minute(string text) {
        TimeSpan value;
        return TimeSpan.TryParseExact(text.Trim(),new[]{@"h\:mm",@"hh\:mm"},null,out value) && value.TotalMinutes<1440 ? (int)value.TotalMinutes : -1;
    }
    [STAThread] public static void Main() {
        bool first;
        using(var guard=new Mutex(true,"Local\\DailyDesk_74ba0d12",out first)) {
            if(!first) { RoundedSurfaces.Notice(null,"日序 · RiXu","日序已经打开。 / RiXu is already open."); return; }
            try {
                string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RiXuDesk","desk.xml");
                var data=new DeskData();
                if(File.Exists(path)) using(var stream=File.OpenRead(path)) data=(DeskData)new XmlSerializer(typeof(DeskData)).Deserialize(stream);
                var app=new Application();
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Dashboard.xaml")) {
                    var window=(Window)XamlReader.Load(stream);
                    new DayDesk(window,data,path); app.Run(window);
                }
            } catch(Exception ex) {
                string message="日序运行失败 / RiXu could not run: "+ex.Message;
                try {RoundedSurfaces.Notice(null,"日序 · RiXu",message);}
                catch {MessageBox.Show(message,"RiXu");}
            }
        }
    }
}
