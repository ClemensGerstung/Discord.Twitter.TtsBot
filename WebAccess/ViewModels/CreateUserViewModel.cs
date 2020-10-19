using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WebAccess.ViewModels
{
  public class CreateUserViewModel
  {
    private TaskCompletionSource<object> _tcs;

    public bool IsOpen { get; set; } = true;

    public async Task<object> OpenDialog()
    {
      _tcs = new TaskCompletionSource<object>();
      IsOpen = true;

      return await _tcs.Task;
    }

    public void OkClick()
    {
      _tcs.SetResult(this);
      IsOpen = false;
    }
  }
}
