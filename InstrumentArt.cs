using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

// The static dial is shared by the full clock and all compact clocks.
public sealed class InstrumentArt : FrameworkElement {

    static DrawingGroup cachedFace;
    public DateTime FaceTime = new DateTime(2000,1,1,10,9,38);
    public InstrumentArt() { ClipToBounds=true; }
    static Color ColorOf(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }
    static Brush Ink(string hex) { return new SolidColorBrush(ColorOf(hex)); }
    static Brush Metal(string a,string b,string c) {
        var brush=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(1,1)};
        brush.GradientStops.Add(new GradientStop(ColorOf(a),0));
        brush.GradientStops.Add(new GradientStop(ColorOf(b),.48));
        brush.GradientStops.Add(new GradientStop(ColorOf(c),1)); return brush;
    }
    static void Line(DrawingContext d,string color,double width,double x,double y,double x2,double y2) {
        var pen=new Pen(Ink(color),width) {StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
        d.DrawLine(pen,new Point(x,y),new Point(x2,y2));
    }
    static void Type(DrawingContext d,string text,double size,double x,double y,string color,FontWeight weight) {
        var f=new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,weight,FontStretches.Normal),size,Ink(color),1.0);
        d.DrawText(f,new Point(x-f.Width/2,y-f.Height/2));
    }
    static void Disc(DrawingContext d,double radius,Brush brush) { d.DrawEllipse(brush,null,new Point(0,0),radius,radius); }
    static void Hand(DrawingContext d,double angle,double length,double tail,double width,string color) {
        d.PushTransform(new RotateTransform(angle)); Line(d,color,width,0,tail,0,-length); d.Pop();
    }
    static void DrawFace(DrawingContext d) {
        Disc(d,103,Metal("#F4F0ED","#72766F","#C8C5B8"));
        Disc(d,100,Metal("#686B60","#DCD9CA","#55594E"));
        Disc(d,96,Ink("#171D19")); Disc(d,92,Metal("#43493E","#2C342C","#1D241D"));
        for(int i=0;i<60;i++) {
            d.PushTransform(new RotateTransform(i*6)); bool major=i%5==0;
            Line(d,major?"#E4E2D6":"#919684",major?2.4:1,0,-87,0,major?-79:-84); d.Pop();
        }
        Type(d,"12",21,0,-63,"#FFF8EF",FontWeights.SemiBold);
        Type(d,"3",20,66,0,"#FFF8EF",FontWeights.SemiBold);
        Type(d,"6",20,0,63,"#FFF8EF",FontWeights.SemiBold);
        Type(d,"9",20,-65,0,"#FFF8EF",FontWeights.SemiBold);
    }
    static void Watch(DrawingContext d,DateTime time) {
        if(cachedFace==null) {
            cachedFace=new DrawingGroup();
            using(var face=cachedFace.Open()) DrawFace(face);
            cachedFace.Freeze();
        }
        d.DrawDrawing(cachedFace);
        double seconds=time.TimeOfDay.TotalSeconds;
        Hand(d,seconds/120,46,7,6,"#F8EFE3"); Hand(d,seconds/10,69,8,4,"#F6F2ED");
        Hand(d,seconds*6,77,18,1.6,"#FF8F5D"); Disc(d,4,Ink("#FF9A65")); Disc(d,1.6,Ink("#313342"));
    }
    protected override void OnRender(DrawingContext drawing) {
        double scale=Math.Min(ActualWidth,ActualHeight)/225.0;
        if(scale<=0)return;
        drawing.PushTransform(new TranslateTransform(ActualWidth/2,ActualHeight/2));
        drawing.PushTransform(new ScaleTransform(scale,scale));
        Watch(drawing,FaceTime);
        drawing.Pop();drawing.Pop();
    }
}
