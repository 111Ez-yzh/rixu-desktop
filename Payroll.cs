using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

public class DayLunch {
    public int Day, Minutes;
}

public class WorkMonth {
    public int Year, Month;
    public bool CustomSchedule;
    public List<int> WorkDays = new List<int>();
    public List<DayLunch> Lunches = new List<DayLunch>();

    internal WorkMonth Copy() {
        return new WorkMonth {Year=Year, Month=Month, CustomSchedule=CustomSchedule,
            WorkDays=new List<int>(WorkDays), Lunches=Lunches.Select(b=>new DayLunch {Day=b.Day, Minutes=b.Minutes}).ToList()};
    }
}

public class Pay {
    // Resume is also read from earlier settings that stored both ends of lunch.
    public decimal Monthly, Days = 22;
    public string Currency = "CNY";
    public int Start = 540, Lunch = 720, Resume = 780, End = 1080;
    public bool Weekdays = true;
    public List<WorkMonth> Months = new List<WorkMonth>();

    internal Pay Copy() {
        var copy=(Pay)MemberwiseClone();
        copy.Months=Months.Select(m=>m.Copy()).ToList();
        return copy;
    }
    internal WorkMonth MonthAt(DateTime date, bool create=false) {
        var month=Months.Find(m=>m.Year==date.Year && m.Month==date.Month);
        if(month==null && create) {
            month=new WorkMonth {Year=date.Year, Month=date.Month};
            Months.Add(month);
        }
        return month;
    }
    internal static bool IsWeekday(DateTime date) {
        return date.DayOfWeek!=DayOfWeek.Saturday && date.DayOfWeek!=DayOfWeek.Sunday;
    }
    internal bool LunchFits(int minutes) {
        return minutes>=0 && minutes<=End-Lunch && minutes<End-Start;
    }
    public DayRate ForDay(DateTime date) {
        var month=MonthAt(date);
        bool custom=month!=null && month.CustomSchedule;
        bool work=custom ? month.WorkDays.Contains(date.Day) : !Weekdays || IsWeekday(date);
        var lunch=month==null ? null : month.Lunches.Find(b=>b.Day==date.Day);
        int minutes=lunch==null ? Resume-Lunch : lunch.Minutes;
        return new DayRate(Monthly,custom ? month.WorkDays.Count : Days,work,Start,End,Lunch,minutes);
    }
}

internal sealed class PayCurrency {
    internal static readonly List<PayCurrency> Choices=Load();
    internal string Code, ChineseName, EnglishName;
    string chineseSymbol, englishSymbol;
    int digits;

    static List<PayCurrency> Load() {
        var choices=new List<PayCurrency>();
        using(var reader=new StreamReader(typeof(PayCurrency).Assembly.GetManifestResourceStream("Currencies.tsv"))) {
            string line;
            while((line=reader.ReadLine())!=null) {
                var f=line.Split('\t');
                choices.Add(new PayCurrency {Code=f[0], ChineseName=f[1], EnglishName=f[2],
                    chineseSymbol=f[3], englishSymbol=f[4], digits=int.Parse(f[5],CultureInfo.InvariantCulture)});
            }
        }
        return choices;
    }
    internal static PayCurrency ForCode(string code) {
        return Choices.Find(c=>String.Equals(c.Code,code,StringComparison.OrdinalIgnoreCase)) ?? Choices[0];
    }
    internal string Format(decimal? value,CultureInfo culture,bool live=false) {
        string symbol=culture.TwoLetterISOLanguageName=="zh" ? chineseSymbol : englishSymbol;
        return symbol+" "+(value.HasValue ? value.Value.ToString("N"+(live?4:digits),culture) : "—");
    }
}

public class DayRate {
    public readonly decimal MonthDays, DailyPay, PerSecond;
    public readonly bool IsWorkday;
    public readonly int LunchEnd;
    readonly int start, end, lunchStart, paidSeconds;

    internal DayRate(decimal salary,decimal days,bool work,int start,int end,int lunch,int duration) {
        MonthDays=days; IsWorkday=work;
        this.start=start; this.end=end; lunchStart=lunch; LunchEnd=lunch+duration;
        paidSeconds=(end-start-duration)*60;
        DailyPay=days>0 ? salary/days : 0;
        PerSecond=paidSeconds>0 ? DailyPay/paidSeconds : 0;
    }
    public decimal Earned(DateTime now) {
        if(!IsWorkday || paidSeconds<=0)return 0;
        int seconds=(int)now.TimeOfDay.TotalSeconds;
        int worked=Math.Max(0,Math.Min(seconds,lunchStart*60)-start*60)
                 + Math.Max(0,Math.Min(seconds,end*60)-LunchEnd*60);
        return DailyPay*worked/paidSeconds;
    }
}

public partial class DayDesk {
    static TextBlock PayHint(string value) {
        return new TextBlock {Text=value,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DimGray,
            FontSize=13,Margin=new Thickness(0,6,0,6)};
    }
    static Button PayButton(string text,string name,Action action) {
        var button=new Button {Name=name,Content=text,Padding=new Thickness(10,6,10,6),Margin=new Thickness(2)};
        button.Click+=delegate {action();};
        return button;
    }
    static ScrollViewer PayScroll(FrameworkElement content) {
        return new ScrollViewer {Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    }

    void EditPay() {
        var draft=data.Pay.Copy();
        var root=new DockPanel {Margin=new Thickness(18)};
        var dialog=Dialog(T("paySettings"),root,585);
        dialog.Width=560;
        var footer=new DockPanel {Margin=new Thickness(0,8,0,0)};
        var save=ActionButton(T("save")); save.Name="PaySave"; save.IsDefault=true;
        DockPanel.SetDock(save,Dock.Right); footer.Children.Add(save);
        var error=PayHint(""); error.Name="PayError"; error.Foreground=Brushes.Firebrick;
        footer.Children.Add(error); DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
        var tabs=new TabControl {Name="PayTabs",Style=(Style)window.Resources["payTabs"],Background=Brushes.Transparent,BorderThickness=new Thickness(0),
            ItemContainerStyle=(Style)window.Resources["payTab"]};
        root.Children.Add(tabs);
        var shift=new StackPanel {Margin=new Thickness(12)};
        tabs.Items.Add(new TabItem {Header=T("shiftTab"),Content=PayScroll(shift)});
        var currencyRow=new DockPanel {Margin=new Thickness(0,5,0,5)};
        currencyRow.Children.Add(new TextBlock {Text=T("payCurrency"),Width=180,VerticalAlignment=VerticalAlignment.Center});
        var currency=new ComboBox {Name="PayCurrency",SelectedValuePath="Tag",MaxDropDownHeight=300,Padding=new Thickness(6,4,6,4)};
        AutomationProperties.SetName(currency,T("payCurrency"));
        foreach(var unit in PayCurrency.Choices) {
            var item=new ComboBoxItem {Tag=unit.Code,Content=unit.Code+"  ·  "+(EnglishUI?unit.EnglishName:unit.ChineseName)};
            TextSearch.SetText(item,unit.Code);
            currency.Items.Add(item);
        }
        currency.SelectedValue=PayCurrency.ForCode(draft.Currency).Code;
        currencyRow.Children.Add(currency); shift.Children.Add(currencyRow);
        var salary=Field(shift,T("monthly"),draft.Monthly.ToString(UiCulture)); salary.Name="Monthly";
        shift.Children.Add(PayHint(T("currencyHint")));
        var start=Field(shift,T("shiftStart"),Clock(draft.Start)); start.Name="ShiftStart";
        var end=Field(shift,T("shiftEnd"),Clock(draft.End)); end.Name="ShiftEnd";
        var lunch=Field(shift,T("breakStart"),Clock(draft.Lunch)); lunch.Name="LunchStart";
        var durationRow=new DockPanel {Margin=new Thickness(0,5,0,5)};
        durationRow.Children.Add(new TextBlock {Text=T("lunchDuration"),Width=180,VerticalAlignment=VerticalAlignment.Center});
        var duration=new ComboBox {Name="LunchDuration",IsEditable=true,IsTextSearchEnabled=false,
            ItemsSource=new[]{"0","15","30","45","60","90","120"},Text=(draft.Resume-draft.Lunch).ToString(UiCulture),
            Padding=new Thickness(6,4,6,4)};
        AutomationProperties.SetName(duration,T("lunchDuration"));
        durationRow.Children.Add(duration); shift.Children.Add(durationRow);
        shift.Children.Add(PayHint(T("lunchHelp")));
        shift.Children.Add(new TextBlock {Text=T("fallbackRules"),FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,14,0,2)});
        var days=Field(shift,T("days"),draft.Days.ToString(UiCulture)); days.Name="PaidDays";
        var weekdays=new CheckBox {Content=T("weekdays"),IsChecked=draft.Weekdays,Margin=new Thickness(0,5,0,5)};
        shift.Children.Add(weekdays);
        var calendar=new PayCalendar(this,draft,dialog);
        tabs.Items.Add(new TabItem {Header=T("monthTab"),Content=PayScroll(calendar.Panel)});
        weekdays.Click+=delegate {draft.Weekdays=weekdays.IsChecked==true; calendar.Render();};
        save.Click+=delegate {
            int minutes;
            draft.Start=Minute(start.Text); draft.End=Minute(end.Text); draft.Lunch=Minute(lunch.Text);
            if(!decimal.TryParse(salary.Text,NumberStyles.Number,UiCulture,out draft.Monthly) || draft.Monthly<0 || draft.Monthly>1000000000 ||
               !decimal.TryParse(days.Text,NumberStyles.Number,UiCulture,out draft.Days) || draft.Days<1 || draft.Days>31) {
                tabs.SelectedIndex=0; error.Text=T("payInvalid"); return;
            }
            if(draft.Start<0 || draft.Start>=draft.End || draft.Lunch<draft.Start || draft.Lunch>draft.End) {
                tabs.SelectedIndex=0; error.Text=T("shiftInvalid"); return;
            }
            if(!int.TryParse(duration.Text,out minutes) || !draft.LunchFits(minutes)) {
                tabs.SelectedIndex=0; error.Text=T("lunchInvalid"); return;
            }
            foreach(var m in draft.Months) {
                var bad=m.Lunches.Find(b=>!draft.LunchFits(b.Minutes));
                if(bad!=null) {
                    var date=new DateTime(m.Year,m.Month,bad.Day);
                    calendar.ShowMonth(date); tabs.SelectedIndex=1;
                    error.Text=F("dateBreakInvalid",date.ToString("d",UiCulture)); return;
                }
            }
            draft.Resume=draft.Lunch+minutes;
            draft.Currency=(string)currency.SelectedValue;
            draft.Months.RemoveAll(m=>!m.CustomSchedule && m.Lunches.Count==0);
            data.Pay=draft; Save(); Refresh(); dialog.Close();
        };
        dialog.ShowDialog();
    }

    sealed class PayCalendar {
        readonly DayDesk desk;
        readonly Pay draft;
        readonly Window owner;
        DateTime month;
        readonly TextBlock heading, summary;
        readonly Button previous, next;
        readonly UniformGrid grid=new UniformGrid {Columns=7};
        public readonly StackPanel Panel=new StackPanel {Margin=new Thickness(12)};

        public PayCalendar(DayDesk desk,Pay draft,Window owner) {
            this.desk=desk; this.draft=draft; this.owner=owner;
            var bar=new DockPanel();
            previous=PayButton("‹","PreviousMonth",delegate {ShowMonth(month.AddMonths(-1));});
            next=PayButton("›","NextMonth",delegate {ShowMonth(month.AddMonths(1));});
            AutomationProperties.SetName(previous,desk.T("monthBack")); previous.ToolTip=desk.T("monthBack");
            AutomationProperties.SetName(next,desk.T("monthForward")); next.ToolTip=desk.T("monthForward");
            DockPanel.SetDock(next,Dock.Right); bar.Children.Add(next); bar.Children.Add(previous);
            heading=new TextBlock {FontSize=21,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
            bar.Children.Add(heading); Panel.Children.Add(bar);
            summary=PayHint(""); summary.Name="MonthSummary"; Panel.Children.Add(summary);
            var presets=new WrapPanel();
            AddPreset(presets,"monthWeekdays",Pay.IsWeekday);
            AddPreset(presets,"monthAll",date=>true);
            AddPreset(presets,"monthNone",date=>false);
            presets.Children.Add(PayButton(desk.T("monthReset"),"monthReset",delegate {
                var m=draft.MonthAt(month);
                if(m!=null) { m.CustomSchedule=false; m.WorkDays.Clear(); }
                Render();
            }));
            Panel.Children.Add(presets);
            grid.Margin=new Thickness(0,6,0,4); Panel.Children.Add(grid);
            Panel.Children.Add(PayHint(desk.T("calendarHelp")));
            var daily=PayButton(desk.T("lunchDate"),"DateLunch",EditDateLunch);
            daily.HorizontalAlignment=HorizontalAlignment.Left; Panel.Children.Add(daily);
            ShowMonth(DateTime.Today);
        }
        public void ShowMonth(DateTime date) {
            month=new DateTime(date.Year,date.Month,1);
            Render();
        }
        IEnumerable<DateTime> Dates() {
            return Enumerable.Range(0,DateTime.DaysInMonth(month.Year,month.Month)).Select(day=>month.AddDays(day));
        }
        void AddPreset(Panel parent,string key,Func<DateTime,bool> include) {
            parent.Children.Add(PayButton(desk.T(key),key,delegate {SetDates(include); Render();}));
        }
        WorkMonth SetDates(Func<DateTime,bool> include) {
            var plan=draft.MonthAt(month,true);
            plan.WorkDays=Dates().Where(include).Select(date=>date.Day).ToList();
            plan.CustomSchedule=true;
            return plan;
        }
        void Toggle(DateTime date,Button button) {
            var plan=draft.MonthAt(month);
            if(plan==null || !plan.CustomSchedule)plan=SetDates(d=>!draft.Weekdays || Pay.IsWeekday(d));
            if(!plan.WorkDays.Remove(date.Day))plan.WorkDays.Add(date.Day);
            summary.Text=desk.F("calendarCustom",plan.WorkDays.Count);
            StyleDay(button,date,plan);
        }
        void StyleDay(Button button,DateTime day,WorkMonth plan) {
            bool work=draft.ForDay(day).IsWorkday;
            var custom=plan==null ? null : plan.Lunches.Find(b=>b.Day==day.Day);
            button.Content=day.Day.ToString()+(custom==null ? "" : " ·");
            if(work) {button.Background=desk.PayAccent; button.Foreground=Brushes.White;}
            else {button.ClearValue(Control.BackgroundProperty);button.ClearValue(Control.ForegroundProperty);}
            string tip=day.ToString("D",desk.UiCulture)+" · "+desk.T(work?"workDate":"restDate");
            if(custom!=null)tip+=" · "+desk.F("lunchCustomInfo",custom.Minutes);
            button.ToolTip=tip; AutomationProperties.SetName(button,tip);
        }
        public void Render() {
            heading.Text=month.ToString("Y",desk.UiCulture);
            previous.IsEnabled=month>DateTime.MinValue;
            next.IsEnabled=month.Year<9999 || month.Month<12;
            var plan=draft.MonthAt(month);
            summary.Text=plan!=null && plan.CustomSchedule ? desk.F("calendarCustom",plan.WorkDays.Count) : desk.T("calendarDefault");
            grid.Children.Clear();
            for(int i=0;i<7;i++) {
                var label=desk.UiCulture.DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)((i+1)%7));
                grid.Children.Add(new TextBlock {Text=label,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,4,0,4)});
            }
            int offset=((int)month.DayOfWeek+6)%7;
            for(int i=0;i<offset;i++)grid.Children.Add(new Border());
            foreach(var date in Dates()) {
                var day=date;
                var button=new Button {Name="Day"+day.Day,Padding=new Thickness(2,5,2,5),Margin=new Thickness(2)};
                button.Click+=delegate {Toggle(day,button);};
                StyleDay(button,day,plan);
                grid.Children.Add(button);
            }
        }
        void EditDateLunch() {
            var panel=new StackPanel {Margin=new Thickness(18)};
            var dialog=desk.Dialog(desk.T("lunchDateTitle"),panel,320);
            dialog.Owner=owner; dialog.MinHeight=290;
            var today=DateTime.Today;
            var initial=today.Year==month.Year && today.Month==month.Month ? today : month;
            panel.Children.Add(PayHint(desk.T("lunchForDate")));
            var date=new DatePicker {Name="LunchDate",SelectedDate=initial,IsTodayHighlighted=true,FirstDayOfWeek=DayOfWeek.Monday};
            panel.Children.Add(date);
            var value=Field(panel,desk.T("lunchMinutes"),""); value.Name="DateMinutes";
            panel.Children.Add(PayHint(desk.T("dateBreakHelp")));
            var error=PayHint(""); error.Foreground=Brushes.Firebrick; panel.Children.Add(error);
            Action load=delegate {
                value.Text="";
                if(!date.SelectedDate.HasValue)return;
                var selected=date.SelectedDate.Value;
                var plan=draft.MonthAt(selected);
                var custom=plan==null ? null : plan.Lunches.Find(b=>b.Day==selected.Day);
                if(custom!=null)value.Text=custom.Minutes.ToString();
            };
            load(); date.SelectedDateChanged+=delegate {load();};
            var save=ActionButton(desk.T("save")); save.Name="DateSave"; save.IsDefault=true; panel.Children.Add(save);
            save.Click+=delegate {
                if(!date.SelectedDate.HasValue) {date.Focus(); return;}
                bool clear=String.IsNullOrWhiteSpace(value.Text);
                int minutes=0;
                if(!clear && (!int.TryParse(value.Text,out minutes) || minutes<0 || minutes>=1440)) {
                    error.Text=desk.T("lunchInvalid"); return;
                }
                var selected=date.SelectedDate.Value;
                var plan=draft.MonthAt(selected,!clear);
                if(plan!=null) {
                    plan.Lunches.RemoveAll(b=>b.Day==selected.Day);
                    if(!clear)plan.Lunches.Add(new DayLunch {Day=selected.Day,Minutes=minutes});
                }
                ShowMonth(selected); dialog.Close();
            };
            dialog.ShowDialog();
        }
    }
    readonly Brush PayAccent=new SolidColorBrush(Color.FromRgb(64,90,135));
}
