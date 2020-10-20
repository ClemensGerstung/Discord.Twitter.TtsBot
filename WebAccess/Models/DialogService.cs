using System;
using System.Threading.Tasks;

namespace WebAccess.Models
{
  public interface IDialogService
  {
    Task<bool?> OpenDialog();

    object State { get; }
  }

  public interface IDialogServiceExt
  {
    event EventHandler DialogOpen;
    event EventHandler DialogClose;

    void CloseDialog(bool? result);

    object State { get; set; }
  }

  class DialogServiceImplementation : IDialogService,
                                      IDialogServiceExt
  {
    private TaskCompletionSource<bool?> _tcs;

    object IDialogService.State => State;

    public object State { get; set; }

    public event EventHandler DialogOpen;
    public event EventHandler DialogClose;

    public void CloseDialog(bool? result)
    {
      if (_tcs.Task.IsCompleted) return;
      _tcs.SetResult(result);
      DialogClose?.Invoke(this, EventArgs.Empty);
    }

    public Task<bool?> OpenDialog()
    {
      _tcs = new TaskCompletionSource<bool?>();
      State = null;
      DialogOpen?.Invoke(this, EventArgs.Empty);
      return _tcs.Task;
    }
  }
}
