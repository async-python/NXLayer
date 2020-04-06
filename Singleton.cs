using NXOpen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp3
{
    public class Singleton
    {
        private static readonly Singleton instance = new Singleton();

        public const string InputTextCategory = "введите имя категории";
        public const string InputTextGroup = "введите имя группы";
        public const string NxMainGategory = "ALL";
        public const int WorkLayer = 1;
        public const int maxLayersCount = 256;
        private Singleton() { }

        public static Singleton GetInstance()
        {
            return instance;
        }
    }
}
