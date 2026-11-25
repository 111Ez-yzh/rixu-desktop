using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

// Shared flat artwork keeps the individual compact clocks lightweight.
public sealed class InstrumentArt : FrameworkElement {

    static readonly Dictionary<DialDesign,DrawingGroup> faces=new Dictionary<DialDesign,DrawingGroup>();
    internal DialDesign Design=DialDesign.Titanium;
    static readonly Brush flagFade=new LinearGradientBrush(Color.FromArgb(110,255,255,255),Color.FromArgb(30,255,255,255),55);
    public ImageSource Flag { get; set; }
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
    static void Disc(DrawingContext d,double radius,Brush brush) { d.DrawEllipse(brush,null,new Point(0,0),radius,radius); }
    static void Hand(DrawingContext d,double angle,double length,double tail,double width,string color) {
        d.PushTransform(new RotateTransform(angle)); Line(d,color,width,0,tail,0,-length); d.Pop();
    }
    static void DrawFace(DrawingContext d,DialDesign design) {
        if(design==DialDesign.Station)Disc(d,102,Ink(design.Case));
        else if(design==DialDesign.Orbit) {
            var rim=new StreamGeometry();
            using(var path=rim.Open()) {
                for(int i=0;i<8;i++) {
                    double a=(i+.5)*Math.PI/4;var point=new Point(106*Math.Sin(a),106*Math.Cos(a));
                    if(i==0)path.BeginFigure(point,true,true);else path.LineTo(point,true,false);
                }
            }
            d.DrawGeometry(Ink(design.Case),null,rim);
        } else {
            bool wide=design==DialDesign.Heritage;
            d.DrawRoundedRectangle(Ink(design.Case),null,new Rect(-105,wide?-89:-99,210,wide?178:198),wide?28:47,wide?28:47);
        }
        double radius=design==DialDesign.Heritage?82:87;
        Disc(d,radius+4,Ink(design==DialDesign.Heritage?"#C4A275":"#525862"));Disc(d,radius,Metal(design.Top,design.Bottom,design.Bottom));
        for(int i=0;i<12;i++) {
            d.PushTransform(new RotateTransform(i*30));bool major=i%3==0;
            Line(d,major?design.Ink:design.Muted,major?5:3,0,-radius+6,0,major?-radius+22:-radius+13);d.Pop();
        }
    }
    void Watch(DrawingContext d,DateTime time) {
        DrawingGroup cachedFace;
        if(!faces.TryGetValue(Design,out cachedFace)) {
            cachedFace=new DrawingGroup();
            using(var face=cachedFace.Open()) DrawFace(face,Design);
            cachedFace.Freeze();faces.Add(Design,cachedFace);
        }
        d.DrawDrawing(cachedFace);
        if(Flag!=null) {
            var bounds=new Rect(-49,-49,98,98);
            d.PushOpacityMask(flagFade);
            d.DrawImage(Flag,bounds);
            d.Pop();
        }
        double seconds=time.TimeOfDay.TotalSeconds;
        Hand(d,seconds/120,45,5,9,Design.Hand);Hand(d,seconds/10,66,6,6,Design.Hand);
        Hand(d,seconds*6,76,15,2.8,Design.Accent);Disc(d,4,Ink(Design.Accent));
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
