using System.Windows;
using MergeFolders.Core;

namespace MergeFolders.UI;

internal sealed class UiConflictResolver : IConflictResolver
{
    private readonly Window _owner;
    private ConflictDecision? _remembered;

    public UiConflictResolver(Window owner) => _owner = owner;

    public Task<ConflictDecision> ResolveAsync(ConflictContext context, CancellationToken cancellationToken)
    {
        if (_remembered is not null) return Task.FromResult(_remembered.Value);

        ConflictDecision decision = ConflictDecision.Cancel;
        bool applyAll = false;
        _owner.Dispatcher.Invoke(() =>
        {
            var window = new ConflictWindow(context) { Owner = _owner };
            if (window.ShowDialog() == true)
            {
                decision = window.Decision;
                applyAll = window.ApplyToAll;
            }
        });
        if (applyAll) _remembered = decision;
        return Task.FromResult(decision);
    }
}
