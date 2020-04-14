using System.Windows;

namespace NXLM
{
    public class Singleton
    {
        private static readonly Singleton Instance = new Singleton();

        public const string InputTextCategory = "введите имя категории";
        public const string InputTextGroup = "введите имя группы";
        public const string NxMainCategory = "ALL";
        public const int WorkLayer = 1;
        public const int MaxLayersCount = 256;
        private Singleton() { }

        public static Singleton GetInstance()
        {
            
            return Instance;
        }
    }
}