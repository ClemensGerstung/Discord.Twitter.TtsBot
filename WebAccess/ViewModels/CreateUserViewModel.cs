using System.Threading.Tasks;
using WebAccess.Models;
using WebAccess.ViewModels.Base;

namespace WebAccess.ViewModels
{
  public class CreateUserViewModel : ViewModelBase
  {
    private IDialogServiceExt _dialogService;
    private bool _isOpen;

    public bool IsOpen
    {
      get => _isOpen; 
      set
      {
        if(!value)
          _dialogService.CloseDialog(null);

        if (_isOpen != value)
        {
          _isOpen = value;
          RaisePropertyChanged();
        }
      }
    }

    public string Handle { get; set; }
    public string Language { get; set; }
    public string VoiceName { get; set; }

    public CreateUserViewModel(IDialogServiceExt dialogService)
    {
      _dialogService = dialogService;
      _dialogService.DialogOpen += OnDialogOpen;
    }

    private void OnDialogOpen(object sender, System.EventArgs e)
    {
      IsOpen = true;
    }

    public void OkClick()
    {
      _dialogService.State = new NewUserData
      {
        Handle = Handle,
        Langauge = Language,
        VoiceName = VoiceName
      };
      _dialogService.CloseDialog(true);
      IsOpen = false;
    }

    public void CancelClick()
    {
      _dialogService.CloseDialog(false);
      IsOpen = false;
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing)
      {
        _dialogService.DialogOpen -= OnDialogOpen;
      }

      base.Dispose(disposing);
    }
  }
}
