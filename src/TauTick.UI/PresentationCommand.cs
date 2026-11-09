using System.Windows.Input;

namespace TauTick.UI;

/// <summary>A synchronous presentation action. Owners retain async operation/error lifetime.</summary>
public sealed class PresentationCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}
