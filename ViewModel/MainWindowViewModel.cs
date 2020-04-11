using System.Collections.ObjectModel;
using Prism.Mvvm;

namespace NXLM.ViewModel
{
    internal class MainWindowViewModel : BindableBase
    {
        public ObservableCollection<Category> CategoryList { get; set; }
        public MainWindowViewModel(ObservableCollection<Category> list)
        {
            CategoryList = list;
        }
    }
}