using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfApp3;

namespace WpfApp3.ViewModel
{
    class MainWindowViewModel : BindableBase
    {
        public ObservableCollection<Category> CategoryList { get; set; }
        public MainWindowViewModel(ObservableCollection<Category> List)
        {
            CategoryList = List;
        }
    }
}