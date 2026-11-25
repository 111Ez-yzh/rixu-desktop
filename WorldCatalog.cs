using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
public sealed class Place {
    public string Code, Country, English, Zone, City;
    public string Key { get { return Code+"|"+Zone+"|"+City; } }
    public string Title { get { return City==Country ? City : Country+" · "+City; } }
    public bool Matches(string query) {
        return (Title+" "+English+" "+Code+" "+Zone.Replace('_',' ')+" "+Zone).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0;
    }
}
public sealed class WorldCatalog {
    sealed class Timeline { public long[] At; public int[] Offsets; }
    readonly Dictionary<string,Timeline> rules=new Dictionary<string,Timeline>();
    public readonly List<Place> Places=new List<Place>();
    public WorldCatalog() {
        var asm=Assembly.GetExecutingAssembly();
        using(var r=new StreamReader(asm.GetManifestResourceStream("Places.tsv"))) {
            string line; while((line=r.ReadLine())!=null) { var f=line.Split('\t'); Places.Add(new Place {Code=f[0],Country=f[1],English=f[2],Zone=f[3],City=f[4]}); }
        }
        using(var r=new StreamReader(new GZipStream(asm.GetManifestResourceStream("Offsets.gz"),CompressionMode.Decompress))) {
            string line; while((line=r.ReadLine())!=null) {
                var f=line.Split('\t'); var times=f[1].Split(',').Select(t=>t.Split(':')).ToArray();
                rules[f[0]]=new Timeline { At=times.Select(t=>long.Parse(t[0])).ToArray(),Offsets=times.Select(t=>int.Parse(t[1])).ToArray() };
            }
        }
    }
    public TimeSpan Offset(Place p,DateTime utc) {
        utc=utc.ToUniversalTime();
        if(p.Code=="WIN") return TimeZoneInfo.FindSystemTimeZoneById(p.Zone).GetUtcOffset(utc);
        var rule=rules[p.Zone]; long epoch=(utc.Ticks-621355968000000000L)/TimeSpan.TicksPerSecond;
        int i=Array.BinarySearch(rule.At,epoch); if(i<0) i=~i-1;
        return TimeSpan.FromSeconds(rule.Offsets[Math.Max(0,i)]);
    }
    public List<Place> Restore(string[] ids) {
        var selected=new List<Place>();
        foreach(string id in ids) {
            var place=Places.Find(p=>p.Key==id);
            if(place==null) {
                string city;
                var known=new Dictionary<string,string>{{"China Standard Time","北京"},{"Tokyo Standard Time","东京"},{"GMT Standard Time","伦敦"},{"Eastern Standard Time","纽约"},{"Pacific Standard Time","洛杉矶"},{"India Standard Time","加尔各答"},{"AUS Eastern Standard Time","悉尼"},{"Singapore Standard Time","新加坡"},{"Korea Standard Time","首尔"},{"W. Europe Standard Time","柏林"},{"Romance Standard Time","巴黎"},{"Arabian Standard Time","迪拜"}};
                if(known.TryGetValue(id,out city)) place=Places.Find(p=>p.City==city);
                if(place==null) {
                    var z=TimeZoneInfo.GetSystemTimeZones().FirstOrDefault(t=>t.Id==id);
                    if(z!=null){ place=new Place {Code="WIN",Country="系统时区",English=z.Id,Zone=z.Id,City=z.DisplayName};Places.Add(place); }
                }
            }
            if(place!=null && place.Zone!="-" && !selected.Contains(place)) selected.Add(place);
        }
        if(selected.Count==0) selected.Add(Places.First(p=>p.City=="北京"));
        return selected;
    }
}

