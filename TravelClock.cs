using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

internal sealed class TravelClock : Grid {
    const double RestPitch=7, RestYaw=-21;
    readonly PerspectiveCamera camera=new PerspectiveCamera(new Point3D(0,0,5),new Vector3D(0,0,-1),new Vector3D(0,1,0),30);
    readonly AxisAngleRotation3D pitch=Axis(1,0,0,RestPitch),yaw=Axis(0,1,0,RestYaw);
    readonly AxisAngleRotation3D hour=Axis(0,0,1,0),minute=Axis(0,0,1,0),second=Axis(0,0,1,0),day=Axis(0,0,1,0);
    readonly DiffuseMaterial dialMaterial=new DiffuseMaterial();
    readonly Ellipse shadow=new Ellipse {IsHitTestVisible=false};
    Viewport3D viewport;
    ImageSource flag,printedFlag;
    DateTime faceTime=DateTime.Now;
    int printedDay;
    double targetPitch=RestPitch,targetYaw=RestYaw,dragPitch,dragYaw;
    Point anchor;
    bool ready;
    DialDesign design=DialDesign.Titanium;
    internal DialDesign Design {
        get {return design;}
        set {
            if(value==design)return;
            design=value;Rest();
            if(viewport!=null){Children.Remove(viewport);viewport=null;}
            ready=false;printedDay=0;
            if(IsVisible && IsLoaded)ShowTime(faceTime);
        }
    }

    internal TravelClock() {
        Background=Brushes.Transparent;ClipToBounds=true;
        SetResourceReference(ToolTipProperty,"ui.clockTilt");
        SetResourceReference(System.Windows.Automation.AutomationProperties.NameProperty,"ui.analog");
        ApplyMotionPreference();
        ShadowBrush();
        Children.Add(shadow);
        SizeChanged+=delegate {FrameCamera();};
        IsVisibleChanged+=delegate {if(IsVisible)ShowTime(faceTime);else Rest();};
    }
    static AxisAngleRotation3D Axis(double x,double y,double z,double angle) {
        return new AxisAngleRotation3D(new Vector3D(x,y,z),angle);
    }
    static Color ColorOf(string hex) {return (Color)ColorConverter.ConvertFromString(hex);}
    void ShadowBrush() {
        var brush=new RadialGradientBrush();
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(110,31,32,37),0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(66,31,32,37),.40));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(15,31,32,37),.78));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0,0,0,0),1));
        brush.Freeze();shadow.Fill=brush;
    }
    void FrameCamera() {
        if(ActualWidth<=0 || ActualHeight<=0)return;
        double halfWidth=1.36*Math.Max(1,ActualWidth/ActualHeight);
        camera.Position=new Point3D(0,0,halfWidth/Math.Tan(Math.PI/12)+.1);
        double size=Math.Min(ActualWidth,ActualHeight);
        shadow.Width=size*.88;shadow.Height=size*.105;
        double floor=design==DialDesign.Titanium?.405:design==DialDesign.Orbit?.368:.38;
        shadow.RenderTransform=new TranslateTransform(size*.017,size*floor);
        if(viewport!=null && (RenderCapability.Tier>>16)>=2) {
            // Supersample fine engraving on standard-DPI displays. Bound the
            // intermediate surface when this panel fills a large monitor.
            var source=PresentationSource.FromVisual(this);
            double dpi=source==null ? 1 : source.CompositionTarget.TransformToDevice.M11;
            double scale=Math.Min(2/dpi,1250/(Math.Max(ActualWidth,ActualHeight)*dpi));
            var cache=viewport.CacheMode as BitmapCache;
            if(scale<=1.05)viewport.CacheMode=null;
            else if(cache==null)viewport.CacheMode=new BitmapCache(scale);
            else cache.RenderAtScale=scale;
        }
    }
    public ImageSource Flag {
        get {return flag;}
        set {flag=value;}
    }
    internal void ShowTime(DateTime value) {
        faceTime=value;
        if(!IsVisible)return;
        if(!ready)BuildScene();
        if(printedDay!=value.Day || printedFlag!=flag) {
            dialMaterial.Brush=TravelClockFace.Create(design,flag,value.Day);
            printedFlag=flag;printedDay=value.Day;
        }
        double seconds=value.TimeOfDay.TotalSeconds;
        hour.Angle=-(seconds%43200)/120;minute.Angle=-(seconds%3600)/10;
        second.Angle=-(seconds%60)*6;day.Angle=-seconds/240;
    }
    // Only the main analog face receives the existing clock rendering pulse.
    internal void Advance(double elapsed) {
        double step=1-Math.Exp(-11*Math.Min(.15,elapsed));
        pitch.Angle=Approach(pitch.Angle,targetPitch,step);
        yaw.Angle=Approach(yaw.Angle,targetYaw,step);
    }
    static double Approach(double current,double target,double step) {
        return Math.Abs(target-current)<.025 ? target : current+(target-current)*step;
    }
    internal void Rest() {
        if(IsMouseCaptured)ReleaseMouseCapture();
        targetPitch=pitch.Angle=RestPitch;targetYaw=yaw.Angle=RestYaw;
    }
    internal void ApplyMotionPreference() {
        bool enabled=SystemParameters.ClientAreaAnimation;
        Cursor=enabled?Cursors.Hand:Cursors.Arrow;ToolTipService.SetIsEnabled(this,enabled);
        if(!enabled)Rest();
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) {
        base.OnMouseLeftButtonDown(e);
        if(!SystemParameters.ClientAreaAnimation || !CaptureMouse())return;
        Cursor=Cursors.SizeAll;anchor=e.GetPosition(this);dragPitch=pitch.Angle;dragYaw=yaw.Angle;e.Handled=true;
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
        base.OnMouseLeftButtonUp(e);
        if(IsMouseCaptured){ReleaseMouseCapture();e.Handled=true;}
    }
    protected override void OnLostMouseCapture(MouseEventArgs e) {
        base.OnLostMouseCapture(e);Cursor=SystemParameters.ClientAreaAnimation?Cursors.Hand:Cursors.Arrow;targetPitch=RestPitch;targetYaw=RestYaw;
    }
    protected override void OnMouseLeave(MouseEventArgs e) {
        base.OnMouseLeave(e);
        if(!IsMouseCaptured){targetPitch=RestPitch;targetYaw=RestYaw;}
    }
    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);
        if(!SystemParameters.ClientAreaAnimation)return;
        TrackPointer(e.GetPosition(this));
    }
    void TrackPointer(Point point) {
        double size=Math.Max(1,Math.Min(ActualWidth,ActualHeight));
        if(IsMouseCaptured) {
            targetPitch=Math.Max(-40,Math.Min(40,dragPitch+(point.Y-anchor.Y)*105/size));
            targetYaw=Math.Max(-55,Math.Min(55,dragYaw+(point.X-anchor.X)*125/size));
        } else {
            targetPitch=RestPitch+Math.Max(-1,Math.Min(1,(point.Y-ActualHeight/2)*2/size))*8;
            targetYaw=RestYaw+Math.Max(-1,Math.Min(1,(point.X-ActualWidth/2)*2/size))*12;
        }
    }

    static Material Finish(Brush surface,string highlight,double polish) {
        var group=new MaterialGroup();group.Children.Add(new DiffuseMaterial(surface));
        if(highlight!=null)group.Children.Add(new SpecularMaterial(new SolidColorBrush(ColorOf(highlight)),polish));
        group.Freeze();return group;
    }
    static Brush Titanium() {
        var b=new LinearGradientBrush {StartPoint=new Point(.12,0),EndPoint=new Point(.85,1)};
        b.GradientStops.Add(new GradientStop(ColorOf("#CAD0D1"),0));
        b.GradientStops.Add(new GradientStop(ColorOf("#E0E1DC"),.22));
        b.GradientStops.Add(new GradientStop(ColorOf("#A5A9AB"),.52));
        b.GradientStops.Add(new GradientStop(ColorOf("#909595"),.76));
        b.GradientStops.Add(new GradientStop(ColorOf("#C3C4C0"),1));
        return b;
    }
    static Brush Walnut() {
        var grain=new DrawingGroup();
        using(var dc=grain.Open()) {
            dc.DrawRectangle(new LinearGradientBrush(ColorOf("#AB7051"),ColorOf("#654030"),80),null,new Rect(0,0,256,256));
            for(int i=-8;i<265;i+=3) {
                var line=new StreamGeometry();
                using(var path=line.Open()) {
                    path.BeginFigure(new Point(0,i),false,false);
                    path.BezierTo(new Point(90,i+14*Math.Sin(i*.023)),new Point(147,i-16*Math.Cos(i*.041)),new Point(256,i+3),true,false);
                }
                dc.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromArgb((byte)(18+i%11),47,24,14)),.9),line);
            }
        }
        return new DrawingBrush(grain) {ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,256,256),Stretch=Stretch.Fill};
    }
    static Geometry3D ReadMesh(BinaryReader reader) {
        int vertices=reader.ReadInt32(),indices=reader.ReadInt32();
        var points=new Point3DCollection(vertices);var normals=new Vector3DCollection(vertices);var uv=new PointCollection(vertices);
        for(int i=0;i<vertices;i++) {
            points.Add(new Point3D(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle()));
            normals.Add(new Vector3D(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle()));
            uv.Add(new Point(reader.ReadSingle(),reader.ReadSingle()));
        }
        var triangles=new Int32Collection(indices);
        for(int i=0;i<indices;i++)triangles.Add(reader.ReadInt32());
        var mesh=new MeshGeometry3D {Positions=points,Normals=normals,TextureCoordinates=uv,TriangleIndices=triangles};
        mesh.Freeze();return mesh;
    }
    void BuildScene() {
        var finishes=new Dictionary<string,Material> {
            {"dial",dialMaterial},{"smoke",Finish(new SolidColorBrush(ColorOf("#272C31")),"#596068",38)},
            {"sidewall",Finish(new SolidColorBrush(ColorOf("#565D63")),"#88919B",52)},
            {"titanium",Finish(design==DialDesign.Titanium?Titanium():design==DialDesign.Heritage?Walnut():new SolidColorBrush(ColorOf(design.Case)),
                design==DialDesign.Heritage?"#786257":"#A6A6AC",54)},
            {"silver",Finish(new SolidColorBrush(ColorOf(design==DialDesign.Heritage?"#B99763":"#B0B2B0")),"#ECE9E4",75)},
            {"ink",Finish(new SolidColorBrush(ColorOf("#26353E")),"#6C7A84",42)},
            {"handEdge",Finish(new SolidColorBrush(ColorOf("#8C9CA4")),"#C2CBD0",60)},
            {"lume",Finish(new SolidColorBrush(ColorOf(design.Hand)),null,1)},
            {"amber",Finish(new SolidColorBrush(ColorOf(design.Accent)),null,1)},
            {"crystal",Finish(new SolidColorBrush(ColorOf("#03B7D8E4")),"#126A8798",72)}
        };
        var body=new Model3DGroup();
        var pivots=new Dictionary<string,Model3DGroup>{{"body",body}};
        var rotations=new Dictionary<string,AxisAngleRotation3D>{{"hour",hour},{"minute",minute},{"second",second},{"day",day}};
        foreach(var pair in rotations) {
            var pose=new Transform3DGroup();pose.Children.Add(new RotateTransform3D(pair.Value));
            if(pair.Key=="day")pose.Children.Add(new TranslateTransform3D(0,-.413,0));
            var hands=new Model3DGroup {Transform=pose};body.Children.Add(hands);pivots.Add(pair.Key,hands);
        }
        using(var packed=typeof(TravelClock).Assembly.GetManifestResourceStream(design.Mesh+".mesh.gz"))
        using(var reader=new BinaryReader(new BufferedStream(new GZipStream(packed,CompressionMode.Decompress),65536))) {
            if(Encoding.ASCII.GetString(reader.ReadBytes(4))!="RXC1")throw new InvalidDataException("Travel clock mesh");
            int count=reader.ReadInt32();
            for(int i=0;i<count;i++) {
                string pivot=reader.ReadString(),finish=reader.ReadString();
                pivots[pivot].Children.Add(new GeometryModel3D(ReadMesh(reader),finishes[finish]));
            }
        }
        var tilt=new Transform3DGroup();tilt.Children.Add(new RotateTransform3D(pitch));tilt.Children.Add(new RotateTransform3D(yaw));body.Transform=tilt;
        var studio=new Model3DGroup();
        studio.Children.Add(new AmbientLight(ColorOf("#A0A2A3")));
        studio.Children.Add(new DirectionalLight(ColorOf("#D1CDC3"),new Vector3D(.7,-.95,-1.55)));
        studio.Children.Add(new DirectionalLight(ColorOf("#7F8B99"),new Vector3D(-1,.1,-.65)));
        studio.Children.Add(body);
        viewport=new Viewport3D {Camera=camera,IsHitTestVisible=false,ClipToBounds=false};
        RenderOptions.SetBitmapScalingMode(viewport,BitmapScalingMode.HighQuality);
        viewport.Children.Add(new ModelVisual3D {Content=studio});
        Children.Add(viewport);FrameCamera();ready=true;
    }
}
