using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NXLM.ViewModel;
using NXOpen;

namespace NXLM
{
    public partial class MainWindow
    {
        private readonly ThreadCategoryCreate _mThreadCategoryCreator;
        private readonly Session _mSession;
        private readonly Part _mWorkPart;
        private NXOpen.Layer.Category[] _mNxCategories;
        private const int WorkLayer = Singleton.WorkLayer; //Неиспользуемый рабочий слой
        private const int MaxLayersCount = Singleton.MaxLayersCount; //Общее число слоев
        private const string NxMainCategory = Singleton.NxMainCategory; //Базовая категория NX, которой принадлежат все слои
        private const int MaxLayersQuantityInSubMenu = 10; //Максимальное количество добавляемых слоев из субменю основного списка категорий

        //Список отображаемых категорий с количеством слоев в основном окне приложения
        public ObservableCollection<Category> DisplayCategoryList { get; set; }
            = new ObservableCollection<Category>();

        //=====================================================================
        //Конструктор класса - родителя
        //=====================================================================
        public MainWindow()
        {
            try
            {
                Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
                InitializeComponent();
                _mSession = Session.GetSession();
                Singleton.GetInstance();
                _mWorkPart = _mSession.Parts.Work;
                _mThreadCategoryCreator = new ThreadCategoryCreate(
                    ProgressBarIncrease, ProgressBarReset,
                    ExceptionThread, ButtonAccess,
                    ItemUpdate, ItemAddUpdate);
                this.DataContext = new MainWindowViewModel(DisplayCategoryList);
                UpdateItemList();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Обновить информацию по количеству слоев в категории в списке приложения
        //mDisplayCategoryList
        //=====================================================================
        private void UpdateSingleCategoryInList(string name)
        {
            try
            {
                var category = _mWorkPart.LayerCategories.FindObject(name);
                foreach (var x in DisplayCategoryList)
                {
                    if (x.Name == name) { x.LayCount = category.GetMemberLayers().Length; }
                }
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Добавить категории в список категорий приложения
        //=====================================================================
        private void AddGroupCategoriesToList(List<Category> @group)
        {
            try
            {
                @group.ForEach(x =>
                {
                    DisplayCategoryList.Add(x);
                });
                UpdateItemList();
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Синхронизация списка категорий в приложении со списком категорий в NX
        //=====================================================================
        private void UpdateItemList()
        {
            try
            {
                DisplayCategoryList.Clear();
                _mNxCategories = _mWorkPart.LayerCategories.ToArray();
                var tempArray = _mNxCategories.OrderBy(g => g.Name).SkipWhile(x => x.Name == NxMainCategory).ToList();
                tempArray.ForEach(g =>
                {
                    if (g.Name != NxMainCategory) DisplayCategoryList.Add(new Category(g.Name, g.GetMemberLayers().Count()));
                });
            }
            catch (ThreadAbortException) { }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        //=====================================================================
        //Точка входа в программу
        //=====================================================================
        [STAThread]
        public static int Main()
        {
            try
            {
                var myForm = new MainWindow
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                Application app = new App
                {
                    ShutdownMode = ShutdownMode.OnLastWindowClose
                };
                app.Run(myForm);
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            return 0;
        }

        //=====================================================================
        //Метод выгрузки библиотеки
        //=====================================================================
        public static int GetUnloadOption(string arg)
        {
            return Convert.ToInt32(Session.LibraryUnloadOption.Immediately);
        }

        //=====================================================================
        //Удалить текущий список категорий
        //=====================================================================
        private void DeleteButton_CLick(object sender, RoutedEventArgs e)
        {
            try
            {
                DeleteCurrentCategories();
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Удалить текущий список категорий
        //=====================================================================
        private void DeleteCurrentCategories()
        {
            try
            {
                _mNxCategories = _mWorkPart.LayerCategories.ToArray();
                foreach (var x in _mNxCategories)
                {
                    _mSession.UpdateManager.AddToDeleteList(x);
                    UpdateNxScreen();
                    UpdateItemList();
                }
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Обновить экран NX
        //=====================================================================
        private void UpdateNxScreen()
        {
            try
            {
                var id = _mSession.NewestVisibleUndoMark;
                _mSession.UpdateManager.DoUpdate(id);
                _mSession.DeleteUndoMark(id, null);
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Добавить список ШАБЛОННЫХ категорий
        //=====================================================================
        private void CreateCategoriesTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DeleteCurrentCategories();
                var config = new CategoryConfigurator();
                var groupTemplate = config.GetGroupTemplate();
                var layerNumber = WorkLayer + 1;
                foreach (var x in groupTemplate)
                {
                    var arr = new int[x.LayCount];
                    for (var i = 0; i < arr.Count(); ++i)
                    {
                        if (layerNumber == MaxLayersCount) throw new Exception("Недостаточно количества слоев");
                        arr[i] = layerNumber;
                        ++layerNumber;
                    }
                    _mWorkPart.LayerCategories.CreateCategory(x.Name, "", arr);
                }
                UpdateNxScreen();
                UpdateItemList();
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Закрыть приложение
        //=====================================================================
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Close();
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Создать отдельную категорию в новом потоке
        //=====================================================================
        private void CreateSingleCategory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (InputLayerCount.Text.Length == 0 || Convert.ToInt32(InputLayerCount.Text) == 0)
                {
                    throw new Exception("число категорий должно быть больше 0");
                }

                var requestCategoryName = InputCategoryName.Text;
                var requestLayersCount = Convert.ToInt32(InputLayerCount.Text);
                ProgressBarCategory.Maximum = requestLayersCount;

                var categoryGroup = new List<Category>() { new Category(requestCategoryName, requestLayersCount) };
                _mThreadCategoryCreator.CreateListCategories(categoryGroup);
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Настройка контролов при загрузке формы
        //=====================================================================
        private void NXLayerManager_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                InputCategoryName.Text = Singleton.InputTextCategory;
                InputGroupName.Text = Singleton.InputTextGroup;
                InputCategoryName.Foreground = Brushes.LightGray;
                InputGroupName.Foreground = Brushes.LightGray;
                InputCategoryName.SetStringTextBoxBehavior(InputCategoryName.Text, CreateCategory);
                InputGroupName.SetStringTextBoxBehavior(InputGroupName.Text, CreateGroupCategories);
                InputLayerCount.SetIntTextBoxBehavior(InputLayerCount.Text);
                InputGroupCount.SetIntTextBoxBehavior(InputGroupCount.Text);
                InputGroupLayersCount.SetIntTextBoxBehavior(InputGroupLayersCount.Text);
                GenerateSubmenuLayersControl(MenuItemAdd, MaxLayersQuantityInSubMenu);
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Методы для кэлбэков
        //=====================================================================
        private void ProgressBarIncrease(bool val)
        {
            try
            {
                if (val) Dispatcher.Invoke(() => ++ProgressBarCategory.Value);
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void ProgressBarReset(bool val)
        {
            try
            {
                if (val) Dispatcher.Invoke(() => { ProgressBarCategory.Value = 0; });
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
        private void ExceptionThread(Exception ex)
        {
            try
            {
                Dispatcher.Invoke(() => { MessageBox.Show(ex.Message); ProgressBarCategory.Value = 0; });
            }
            catch (ThreadAbortException) { }
            catch (Exception)
            {
                // ignored
            }
        }

        private void ButtonAccess(bool val)
        {
            try
            {
                Dispatcher.Invoke(() =>
            {
                if (val)
                {
                    if (InputCategoryName.Text != Singleton.InputTextCategory) CreateCategory.IsEnabled = true;
                    if (InputGroupName.Text != Singleton.InputTextGroup) CreateGroupCategories.IsEnabled = true;
                }
                else
                {
                    CreateCategory.IsEnabled = false;
                    CreateGroupCategories.IsEnabled = false;
                }
            });
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void ItemUpdate(string name)
        {
            try
            {
                Dispatcher.Invoke(() => { UpdateSingleCategoryInList(name); });
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void ItemAddUpdate(List<Category> croup)
        {
            try
            {
                Dispatcher.Invoke(() => { AddGroupCategoriesToList(croup); });
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Создать категории в отдельном потоке
        //=====================================================================
        private void CreateGroupCategories_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var requestGroupName = InputGroupName.Text;
                var requestGroupCount = Convert.ToInt32(InputGroupCount.Text);
                var requestLayersCount = Convert.ToInt32(InputGroupLayersCount.Text);
                ProgressBarCategory.Maximum = requestLayersCount;
                var config = new CategoryConfigurator();
                var categoryGroup = config.GetCategoryGroup(requestGroupName, requestGroupCount, requestLayersCount);
                _mThreadCategoryCreator.CreateListCategories(categoryGroup);
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Удалить все категории в списке
        //=====================================================================
        public void DeleteAllCategories(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItems = ListViewCategories.SelectedItems;
                var x = selectedItems.Count;
                for (var i = 0; i < x; ++i)
                {
                    var item = ListViewCategories.SelectedItem;
                    var category = item as Category;
                    if (category != null)
                    {
                        var nxCategory= _mWorkPart.LayerCategories.FindObject(category.Name);
                        _mSession.UpdateManager.AddToDeleteList(nxCategory);
                    }

                    DisplayCategoryList.Remove(category);
                }
                UpdateNxScreen();
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Расчет и запуск субменю удаления слоев из категориий
        //=====================================================================
        private void ListViewCategories_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            try
            {
                if (ListViewCategories.SelectedItems.Count == 0) e.Handled = true;
                else
                {
                    MenuItemDel.Items.Clear();
                    var selItems = ListViewCategories.SelectedItems;
                    var selectedItemsCount = ListViewCategories.SelectedItems.Count;
                    var categoryQuantity = new List<int>();
                    for (var i = 0; i < selectedItemsCount; i++)
                    {
                        var item = selItems[i];
                        var layCount = ((Category) item).LayCount;
                        categoryQuantity.Add(layCount);
                    }
                    const int maxVal = 15;
                    var totalCount = maxVal;
                    if (categoryQuantity.Min() < maxVal) totalCount = categoryQuantity.Min();
                    GenerateSubmenuLayersControl(MenuItemDel, totalCount);
                }
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Генерация элементов субменю для добавления и удаления слоев из категории
        //=====================================================================
        private void GenerateSubmenuLayersControl(MenuItem menuItem, int subItemsQuantity)
        {
            if (menuItem == null) throw new ArgumentNullException(nameof(menuItem));
            try
            {
                var plusIcon = new Uri("Resources/plus_icon.ico", UriKind.Relative);
                var minusIcon = new Uri("Resources/minus_icon.ico", UriKind.Relative);
                var currentUri = (menuItem == MenuItemDel) ? minusIcon : plusIcon;

                var listSubMenuItems = new List<MenuItem>();
                for (var i = 0; i < subItemsQuantity; i++)
                {
                    listSubMenuItems.Add(new MenuItem());
                    listSubMenuItems[i].Header = (i + 1).ToString();
                    listSubMenuItems[i].HorizontalContentAlignment = HorizontalAlignment.Center;
                    listSubMenuItems[i].StaysOpenOnClick = false;
                    listSubMenuItems[i].Icon = new Image
                    {
                        Source = new BitmapImage(currentUri)
                    };
                    listSubMenuItems[i].Click += (se, sa) =>
                    {
                        var subItem = se as MenuItem;
                        if (menuItem == MenuItemDel)
                            if (subItem != null)
                                DeleteLayersFromCategory(Convert.ToInt32(subItem.Header));
                        if (menuItem == MenuItemAdd) AddLayersToCategory(Convert.ToInt32(subItem?.Header));
                    };
                    listSubMenuItems[i].IsCheckable = false;
                    menuItem.Items.Add(listSubMenuItems[i]);
                }
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Удаляет заданное число слоев из категории
        //=====================================================================
        private void DeleteLayersFromCategory(int quantityLayersToDelete)
        {
            try
            {
                var names = GetCategoriesNamesFromListView();
                names.ForEach(x =>
                {
                    var nxCategory = _mWorkPart.LayerCategories.FindObject(x);
                    var layers = nxCategory.GetMemberLayers();
                    var count = layers.Length - quantityLayersToDelete;
                    if (count < 0) throw new Exception("количество слоев меньше 0");
                    var newLayers = new int[layers.Length - quantityLayersToDelete];
                    for (var i = 0; i < count; ++i)
                    {
                        newLayers[i] = layers[i];
                    }
                    nxCategory.SetMemberLayers(newLayers);
                });
                UpdateItemList();
                UpdateNxScreen();
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Возврат имен категорий со списка
        //=====================================================================
        private List<string> GetCategoriesNamesFromListView()
        {
            var categories = ListViewCategories.SelectedItems;
            return (from object t in categories select (t as Category)?.Name).ToList();
        }

        private void AddLayersToCategory(int addLayersQuantity)
        {
            try
            {
                if (addLayersQuantity == 0) throw new Exception("addLayersToCategory() have zero argument");
                var names = GetCategoriesNamesFromListView();
                if (names.Count == 0) throw new Exception("addLayersToCategory() have zero name group");
                _mThreadCategoryCreator.AddLayersToExistCategory(names, addLayersQuantity);
                ProgressBarCategory.Maximum = names.Count * addLayersQuantity;
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            try
            {
                e.CanExecute = true;
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                if (ListViewCategories.SelectedItem is Category item) Clipboard.SetText(item.Name);
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void MenuDelEmpty_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var items = GetSelectedCategories();
                _mThreadCategoryCreator.ClearLayers(items, 0);
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void MenuDelFull_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var items = GetSelectedCategories();
                _mThreadCategoryCreator.ClearLayers(items, 1);
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private List<Category> GetSelectedCategories()
        {
            var items = new List<Category>();
            try
            {
                var temp = ListViewCategories.SelectedItems;
                items.AddRange(from object x in temp select x as Category);
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            return items;
        }

        private void MenuItemDelAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selItems = GetSelectedCategories();
                selItems.ForEach(x =>
                {
                    var category = _mWorkPart.LayerCategories.FindObject(x.Name);
                    category.SetMemberLayers(new int[0]);
                    UpdateSingleCategoryInList(x.Name);
                });
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
    }
}