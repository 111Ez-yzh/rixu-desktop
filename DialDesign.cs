using System.Linq;

internal sealed class DialDesign {
    public readonly string Id, Mesh, Case, Top, Bottom, Ink, Muted, Hand, Accent;
    DialDesign(string id,string mesh,string shell,string top,string bottom,string ink,string muted,string hand,string accent) {
        Id=id;Mesh=mesh;Case=shell;Top=top;Bottom=bottom;Ink=ink;Muted=muted;Hand=hand;Accent=accent;
    }
    public static readonly DialDesign Titanium=new DialDesign("Titanium","TravelClock","#BFC3C6","#334E66","#102131","#F2EFE4","#A4B6C0","#ECE9DC","#FAAD35");
    public static readonly DialDesign Station=new DialDesign("Station","Station","#C3CCD7","#FFFCF3","#E5E3DD","#20232A","#65666B","#24262D","#DB4546");
    public static readonly DialDesign Orbit=new DialDesign("Orbit","Orbit","#373942","#353641","#111117","#F5F3FF","#B4AFD1","#ECE9FF","#AB98FF");
    public static readonly DialDesign Heritage=new DialDesign("Heritage","Heritage","#81513F","#F5E4C6","#D8BD99","#3C2C26","#71564A","#47322A","#A54D30");
    public static readonly DialDesign[] All={Titanium,Station,Orbit,Heritage};
    public static DialDesign Find(string id) {return All.FirstOrDefault(style=>style.Id==id)??Titanium;}
}
