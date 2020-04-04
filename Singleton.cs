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

        public string Date { get; private set; }
        public const string InputTextCategory = "введите имя категории";
        public const string InputTextGroup = "введите имя группы";
        public static object locker;
        private Singleton()
        {
            locker = new object();
            Date = System.DateTime.Now.TimeOfDay.ToString();
        }

        public static Singleton GetInstance()
        {
            return instance;
        }
    }
}
