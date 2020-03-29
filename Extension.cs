using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfApp3
{
    //=====================================================================
    //Расширения для стандартных контролов
    //=====================================================================
    public static class Extension
    {
        /*Расширение для TextBox принимающего строковые значения:
        Расширение отображает строку приглашения ввода, и управляет состояниями поля ввода и связанной кнопки
        в зависимости от содержимого поля ввода*/
        public static void setStringTextBoxBehavior(this TextBox textBox, string defaultMessage, Button relativeButton)
        {
            string Message = defaultMessage; //Текст приглашения ввода текста в TwxtBox
            string nullString = ""; //Пустой текст
            int zero = 0; //Ну тут очевидно
            SolidColorBrush colorBlack = Brushes.Black; //Цвет активного текста
            SolidColorBrush colorLightGray = Brushes.LightGray; // Цвет неактивного текста

            textBox.TextChanged += (se, ea) =>
            {
                if (textBox.Text.Length == zero || textBox.Text == defaultMessage) relativeButton.IsEnabled = false;
                else
                {
                    textBox.Foreground = colorBlack;
                    relativeButton.IsEnabled = true;
                }
            };

            textBox.MouseEnter += (se, ea) => {if (textBox.Text == Message) textBox.Text = nullString;};

            textBox.LostFocus += (se, ea) => 
            {
                if (textBox.Text.Length == zero)
                {
                    textBox.Text = Message;
                    textBox.Foreground = colorLightGray; 
                }
            };

            textBox.MouseLeave += (se, ea) =>
            {
                if (textBox.Text.Length == zero && !textBox.IsSelectionActive) 
                {
                        textBox.Text = Message;
                        textBox.Foreground = colorLightGray;
                        relativeButton.IsEnabled = false;
                } 
            };
        }

        public static void setIntTextBoxBehavior(this TextBox textBox, string defaulValue)
        {
            int textBoxLenght = 3; //Максимальная длина поля ввода
            textBox.MaxLength = textBoxLenght;
            string defValue = defaulValue;
            textBox.KeyDown += (se, ea) =>
            {
                char ch = ((char)KeyInterop.VirtualKeyFromKey(ea.Key));
                if (!char.IsDigit(ch)) ea.Handled = true;
            };

            textBox.TextChanged += (se, sa) => { };

            textBox.MouseLeave += (se, sa) => 
            {
                if (textBox.Text.Length == 0 || textBox.Text == "0") textBox.Text = defValue;
            };
        }
    }
}