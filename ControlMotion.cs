using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

// Read the native control states so capture, keyboard activation and disabled
// controls keep their usual WPF behavior across windows and popup menus.
internal sealed class ControlMotion : IMultiValueConverter {
    enum Phase { Rest, Hover, Press }
    static readonly DependencyProperty PhaseProperty=DependencyProperty.RegisterAttached(
        "Phase",typeof(Phase),typeof(ControlMotion),new PropertyMetadata(Phase.Rest,OnPhase));
    static readonly DependencyProperty DownProperty=DependencyProperty.RegisterAttached(
        "Down",typeof(bool),typeof(ControlMotion),new PropertyMetadata(false));
    static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached(
        "Enabled",typeof(bool),typeof(ControlMotion),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.Inherits,OnScope));
    static readonly ControlMotion converter=new ControlMotion();
    static readonly CubicEase ease=new CubicEase {EasingMode=EasingMode.EaseOut};
    static bool installed;

    internal static void Enable(FrameworkElement root) {
        if(!installed) {
            installed=true; ease.Freeze();
            EventManager.RegisterClassHandler(typeof(CheckBox),ToggleButton.CheckedEvent,new RoutedEventHandler(OnCheck));
            EventManager.RegisterClassHandler(typeof(CheckBox),ToggleButton.UncheckedEvent,new RoutedEventHandler(OnCheck));
            EventManager.RegisterClassHandler(typeof(Button),ButtonBase.ClickEvent,new RoutedEventHandler(OnButtonClick));
            EventManager.RegisterClassHandler(typeof(Button),Mouse.MouseMoveEvent,new MouseEventHandler(MoveGlint));
        }
        root.SetValue(EnabledProperty,true);
    }
    static Binding Read(DependencyProperty property) {
        return new Binding {RelativeSource=RelativeSource.Self,Path=new PropertyPath(property)};
    }
    static void OnScope(DependencyObject sender,DependencyPropertyChangedEventArgs e) {
        if(!(bool)e.NewValue)return;
        var control=sender as Control;
        if(!(control is ButtonBase || control is TabItem || control is ComboBox || control is ComboBoxItem || control is MenuItem))return;
        if(BindingOperations.IsDataBound(control,PhaseProperty))return;
        if(control is ToggleButton && control.TemplatedParent is ComboBox)return;
        var state=new MultiBinding {Converter=converter,Mode=BindingMode.OneWay};
        state.Bindings.Add(Read(UIElement.IsEnabledProperty));
        state.Bindings.Add(Read(control is MenuItem ? MenuItem.IsHighlightedProperty :
            control is ComboBoxItem ? ComboBoxItem.IsHighlightedProperty : UIElement.IsMouseOverProperty));
        var down=control is ButtonBase ? ButtonBase.IsPressedProperty : control is MenuItem ? MenuItem.IsPressedProperty : DownProperty;
        if(down==DownProperty) {
            control.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,new MouseButtonEventHandler(PointerDown),true);
            control.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent,new MouseButtonEventHandler(PointerUp),true);
            control.MouseLeave+=PointerReset;
            control.LostMouseCapture+=PointerReset;
            control.Unloaded+=delegate {control.ClearValue(DownProperty);};
        }
        state.Bindings.Add(Read(down));
        BindingOperations.SetBinding(control,PhaseProperty,state);
    }
    static void PointerDown(object sender,MouseButtonEventArgs e) { ((Control)sender).SetValue(DownProperty,true); }
    static void PointerUp(object sender,MouseButtonEventArgs e) { ((Control)sender).ClearValue(DownProperty); }
    static void PointerReset(object sender,MouseEventArgs e) { ((Control)sender).ClearValue(DownProperty); }
    public object Convert(object[] values,Type type,object parameter,CultureInfo culture) {
        if(!Object.Equals(values[0],true))return Phase.Rest;
        if(Object.Equals(values[2],true))return Phase.Press;
        return Object.Equals(values[1],true) ? Phase.Hover : Phase.Rest;
    }
    public object[] ConvertBack(object value,Type[] types,object parameter,CultureInfo culture) {
        throw new NotSupportedException();
    }
    static void OnPhase(DependencyObject sender,DependencyPropertyChangedEventArgs e) {
        Apply((Control)sender,(Phase)e.NewValue,(Phase)e.OldValue==Phase.Press);
    }
    static void OnButtonClick(object sender,RoutedEventArgs e) {
        var control=(Button)sender;
        var phase=(Phase)control.GetValue(PhaseProperty);
        if(phase!=Phase.Press)Apply(control,phase,true);
    }
    static void MoveGlint(object sender,MouseEventArgs e) {
        var button=(Button)sender;
        if(!SystemParameters.ClientAreaAnimation || !button.IsEnabled || button.Template==null)return;
        var lens=button.Template.FindName("MotionTint",button) as Border;
        var glint=lens==null?null:lens.Background as RadialGradientBrush;
        if(glint==null || lens.ActualWidth<=0 || lens.ActualHeight<=0)return;
        if(glint.IsFrozen || lens.ReadLocalValue(Border.BackgroundProperty)==DependencyProperty.UnsetValue) {
            glint=glint.CloneCurrentValue();lens.Background=glint;
        }
        var p=e.GetPosition(lens);
        var centre=new Point(Math.Max(0,Math.Min(1,p.X/lens.ActualWidth)),Math.Max(0,Math.Min(.48,p.Y/lens.ActualHeight*.48)));
        glint.Center=centre;glint.GradientOrigin=centre;
    }
    static void Apply(Control control,Phase phase,bool releasing) {
        if(!control.IsLoaded || control.Template==null)return;
        var surface=control.Template.FindName("MotionSurface",control) as FrameworkElement;
        if(surface==null && VisualTreeHelper.GetChildrenCount(control)>0)
            surface=VisualTreeHelper.GetChild(control,0) as FrameworkElement;
        if(surface==null)return;
        bool animate=control.IsEnabled && SystemParameters.ClientAreaAnimation;
        int ms=phase==Phase.Press ? 75 : releasing ? 165 : 125;
        var scale=surface.RenderTransform as ScaleTransform;
        if(scale==null || scale.IsFrozen) {
            if(surface.RenderTransform!=null && !surface.RenderTransform.Value.IsIdentity)return;
            scale=new ScaleTransform();surface.RenderTransformOrigin=new Point(.5,.5);surface.RenderTransform=scale;
        }
        double width=Math.Max(1,surface.ActualWidth);
        double zoom=phase==Phase.Press ? 1-Math.Min(.09,5/width) : phase==Phase.Hover ? 1+Math.Min(.045,2/width) : 1;
        // A tap or Enter can finish between two rendered frames.
        double startLimit=releasing ? 1-Math.Min(.035,2.5/width) : double.PositiveInfinity;
        SetScale(scale,ScaleTransform.ScaleXProperty,animate?zoom:1,startLimit,ms,animate);
        SetScale(scale,ScaleTransform.ScaleYProperty,animate?zoom:1,startLimit,ms,animate);
        var tint=control.Template.FindName("MotionTint",control) as UIElement;
        if(tint==null)return;
        double from=tint.Opacity;
        double opacity=phase==Phase.Press ? .31 : phase==Phase.Hover ? .16 : 0;
        tint.Opacity=opacity;
        tint.BeginAnimation(UIElement.OpacityProperty,animate ? Tween(from,opacity,ms) : null);
    }
    static void SetScale(ScaleTransform scale,DependencyProperty property,double target,double startLimit,int ms,bool animate) {
        double from=Math.Min((double)scale.GetValue(property),startLimit);
        scale.SetValue(property,target);
        scale.BeginAnimation(property,animate ? Tween(from,target,ms) : null);
    }
    static DoubleAnimation Tween(double from,double to,int ms) {
        return new DoubleAnimation(from,to,TimeSpan.FromMilliseconds(ms)) {EasingFunction=ease,FillBehavior=FillBehavior.Stop};
    }
    static void OnCheck(object sender,RoutedEventArgs e) { Reveal((CheckBox)sender); }
    internal static void Reveal(CheckBox check) {
        if(!SystemParameters.ClientAreaAnimation || !check.IsLoaded || check.Template==null)return;
        var ink=check.Template.FindName("SelectionInk",check) as UIElement;
        if(ink==null)return;
        double to=check.IsChecked==false ? 0 : 1;
        ink.BeginAnimation(UIElement.OpacityProperty,Tween(1-to,to,150));
    }
}
