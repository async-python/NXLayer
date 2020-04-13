using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Prism.Mvvm;

namespace NXLM
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
        private string _name;
        private int _layerCount;

        public Category() { }
        public Category(string name, int layCount)
        {
            this._name = name;
            this._layerCount = layCount;
        }

        public string Name 
        {
            set 
            { 
                _name = value;
                RaisePropertyChanged(nameof(_name));
            }

            get => _name;
        }

        public int LayCount 
        {
            set 
            { 
                _layerCount = value;
                RaisePropertyChanged(nameof(LayCount));
            }
            get => _layerCount;
        }
    }
    //=====================================================================
    //Класс шаблон параметров групп категорий
    //=====================================================================
    public class GroupTemplate
    {
        public GroupTemplate(string name, int count, int layCount)
        {
            this.Name = name;
            this.Count = count;
            this.LayCount = layCount;
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
        private readonly List<GroupTemplate> _mCategoriesTemplate = new List<GroupTemplate>();

        public CategoryConfigurator()
        {
            _mCategoriesTemplate.Add(new GroupTemplate("01_TEH", 1, 1));
            _mCategoriesTemplate.Add(new GroupTemplate("03_ZAG", 5, 1));
            _mCategoriesTemplate.Add(new GroupTemplate("04_OSN", 5, 1));
            _mCategoriesTemplate.Add(new GroupTemplate("06_OBR", 5, 12));
        }

        public List<Category> GetCategoryGroup(string groupCategoriesName, int categoriesCount, int layerCountInCategory) //возвращает сконфигурированную группу категорий
        {
            var categoryGroup = new List<Category>();
            if (categoriesCount <= 0) throw new Exception("Количесто категорий меньше 1");
            for (var i = 1; i < categoriesCount + 1; ++i)
            {
                var category = new Category();
                if (categoriesCount == 1) category.Name = groupCategoriesName;
                else category.Name = groupCategoriesName + "_" + i;
                category.LayCount = layerCountInCategory;
                categoryGroup.Add(category);
            }
            return categoryGroup;
        }

        public List<Category> GetGroupTemplate() //возвращает сконфигурированный шаблон с группами категорий согласно перечню CategoriesTemplate
        {
            var groupTemplate = new List<Category>();
            for (var i = 0; i < _mCategoriesTemplate.Count(); ++i)
            {
                var temp = GetCategoryGroup(
                    _mCategoriesTemplate[i].Name, 
                    _mCategoriesTemplate[i].Count, 
                    _mCategoriesTemplate[i].LayCount);
                groupTemplate.AddRange(temp);
            }
            return groupTemplate;
        }
    }
}