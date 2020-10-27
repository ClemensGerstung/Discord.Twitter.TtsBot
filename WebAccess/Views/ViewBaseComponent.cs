using FastMember;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using WebAccess.ViewModels.Base;

namespace WebAccess.Views
{
  public class ViewBaseComponent<TViewModel> : LayoutComponentBase, IDisposable
    where TViewModel : IViewModel
  {
    private IList<INotifyCollectionChanged> _collections = new List<INotifyCollectionChanged>();
    private bool disposedValue;

    [Inject]
    public TViewModel ViewModel { get; set; }

    private void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
      StateHasChanged();
    }

    protected override async Task OnInitializedAsync()
    {
      await base.OnInitializedAsync();

      if (ViewModel != null)
      {
        TypeAccessor accessor = TypeAccessor.Create(typeof(TViewModel));
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        foreach (var collection in accessor.GetMembers()
                                        .Where(member => typeof(INotifyCollectionChanged).IsAssignableFrom(member.Type))
                                        .Select(member => accessor[ViewModel, member.Name] as INotifyCollectionChanged))
        {
          collection.CollectionChanged += OnViewModelCollectionChanged;
          _collections.Add(collection);
        }

        await ViewModel.InitializeAsync();
      }
    }

    private void OnViewModelCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
      StateHasChanged();
    }

    protected virtual void Dispose(bool disposing)
    {
      if (!disposedValue)
      {
        if (disposing)
        {
          if (ViewModel != null)
          {
            foreach (var collection in _collections)
            {
              collection.CollectionChanged -= OnViewModelCollectionChanged;
            }

            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            ViewModel.Dispose();
            ViewModel = default;
          }
        }

        _collections.Clear();

        // TODO: free unmanaged resources (unmanaged objects) and override finalizer
        // TODO: set large fields to null
        disposedValue = true;
      }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~ViewBaseComponent()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
      // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
      Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }
  }
}
