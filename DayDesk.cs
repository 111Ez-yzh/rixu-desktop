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

public class Todo { public string Title; public bool Done; }
public class Pay {
    public decimal Monthly, Days = 22;
    public int Start = 540, Lunch = 720, Resume = 780, End = 1080;
    public bool Weekdays = true;
    public int PaidMinutes { get { return End - Start - (Resume - Lunch); } }
    public bool WorkingDay(DateTime now) {
        return !Weekdays || (now.DayOfWeek != DayOfWeek.Saturday && now.DayOfWeek != DayOfWeek.Sunday);
    }
    public decimal Earned(DateTime now) {
        if (!WorkingDay(now)) return 0;
        int seconds = (int)now.TimeOfDay.TotalSeconds;
        int paid = Math.Max(0, Math.Min(seconds, Lunch * 60) - Start * 60)
                 + Math.Max(0, Math.Min(seconds, End * 60) - Resume * 60);
        return Monthly / Days * paid / (PaidMinutes * 60);
    }
}
public class DeskData {
    public Pay Pay = new Pay();
    public List<Todo> Tasks = new List<Todo>();
    public string[] Zones = {"China Standard Time","Tokyo Standard Time","GMT Standard Time","Eastern Standard Time"};
    public bool Pinned; public bool DigitalFace; public string ActiveCity; public string[] CompactZones;
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
        var pin = Element<CheckBox>(window,"Pin");
        pin.IsChecked=window.Topmost=data.Pinned;
        pin.Click += delegate { window.Topmost=data.Pinned=pin.IsChecked == true; Save(); };
        Button("Settings").Click += delegate { EditPay(); };
        Button("Zones").Click += delegate { EditZones(); };
        Button("AddTask").Click += delegate { AddTask(); };
        Element<TextBox>(window,"TaskEntry").KeyDown += (s,e) => { if(e.Key==Key.Enter && !e.ImeProcessedKey.Equals(Key.Enter)) { AddTask(); e.Handled=true; } };
        Button("StartFocus").Click += delegate {
            if(focusEnd.HasValue) { remaining=SecondsLeft(); focusEnd=null; }
            else { if(remaining<=0) remaining=1500; focusEnd=DateTime.UtcNow.AddSeconds(remaining); }
            Refresh();
        };
        Button("ResetFocus").Click += delegate { remaining=1500; focusEnd=null; Refresh(); };
        SetupPages(); DrawTasks(); DrawClocks(); Navigate("Home"); Refresh();
        timer.Interval=TimeSpan.FromSeconds(1); timer.Tick += delegate { Refresh(); }; timer.Start();
        window.Closed += delegate { timer.Stop(); };
    }
    void Save() {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp=path+".tmp";
            using(var stream=File.Create(temp)) new XmlSerializer(typeof(DeskData)).Serialize(stream,data);
            if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
        } catch(Exception ex) { MessageBox.Show(widget??window,"本次修改仅在当前窗口生效。保存失败：" + ex.Message); }
    }
    void AddTask() {
        var input=Element<TextBox>(window,"TaskEntry");
        string title=input.Text.Trim();
        if(title.Length==0) { input.Focus(); return; }
        data.Tasks.Add(new Todo {Title=title}); input.Clear(); DrawTasks(); Save(); input.Focus();
    }
    void DrawTasks() {
        var list=Element<StackPanel>(window,"Tasks"); list.Children.Clear();
        foreach(var item in data.Tasks.OrderBy(t=>t.Done)) {
            Todo task=item;
            var row=new DockPanel {Margin=new Thickness(0,3,0,3)};
            var remove=new Button {Content="删除",ToolTip="删除此待办"};
            DockPanel.SetDock(remove,Dock.Right); row.Children.Add(remove);
            var label=new TextBlock {Text=task.Title,TextWrapping=TextWrapping.Wrap};
            if(task.Done) label.TextDecorations=TextDecorations.Strikethrough;
            var check=new CheckBox {Content=label,IsChecked=task.Done,VerticalAlignment=VerticalAlignment.Center};
            row.Children.Add(check); list.Children.Add(row);
            check.Click += delegate { task.Done=check.IsChecked==true; DrawTasks(); Save(); };
            remove.Click += delegate { data.Tasks.Remove(task); DrawTasks(); Save(); };
        }
        Say("TaskCount",data.Tasks.Count(t=>!t.Done)+" 项待办");
        Element<TextBlock>(window,"Empty").Visibility=data.Tasks.Count==0 ? Visibility.Visible : Visibility.Hidden;
    }
    int SecondsLeft() { return Math.Max(0,(int)Math.Ceiling((focusEnd.Value-DateTime.UtcNow).TotalSeconds)); }
    void Refresh() {
        var utc=DateTime.UtcNow; var now=utc.ToLocalTime(); var p=data.Pay;
        if(page=="World") UpdateClocks(utc);
        Say("DateLabel",now.ToString("M 月 d 日  dddd",new System.Globalization.CultureInfo("zh-CN")));
        Say("Amount",p.Monthly==0 ? "¥ —" : "¥ "+p.Earned(now).ToString("N4"));
        Say("Rate",p.Monthly==0 ? "在薪资设置中填写月薪，即可开始估算" : String.Format("秒薪 ¥{0:F4}     /     日薪 ¥{1:N2}",p.Monthly/p.Days/(p.PaidMinutes*60),p.Monthly/p.Days));
        int minute=now.Hour*60+now.Minute;
        string shift=!p.WorkingDay(now) ? "休息日" : minute<p.Start ? "尚未上班" : minute>=p.End ? "已下班" : minute>=p.Lunch && minute<p.Resume ? "午休中" : "工作中";
        Say("Shift",shift);
        if(page=="Home") {
            Say("IssueMonth",now.ToString("MMMM yyyy",System.Globalization.CultureInfo.InvariantCulture));
            Say("IssueDay",now.ToString("dd"));Say("LocalTime",now.ToString("HH:mm:ss"));
            summaries["Payroll"].Text=p.Monthly>0 ? shift+"  ·  今日预计已赚 "+p.Earned(now).ToString("C2",new System.Globalization.CultureInfo("zh-CN")) : "设置月薪与工作时间";
            summaries["World"].Text=selected.Count+" 个地点  ·  "+active.City;
            summaries["TodoPage"].Text=data.Tasks.Count(t=>!t.Done)+" 项待办  ·  "+data.Tasks.Count(t=>t.Done)+" 项已完成";
        }
        RefreshWidget(utc);
        Say("Countdown",p.WorkingDay(now) && minute<p.End ? "距下班  "+(now.Date.AddMinutes(p.End)-now).ToString(@"hh\:mm\:ss") : "");
        if(focusEnd.HasValue) {
            remaining=SecondsLeft();
            if(remaining==0) { focusEnd=null; System.Media.SystemSounds.Asterisk.Play(); }
        }
        Say("FocusTime",TimeSpan.FromSeconds(remaining).ToString(@"mm\:ss"));
        Say("FocusLabel",remaining==0 ? "专注完成，休息一下" : "专注计时");
        Button("StartFocus").Content=focusEnd.HasValue ? "暂停" : remaining==1500 || remaining==0 ? "开始专注" : "继续";
    }
    Window Dialog(string title,FrameworkElement content,int height) {
        return new Window {Resources=window.Resources, Background=window.Background,Foreground=window.Foreground, Title=title,Owner=window,Content=content,Width=460,Height=height,
            MinWidth=400,MinHeight=360,FontFamily=window.FontFamily,FontSize=14,WindowStartupLocation=WindowStartupLocation.CenterOwner};
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
    void EditPay() {
        var panel=new StackPanel {Margin=new Thickness(22)};
        var dialog=Dialog("薪资设置",new ScrollViewer {Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},454);
        var p=data.Pay;
        var salary=Field(panel,"月薪（人民币）",p.Monthly.ToString());
        var days=Field(panel,"月计薪天数",p.Days.ToString());
        var start=Field(panel,"上班时间",Clock(p.Start));
        var lunch=Field(panel,"午休开始",Clock(p.Lunch));
        var resume=Field(panel,"午休结束",Clock(p.Resume));
        var end=Field(panel,"下班时间",Clock(p.End));
        panel.Children.Add(new TextBlock {Text="使用 24 小时制。无需午休时，将午休起止设为同一时间。",TextWrapping=TextWrapping.Wrap});
        var weekdays=new CheckBox {Content="仅周一至周五计薪",IsChecked=p.Weekdays,Margin=new Thickness(0,10,0,4)};
        panel.Children.Add(weekdays);
        var save=ActionButton("保存"); panel.Children.Add(save);
        save.Click += delegate {
            var next=new Pay {Start=Minute(start.Text),Lunch=Minute(lunch.Text),Resume=Minute(resume.Text),End=Minute(end.Text),Weekdays=weekdays.IsChecked==true};
            if(!decimal.TryParse(salary.Text,out next.Monthly) || next.Monthly<=0 || next.Monthly>1000000000 ||
                !decimal.TryParse(days.Text,out next.Days) || next.Days<1 || next.Days>31) {
                MessageBox.Show(dialog,"请填写有效月薪（最多 10 亿元）和 1 至 31 之间的计薪天数。"); return;
            }
            if(next.Start<0 || next.Start>next.Lunch || next.Lunch>next.Resume || next.Resume>next.End || next.PaidMinutes<=0) {
                MessageBox.Show(dialog,"请按同一天内的先后顺序填写上下班与午休时间，并保留有效工时。"); return;
            }
            data.Pay=next; Save(); Refresh(); dialog.Close();
        };
        dialog.ShowDialog();
    }
    [STAThread] public static void Main() {
        bool first;
        using(var guard=new Mutex(true,"Local\\DailyDesk_74ba0d12",out first)) {
            if(!first) { MessageBox.Show("日序已经打开。"); return; }
            try {
                string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RiXuDesk","desk.xml");
                var data=new DeskData();
                if(File.Exists(path)) using(var stream=File.OpenRead(path)) data=(DeskData)new XmlSerializer(typeof(DeskData)).Deserialize(stream);
                var app=new Application();
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Dashboard.xaml")) {
                    var window=(Window)XamlReader.Load(stream);
                    new DayDesk(window,data,path); app.Run(window);
                }
            } catch(Exception ex) { MessageBox.Show("日序启动或运行失败："+ex.Message,"日序"); }
        }
    }
}



