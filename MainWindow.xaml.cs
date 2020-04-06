using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NXOpen;
using WpfApp3;
using System.Collections.ObjectModel;
using WpfApp3.ViewModel;

namespace WpfApp3
{
    public partial class MainWindow : Window
    {
        private ThreadCategoryCreate mThreadCategoryCreator;
        private Session mSession;
        private Part mWorkPart;
        private NXOpen.BasePart mBasePart;
        private NXOpen.Part mDisplayPart;
        private NXOpen.Layer.Category[] mNxCategories;
        private int mWorkLayer = Singleton.WorkLayer; //Неиспользуемый рабочий слой
        private int mMaxLayersCount = Singleton.maxLayersCount; //Общее число слоев
        private string mNxMainGategory = Singleton.NxMainGategory; //Базовая категория NX, которой принадлежат все слои
        private int mMaxLayersQuantityInSubMenu = 10; //Максимальное количество добавляемых слоев из субменю основного списка категорий

        //Список отображаемых категорий с количеством слоев в основном окне приложения
        public ObservableCollection<Category> mDisplayCategoryList { get; set; }
            = new ObservableCollection<Category>();

        //=====================================================================
        //Конструктор класса - родителя
        //=====================================================================
        public MainWindow()
        {
            try
            {
                InitializeComponent();
                mSession = Session.GetSession();
                Singleton.GetInstance();
                mWorkPart = mSession.Parts.Work;
                mBasePart = mSession.Parts.BaseWork;
                mDisplayPart = mSession.Parts.Display;
                mThreadCategoryCreator = new ThreadCategoryCreate(
                    new ProgressBarIncreaseCallback(progressBarIncrease),
                    new ProgressBarResetCallback(progressBarReset),
                    new ExceptionCallback(ExceptionThread),
                    new ButtonControlsAccess(buttonAccess),
                    new UpdateDisplayCategories(ItemUpdate),
                    new UpdateLayersQuantity(ItemAddUpdate));
                this.DataContext = new MainWindowViewModel(mDisplayCategoryList);
                updateItemList();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Обновить информацию по количеству слоев в категории в списке приложения
        //mDisplayCategoryList
        //=====================================================================
        private void updateSingleCategoryInList(string name)
        {
            try
            {
                var category = mWorkPart.LayerCategories.FindObject(name);
                foreach (Category x in mDisplayCategoryList)
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
        private void addGroupCategoriesToList(List<Category> Group)
        {
            try
            {
                Group.ForEach(x =>
                {
                    mDisplayCategoryList.Add(x);
                });
                updateItemList();
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Синхронизация списка категорий в приложении со списком категорий в NX
        //=====================================================================
        private void updateItemList()
        {
            try
            {
                mDisplayCategoryList.Clear();
                mNxCategories = mWorkPart.LayerCategories.ToArray();
                var tempArray = mNxCategories.OrderBy(g => g.Name).SkipWhile(x => x.Name == mNxMainGategory).ToList();
                tempArray.ForEach(g =>
                {
                    if (g.Name != mNxMainGategory) mDisplayCategoryList.Add(new Category(g.Name, g.GetMemberLayers().Count()));
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
                MainWindow myForm = new MainWindow();
                myForm.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                Application app = new App();
                app.ShutdownMode = ShutdownMode.OnLastWindowClose;
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
                deleteCurrentCategories();
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Удалить текущий список категорий
        //=====================================================================
        private void deleteCurrentCategories()
        {
            try
            {
                mNxCategories = mWorkPart.LayerCategories.ToArray();
                foreach (NXOpen.Layer.Category X in mNxCategories)
                {
                    mSession.UpdateManager.AddToDeleteList(X);
                    updateNxScreen();
                    updateItemList();
                }
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Обновить экран NX
        //=====================================================================
        private void updateNxScreen()
        {
            try
            {
                NXOpen.Session.UndoMarkId id = mSession.NewestVisibleUndoMark;
                mSession.UpdateManager.DoUpdate(id);
                mSession.DeleteUndoMark(id, null);
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
                deleteCurrentCategories();
                CategoryConfigurator config = new CategoryConfigurator();
                List<Category> GroupTemplate = config.getGroupTemplate();
                int laynumber = mWorkLayer + 1;
                foreach (Category X in GroupTemplate)
                {
                    int[] arr = new int[X.LayCount];
                    for (int i = 0; i < arr.Count(); ++i)
                    {
                        if (laynumber == mMaxLayersCount) throw new Exception("Недостаточно количества слоев");
                        arr[i] = laynumber;
                        ++laynumber;
                    }
                    mWorkPart.LayerCategories.CreateCategory(X.Name, "", arr);
                }
                updateNxScreen();
                updateItemList();
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
                };
                string requestCategoryName = InputCategoryName.Text;
                int requestLayersCount = Convert.ToInt32(InputLayerCount.Text);
                ProgressBarCategory.Maximum = requestLayersCount;

                List<Category> CategoryGroup = new List<Category>() { new Category(requestCategoryName, requestLayersCount) };
                mThreadCategoryCreator.createListCategories(CategoryGroup);
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
                InputCategoryName.setStringTextBoxBehavior(InputCategoryName.Text, CreateCategory);
                InputGroupName.setStringTextBoxBehavior(InputGroupName.Text, CreateGroupCategories);
                InputLayerCount.setIntTextBoxBehavior(InputLayerCount.Text);
                InputGroupCount.setIntTextBoxBehavior(InputGroupCount.Text);
                InputGroupLayersCount.setIntTextBoxBehavior(InputGroupLayersCount.Text);
                generateSubmenuLayersControl(MenuItemAdd, mMaxLayersQuantityInSubMenu);
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Методы для кэлбэков
        //=====================================================================
        private void progressBarIncrease(Boolean val)
        {
            try
            {
                if (val) Dispatcher.Invoke(() => ++ProgressBarCategory.Value);
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void progressBarReset(Boolean val)
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
            catch (Exception) { }
        }

        private void buttonAccess(Boolean val)
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
                Dispatcher.Invoke(() => { updateSingleCategoryInList(name); });
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void ItemAddUpdate(List<Category> Croup)
        {
            try
            {
                Dispatcher.Invoke(() => { addGroupCategoriesToList(Croup); });
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
                string requestGroupName = InputGroupName.Text;
                int requestGroupCount = Convert.ToInt32(InputGroupCount.Text);
                int requestLayersCount = Convert.ToInt32(InputGroupLayersCount.Text);
                ProgressBarCategory.Maximum = requestLayersCount;
                CategoryConfigurator config = new CategoryConfigurator();
                List<Category> CategoryGroup = config.getCategoryGroup(requestGroupName, requestGroupCount, requestLayersCount);
                mThreadCategoryCreator.createListCategories(CategoryGroup);
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Удалить все категории в списке
        //=====================================================================
        public void deleteAllCategories(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItems = ListViewCategories.SelectedItems;
                int x = selectedItems.Count;
                for (int i = 0; i < x; ++i)
                {
                    var item = ListViewCategories.SelectedItem;
                    var category = item as Category;
                    var NXcategory = mWorkPart.LayerCategories.FindObject(category.Name);
                    mSession.UpdateManager.AddToDeleteList(NXcategory);
                    mDisplayCategoryList.Remove(category);
                }
                updateNxScreen();
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
                    int selectedItemsCount = ListViewCategories.SelectedItems.Count;
                    List<int> categoryQuantity = new List<int>();
                    for (int i = 0; i < selectedItemsCount; i++)
                    {
                        var item = selItems[i];
                        var layCount = (item as Category).LayCount;
                        categoryQuantity.Add(layCount);
                    }
                    var maxVal = 15;
                    var totalCount = maxVal;
                    if (categoryQuantity.Min() < maxVal) totalCount = categoryQuantity.Min();
                    generateSubmenuLayersControl(MenuItemDel, totalCount);
                }
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Генерация элементов субменю для добавления и удаления слоев из категории
        //=====================================================================
        private void generateSubmenuLayersControl(MenuItem menuItem, int subItemsQuantity)
        {
            try
            {
                Uri plus_icon = new Uri("Resources/plus_icon.ico", UriKind.Relative);
                Uri minus_icon = new Uri("Resources/minus_icon.ico", UriKind.Relative);
                Uri currentUri = (menuItem == MenuItemDel) ? minus_icon : plus_icon;

                List<MenuItem> listSubMenuItems = new List<MenuItem>();
                for (int i = 0; i < subItemsQuantity; i++)
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
                        var subitem = se as MenuItem;
                        if (menuItem == MenuItemDel) deleteLayersFromCategory(Convert.ToInt32(subitem.Header));
                        if (menuItem == MenuItemAdd) addLayersToCategory(Convert.ToInt32(subitem.Header));
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
        private void deleteLayersFromCategory(int quantityLayersToDelete)
        {
            try
            {
                List<string> names = getCategoriesNamesFromListView();
                names.ForEach(x =>
                {
                    var nxCategory = mWorkPart.LayerCategories.FindObject(x);
                    var layers = nxCategory.GetMemberLayers();
                    int count = layers.Length - quantityLayersToDelete;
                    if (count < 0) throw new Exception("количество слоев меньше 0");
                    int[] newLayers = new int[layers.Length - quantityLayersToDelete];
                    for (int i = 0; i < count; ++i)
                    {
                        newLayers[i] = layers[i];
                    }
                    nxCategory.SetMemberLayers(newLayers);
                });
                updateItemList();
                updateNxScreen();
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //=====================================================================
        //Возврат имен категорий со списка
        //=====================================================================
        private List<string> getCategoriesNamesFromListView()
        {
            List<string> names = new List<string>();
            var categories = ListViewCategories.SelectedItems;
            for (int i = 0; i < categories.Count; ++i)
            {
                names.Add((categories[i] as Category).Name);
            }
            return names;
        }

        private void addLayersToCategory(int addLayersQuantity)
        {
            try
            {
                if (addLayersQuantity == 0) throw new Exception("addLayersToCategory() have zero argument");
                List<string> names = getCategoriesNamesFromListView();
                if (names.Count == 0) throw new Exception("addLayersToCategory() have zero name group");
                mThreadCategoryCreator.addLayersToExistCategory(names, addLayersQuantity);
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
                var item = ListViewCategories.SelectedItem as Category;
                Clipboard.SetText(item.Name);
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void MenuDelEmpty_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                List<Category> items = getSelectedCategories();
                mThreadCategoryCreator.ClearLayers(items, 0);
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void MenuDelFull_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                List<Category> items = getSelectedCategories();
                mThreadCategoryCreator.ClearLayers(items, 1);
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private List<Category> getSelectedCategories()
        {
            List<Category> items = new List<Category>();
            try
            {
                var temp = ListViewCategories.SelectedItems;
                foreach (var x in temp)
                {
                    items.Add(x as Category);
                }
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            return items;
        }
    }
}