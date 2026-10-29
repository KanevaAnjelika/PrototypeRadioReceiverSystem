using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PrototypeRadioReceiverSystem
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Флаг текущей темы: false = светлая, true = тёмная
        private bool _isDarkTheme = false;

        // Флаг подключения (заглушка, без реального SerialPort)
        private bool _isConnected = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        #region Тема

        // Переключение светлой/тёмной темы
        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            _isDarkTheme = !_isDarkTheme;
            ApplyTheme(_isDarkTheme);
            ThemeButton.Content = _isDarkTheme ? "☀" : "🌙";
        }

        // Применяет тему к ресурсам приложения — влияет и на другие окна
        private void ApplyTheme(bool dark)
        {
            var res = Application.Current.Resources;

            if (dark)
            {
                // Тёмная тема
                res["WindowBgBrush"] = MakeBrush(0x7F, 0x4E, 0xA8);
                res["PanelBrush"]    = MakeBrush(0xBA, 0x8C, 0xBE);
                res["PrimaryButtonBrush"] = MakeBrush(0xC7, 0xA3, 0xD2);
                res["PrimaryButtonFgBrush"] = MakeBrush(0x33, 0x33, 0x33);
                res["AccentBrush"]   = MakeBrush(0x9C, 0x93, 0xE5);
                res["TextBrush"]     = Brushes.White;
            }
            else
            {
                // Светлая тема
                res["WindowBgBrush"] = MakeBrush(0xF5, 0xED, 0xF0);
                res["PanelBrush"]    = MakeBrush(0xDC, 0xBD, 0xDF);
                res["PrimaryButtonBrush"] = MakeBrush(0x9A, 0x99, 0xE1);
                res["PrimaryButtonFgBrush"] = Brushes.White;
                res["AccentBrush"]   = MakeBrush(0x00, 0x8B, 0xD0);
                res["TextBrush"]     = MakeBrush(0x3A, 0x2B, 0x45);
            }
        }

        private static SolidColorBrush MakeBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        #endregion

        #region Подключение (заглушка)

        // Заглушка: переключает текст кнопки и цвет индикатора
        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            ConnectToPort();
        }

        private void ConnectToPort()
        {
            _isConnected = !_isConnected;

            if (_isConnected)
            {
                ConnectButton.Content = "Отключиться";
                StatusIndicator.Fill = MakeBrush(0x00, 0xC8, 0x53); // зелёный
            }
            else
            {
                ConnectButton.Content = "Подключиться";
                StatusIndicator.Fill = MakeBrush(0x9E, 0x9E, 0x9E); // серый
            }
        }

        #endregion

        #region Кнопки управления (заглушки)

        private void FreqUpButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: отправка команды "UP" в COM-порт
        }

        private void FreqDownButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: отправка команды "DOWN" в COM-порт
        }

        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: отправка команды "SCAN" в COM-порт
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: сохранение частоты в избранное
        }

        #endregion

        #region Навигация

        // Открыть окно избранного
        private void FavoritesButton_Click(object sender, RoutedEventArgs e)
        {
            var favoritesWindow = new FavoritesWindow { Owner = this };
            favoritesWindow.ShowDialog();
        }

        // Открыть журнал операций
        private void LogButton_Click(object sender, RoutedEventArgs e)
        {
            var logWindow = new LogWindow { Owner = this };
            logWindow.ShowDialog();
        }

        #endregion

        #region Управление окном

        // Перетаскивание окна за шапку
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion
    }
}
