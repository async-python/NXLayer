using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NXOpen;

namespace NXLM
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
        private readonly Part mWorkPart;
        private static readonly object Locker = new object(); //обеспечивает последовательность обращений потоков к API

        //Кэлбэки для общения с основным потоком
        private readonly ProgressBarIncreaseCallback mProgrBarIncreaseCallback; //Увеличение значения прогресбара на 1
        private readonly ProgressBarResetCallback prBarResetCallback; //Обнуление прогрессбара
        private readonly ExceptionCallback exceptionCallback; //Вывод сообщения ошибки в основной поток
        private readonly ButtonControlsAccess controlStateCallback; //Активация и деактивация кнопок управления
        private readonly UpdateDisplayCategories updateCategoriesCallback; //Обновление в приложении списка категорий
        private readonly UpdateLayersQuantity updateLayesQuantityCalback; //Обновить число слоев в категории

        private const string NxMainGategory = Singleton.NxMainCategory;
        private const int WorkLayer = Singleton.WorkLayer;
        private const int MaxLayersCount = Singleton.MaxLayersCount;
        private Thread currentThread;

        public ThreadCategoryCreate(
            ProgressBarIncreaseCallback cback,
            ProgressBarResetCallback rback,
            ExceptionCallback eback,
            ButtonControlsAccess bback,
            UpdateDisplayCategories updback,
            UpdateLayersQuantity addback)
        {
            var session = Session.GetSession();
            mWorkPart = session.Parts.Work;

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
        public void CreateListCategories(List<Category> @group)
        {
            try
            {
                var ts = new ThreadStart(() =>
            {
                lock (Locker)
                {
                    try
                    {
                        controlStateCallback(false);
                        if (@group.Count == 0) throw new Exception("Количество категорий меньше 1");
                        var currentCategoryList = mWorkPart.LayerCategories.ToArray().ToList();
                        int requestLayersCount = 0;
                        @group.ForEach(x =>
                        {
                            currentCategoryList.ForEach(element =>
                            {
                                if (element.Name == x.Name) throw new Exception("Имя категории уже существует");
                            });
                            requestLayersCount += x.LayCount;
                        });
                        if (requestLayersCount > MaxLayersCount - 1) throw new Exception("Недостаточно слоев");
                        var freeLayers = GetLayersWithoutObjects(requestLayersCount);
                        var apartedLayers = GetApartedArray(freeLayers, @group.Count);
                        if (apartedLayers.Count != @group.Count) throw new Exception("ошибка сравнения массивов в функции createListCategories");
                        @group.ForEach(x =>
                        {
                            mWorkPart.LayerCategories.CreateCategory(x.Name, "", apartedLayers[@group.IndexOf(x)]);
                        });
                        updateLayesQuantityCalback(@group);
                        prBarResetCallback(true);
                        controlStateCallback(true);
                    }
                    catch (ThreadAbortException) { }
                    catch (Exception ex) { ExceptionActions(ex); }
                }
            });
                currentThread = new Thread(new ThreadStart(ts));
                currentThread.SetApartmentState(ApartmentState.STA);
                currentThread.IsBackground = true;
                currentThread.Start();
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { ExceptionActions(ex); }
        }

        //=====================================================================
        //Возвращает массив слоев без обьектов и вне существующих категорий
        //=====================================================================
        private int[] GetLayersWithoutObjects(int requestLayersCount)
        {
            var freeLayers = new int[requestLayersCount];
            try
            {
                var currentCategoryList = mWorkPart.LayerCategories.ToArray().ToList();
                currentCategoryList = currentCategoryList.Where(x => x.Name != NxMainGategory).ToList();
                var layers = new List<int>();
                currentCategoryList.ForEach(x => { layers.AddRange(x.GetMemberLayers().ToList()); });
                var allLayers = mWorkPart.LayerCategories.FindObject(NxMainGategory).GetMemberLayers().ToList();
                var resultArray = allLayers.Distinct().Except(layers).ToList();
                resultArray.Remove(WorkLayer);
                //if (reultArray.Count < requestLayersCount) throw new Exception("Недостаточно свободных слоев");
                var z = 0;
                for (var i = 0; i < resultArray.Count(); ++i)
                {
                    if (z == requestLayersCount) break;
                    if (resultArray[i] > MaxLayersCount) throw new Exception("Недостаточно свободных слоев без обьектов для создания категории");
                    if (mWorkPart.Layers.GetAllObjectsOnLayer(resultArray[i]).Any()) continue;
                    freeLayers[z] = resultArray[i];
                    ++z;
                    mProgrBarIncreaseCallback(true);
                }
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { ExceptionActions(ex); }
            return freeLayers;
        }

        //=====================================================================
        //Добавляет слои без обьектов к существующим категориям
        //=====================================================================
        public void AddLayersToExistCategory(List<string> names, int layersQuantity)
        {
            try
            {
                var ts = new ThreadStart(() =>
            {
                try
                {
                    lock (Locker)
                    {
                        try
                        {
                            controlStateCallback(false);
                            var totalLayersRequest = layersQuantity * names.Count;
                            var freeLayers = GetLayersWithoutObjects(totalLayersRequest);
                            var freeLayersGroups = GetApartedArray(freeLayers, names.Count);
                            for (var i = 0; i < names.Count; ++i)
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
                        catch (Exception ex) { ExceptionActions(ex); }
                    }
                }
                catch (ThreadAbortException) { }
                catch (Exception ex) { ExceptionActions(ex); }
            });
                currentThread = new Thread(new ThreadStart(ts));
                currentThread.SetApartmentState(ApartmentState.STA);
                currentThread.IsBackground = true;
                currentThread.Start();
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { ExceptionActions(ex); }
        }

        //=====================================================================
        //Разделение массива на несколько частей согласно количеству 
        //выделенных категорий
        //=====================================================================
        private List<int[]> GetApartedArray(int[] apartArray, int partsQuantity)
        {
            var aparted = new List<int[]>();
            try
            {
                //if (partsQuantity < 1) Thread.CurrentThread.Abort();
                if (partsQuantity < 1) throw new Exception("функция getApartArray() аргумент меньше 0");
                var layersInPart = apartArray.Length / partsQuantity;
                var k = 0;
                for (var i = 0; i < partsQuantity; ++i)
                {
                    var arr = new int[layersInPart];
                    for (var w = 0; w < layersInPart; ++w)
                    {
                        arr[w] = apartArray[k];
                        ++k;
                    }
                    aparted.Add(arr);
                }
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { ExceptionActions(ex); }
            return aparted;
        }

        private void ExceptionActions(Exception ex)
        {
            try
            {
                prBarResetCallback(true);
                exceptionCallback(ex);
                controlStateCallback(true);
            }
            catch (ThreadAbortException) { }
            catch (Exception)
            {
                // ignored
            }
        }

        public void ClearLayers(List<Category> group, int val)
        {
            try
            {
                var ts = new ThreadStart(() =>
                {
                    try
                    {
                        lock (Locker)
                        {
                            var categories = new List<NXOpen.Layer.Category>();
                            group.ForEach(x =>
                            {
                                var item = mWorkPart.LayerCategories.FindObject(x.Name);
                                if (item != null) categories.Add(item);
                            });
                            categories.ForEach(x =>
                            {
                                var quantity = x.GetMemberLayers().Length;
                                var layers = new List<int>();
                                var memberLayers = x.GetMemberLayers().ToList();
                                memberLayers.ForEach(z =>
                                {
                                    switch (val)
                                    {
                                        case 0:
                                            if (mWorkPart.Layers.GetAllObjectsOnLayer(z).Length != 0) layers.Add(z);
                                            break;
                                        case 1:
                                            if (mWorkPart.Layers.GetAllObjectsOnLayer(z).Length == 0) layers.Add(z);
                                            break;
                                    }
                                });
                                x.SetMemberLayers(layers.ToArray());
                                updateCategoriesCallback(x.Name);
                            });
                        }
                    }
                    catch (ThreadAbortException) { }
                    catch (Exception)
                    {
                        // ignored
                    }
                });
                currentThread = new Thread(ts);
                currentThread.SetApartmentState(ApartmentState.STA);
                currentThread.IsBackground = true;
                currentThread.Start();
            }
            catch (ThreadAbortException) { }
            catch (Exception)
            {
                // ignored
            }
        }
    }

    //=====================================================================
    //Делегаты для кэлбэков в основной поток
    //=====================================================================
    public delegate void ProgressBarIncreaseCallback(bool val);
    public delegate void ProgressBarResetCallback(bool val);
    public delegate void ExceptionCallback(Exception ex);
    public delegate void ButtonControlsAccess(bool val);
    public delegate void UpdateDisplayCategories(string name);
    public delegate void UpdateLayersQuantity(List<Category> @group);
}