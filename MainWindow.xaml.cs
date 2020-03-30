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

namespace WpfApp3
{
    public partial class MainWindow : Window
    {
        private ThreadCategoryCreate threadCategoryCreator;
        private NXOpen.Session theSession;
        private NXOpen.Part workPart;
        private NXOpen.BasePart BasePart;
        private NXOpen.Part displayPart;
        private NXOpen.Layer.Category[] NXcategories;
        private int unusedLayer = 1; //Неиспользуемый рабочий слой
        private int maxLayersCount = 256; //Общее число слоев
        private string NxMainGategory = "ALL"; //Базовая категория NX, которой принадлежат все слои
        //=====================================================================
        //Конструктор класса - родителя
        //=====================================================================
        public MainWindow()
        {
            try
            {
                InitializeComponent();
                Singleton.GetInstance();
                theSession = NXOpen.Session.GetSession();
                workPart = theSession.Parts.Work;
                BasePart = theSession.Parts.BaseWork;
                displayPart = theSession.Parts.Display;
                threadCategoryCreator = new ThreadCategoryCreate(
                    new ProgressBarIncreaseCallback(progressBarIncrease),
                    new ProgressBarResetCallback(progressBarReset),
                    new ExceptionCallback(ExceptionThread),
                    new ButtonControlsAccess(buttonAccess)
                    );
                updateLayerCategories();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //=====================================================================
        //Точка входа в программу
        //=====================================================================
        public static int Main()
        {
            try
            {
                MainWindow myform = new MainWindow();
                myform.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                Application app = new App();
                app.ShutdownMode = ShutdownMode.OnLastWindowClose;
                app.Run(myform);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            return 0;
        }

        //=====================================================================
        //Метод выгрузки библиотеки
        //=====================================================================
        public static int GetUnloadOption(string arg)
        {
            return System.Convert.ToInt32(Session.LibraryUnloadOption.Immediately);
        }

        //=====================================================================
        //Преобразование категорий NXopen для bindable представления
        //=====================================================================

        private List<Category> transformNxCategories()
        {
            List<Category> CatArray = new List<Category>();
            NXcategories = workPart.LayerCategories.ToArray();
            var r = NXcategories.OrderBy(g => g.Name).SkipWhile(x => x.Name == NxMainGategory).ToList();
            r.ForEach(g =>
            {
                if (g.Name != NxMainGategory) CatArray.Add(new Category(g.Name, g.GetMemberLayers().Count()));
            });
            return CatArray;
        }
        //=====================================================================
        //Обновление списка категорий на экране
        //=====================================================================
        public void updateLayerCategories()
        {
            ListViewCategories.Items.Clear();
            List<Category> CatArray = transformNxCategories();
            CatArray.ForEach(g => ListViewCategories.Items.Add(g));
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        //=====================================================================
        //Удалить текущий список категорий
        //=====================================================================
        private void DeleteButton_CLick(object sender, RoutedEventArgs e)
        {
            deleteCurrentCategories();
        }

        //=====================================================================
        //Удалить текущий список категорий
        //=====================================================================
        private void deleteCurrentCategories()
        {
            NXcategories = workPart.LayerCategories.ToArray();
            foreach (NXOpen.Layer.Category X in NXcategories)
            {
                theSession.UpdateManager.AddToDeleteList(X);
                updateNxScreen();
                updateLayerCategories();
            }
        }

        //=====================================================================
        //Обновить экран NX
        //=====================================================================
        private void updateNxScreen()
        {
            NXOpen.Session.UndoMarkId id = theSession.NewestVisibleUndoMark;
            theSession.UpdateManager.DoUpdate(id);
            theSession.DeleteUndoMark(id, null);
        }

        //=====================================================================
        //Добавить список категорий
        //=====================================================================
        private void CreateCategoriesTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                deleteCurrentCategories();
                CategoryConfigurator config = new CategoryConfigurator();
                List<Category> GroupTemplate = config.getGroupTemplate();
                int laynumber = unusedLayer + 1;
                foreach (Category X in GroupTemplate)
                {
                    int[] arr = new int[X.LayCount];
                    for (int i = 0; i < arr.Count(); ++i)
                    {
                        if (laynumber == maxLayersCount) throw new Exception("Недостаточно количества слоев");
                        arr[i] = laynumber;
                        ++laynumber;
                    }
                    workPart.LayerCategories.CreateCategory(X.Name, "", arr);
                }
                updateNxScreen();
                updateLayerCategories();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //=====================================================================
        //Закрыть приложение
        //=====================================================================
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        //=====================================================================
        //Создать отдельную категорию в новом потоке
        //=====================================================================
        private void CreateSingleCategory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (InputLayerCount.Text.Length == 0 || Convert.ToInt32(InputLayerCount.Text) == 0) {
                    throw new Exception("число категорий должно быть больше 0");
                };
                string requestCategoryName = InputCategoryName.Text;
                int requestLayersCount = Convert.ToInt32(InputLayerCount.Text);
                ProgressBarCategory.Maximum = requestLayersCount;

                List<Category> CategoryGroup = new List<Category>() { new Category(requestCategoryName, requestLayersCount) };
                threadCategoryCreator.createListCategories(CategoryGroup);
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message);
            }
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
            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.Message);
            }
        }

        //=====================================================================
        //Методы для кэлбэков
        //=====================================================================
        private void progressBarIncrease(Boolean val)
        {
            if (val) Dispatcher.Invoke(() => ++ProgressBarCategory.Value);
        }

        private void progressBarReset(Boolean val)
        {
            if (val) Dispatcher.Invoke(() =>
            {
                updateLayerCategories();
                ProgressBarCategory.Value = 0;
            });
        }
        private void ExceptionThread(Exception ex)
        {
            Dispatcher.Invoke(() => { MessageBox.Show(ex.Message); ProgressBarCategory.Value = 0; });
        }

        private void buttonAccess(Boolean val)
        {
            Dispatcher.Invoke(() =>
            {
                if (val)
                {
                    if (InputCategoryName.Text != Singleton.InputTextCategory) CreateCategory.IsEnabled = true;
                    if (InputGroupName.Text != Singleton.InputTextGroup) CreateGroupCategories.IsEnabled = true;
                }
                else {
                    CreateCategory.IsEnabled = false;
                    CreateGroupCategories.IsEnabled = false;
                }
            });
        }

        //=====================================================================
        //Создать группу категорий в отдельном потоке
        //=====================================================================
        private void CreateGroupCategories_Click(object sender, RoutedEventArgs e) 
        {
            string requestGroupName = InputGroupName.Text;
            int requestGroupCount = Convert.ToInt32(InputGroupCount.Text);
            int requestLayersCount = Convert.ToInt32(InputGroupLayersCount.Text);
            ProgressBarCategory.Maximum = requestLayersCount;

            CategoryConfigurator config = new CategoryConfigurator();
            List<Category> CategoryGroup = config.getCategoryGroup(requestGroupName, requestGroupCount, requestLayersCount);
            threadCategoryCreator.createListCategories(CategoryGroup);
        }

        public void showMessageBox(object sender, RoutedEventArgs e) {
            var sitems = ListViewCategories.SelectedItems;
            int x = sitems.Count;
            for (int i = 0; i < x; ++i)
            {
                var item = ListViewCategories.SelectedItem;
                var r = item as Category;
                //MessageBox.Show(r.Name);
                var t = workPart.LayerCategories.FindObject(r.Name);
                theSession.UpdateManager.AddToDeleteList(t);
                ListViewCategories.Items.Remove(item);
            }
            updateNxScreen();
        }
    }
}