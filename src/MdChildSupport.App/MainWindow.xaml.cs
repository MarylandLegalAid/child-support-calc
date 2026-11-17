using System.Diagnostics;
using System.Runtime.InteropServices;
using MdChildSupport.Core;
using MdChildSupport.Core.Pdf;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;

namespace MdChildSupport.App;

public sealed partial class MainWindow : Window
{
    private const string AppTitle = "Maryland Child Support Calculator";
    private const string OvernightsBoxName = "Mother overnights per year";

    private readonly AppSettings _settings = AppSettings.Load();
    private string? _path;
    private bool _closeConfirmed;
    private bool _busy;
    private string? _pendingOpen;

    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        var tb = AppWindow.TitleBar;
        tb.PreferredHeightOption = TitleBarHeightOption.Tall;
        tb.ButtonBackgroundColor = Colors.Transparent;
        tb.ButtonInactiveBackgroundColor = Colors.Transparent;
        tb.ButtonForegroundColor = Colors.White;
        tb.ButtonInactiveForegroundColor = ResourceColor("BrandOnPrimarySubtleColor");
        tb.ButtonHoverBackgroundColor = ResourceColor("BrandPrimaryMidColor");
        tb.ButtonPressedBackgroundColor = ResourceColor("BrandPrimaryMidColor");
        tb.ButtonHoverForegroundColor = Colors.White;
        tb.ButtonPressedForegroundColor = Colors.White;

        string icon = Path.Combine(AppContext.BaseDirectory, "Branding", "AppIcon.ico");
        if (File.Exists(icon)) AppWindow.SetIcon(icon);
        SizeAndCenter(1280, 960);

        AppWindow.Closing += OnClosing;
        ViewModel.DirtyChanged += (_, _) => UpdateTitle();
        UpdateTitle();

        if (Content is FrameworkElement root) root.Loaded += OnRootLoaded;
    }

    private static Color ResourceColor(string key) => (Color)Application.Current.Resources[key];

    // ------------------------------------------------------------------ window

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private void SizeAndCenter(int width, int height)
    {
        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        double scale = Math.Max(1, GetDpiForWindow(hwnd)) / 96.0;
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        int w = Math.Min((int)Math.Round(width * scale), area.Width);
        int h = Math.Min((int)Math.Round(height * scale), area.Height);
        AppWindow.MoveAndResize(new RectInt32(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h));
    }

    private string DisplayName => _path is null ? "Untitled" : Path.GetFileName(_path);

    private void UpdateTitle()
    {
        string name = DisplayName + (ViewModel.IsDirty ? " *" : "");
        FileNameText.Text = name;
        Title = $"{name} – {AppTitle}";
    }

    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        HookOvernightsBox();
        HookAmountNote();
        if (_pendingOpen is string p)
        {
            _pendingOpen = null;
            _ = OpenPathAsync(p);
        }
    }

    public void OpenOnLaunch(string path)
    {
        if (Content is FrameworkElement { IsLoaded: true }) _ = OpenPathAsync(path);
        else _pendingOpen = path;
    }

    /// <summary>
    /// The overnights NumberBox limits input to 92–273 by itself. Read what was typed just before it does, so an
    /// out-of-range entry is kept and the caution is shown (DECISIONS D7, P5).
    /// </summary>
    private void HookOvernightsBox()
    {
        if (FindByAutomationName<NumberBox>((DependencyObject)Content, OvernightsBoxName) is not NumberBox box) return;
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Enter) ReportTypedOvernights(box);
        };
        box.LosingFocus += (_, _) => ReportTypedOvernights(box);
    }

    /// <summary>
    /// When the FL §12-204(m)(5) limit lowers the amount, the large amount in the results header gets a hover note with
    /// the pre-limit figure (DECISIONS D10). The TextBlock has no name in MainWindow.xaml, so it is found by its role.
    /// </summary>
    private void HookAmountNote()
    {
        if (FindAll<TextBlock>((DependencyObject)Content).FirstOrDefault(t => t.FontSize >= 40) is not TextBlock amount) return;
        void Apply()
        {
            string note = ViewModel.RecommendedAmountNote;
            ToolTipService.SetToolTip(amount, note.Length > 0 ? note : null);
            AutomationProperties.SetHelpText(amount, note);
        }
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(MainViewModel.RecommendedAmountNote)) Apply();
        };
        Apply();
    }

    private static IEnumerable<T> FindAll<T>(DependencyObject root) where T : DependencyObject
    {
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in FindAll<T>(child)) yield return d;
        }
    }

    private void ReportTypedOvernights(NumberBox box)
    {
        if (FindChild<TextBox>(box, "InputBox") is TextBox input) ViewModel.NoteTypedOvernights(input.Text);
    }

    private static T? FindByAutomationName<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        if (root is T t && AutomationProperties.GetName(t) == name) return t;
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
            if (FindByAutomationName<T>(VisualTreeHelper.GetChild(root, i), name) is T found) return found;
        return null;
    }

    private static T? FindChild<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t && t.Name == name) return t;
            if (FindChild<T>(child, name) is T found) return found;
        }
        return null;
    }

    private async void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closeConfirmed || !ViewModel.IsDirty) return;
        args.Cancel = true;
        if (_busy) return;
        if (await ConfirmDiscardOrSaveAsync())
        {
            _closeConfirmed = true;
            Close();
        }
    }

    // ------------------------------------------------------------------ dialogs

    private ContentDialog NewDialog(string title, object content) => new()
    {
        XamlRoot = Content.XamlRoot,
        Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
        Title = title,
        Content = content,
    };

    private async Task ShowErrorAsync(string title, Exception ex)
    {
        var d = NewDialog(title, new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap });
        d.CloseButtonText = "OK";
        await d.ShowAsync();
    }

    /// <summary>"Save changes?" — returns true when it is fine to continue (saved or discarded).</summary>
    private async Task<bool> ConfirmDiscardOrSaveAsync()
    {
        if (!ViewModel.IsDirty) return true;
        var d = NewDialog("Save changes?", new TextBlock
        {
            Text = $"Do you want to save your changes to {DisplayName}?",
            TextWrapping = TextWrapping.Wrap,
        });
        d.PrimaryButtonText = "Save";
        d.SecondaryButtonText = "Don't save";
        d.CloseButtonText = "Cancel";
        d.DefaultButton = ContentDialogButton.Primary;
        _busy = true;
        try
        {
            return await d.ShowAsync() switch
            {
                ContentDialogResult.Primary => await SaveAsync(saveAs: false),
                ContentDialogResult.Secondary => true,
                _ => false,
            };
        }
        finally { _busy = false; }
    }

    private IntPtr Hwnd => WinRT.Interop.WindowNative.GetWindowHandle(this);

    // ------------------------------------------------------------------ commands

    private async void New_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || !await ConfirmDiscardOrSaveAsync()) return;
        _path = null;
        ViewModel.Load(CaseData.CreateNew(DateTime.Today));
        Bindings.Update();
        UpdateTitle();
    }

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || !await ConfirmDiscardOrSaveAsync()) return;
        _busy = true;
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeFilter.Add(CaseFile.Extension);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, Hwnd);
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            await OpenPathAsync(file.Path);
        }
        catch (Exception ex) { await ShowErrorAsync("Couldn't open the file", ex); }
        finally { _busy = false; }
    }

    private async Task OpenPathAsync(string path)
    {
        try
        {
            var data = CaseFile.Load(path);
            _path = path;
            ViewModel.Load(data);
            Bindings.Update();
            UpdateTitle();
        }
        catch (Exception ex) { await ShowErrorAsync("Couldn't open the file", ex); }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        try { await SaveAsync(saveAs: false); }
        finally { _busy = false; }
    }

    private async void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        try { await SaveAsync(saveAs: true); }
        finally { _busy = false; }
    }

    private string BaseName => _path is null ? CaseFile.SuggestedName(ViewModel.CaseNumber) : Path.GetFileNameWithoutExtension(_path);

    /// <summary>Returns true when the case was saved.</summary>
    private async Task<bool> SaveAsync(bool saveAs)
    {
        try
        {
            string? path = _path;
            if (saveAs || path is null)
            {
                var picker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    SuggestedFileName = CaseFile.SuggestedName(ViewModel.CaseNumber),
                };
                picker.FileTypeChoices.Add("Child support case", new List<string> { CaseFile.Extension });
                WinRT.Interop.InitializeWithWindow.Initialize(picker, Hwnd);
                var file = await picker.PickSaveFileAsync();
                if (file is null) return false;
                path = file.Path;
            }
            CaseFile.Save(path, ViewModel.Snapshot());
            _path = path;
            ViewModel.MarkSaved();
            UpdateTitle();
            return true;
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Couldn't save the case", ex);
            return false;
        }
    }

    private async void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = BaseName + " worksheet",
            };
            picker.FileTypeChoices.Add("PDF document", new List<string> { ".pdf" });
            WinRT.Interop.InitializeWithWindow.Initialize(picker, Hwnd);
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            var data = ViewModel.Snapshot();
            var options = new PdfOptions { FooterLine = _settings.FooterLine };
            await Task.Run(() => WorksheetPdf.Write(data, file.Path, options));
            Process.Start(new ProcessStartInfo(file.Path) { UseShellExecute = true });
        }
        catch (Exception ex) { await ShowErrorAsync("Couldn't export the worksheet", ex); }
        finally { _busy = false; }
    }

    private async void Comments_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var box = new TextBox
        {
            Text = ViewModel.Comments,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Width = 480,
            Height = 200,
            Header = "Comments or special adjustments (printed on the worksheet)",
        };
        ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);
        AutomationProperties.SetName(box, "Comments");
        var d = NewDialog("Comments", box);
        d.PrimaryButtonText = "OK";
        d.CloseButtonText = "Cancel";
        d.DefaultButton = ContentDialogButton.Primary;
        _busy = true;
        try
        {
            if (await d.ShowAsync() == ContentDialogResult.Primary) ViewModel.Comments = box.Text;
        }
        finally { _busy = false; }
    }

    private async void MultiFamily_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || sender is not FrameworkElement { Tag: string tag }) return;
        Parent parent = tag == "Father" ? Parent.Parent2 : Parent.Parent1;
        string name = parent == Parent.Parent1 ? ViewModel.MotherHeader : ViewModel.FatherHeader;

        var preview = new TextBlock { TextWrapping = TextWrapping.Wrap, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        var count = new NumberBox
        {
            Header = $"Additional children living in {name}'s home",
            Minimum = 0,
            Maximum = MultiFamily.MaxAdditionalChildren,
            Value = 1,
            SmallChange = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 200,
        };
        void Update()
        {
            int k = double.IsNaN(count.Value) ? 0 : (int)count.Value;
            var a = ViewModel.PreviewMultiFamily(parent, k);
            preview.Text = k == 0
                ? "No additional children: the adjustment is $0."
                : $"Schedule amount for {k} {(k == 1 ? "child" : "children")} at {name}'s actual income of {Money.Format(a.ActualIncome)}: " +
                  $"{Money.Format(a.Schedule.Amount)}{(a.Schedule.Extrapolated ? " (extrapolated above the schedule)" : "")} × 75% = {Money.Format(a.Allowance)}";
        }
        count.ValueChanged += (_, _) => Update();
        Update();

        var panel = new StackPanel { Spacing = 12, MaxWidth = 480 };
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = "FL §12-201(c)(1)(iii) and (c)(2): a parent may deduct an allowance for each child living in that parent's home " +
                   "more than 92 overnights a year, to whom the parent owes a duty of support, who is not covered by this order. " +
                   "The allowance is the schedule's basic obligation for those children, using only this parent's actual income, " +
                   "multiplied by 75%.",
        });
        panel.Children.Add(count);
        panel.Children.Add(preview);
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["SecondaryText"],
            Text = "Apply fills the amount and adds a footnote to the worksheet. Under §12-201(c)(3) the court must decline the " +
                   "allowance if it would be unjust or inappropriate; you can always type a different amount.",
        });

        var d = NewDialog("Multifamily adjustment", panel);
        d.PrimaryButtonText = "Apply";
        d.SecondaryButtonText = "Clear";
        d.CloseButtonText = "Cancel";
        d.DefaultButton = ContentDialogButton.Primary;
        _busy = true;
        try
        {
            switch (await d.ShowAsync())
            {
                case ContentDialogResult.Primary:
                    ViewModel.ApplyMultiFamily(parent, double.IsNaN(count.Value) ? 0 : (int)count.Value);
                    break;
                case ContentDialogResult.Secondary:
                    ViewModel.ClearMultiFamily(parent);
                    break;
            }
        }
        finally { _busy = false; }
    }
}
