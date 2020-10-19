using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WebAccess
{
  public delegate Task OnExecute();

  public delegate bool OnCanExecute();

  public class AsyncCommand : ICommand
  {
    private OnExecute _onExecute;
    private OnCanExecute _onCanExecute;
    private bool _executing = false;

    public event EventHandler CanExecuteChanged;

    public AsyncCommand(OnExecute onExecute, OnCanExecute onCanExecute = null)
    {
      _onExecute = onExecute;
      _onCanExecute = onCanExecute ?? (() => true);
    }

    public bool CanExecute(object parameter)
    {
      return !_executing &&
             _onCanExecute();
    }

    public void Execute(object parameter)
    {
      _ = Task.Run(Start)
              .ContinueWith(task =>
              {
                _executing = false;
                RaiseCanExecuteChanged();
              });

      void Start()
      {
        _executing = true;
        _onExecute().GetAwaiter().GetResult();
      }
    }

    public void RaiseCanExecuteChanged()
    {
      CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
  }
}
