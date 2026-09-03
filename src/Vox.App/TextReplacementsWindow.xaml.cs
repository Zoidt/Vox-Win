using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Vox.Core;
using Vox.Windows;

namespace Vox.App;

public partial class TextReplacementsWindow : Window
{
    private readonly VoxController _controller;
    private readonly ObservableCollection<ReplacementRow> _rows;

    public TextReplacementsWindow(VoxController controller)
    {
        _controller = controller;
        InitializeComponent();
        SourceInitialized += (_, _) => WindowAppearance.UseDarkTitleBar(this);
        _rows = new(controller.Settings.Replacements.Select(rule => new ReplacementRow { From = rule.From, To = rule.To }));
        RulesList.ItemsSource = _rows;
    }

    private void AddRule(object sender, RoutedEventArgs e) => _rows.Add(new());
    private void RemoveRule(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ReplacementRow row }) _rows.Remove(row);
    }

    private TextReplacement[] ReadRules() => _rows.Select(row => new TextReplacement(row.From.Trim(), row.To)).ToArray();

    private void PreviewRules(object sender, RoutedEventArgs e)
    {
        try
        {
            SampleOutput.Text = new TextRewriter(ReadRules()).Apply(SampleInput.Text);
            ErrorText.Text = "";
        }
        catch (Exception ex) when (ex is ArgumentException or System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            SampleOutput.Clear();
            ErrorText.Text = ex.Message;
        }
    }

    private void SaveRules(object sender, RoutedEventArgs e)
    {
        var rules = ReadRules();
        if (_controller.SaveSettings(_controller.Settings with { Replacements = rules })) DialogResult = true;
        else ErrorText.Text = _controller.Status;
    }

    public sealed class ReplacementRow
    {
        public string From { get; set; } = "";
        public string To { get; set; } = "";
    }
}
