using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace WebAccess.ViewModels.Base
{
  public interface IViewModel : INotifyPropertyChanged, IDisposable
  {
    Task InitializeAsync();
  }
}
