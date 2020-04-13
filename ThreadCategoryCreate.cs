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
        private readonly Part _mWorkPart;
        private static readonly object Locker = new object(); //обеспечивает последовательность обращений потоков к API

        //Кэлбэки для общения с основным потоком
        private readonly ProgressBarIncreaseCallback _mProgressBarIncreaseCallback; //Увеличение значения прогресбара на 1
        private readonly ProgressBarResetCallback _prBarResetCallback; //Обнуление прогрессбара
        private readonly ExceptionCallback _exceptionCallback; //Вывод сообщения ошибки в основной поток
        private readonly ButtonControlsAccess _controlStateCallback; //Активация и деактивация кнопок управления
        private readonly UpdateDisplayCategories _updateCategoriesCallback; //Обновление в приложении списка категорий
        private readonly UpdateLayersQuantity _updateLayersQuantityCallback; //Обновить число слоев в категории

        private const string NxMainCategory = Singleton.NxMainCategory;
        private const int WorkLayer = Singleton.WorkLayer;
        private const int MaxLayersCount = Singleton.MaxLayersCount;
        private Thread _currentThread;

        public ThreadCategoryCreate(
            ProgressBarIncreaseCallback progressBarIncreaseCallback,
            ProgressBarResetCallback progressBarResetCallback,
            ExceptionCallback exceptionCallback,
            ButtonControlsAccess buttonControlsAccess,
            UpdateDisplayCategories updateDisplayCategories,
            UpdateLayersQuantity updateLayersQuantity)
        {
            var session = Session.GetSession();
            _mWorkPart = session.Parts.Work;

            _mProgressBarIncreaseCallback = progressBarIncreaseCallback;
            _prBarResetCallback = progressBarResetCallback;
            this._exceptionCallback = exceptionCallback;
            _controlStateCallback = buttonControlsAccess;
            _updateCategoriesCallback = updateDisplayCategories;
            _updateLayersQuantityCallback = updateLayersQuantity;
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
                        _controlStateCallback(false);
                        if (@group.Count == 0) throw new Exception("Количество категорий меньше 1");
                        var currentCategoryList = _mWorkPart.LayerCategories.ToArray().ToList();
                        var requestLayersCount = 0;
                        @group.ForEach(x =>
                        {
                            currentCategoryList.ForEach(element =>
                            {
                                if (element.Name == x.Name) 
                                    throw new Exception("Имя категории уже существует");
                            });
                            requestLayersCount += x.LayCount;
                        });
                        if (requestLayersCount > MaxLayersCount - 1) 
                            throw new Exception("Недостаточно слоев");
                        var freeLayers = GetLayersWithoutObjects(requestLayersCount);
                        var apartedLayers = GetApartedArray(freeLayers, @group.Count);
                        if (apartedLayers.Count != @group.Count) 
                            throw new Exception("ошибка сравнения массивов в функции createListCategories");
                        @group.ForEach(x =>
                        {
                            _mWorkPart.LayerCategories.CreateCategory(x.Name, "", apartedLayers[@group.IndexOf(x)]);
                        });
                        _updateLayersQuantityCallback(@group);
                        _prBarResetCallback(true);
                        _controlStateCallback(true);
                    }
                    catch (ThreadAbortException) { }
                    catch (Exception ex) { ExceptionActions(ex); }
                }
            });
                _currentThread = new Thread(ts);
                _currentThread.SetApartmentState(ApartmentState.STA);
                _currentThread.IsBackground = true;
                _currentThread.Start();
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
                var currentCategoryList = _mWorkPart.LayerCategories.ToArray().ToList();
                currentCategoryList = currentCategoryList.Where(x => x.Name != NxMainCategory).ToList();
                var layers = new List<int>();
                currentCategoryList.ForEach(x => { layers.AddRange(x.GetMemberLayers().ToList()); });
                var allLayers = _mWorkPart.LayerCategories.FindObject(NxMainCategory).GetMemberLayers().ToList();
                var resultArray = allLayers.Distinct().Except(layers).ToList();
                resultArray.Remove(WorkLayer);
                var z = 0;
                for (var i = 0; i < resultArray.Count(); ++i)
                {
                    if (z == requestLayersCount) break;
                    if (resultArray[i] > MaxLayersCount) throw new Exception("Недостаточно свободных слоев без обьектов для создания категории");
                    if (_mWorkPart.Layers.GetAllObjectsOnLayer(resultArray[i]).Any()) continue;
                    freeLayers[z] = resultArray[i];
                    ++z;
                    _mProgressBarIncreaseCallback(true);
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
                            _controlStateCallback(false);
                            var totalLayersRequest = layersQuantity * names.Count;
                            var freeLayers = GetLayersWithoutObjects(totalLayersRequest);
                            var freeLayersGroups = GetApartedArray(freeLayers, names.Count);
                            for (var i = 0; i < names.Count; ++i)
                            {
                                var category = _mWorkPart.LayerCategories.FindObject(names[i]);
                                var currentLayers = category.GetMemberLayers().ToList().Concat(freeLayersGroups[i].ToList());
                                category.SetMemberLayers(currentLayers.ToArray());
                                _updateCategoriesCallback(category.Name);
                            }
                            _prBarResetCallback(true);
                            _controlStateCallback(true);
                        }
                        catch (ThreadAbortException) { }
                        catch (Exception ex) { ExceptionActions(ex); }
                    }
                }
                catch (ThreadAbortException) { }
                catch (Exception ex) { ExceptionActions(ex); }
            });
                _currentThread = new Thread(ts);
                _currentThread.SetApartmentState(ApartmentState.STA);
                _currentThread.IsBackground = true;
                _currentThread.Start();
            }
            catch (ThreadAbortException) { }
            catch (Exception ex) { ExceptionActions(ex); }
        }

        //=====================================================================
        //Разделение массива на несколько частей согласно количеству 
        //выделенных категорий
        //=====================================================================
        private List<int[]> GetApartedArray(IReadOnlyList<int> apartArray, int partsQuantity)
        {
            if (apartArray == null) throw new ArgumentNullException(nameof(apartArray));
            if (partsQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(partsQuantity));
            var aparted = new List<int[]>();
            try
            {
                //if (partsQuantity < 1) Thread.CurrentThread.Abort();
                if (partsQuantity < 1) throw new Exception("функция getApartArray() аргумент меньше 0");
                var layersInPart = apartArray.Count / partsQuantity;
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
                _prBarResetCallback(true);
                _exceptionCallback(ex);
                _controlStateCallback(true);
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
                                var item = _mWorkPart.LayerCategories.FindObject(x.Name);
                                if (item != null) categories.Add(item);
                            });
                            categories.ForEach(x =>
                            {
                                var layers = new List<int>();
                                var memberLayers = x.GetMemberLayers().ToList();
                                memberLayers.ForEach(z =>
                                {
                                    switch (val)
                                    {
                                        case 0:
                                            if (_mWorkPart.Layers.GetAllObjectsOnLayer(z).Length != 0) layers.Add(z);
                                            break;
                                        case 1:
                                            if (_mWorkPart.Layers.GetAllObjectsOnLayer(z).Length == 0) layers.Add(z);
                                            break;
                                    }
                                });
                                x.SetMemberLayers(layers.ToArray());
                                _updateCategoriesCallback(x.Name);
                            });
                        }
                    }
                    catch (ThreadAbortException) { }
                    catch (Exception)
                    {
                        // ignored
                    }
                });
                _currentThread = new Thread(ts);
                _currentThread.SetApartmentState(ApartmentState.STA);
                _currentThread.IsBackground = true;
                _currentThread.Start();
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