using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
namespace LaptopBar;
public static class PopupMotion
{
 public static void Attach(Window window) { window.Loaded += (_, _) => {
  if (!Preferences.Current.Animations || window.Content is not FrameworkElement surface) return;
  var host = new Grid(); window.Content = null; host.Children.Add(surface);
  var dot = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(Color.FromRgb(176,157,255)), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0,10,18,0), IsHitTestVisible = false }; host.Children.Add(dot); window.Content = host;
  var scale = new ScaleTransform(); surface.RenderTransform = scale; surface.RenderTransformOrigin = new Point(.94,.04);
  var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
  var x = new DoubleAnimationUsingKeyFrames(); x.KeyFrames.Add(new DiscreteDoubleKeyFrame(.015,KeyTime.FromTimeSpan(TimeSpan.Zero))); x.KeyFrames.Add(new EasingDoubleKeyFrame(.18,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120)),ease)); x.KeyFrames.Add(new EasingDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340)),ease));
  var y = new DoubleAnimationUsingKeyFrames(); y.KeyFrames.Add(new DiscreteDoubleKeyFrame(.015,KeyTime.FromTimeSpan(TimeSpan.Zero))); y.KeyFrames.Add(new EasingDoubleKeyFrame(.05,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120)),ease)); y.KeyFrames.Add(new EasingDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380)),ease));
  scale.BeginAnimation(ScaleTransform.ScaleXProperty,x); scale.BeginAnimation(ScaleTransform.ScaleYProperty,y);
  surface.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(240)) { BeginTime = TimeSpan.FromMilliseconds(75) });
  var fade = new DoubleAnimation(1,0,TimeSpan.FromMilliseconds(120)) { BeginTime = TimeSpan.FromMilliseconds(120) }; fade.Completed += (_,_) => dot.Visibility = Visibility.Collapsed; dot.BeginAnimation(UIElement.OpacityProperty,fade);
 }; }
}
