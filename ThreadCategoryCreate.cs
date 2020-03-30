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
        private NXOpen.Session theSession;
        private NXOpen.Part workPart;

        //Кэлбэки для общения с основным потоком
        private ProgressBarIncreaseCallback increaseCallback; //Увеличение значения прогресбара на 1
        private ProgressBarResetCallback resetCallback; //Обнуление прогрессбара
        private ExceptionCallback exCallback; //Вывод сообщения ошибки в основной поток

        private const string NxMainGategory = "ALL";
        private const int unusedLayer = 1;
        private const int maxLayersCount = 256;

        public ThreadCategoryCreate(
            ProgressBarIncreaseCallback cback,
            ProgressBarResetCallback rback,
            ExceptionCallback eback)
        {
            theSession = NXOpen.Session.GetSession();
            workPart = theSession.Parts.Work;

            increaseCallback = cback;
            resetCallback = rback;
            exCallback = eback;
        }

        public void createListCategories(List<Category> Group)
        {
            Thread t = getMultiplyThread(Group);
            t.Start();
        }

        private Thread getMultiplyThread(List<Category> Group)
        {
            ThreadStart ts = new ThreadStart(() =>
            {
                try
                {
                    if (Group.Count == 0) throw new Exception("Количество категорий меньше 1");
                    var currentCategoryList = workPart.LayerCategories.ToArray().ToList();
                    int requestLayersCount = 0;
                    Group.ForEach(x => {
                        currentCategoryList.ForEach(element => { 
                            if (element.Name == x.Name) throw new Exception("Имя категории уже существует"); 
                        });
                        requestLayersCount += x.LayCount; 
                    });
                    currentCategoryList = currentCategoryList.Where(x => x.Name != NxMainGategory).ToList();
                    var layers = new List<int>();
                    currentCategoryList.ForEach(x => { layers.AddRange(x.GetMemberLayers().ToList()); });
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
                            increaseCallback(true);
                        }
                    }
                    int w = 0;
                    Group.ForEach(x=> {
                        int[] arr = new int[x.LayCount];
                        for (int i = 0; i < arr.Length; i++)
                        {
                            arr[i] = freeLayers[w];
                            ++w;
                        }
                        workPart.LayerCategories.CreateCategory(x.Name, "", arr);
                    });
                    resetCallback(true);
                }
                catch (Exception ex) { exCallback(ex); }
            });
            Thread t = new Thread(new ThreadStart(ts));
            return t;
        }
    }

    public delegate void ProgressBarIncreaseCallback(Boolean val);
    public delegate void ProgressBarResetCallback(Boolean val);
    public delegate void ExceptionCallback(Exception ex);
}