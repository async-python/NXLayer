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
                theSession = NXOpen.Session.GetSession();
                workPart = theSession.Parts.Work;
                BasePart = theSession.Parts.BaseWork;
                displayPart = theSession.Parts.Display;
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

        private void createNewCategory() { 

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            string requestCategoryName = InputCategoryName.Text;
            int requestLayersCount = Convert.ToInt32(InputLayerCount.Text);
            ThreadCategoryCreate tcc = new ThreadCategoryCreate(requestCategoryName, requestLayersCount, new ExampleCallback(ResultCallback));
            Thread testth = new Thread(new ThreadStart(tcc.beginProc));
            testth.Start();
            ProgressBarCategory.Maximum = requestLayersCount;
            ThreadStart ts = new ThreadStart(() =>
            {
                try
                {
                    var CatArray = workPart.LayerCategories.ToArray().Where(x => x.Name != NxMainGategory).ToList();
                    CatArray.ForEach(x => { if (x.Name == requestCategoryName) throw new Exception("Имя категории уже существует"); });
                    var layers = new List<int>();
                    CatArray.ForEach(x => { layers.AddRange(x.GetMemberLayers().ToList()); });
                    var allLayers = workPart.LayerCategories.FindObject(NxMainGategory).GetMemberLayers().ToList();
                    var reultArray = allLayers.Distinct().Except(layers).ToList();
                    reultArray.Remove(unusedLayer);
                    if (reultArray.Count < requestLayersCount) throw new Exception("Недостаточно слоев для создания категории");
                    int z = 0;
                    int[] freeLayers = new int[requestLayersCount];
                    for (int i = 0; i < reultArray.Count(); ++i)
                    {
                        if (z == requestLayersCount) break;
                        if (reultArray[i] > maxLayersCount) throw new Exception("Недостаточно свободных слоев без обьектов для создания категории");
                        if (!workPart.Layers.GetAllObjectsOnLayer(reultArray[i]).Any())
                        {
                            freeLayers[z] = reultArray[i];
                            ++z;
                            Dispatcher.Invoke(() => { ++ProgressBarCategory.Value; });
                        }
                    }
                    workPart.LayerCategories.CreateCategory(requestCategoryName, "", freeLayers);
                    Dispatcher.Invoke(() =>
                    {
                        updateLayerCategories();
                        ProgressBarCategory.Value = 0;
                    });
                }
                catch (Exception ex) { Dispatcher.Invoke(() => { MessageBox.Show(ex.Message); ProgressBarCategory.Value = 0; }); }
            });
            Thread t = new Thread(new ThreadStart(ts));
            t.Start();
        }

        private void NXLayerManager_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
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

        public void ResultCallback(string name)
        {
            Dispatcher.Invoke(()=>CreateCategory.Content = name);
        }
    }
}
