using System.Windows;
using System.Windows.Controls;
using MergeFolders.Core;

namespace MergeFolders.UI;

public partial class MainWindow : Window
{
    private readonly List<string> _sources;
    private CancellationTokenSource? _cts;

    public MainWindow(IReadOnlyList<string> sources)
    {
        InitializeComponent();
        _sources = sources.Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists).ToList();
        SourceList.ItemsSource = _sources;
        if (_sources.Count == 1)
            DestinationText.Text = Path.Combine(Path.GetDirectoryName(_sources[0]) ?? _sources[0], $"{Path.GetFileName(_sources[0])}_Merged");
        else if (_sources.Count > 1)
            DestinationText.Text = Path.Combine(Path.GetDirectoryName(_sources[0]) ?? _sources[0], "Merged");
    }

    private void ChooseDestination_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        dialog.Description = "选择合并后的目标文件夹";
        if (Directory.Exists(DestinationText.Text)) dialog.SelectedPath = DestinationText.Text;
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            DestinationText.Text = dialog.SelectedPath;
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_sources.Count == 0)
        {
            MessageBox.Show("没有有效的源文件夹。", "合并文件夹", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var destination = DestinationText.Text.Trim();
        if (string.IsNullOrWhiteSpace(destination))
        {
            MessageBox.Show("请选择目标文件夹。", "合并文件夹", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        destination = Path.GetFullPath(destination);
        if (_sources.Any(x => string.Equals(Path.GetFullPath(x).TrimEnd('\\'), destination.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("目标文件夹不能直接等于源文件夹。", "合并文件夹", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var mode = CopyMode.IsChecked == true ? Core.CopyMode.Copy : Core.CopyMode.Move;
        var verification = ((ComboBoxItem)VerificationCombo.SelectedItem).Tag?.ToString() switch
        {
            "Size" => VerificationMode.Size,
            "Sha256" => VerificationMode.Sha256,
            _ => VerificationMode.None
        };

        var options = new MergeOptions
        {
            Mode = mode,
            Verification = verification,
            PreserveTimestamp = PreserveTime.IsChecked == true,
            DeleteEmptySourceDirectories = DeleteEmpty.IsChecked == true,
            FileConflict = FileAsk.IsChecked == true ? FileConflictAction.Ask :
                           FileOverwrite.IsChecked == true ? FileConflictAction.Overwrite :
                           FileSkip.IsChecked == true ? FileConflictAction.Skip : FileConflictAction.Rename,
            DirectoryConflict = DirMerge.IsChecked == true ? DirectoryConflictAction.Merge :
                               DirSkip.IsChecked == true ? DirectoryConflictAction.Skip : DirectoryConflictAction.Rename
        };

        _cts = new CancellationTokenSource();
        ToggleUi(false);
        Progress.Value = 0;
        StatusText.Text = "正在准备……";

        try
        {
            IConflictResolver resolver = options.FileConflict == FileConflictAction.Ask
                ? new UiConflictResolver(this)
                : new FixedConflictResolver(options.FileConflict, options.DirectoryConflict);

            var engine = new MergeEngine(resolver);
            var progress = new Progress<MergeProgress>(p =>
            {
                var percent = p.TotalBytes > 0 ? p.CurrentBytes * 100.0 / p.TotalBytes : 0;
                Progress.Value = Math.Min(100, percent);
                StatusText.Text = $"{p.Message}    {FormatBytes(p.CurrentBytes)} / {FormatBytes(p.TotalBytes)}";
            });

            var stats = await engine.MergeAsync(_sources, destination, options, progress, _cts.Token);
            Progress.Value = 100;
            StatusText.Text = $"完成：复制 {stats.FilesCopied} 个，跳过 {stats.FilesSkipped} 个，覆盖 {stats.FilesOverwritten} 个，重命名 {stats.FilesRenamed} 个。";

            var message = $"合并完成。\n\n文件：{stats.FilesCopied} 个\n跳过：{stats.FilesSkipped} 个\n覆盖：{stats.FilesOverwritten} 个\n失败：{stats.FilesFailed} 个\n写入：{FormatBytes(stats.BytesCopied)}";
            if (stats.Errors.Count > 0)
                message += $"\n\n有 {stats.Errors.Count} 项需要检查。";
            MessageBox.Show(message, "合并文件夹", MessageBoxButton.OK,
                stats.Errors.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "已取消";
        }
        catch (Exception ex)
        {
            StatusText.Text = "执行失败";
            MessageBox.Show(ex.ToString(), "合并失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            ToggleUi(true);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    private void ToggleUi(bool enabled)
    {
        SourceList.IsEnabled = enabled;
        DestinationText.IsEnabled = enabled;
        VerificationCombo.IsEnabled = enabled;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int i = 0;
        while (value >= 1024 && i < units.Length - 1) { value /= 1024; i++; }
        return $"{value:0.##} {units[i]}";
    }
}
