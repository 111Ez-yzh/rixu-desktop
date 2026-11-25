using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// The enamel, printing and inset date are baked together only when their
// contents change. Hands, applied hour markers and bezel are separate meshes.
internal static class TravelClockFace {
    static Color ColorOf(string value) { return (Color)ColorConverter.ConvertFromString(value); }
    static SolidColorBrush Ink(string value) { return new SolidColorBrush(ColorOf(value)); }
    static void Label(DrawingContext dc,string value,double size,double x,double y,Brush ink,FontWeight weight,string family="Segoe UI") {
        var text=new FormattedText(value,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,
            new Typeface(new FontFamily(family),FontStyles.Normal,weight,FontStretches.Normal),size,ink,1);
        dc.DrawText(text,new Point(x-text.Width/2,y-text.Height/2));
    }
    static Point Radial(double x,double y,double angle,double radius) {
        return new Point(x+Math.Sin(angle)*radius,y-Math.Cos(angle)*radius);
    }
    internal static ImageBrush Create(DialDesign design,ImageSource flag,int date) {
        var artwork=new DrawingVisual();
        var print=Ink(design.Ink);var light=Ink(design.Muted);
        bool station=design==DialDesign.Station,orbit=design==DialDesign.Orbit,heritage=design==DialDesign.Heritage;
        bool pale=station||heritage;
        using(var dc=artwork.RenderOpen()) {
            dc.PushTransform(new ScaleTransform(1.5,1.5));
            var enamel=new RadialGradientBrush(ColorOf(design.Top),ColorOf(design.Bottom)) {
                GradientOrigin=new Point(.25,.12),Center=new Point(.5,.5),RadiusX=.63,RadiusY=.69};
            dc.DrawRectangle(enamel,null,new Rect(0,0,512,512));
            // Fine sunburst brushing under the ink, with two broad lobes facing the studio light.
            var grain=new Pen(new SolidColorBrush(Color.FromArgb(11,196,215,230)),.72);
            for(int i=0;!pale && i<640;i++) {
                double a=i*Math.PI/320;
                double sheen=Math.Pow(Math.Cos(a+.6),8);
                if(sheen<.10)continue;
                dc.PushOpacity(sheen);
                dc.DrawLine(grain,new Point(256,256),Radial(256,256,a,250));dc.Pop();
            }
            var hairline=new Pen(light,.9);
            dc.DrawEllipse(null,hairline,new Point(256,256),247,247);
            if(heritage)dc.DrawEllipse(null,hairline,new Point(256,256),226,226);
            var tick=new Pen(station?print:light,station?3.5:2);
            var major=new Pen(print,station?11:2.5);
            for(int i=0;i<60;i++) {
                double a=i*Math.PI/30;
                if(i%5==0) {
                    if(pale)dc.DrawLine(major,Radial(256,256,a,241),Radial(256,256,a,station?204:226));
                } else dc.DrawLine(tick,Radial(256,256,a,241),Radial(256,256,a,station?226:233));
            }
            string[] roman={"I","II","III","IV","V","VI","VII","VIII","IX","X","XI","XII"};
            for(int h=1;h<=12;h++) {
                var p=Radial(256,256,h*Math.PI/6,191);
                bool cardinal=h%3==0;
                if(station || (orbit&&!cardinal))continue;
                string legend=heritage?roman[h-1]:orbit?(h*5).ToString("00") : h.ToString(CultureInfo.InvariantCulture);
                Label(dc,legend,orbit?40:cardinal?38:26,p.X,p.Y,cardinal?print:light,cardinal?FontWeights.Medium:FontWeights.Normal,
                    heritage?"Georgia":orbit?"Bahnschrift":"Segoe UI");
            }
            if(flag!=null) {
                dc.PushOpacityMask(new LinearGradientBrush(Color.FromArgb(109,255,255,255),Color.FromArgb(46,255,255,255),55));
                dc.DrawImage(flag,new Rect(194,100,124,124));dc.Pop();
            }
            dc.DrawRoundedRectangle(Ink(pale?design.Top:design.Bottom),hairline,new Rect(345,240,56,32),8,8);
            Label(dc,date.ToString("00",CultureInfo.InvariantCulture),22,373,255,print,FontWeights.Medium);

            // Midnight is at the top. Gold graduation marks span 06:00–18:00.
            double cy=256+.413*256/.893;
            var centre=new Point(256,cy);
            var register=new LinearGradientBrush();
            register.StartPoint=new Point(.5,0);register.EndPoint=new Point(.5,1);
            register.GradientStops.Add(new GradientStop(ColorOf(design.Bottom),0));
            register.GradientStops.Add(new GradientStop(ColorOf(design.Top),1));
            dc.DrawEllipse(register,hairline,centre,49,49);
            var warm=new Pen(Ink(design.Accent),2.4);var cool=new Pen(light,1.8);
            for(int i=0;i<24;i++) {
                double a=i*Math.PI/12;
                dc.DrawLine(i>=6 && i<=18?warm:cool,Radial(256,cy,a,45),Radial(256,cy,a,i%6==0?37:41));
            }
            Label(dc,"24",13,256,cy-25,light,FontWeights.Medium);
            Label(dc,"12",13,256,cy+25,light,FontWeights.Medium);
            dc.Pop();
        }
        var bitmap=new RenderTargetBitmap(768,768,96,96,PixelFormats.Pbgra32);
        bitmap.Render(artwork);bitmap.Freeze();
        var brush=new ImageBrush(bitmap) {Stretch=Stretch.Fill};brush.Freeze();
        return brush;
    }
}
