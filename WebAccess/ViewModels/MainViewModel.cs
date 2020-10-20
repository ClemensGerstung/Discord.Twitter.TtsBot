using MatBlazor;
using System.Collections.Generic;
using System.Linq;

namespace WebAccess.ViewModels
{

  public interface IMainViewModel
  {
    MatTheme Theme { get; }
  }

  public class MainViewModel : IMainViewModel
  {
    public MatTheme Theme { get; }

    public MainViewModel()
    {
      Theme = new MatTheme()
      {
        Primary = MatThemeColors.Blue._700.Value,
        Secondary = MatThemeColors.BlueGrey._500.Value
      };
    }
  }
}
