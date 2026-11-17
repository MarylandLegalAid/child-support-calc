using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MdChildSupport.App;

/// <summary>
/// NumberBox presentation helpers usable from a Style (attached properties):
/// <list type="bullet">
/// <item><c>AlignRight</c> right-aligns the text so amounts line up in columns. NumberBox does not expose
/// TextAlignment in this WinUI version, so it is set on the template's "InputBox" TextBox.</item>
/// <item><c>Format</c> picks one of the shared formatters in Themes/Brand.xaml by key ("DollarFormatter",
/// "PercentFormatter", ...). A NumberFormatter setter in a Style is not applied, so this sets it directly.</item>
/// </list>
/// </summary>
public static class NumberBoxAlignment
{
    public static readonly DependencyProperty AlignRightProperty = DependencyProperty.RegisterAttached(
        "AlignRight", typeof(bool), typeof(NumberBoxAlignment), new PropertyMetadata(false, OnAlignRightChanged));

    public static bool GetAlignRight(DependencyObject obj) => (bool)obj.GetValue(AlignRightProperty);
    public static void SetAlignRight(DependencyObject obj, bool value) => obj.SetValue(AlignRightProperty, value);

    public static readonly DependencyProperty FormatProperty = DependencyProperty.RegisterAttached(
        "Format", typeof(string), typeof(NumberBoxAlignment), new PropertyMetadata(null, OnFormatChanged));

    public static string GetFormat(DependencyObject obj) => (string)obj.GetValue(FormatProperty);
    public static void SetFormat(DependencyObject obj, string value) => obj.SetValue(FormatProperty, value);

    private static void OnFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NumberBox box && e.NewValue is string key && Application.Current.Resources.TryGetValue(key, out object f)
            && f is AffixNumberFormatter formatter)
            box.NumberFormatter = formatter;
    }

    private static void OnAlignRightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NumberBox box) return;
        box.Loaded -= Apply;
        if ((bool)e.NewValue)
        {
            box.Loaded += Apply;
            if (box.IsLoaded) Apply(box, null!);
        }
    }

    private static void Apply(object sender, RoutedEventArgs e)
    {
        if (FindInputBox((DependencyObject)sender) is TextBox input) input.TextAlignment = TextAlignment.Right;
    }

    private static TextBox? FindInputBox(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is TextBox { Name: "InputBox" } tb) return tb;
            if (FindInputBox(child) is TextBox found) return found;
        }
        return null;
    }
}
