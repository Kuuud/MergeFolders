using System.Windows;
using MergeFolders.Core;

namespace MergeFolders.UI;

public partial class ConflictWindow : Window
{
    public ConflictDecision Decision { get; private set; } = ConflictDecision.Cancel;
    public bool ApplyToAll => ApplyAll.IsChecked == true;

    public ConflictWindow(ConflictContext context)
    {
        InitializeComponent();
        TypeText.Text = context.IsDirectory ? "发现同名文件夹" : "发现同名文件";
        SourceText.Text = context.SourcePath;
        DestinationText.Text = context.DestinationPath;
    }

    private void Finish(ConflictDecision decision)
    {
        Decision = decision;
        DialogResult = true;
    }

    private void Overwrite_Click(object sender, RoutedEventArgs e) => Finish(ConflictDecision.Overwrite);
    private void Skip_Click(object sender, RoutedEventArgs e) => Finish(ConflictDecision.Skip);
    private void Rename_Click(object sender, RoutedEventArgs e) => Finish(ConflictDecision.Rename);
    private void Cancel_Click(object sender, RoutedEventArgs e) => Finish(ConflictDecision.Cancel);
}
