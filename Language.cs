using System;
using System.Globalization;
using System.IO;
using System.Windows.Controls;
using System.Windows.Markup;

public partial class DayDesk {
    bool EnglishUI { get { return data.UiLanguage=="en-US"; } }
    CultureInfo UiCulture { get { return CultureInfo.GetCultureInfo(data.UiLanguage); } }

    string T(string key) {
        return (string)window.Resources["ui."+key];
    }
    string F(string key,params object[] values) {
        return String.Format(UiCulture,T(key),values);
    }
    string CityName(Place place) {
        if(!EnglishUI)return place.City;
        if(place.Code=="WIN")return place.Zone;
        if(place.Zone=="-")return "No separate civil time zone";
        if(place.City=="北京")return "Beijing";
        if(place.Code=="XK")return "Pristina";
        return place.Zone.Substring(place.Zone.LastIndexOf('/')+1).Replace('_',' ');
    }
    string CountryName(Place place) {
        if(!EnglishUI)return place.Country;
        return place.Code=="WIN" ? "System time zone" : place.English;
    }
    string PlaceTitle(Place place) {
        string country=CountryName(place),city=CityName(place);
        return country==city ? city : country+" · "+city;
    }
    void LoadStrings() {
        using(var reader=new StreamReader(typeof(DayDesk).Assembly.GetManifestResourceStream("Strings.tsv"))) {
            string line;
            while((line=reader.ReadLine())!=null) {
                var columns=line.Split('\t');
                window.Resources["ui."+columns[0]]=columns[EnglishUI?2:1];
            }
        }
        window.Language=XmlLanguage.GetLanguage(data.UiLanguage);
    }
    void SetupLanguage() {
        data.UiLanguage=EnglishUI?"en-US":"zh-CN";
        LoadStrings();
        var picker=Element<ComboBox>(window,"LanguagePicker");
        picker.SelectedValuePath="Tag";
        picker.SelectedValue=data.UiLanguage;
        picker.SelectionChanged+=delegate {
            var item=picker.SelectedItem as ComboBoxItem;
            string language=item==null ? null : item.Tag as string;
            if(language==null || language==data.UiLanguage)return;
            data.UiLanguage=language;
            LoadStrings();
            BuildLaunchers();
            DrawTasks();
            DrawClocks();
            if(widgetWindow!=null)DisplayWidgets();
            Say("Heading",pages[page]);
            SyncWidgetControls();Refresh();Save();
        };
    }
}
