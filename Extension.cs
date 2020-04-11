using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NXLM
{
    //=====================================================================
    //Расширения для стандартных контролов
    //=====================================================================
    public static class Extension
    {
        /*Расширение для TextBox принимающего строковые значения:
        Расширение отображает строку приглашения ввода, и управляет состояниями поля ввода и связанной кнопки
        в зависимости от содержимого поля ввода*/
        public static void SetStringTextBoxBehavior(this TextBox textBox, string defaultMessage, Button relativeButton)
        {
            var message = defaultMessage; //Текст приглашения ввода текста в TwxtBox
            const string nullString = ""; //Пустой текст
            const int zero = 0; //Ну тут очевидно
            var colorBlack = Brushes.Black; //Цвет активного текста
            var colorLightGray = Brushes.LightGray; // Цвет неактивного текста

            textBox.TextChanged += (se, ea) =>
            {
                if (textBox.Text.Length == zero || textBox.Text == defaultMessage) relativeButton.IsEnabled = false;
                else
                {
                    textBox.Foreground = colorBlack;
                    relativeButton.IsEnabled = true;
                }
            };

            textBox.MouseEnter += (se, ea) => {if (textBox.Text == message) textBox.Text = nullString;};

            textBox.LostFocus += (se, ea) => 
            {
                if (textBox.Text.Length == zero)
                {
                    textBox.Text = message;
                    textBox.Foreground = colorLightGray; 
                }
            };

            textBox.MouseLeave += (se, ea) =>
            {
                if (textBox.Text.Length != zero || textBox.IsSelectionActive) return;
                textBox.Text = message;
                textBox.Foreground = colorLightGray;
                relativeButton.IsEnabled = false;
            };
        }

        public static void SetIntTextBoxBehavior(this TextBox textBox, string defaulValue)
        {
            var textBoxLenght = 3; //Максимальная длина поля ввода
            textBox.MaxLength = textBoxLenght;
            var defValue = defaulValue;
            textBox.KeyDown += (se, ea) =>
            {
                var allowedKeys = new List<Key>() { Key.D0, Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, 
                    Key.D6, Key.D7, Key.D8, Key.D9, Key.NumPad0, Key.NumPad1, Key.NumPad2, Key.NumPad3,Key.NumPad4, 
                    Key.NumPad5, Key.NumPad6, Key.NumPad7, Key.NumPad8, Key.NumPad9};
                if (!allowedKeys.Contains(ea.Key)) ea.Handled = true;
            };

            textBox.TextChanged += (se, sa) => { };

            textBox.MouseLeave += (se, sa) => 
            {
                if (textBox.Text.Length != 0 && textBox.Text != "0") return;
                textBox.Text = defValue;
                textBox.Select(textBox.Text.Length, 0);
            };
        }
    }
}