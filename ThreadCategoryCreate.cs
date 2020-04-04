using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NXOpen;

namespace WpfApp3
{
    public class ThreadCategoryCreate
    {
        private bool threadStarted = false;
        private NXOpen.Session theSession;
        private NXOpen.Part workPart;

        //Кэлбэки для общения с основным потоком
        private ProgressBarIncreaseCallback increaseCallback; //Увеличение значения прогресбара на 1
        private ProgressBarResetCallback resetCallback; //Обнуление прогрессбара
        private ExceptionCallback exCallback; //Вывод сообщения ошибки в основной поток
        private ButtonControlsAccess btCallback; //Активация и деактивация кнопок управления
        private UpdateSingleItem updCallback;
        private AddSingleItem addCalback;

        private const string NxMainGategory = "ALL";
        private const int unusedLayer = 1;
        private const int maxLayersCount = 256;
        public static object locker = Singleton.locker;

        public bool getThreadState()
        {
            return threadStarted;
        }

        public ThreadCategoryCreate(
            ProgressBarIncreaseCallback cback,
            ProgressBarResetCallback rback,
            ExceptionCallback eback,
            ButtonControlsAccess bback,
            UpdateSingleItem updback,
            AddSingleItem addback)
        {
            theSession = NXOpen.Session.GetSession();
            workPart = theSession.Parts.Work;

            increaseCallback = cback;
            resetCallback = rback;
            exCallback = eback;
            btCallback = bback;
            updCallback = updback;
            addCalback = addback;
        }

        //=====================================================================
        //Создает перечень категорий в отдельном потоке
        //=====================================================================
        public void createListCategories(List<Category> Group)
        {
            try
            {
                ThreadStart ts = new ThreadStart(() =>
            {
                lock (locker)
                {
                    threadStarted = true;
                    btCallback(false);
                    if (Group.Count == 0) throw new Exception("Количество категорий меньше 1");
                    var currentCategoryList = workPart.LayerCategories.ToArray().ToList();
                    int requestLayersCount = 0;
                    Group.ForEach(x =>
                    {
                        currentCategoryList.ForEach(element =>
                        {
                            if (element.Name == x.Name) throw new Exception("Имя категории уже существует");
                        });
                        requestLayersCount += x.LayCount;
                    });
                    int[] freeLayers = getLayersWithoutObjects(requestLayersCount);
                    List<int[]> apartedLayers = getApartedArray(freeLayers, Group.Count);
                    if (apartedLayers.Count != Group.Count) throw new Exception("ошибка сравнения массивов в функции createListCategories");
                    Group.ForEach(x =>
                    {
                        workPart.LayerCategories.CreateCategory(x.Name, "", apartedLayers[Group.IndexOf(x)]);
                    });
                    addCalback(true);
                    resetCallback(true);
                    btCallback(true);
                    threadStarted = false;
                }
            });
                Thread t = new Thread(new ThreadStart(ts));
                t = new Thread(new ThreadStart(ts));
                t.SetApartmentState(ApartmentState.STA);
                t.IsBackground = true;
                t.Start();
            }
            catch (ThreadAbortException ex)
            {

            }
            catch (Exception ex) { exCallback(ex); }
        }

        //=====================================================================
        //Возвращает массив слоев без обьектов и вне существующих категорий
        //=====================================================================
        private int[] getLayersWithoutObjects(int requestLayersCount)
        {
            lock (locker)
            {
                var currentCategoryList = workPart.LayerCategories.ToArray().ToList();
                currentCategoryList = currentCategoryList.Where(x => x.Name != NxMainGategory).ToList();
                var layers = new List<int>();
                currentCategoryList.ForEach(x => { layers.AddRange(x.GetMemberLayers().ToList()); });
                var allLayers = workPart.LayerCategories.FindObject(NxMainGategory).GetMemberLayers().ToList();
                var reultArray = allLayers.Distinct().Except(layers).ToList();
                reultArray.Remove(unusedLayer);
                if (reultArray.Count < requestLayersCount) throw new Exception("Недостаточно свободных слоев");
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
                        increaseCallback(true);
                    }
                }
                return freeLayers;
            }
        }

        //=====================================================================
        //Добавляет слои без обьектов к существующим категориям
        //=====================================================================
        public void addLayersToExistCategory(List<string> names, int layersQuantity)
        {
            try
            {
                ThreadStart ts = new ThreadStart(() =>
            {

                lock (locker)
                {
                    threadStarted = true;
                    btCallback(false);
                    int totalLayersRequest = layersQuantity * names.Count;
                    int[] freeLayers = getLayersWithoutObjects(totalLayersRequest);
                    List<int[]> freeLayersGroups = getApartedArray(freeLayers, names.Count);
                    for (int i = 0; i < names.Count; ++i)
                    {
                        var category = workPart.LayerCategories.FindObject(names[i]);
                        var currentLayers = category.GetMemberLayers().ToList().Concat(freeLayersGroups[i].ToList());
                        category.SetMemberLayers(currentLayers.ToArray());
                        updCallback(category.Name);
                    }
                    resetCallback(true);
                    btCallback(true);
                    threadStarted = false;
                }

            });
                Thread t = new Thread(new ThreadStart(ts));
                t.SetApartmentState(ApartmentState.STA);
                t.IsBackground = true;
                t.Start();
            }
            catch (ThreadAbortException ex)
            {

            }
            catch (Exception ex)
            {
                exCallback(ex);
                resetCallback(true);
                btCallback(true);
            }
        }

        //=====================================================================
        //Разделение массива на несколько частей согласно количеству 
        //выделенных категорий
        //=====================================================================
        private List<int[]> getApartedArray(int[] apartArray, int partsQuantity)
        {
            List<int[]> aparted = new List<int[]>();
            try
            {
                lock (locker)
                {
                    //if (partsQuantity < 1) Thread.CurrentThread.Abort();
                    if (partsQuantity < 1) throw new Exception("функция getApartArray() аргумент меньше 0");
                    int layersInPart = apartArray.Length / partsQuantity;
                    int k = 0;
                    for (int i = 0; i < partsQuantity; ++i)
                    {
                        int[] arr = new int[layersInPart];
                        for (int w = 0; w < layersInPart; ++w)
                        {
                            arr[w] = apartArray[k];
                            ++k;
                        }
                        aparted.Add(arr);
                    }
                }
            }
            catch (Exception ex) { exCallback(ex); }
            return aparted;
        }
    }

    //=====================================================================
    //Делегаты для кэлбэков в основной поток
    //=====================================================================
    public delegate void ProgressBarIncreaseCallback(Boolean val);
    public delegate void ProgressBarResetCallback(Boolean val);
    public delegate void ExceptionCallback(Exception ex);
    public delegate void ButtonControlsAccess(Boolean val);
    public delegate void UpdateSingleItem(string name);
    public delegate void AddSingleItem(Boolean val);
}