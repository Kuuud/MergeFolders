using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace MergeFolders.Infrastructure;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = "Local\\MergeFolders-6D9A5E6A-8A42-4C29-9C6B-1D1BB7B71A83";
    private const string PipeName = "MergeFolders-6D9A5E6A-8A42-4C29-9C6B-1D1BB7B71A83";
    private Mutex? _mutex;
    private CancellationTokenSource? _cts;

    public async Task<StartResult> StartOrForwardAsync(IReadOnlyList<string> paths)
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            Forward(paths);
            return new StartResult(false, Array.Empty<string>());
        }

        _cts = new CancellationTokenSource();
        var collected = await CollectAsync(paths, _cts.Token);
        return new StartResult(true, collected);
    }

    private void Forward(IReadOnlyList<string> paths)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(800);
            using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine(JsonSerializer.Serialize(paths));
        }
        catch
        {
            // Explorer may invoke several instances nearly simultaneously. If the
            // primary is still initializing, dropping one duplicate is preferable
            // to opening several merge windows.
        }
    }

    private async Task<IReadOnlyList<string>> CollectAsync(IReadOnlyList<string> initial, CancellationToken ct)
    {
        // Collect late Explorer invocations for a short window before showing the UI.
        var all = initial.ToList();
        var deadline = DateTime.UtcNow.AddMilliseconds(900);

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                var acceptTask = server.WaitForConnectionAsync(ct);
                var completed = await Task.WhenAny(acceptTask, Task.Delay(150, ct));
                if (completed != acceptTask) continue;
                await acceptTask;

                using var reader = new StreamReader(server, Encoding.UTF8);
                var line = await reader.ReadLineAsync(ct);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var paths = JsonSerializer.Deserialize<List<string>>(line) ?? new();
                    foreach (var path in paths)
                        if (Directory.Exists(path) && !all.Contains(path, StringComparer.OrdinalIgnoreCase))
                            all.Add(path);
                }
            }
            catch (OperationCanceledException) { break; }
            catch { }
        }

        return all;
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _mutex?.ReleaseMutex(); } catch { }
        _mutex?.Dispose();
        _cts?.Dispose();
    }

    public sealed record StartResult(bool IsPrimary, IReadOnlyList<string> Paths);
}
