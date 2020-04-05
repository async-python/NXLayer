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
    /*
     * Класс выполняет работу с NX API в фоне (для запросов, обрабатывающихся долгое время)
     * 1. Создание/удаление категорий
     * 2. Поиск свободных слоев без обьектов
     * 3. Взаимодействует с основным потоком интерфейса через кэллбэки
     *    (нельзя использовать метод JOIN - повесит в бесконечный цикл)
     * 4. Обновляет состояние ProgressBar
     * 5. Управляет состоянием контролов
     */
    public class ThreadCategoryCreate
    {
        private readonly Session mSession;
        private readonly Part mWorkPart;
        private static object mLocker = new object(); //обеспечивает последовательность обращений потоков к API

        //Кэлбэки для общения с основным потоком
        private ProgressBarIncreaseCallback mProgrBarIncreaseCallback; //Увеличение значения прогресбара на 1
        private ProgressBarResetCallback prBarResetCallback; //Обнуление прогрессбара
        private ExceptionCallback exceptionCallback; //Вывод сообщения ошибки в основной поток
        private ButtonControlsAccess controlStateCallback; //Активация и деактивация кнопок управления
        private UpdateDisplayCategories updateCategoriesCallback; //Обновление в приложении списка категорий
        private UpdateLayersQuantity updateLayesQuantityCalback; //Обновить число слоев в категории

        private const string NxMainGategory = Singleton.NxMainGategory;
        private const int workLayer = Singleton.WorkLayer;
        private const int maxLayersCount = Singleton.maxLayersCount;
        private Thread currentThread;

        public ThreadCategoryCreate(
            ProgressBarIncreaseCallback cback,
            ProgressBarResetCallback rback,
            ExceptionCallback eback,
            ButtonControlsAccess bback,
            UpdateDisplayCategories updback,
            UpdateLayersQuantity addback)
        {
            mSession = Session.GetSession();
            mWorkPart = mSession.Parts.Work;

            mProgrBarIncreaseCallback = cback;
            prBarResetCallback = rback;
            exceptionCallback = eback;
            controlStateCallback = bback;
            updateCategoriesCallback = updback;
            updateLayesQuantityCalback = addback;
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
                lock (mLocker)
                {
                    try
                    {
                        controlStateCallback(false);
                        if (Group.Count == 0) throw new Exception("Количество категорий меньше 1");
                        var currentCategoryList = mWorkPart.LayerCategories.ToArray().ToList();
                        int requestLayersCount = 0;
                        Group.ForEach(x =>
                        {
                            currentCategoryList.ForEach(element =>
                            {
                                if (element.Name == x.Name) throw new Exception("Имя категории уже существует");
                            });
                            requestLayersCount += x.LayCount;
                        });
                        if (requestLayersCount > maxLayersCount - 1) throw new Exception("Недостаточно слоев");
                        int[] freeLayers = getLayersWithoutObjects(requestLayersCount);
                        List<int[]> apartedLayers = getApartedArray(freeLayers, Group.Count);
                        if (apartedLayers.Count != Group.Count) throw new Exception("ошибка сравнения массивов в функции createListCategories");
                        Group.ForEach(x =>
                        {
                            mWorkPart.LayerCategories.CreateCategory(x.Name, "", apartedLayers[Group.IndexOf(x)]);
                        });
                        updateLayesQuantityCalback(Group);
                        prBarResetCallback(true);
                        controlStateCallback(true);
                    }
                    catch (ThreadAbortException) { }
                    catch (Exception ex) { exceptionActions(ex); }
                }
            });
                currentThread = new Thread(new ThreadStart(ts));
                currentThread.SetApartmentState(ApartmentState.STA);
                currentThread.IsBackground = true;
                currentThread.Start();
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { exceptionActions(ex); }
        }

        //=====================================================================
        //Возвращает массив слоев без обьектов и вне существующих категорий
        //=====================================================================
        private int[] getLayersWithoutObjects(int requestLayersCount)
        {
            int[] freeLayers = new int[requestLayersCount];
            try
            {
                var currentCategoryList = mWorkPart.LayerCategories.ToArray().ToList();
                currentCategoryList = currentCategoryList.Where(x => x.Name != NxMainGategory).ToList();
                var layers = new List<int>();
                currentCategoryList.ForEach(x => { layers.AddRange(x.GetMemberLayers().ToList()); });
                var allLayers = mWorkPart.LayerCategories.FindObject(NxMainGategory).GetMemberLayers().ToList();
                var reultArray = allLayers.Distinct().Except(layers).ToList();
                reultArray.Remove(workLayer);
                //if (reultArray.Count < requestLayersCount) throw new Exception("Недостаточно свободных слоев");
                int z = 0;
                for (int i = 0; i < reultArray.Count(); ++i)
                {
                    if (z == requestLayersCount) break;
                    if (reultArray[i] > maxLayersCount) throw new Exception("Недостаточно свободных слоев без обьектов для создания категории");
                    if (!mWorkPart.Layers.GetAllObjectsOnLayer(reultArray[i]).Any())
                    {
                        freeLayers[z] = reultArray[i];
                        ++z;
                        mProgrBarIncreaseCallback(true);
                    }
                }
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { exceptionActions(ex); }
            return freeLayers;
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
                try
                {
                    lock (mLocker)
                    {
                        try
                        {
                            controlStateCallback(false);
                            int totalLayersRequest = layersQuantity * names.Count;
                            int[] freeLayers = getLayersWithoutObjects(totalLayersRequest);
                            List<int[]> freeLayersGroups = getApartedArray(freeLayers, names.Count);
                            for (int i = 0; i < names.Count; ++i)
                            {
                                var category = mWorkPart.LayerCategories.FindObject(names[i]);
                                var currentLayers = category.GetMemberLayers().ToList().Concat(freeLayersGroups[i].ToList());
                                category.SetMemberLayers(currentLayers.ToArray());
                                updateCategoriesCallback(category.Name);
                            }
                            prBarResetCallback(true);
                            controlStateCallback(true);
                        }
                        catch (ThreadAbortException) { }
                        catch (Exception ex) { exceptionActions(ex); }
                    }
                }
                catch (ThreadAbortException) { }
                catch (Exception ex) { exceptionActions(ex); }
            });
                currentThread = new Thread(new ThreadStart(ts));
                currentThread.SetApartmentState(ApartmentState.STA);
                currentThread.IsBackground = true;
                currentThread.Start();
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { exceptionActions(ex); }
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
            catch (ThreadAbortException) { }
            catch (Exception ex) { exceptionActions(ex); }
            return aparted;
        }

        private void exceptionActions(Exception ex)
        {
            try
            {
                prBarResetCallback(true);
                exceptionCallback(ex);
                controlStateCallback(true);
            }
            catch (ThreadAbortException) { }
            catch (Exception) { }
        }
    }

    //=====================================================================
    //Делегаты для кэлбэков в основной поток
    //=====================================================================
    public delegate void ProgressBarIncreaseCallback(Boolean val);
    public delegate void ProgressBarResetCallback(Boolean val);
    public delegate void ExceptionCallback(Exception ex);
    public delegate void ButtonControlsAccess(Boolean val);
    public delegate void UpdateDisplayCategories(string name);
    public delegate void UpdateLayersQuantity(List<Category> Group);
}