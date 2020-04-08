using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using WpfApp3.ViewModel;

namespace WpfApp3
{
    public class CategoryList
    {
        public ObservableCollection<Category> List { get; set; } = new ObservableCollection<Category>();
    }
    //=====================================================================
    //Класс шаблон параметров категории слоев
    //=====================================================================
    public class Category : BindableBase
    {
        private string name;
        private int laycount;

        public Category() { }
        public Category(string Name, int LayCount)
        {
            this.name = Name;
            this.laycount = LayCount;
        }

        public string Name 
        {
            set 
            { 
                name = value;
                RaisePropertyChanged(nameof(name));
            }

            get { return name; }
        }

        public int LayCount 
        {
            set 
            { 
                laycount = value;
                RaisePropertyChanged(nameof(LayCount));
            }
            get { return laycount; }
        }
    }
    //=====================================================================
    //Класс шаблон параметров групп категорий
    //=====================================================================
    public class GroupTemplate
    {
        public GroupTemplate(string Name, int Count, int LayCount)
        {
            this.Name = Name;
            this.Count = Count;
            this.LayCount = LayCount;
        }
        public string Name { get; set; } //Имя группы категорий
        public int Count { get; set; } //Количество категорий в группе
        public int LayCount { get; set; } //Количество слоев в категории
    }
    //=====================================================================
    //Конфигуратор групп категорий
    //=====================================================================
    public class CategoryConfigurator
    {
        private List<GroupTemplate> CategoriesTemplate = new List<GroupTemplate>();

        public CategoryConfigurator()
        {
            CategoriesTemplate.Add(new GroupTemplate("01_TEH", 1, 1));
            CategoriesTemplate.Add(new GroupTemplate("03_ZAG", 5, 1));
            CategoriesTemplate.Add(new GroupTemplate("04_OSN", 5, 1));
            CategoriesTemplate.Add(new GroupTemplate("06_OBR", 5, 12));
        }

        public List<Category> getCategoryGroup(string groupCategoriesName, int categoriesCount, int layerCountInCategory) //возвращает сконфигурированную группу категорий
        {
            List<Category> CategoryGroup = new List<Category>();
            if (categoriesCount <= 0) throw new Exception("Количесто категорий меньше 1");
            for (int i = 1; i < categoriesCount + 1; ++i)
            {
                Category category = new Category();
                if (categoriesCount == 1) category.Name = groupCategoriesName;
                else category.Name = groupCategoriesName + "_" + i;
                category.LayCount = layerCountInCategory;
                CategoryGroup.Add(category);
            }
            return CategoryGroup;
        }

        public List<Category> getGroupTemplate() //возвращает сконфигурированный шаблон с группами категорий согласно перечню CategoriesTemplate
        {
            List<Category> GroupTemplate = new List<Category>();
            for (int i = 0; i < CategoriesTemplate.Count(); ++i)
            {
                List<Category> temp = getCategoryGroup(CategoriesTemplate[i].Name, CategoriesTemplate[i].Count, CategoriesTemplate[i].LayCount);
                foreach (Category X in temp)
                {
                    GroupTemplate.Add(X);
                }
            }
            return GroupTemplate;
        }

    }
}
