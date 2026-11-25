using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;

// Wall-clock dates keep a task in the local calendar day on which it was checked.
public enum TaskPriority { P0, P1, P2 }
public class Todo {
    public string Title;
    public bool Done;
    public TaskPriority Priority=TaskPriority.P2;
    public DateTime? Starts, Ends, CompletedAt;
    public bool Archived;
}

public partial class DayDesk {
    IEnumerable<Todo> OrderedTasks() {
        return data.Tasks.Where(t=>!t.Archived).OrderBy(t=>t.Done).ThenBy(t=>t.Priority)
            .ThenBy(t=>t.Starts??t.Ends??DateTime.MaxValue);
    }
    Border PriorityBadge(TaskPriority priority) {
        string[] ink={"#943E3A","#82550C","#325A77"};
        string[] paper={"#FAE4DF","#F9EBCB","#E1EDF4"};
        return new Border { Background=Paint(paper[(int)priority]),CornerRadius=new CornerRadius(9),
            Padding=new Thickness(6,2,6,2),VerticalAlignment=VerticalAlignment.Top,
            Child=new TextBlock {Text=priority.ToString(),FontSize=12,FontWeight=FontWeights.Bold,Foreground=Paint(ink[(int)priority])} };
    }
    string Schedule(Todo task,bool compact) {
        string format=compact?"MM/dd HH:mm":"yyyy-MM-dd HH:mm";
        if(task.Starts.HasValue && task.Ends.HasValue) {
            var endFormat=task.Starts.Value.Date==task.Ends.Value.Date?"HH:mm":format;
            return task.Starts.Value.ToString(format,UiCulture)+" – "+task.Ends.Value.ToString(endFormat,UiCulture);
        }
        if(task.Starts.HasValue)return F("startOnly",task.Starts.Value.ToString(format,UiCulture));
        if(task.Ends.HasValue)return F("endOnly",task.Ends.Value.ToString(format,UiCulture));
        return "";
    }
    FrameworkElement TaskLabel(Todo task,bool compact,bool strike) {
        var row=new DockPanel {LastChildFill=true};
        var badge=PriorityBadge(task.Priority);
        badge.Margin=new Thickness(0,1,8,0);row.Children.Add(badge);
        var text=new StackPanel();
        if(compact)text.MaxWidth=143;
        var title=new TextBlock {Text=task.Title,TextWrapping=TextWrapping.Wrap,FontSize=compact?13:15};
        if(strike)title.TextDecorations=TextDecorations.Strikethrough;
        text.Children.Add(title);
        string schedule=Schedule(task,compact);
        if(schedule.Length>0)text.Children.Add(new TextBlock {Text=schedule,FontSize=12,Foreground=Paint("#636B78"),TextWrapping=TextWrapping.Wrap});
        row.Children.Add(text);return row;
    }
    CheckBox TaskCheck(Todo task,bool compact,bool changed) {
        var check=new CheckBox {Content=TaskLabel(task,compact,task.Done),IsChecked=task.Done,
            VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,4,6,4)};
        AutomationProperties.SetName(check,task.Priority+" "+task.Title);
        if(changed) {
            RoutedEventHandler reveal=null;
            reveal=delegate {check.Loaded-=reveal;ControlMotion.Reveal(check);};
            check.Loaded+=reveal;
        }
        check.Click+=delegate {
            bool done=check.IsChecked==true;
            if(task.Done==done)return;
            task.Done=done;
            task.CompletedAt=done?(DateTime?)DateTime.SpecifyKind(DateTime.Now,DateTimeKind.Unspecified):null;
            DrawTasks(task);Save();
        };
        return check;
    }
    sealed class TaskTimeField : StackPanel {
        public readonly DatePicker Date;
        readonly ComboBox hour,minute;

        public DateTime? Value {
            get {
                if(!Date.SelectedDate.HasValue)return null;
                return DateTime.SpecifyKind(Date.SelectedDate.Value.Date,DateTimeKind.Unspecified)
                    .AddHours(hour.SelectedIndex).AddMinutes(minute.SelectedIndex);
            }
        }
        static ComboBox AddNumberPicker(StackPanel clock,int count,string label,string unit) {
            var picker=new ComboBox {Padding=new Thickness(8,4,0,4),ToolTip=label,
                ItemsSource=Enumerable.Range(0,count).Select(n=>n.ToString("D2",CultureInfo.InvariantCulture)).ToArray()};
            AutomationProperties.SetName(picker,label);
            var cell=new Grid {Width=59};
            var hint=new TextBlock {Text=unit,FontSize=12,Foreground=Paint("#637086"),Margin=new Thickness(8,0,20,0),
                IsHitTestVisible=false,VerticalAlignment=VerticalAlignment.Center};
            picker.SelectionChanged+=delegate {hint.Visibility=picker.SelectedIndex<0?Visibility.Visible:Visibility.Collapsed;};
            cell.Children.Add(picker);cell.Children.Add(hint);clock.Children.Add(cell);
            return picker;
        }
        public TaskTimeField(DayDesk desk,string label,DateTime? saved,Func<DateTime> suggestion) {
            Margin=new Thickness(0,6,0,6);
            Children.Add(new TextBlock {Text=label,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,5)});
            var row=new DockPanel {Height=34};
            Children.Add(row);
            string clearLabel=desk.F("clearTaskTime",label);
            var clear=new Button {Content="×",ToolTip=clearLabel,Width=30,Padding=new Thickness(0),Margin=new Thickness(7,0,0,0)};
            AutomationProperties.SetName(clear,clearLabel);
            DockPanel.SetDock(clear,Dock.Right);row.Children.Add(clear);
            var clock=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(9,0,0,0)};
            hour=AddNumberPicker(clock,24,label+" · "+desk.T("hour"),desk.T("hourShort"));
            clock.Children.Add(new TextBlock {Text=":",Margin=new Thickness(4,0,4,0),VerticalAlignment=VerticalAlignment.Center});
            minute=AddNumberPicker(clock,60,label+" · "+desk.T("minute"),desk.T("minuteShort"));
            DockPanel.SetDock(clock,Dock.Right);row.Children.Add(clock);
            Date=new DatePicker {SelectedDate=saved.HasValue?(DateTime?)saved.Value.Date:null,
                FirstDayOfWeek=desk.UiCulture.DateTimeFormat.FirstDayOfWeek,IsTodayHighlighted=true,Focusable=false};
            AutomationProperties.SetName(Date,label+" · "+desk.T("pickDate"));
            row.Children.Add(Date);
            Action<DateTime?> update=value=>{
                clock.IsEnabled=clear.IsEnabled=value.HasValue;
                hour.SelectedIndex=value.HasValue?value.Value.Hour:-1;
                minute.SelectedIndex=value.HasValue?value.Value.Minute:-1;
            };
            update(saved);
            Date.CalendarOpened+=delegate {if(!Date.SelectedDate.HasValue)Date.DisplayDate=suggestion().Date;};
            Date.SelectedDateChanged+=delegate {
                if(!Date.SelectedDate.HasValue)update(null);
                else if(hour.SelectedIndex<0)update(suggestion());
            };
            clear.Click+=delegate {Date.SelectedDate=null;};
        }
    }
    void EditTask(Todo task) {
        var input=Element<TextBox>(window,"TaskEntry");
        var editor=new StackPanel {Margin=new Thickness(22)};
        var dialog=Dialog(T("taskDetails"),new ScrollViewer{Content=editor,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},402);
        dialog.Width=515;
        var nameRow=new StackPanel {Margin=new Thickness(0,0,0,5)};
        nameRow.Children.Add(new TextBlock {Text=T("taskTitle"),FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,5)});
        var name=new TextBox {Text=task==null?input.Text:task.Title,MaxLength=input.MaxLength,Height=34};
        AutomationProperties.SetName(name,T("taskTitle"));
        nameRow.Children.Add(name);editor.Children.Add(nameRow);
        var start=new TaskTimeField(this,T("taskStarts"),task==null?null:task.Starts,()=>DateTime.Now);
        var end=new TaskTimeField(this,T("taskEnds"),task==null?null:task.Ends,delegate {
            var from=start.Value??DateTime.Now;
            return from>DateTime.MaxValue.AddHours(-1)?from:from.AddHours(1);
        });
        editor.Children.Add(start);editor.Children.Add(end);
        var priorityRow=new DockPanel {Margin=new Thickness(0,7,0,0)};
        priorityRow.Children.Add(new TextBlock {Text=T("taskPriority"),Margin=new Thickness(0,0,12,0),
            FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
        var priority=new ComboBox {Width=87,HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(7,4,7,4)};
        foreach(TaskPriority level in Enum.GetValues(typeof(TaskPriority))) {
            var item=new ComboBoxItem {Content=PriorityBadge(level),Tag=level};
            AutomationProperties.SetName(item,level.ToString());priority.Items.Add(item);
        }
        priority.SelectedIndex=(int)(task==null?TaskPriority.P2:task.Priority);
        priorityRow.Children.Add(priority);editor.Children.Add(priorityRow);
        var error=new TextBlock {Foreground=Paint("#943E3A"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0)};
        editor.Children.Add(error);
        var save=ActionButton(T("save"));save.IsDefault=true;editor.Children.Add(save);
        save.Click+=delegate {
            string title=name.Text.Trim();var begins=start.Value;var ends=end.Value;
            if(title.Length==0){error.Text=T("badTitle");name.Focus();return;}
            if(begins.HasValue && ends.HasValue && ends<=begins){error.Text=T("badOrder");return;}
            var target=task??new Todo();
            target.Title=title;target.Starts=begins;target.Ends=ends;target.Priority=(TaskPriority)priority.SelectedIndex;
            if(task==null){data.Tasks.Add(target);input.Clear();}
            DrawTasks();Save();dialog.Close();
        };
        dialog.Loaded+=delegate{name.Focus();};dialog.ShowDialog();
    }

    enum HistoryPeriod { Day,Week,Month,Undated }
    static DateTime PeriodStart(DateTime day,HistoryPeriod mode) {
        if(mode==HistoryPeriod.Month)return new DateTime(day.Year,day.Month,1);
        if(mode==HistoryPeriod.Week)return day.Date.AddDays(-(((int)day.DayOfWeek+6)%7));
        return day.Date;
    }
    static DateTime PeriodEnd(DateTime start,HistoryPeriod mode) {
        if(mode==HistoryPeriod.Month)return new DateTime(start.Year,start.Month,DateTime.DaysInMonth(start.Year,start.Month));
        if(mode==HistoryPeriod.Week)return start.AddDays(Math.Min(6,(DateTime.MaxValue.Date-start).Days));
        return start;
    }
    void ShowTaskHistory() {
        var completed=data.Tasks.Where(t=>t.Done).OrderByDescending(t=>t.CompletedAt).ToList();
        var today=DateTime.Today;
        var days=completed.Where(t=>t.CompletedAt.HasValue).Select(t=>t.CompletedAt.Value.Date).Concat(new[]{today});
        var first=days.Min();var last=days.Max();
        var root=new DockPanel {Margin=new Thickness(20)};
        var dialog=Dialog(T("taskHistory"),root,530);dialog.Width=620;
        var tabs=new WrapPanel();DockPanel.SetDock(tabs,Dock.Top);root.Children.Add(tabs);
        var dates=new WrapPanel {Margin=new Thickness(0,10,0,10)};DockPanel.SetDock(dates,Dock.Top);root.Children.Add(dates);
        var previous=HistoryButton("‹",T("periodPrev"));var next=HistoryButton("›",T("periodNext"));
        var picker=new DatePicker {SelectedDate=today,DisplayDateStart=first,DisplayDateEnd=last,IsTodayHighlighted=true,
            VerticalAlignment=VerticalAlignment.Center,Width=145,Margin=new Thickness(5,0,5,0)};
        AutomationProperties.SetName(picker,T("historyDate"));
        var goToday=HistoryButton(T("historyToday"),T("historyToday"));
        dates.Children.Add(previous);dates.Children.Add(picker);dates.Children.Add(next);dates.Children.Add(goToday);
        var caption=new TextBlock {FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,10)};
        DockPanel.SetDock(caption,Dock.Top);root.Children.Add(caption);
        var rows=new StackPanel();
        root.Children.Add(new ScrollViewer {Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        var mode=HistoryPeriod.Day;
        var buttons=new Dictionary<HistoryPeriod,Button>();
        Action render=delegate {
            var day=(picker.SelectedDate??today).Date;
            var start=PeriodStart(day,mode);var end=PeriodEnd(start,mode);
            bool undated=mode==HistoryPeriod.Undated;
            dates.Visibility=undated?Visibility.Collapsed:Visibility.Visible;
            previous.IsEnabled=start>first;next.IsEnabled=end<last;
            foreach(var pair in buttons){
                pair.Value.Background=pair.Key==mode?Paint("#415983"):Paint("#E9EBF0");
                pair.Value.Foreground=pair.Key==mode?Brushes.White:Paint("#415983");
            }
            var matches=completed.Where(t=>undated?!t.CompletedAt.HasValue:
                t.CompletedAt.HasValue && t.CompletedAt.Value.Date>=start && t.CompletedAt.Value.Date<=end).ToList();
            string range=undated?T("legacyCompletion"):start.ToString(mode==HistoryPeriod.Month?"yyyy-MM":"yyyy-MM-dd",UiCulture);
            if(mode==HistoryPeriod.Week)range+=" – "+end.ToString("yyyy-MM-dd",UiCulture);
            caption.Text=range+"    ·    "+F("historyCount",matches.Count);
            rows.Children.Clear();
            foreach(var group in matches.GroupBy(t=>t.CompletedAt.HasValue?(DateTime?)t.CompletedAt.Value.Date:null)){
                if(group.Key.HasValue && mode!=HistoryPeriod.Day)rows.Children.Add(new TextBlock {
                    Text=group.Key.Value.ToString("MMM d · dddd",UiCulture),FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,12,0,4)});
                foreach(var t in group) {
                    var row=new DockPanel {Margin=new Thickness(0,6,0,6)};
                    if(t.CompletedAt.HasValue)row.Children.Add(new TextBlock {Text=t.CompletedAt.Value.ToString("HH:mm",UiCulture),
                        Foreground=Paint("#636B78"),Width=55});
                    row.Children.Add(TaskLabel(t,false,false));rows.Children.Add(row);
                }
            }
            if(matches.Count==0)rows.Children.Add(new TextBlock {Text=T("emptyHistory"),Foreground=Paint("#636B78"),Margin=new Thickness(0,12,0,12)});
        };
        string[] keys={"periodDay","periodWeek","periodMonth","unknownDay"};
        foreach(HistoryPeriod p in Enum.GetValues(typeof(HistoryPeriod))) {
            if(p==HistoryPeriod.Undated && completed.All(t=>t.CompletedAt.HasValue))continue;
            var period=p;var button=HistoryButton(T(keys[(int)p]),T(keys[(int)p]));
            buttons.Add(p,button);tabs.Children.Add(button);button.Click+=delegate {mode=period;render();};
        }
        previous.Click+=delegate {picker.SelectedDate=PeriodStart(picker.SelectedDate??today,mode).AddDays(-1);};
        next.Click+=delegate {picker.SelectedDate=PeriodEnd(PeriodStart(picker.SelectedDate??today,mode),mode).AddDays(1);};
        goToday.Click+=delegate {picker.SelectedDate=today;};
        picker.SelectedDateChanged+=delegate {render();};
        render();dialog.ShowDialog();
    }
    Button HistoryButton(string text,string hint) {
        return new Button {Content=text,ToolTip=hint,Margin=new Thickness(0,0,4,0),Padding=new Thickness(10,6,10,6)};
    }
}
