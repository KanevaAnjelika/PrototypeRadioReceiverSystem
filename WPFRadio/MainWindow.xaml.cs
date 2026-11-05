using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PrototypeRadioReceiverSystem
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Основной контроллер — связывает интерфейс с COM-портом, данными и журналом.
        /// </summary>
        private MainController _mainController = new MainController();

        /// <summary>
        /// Таймер для опроса Arduino (обновляет частоту и уровень сигнала каждые 500 мс).
        /// </summary>
        private DispatcherTimer _pollTimer;

        private bool _isDarkTheme = false;

        public MainWindow()
        {
            InitializeComponent();

            // Создаём таймер: каждые 500 мс опрашиваем порт и обновляем UI
            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _pollTimer.Tick += PollTimer_Tick;
            _pollTimer.Start();

            // При закрытии окна останавливаем таймер и закрываем порт
            Closed += MainWindow_Closed;
        }

        /// <summary>
        /// Обработчик таймера: опрашивает порт и обновляет отображение.
        /// </summary>
        private void PollTimer_Tick(object sender, EventArgs e)
        {
            // Если порт не подключён — ничего не делаем
            if (!_mainController.IsConnected)
                return;

            // Читаем данные из порта (UpdateDataFromArduino обновляет _radioData)
            _mainController.UpdateDataFromArduino();

            // Обновляем интерфейс
            FrequencyTextBlock.Text = _mainController.Data.GetFrequencyDisplay();
            SignalTextBlock.Text = _mainController.Data.GetSignalDisplay();
        }

        /// <summary>
        /// При закрытии окна останавливаем таймер и отключаемся от порта.
        /// </summary>
        private void MainWindow_Closed(object sender, EventArgs e)
        {
            _pollTimer.Stop();
            if (_mainController.IsConnected)
                _mainController.Disconnect();
        }

        #region Тема

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            _isDarkTheme = !_isDarkTheme;
            ApplyTheme(_isDarkTheme);
            ThemeButton.Content = _isDarkTheme ? "☀" : "🌙";
        }

        private void ApplyTheme(bool dark)
        {
            var res = Application.Current.Resources;

            if (dark)
            {
                res["WindowBgBrush"] = MakeBrush(0x7F, 0x4E, 0xA8);
                res["PanelBrush"] = MakeBrush(0xBA, 0x8C, 0xBE);
                res["PrimaryButtonBrush"] = MakeBrush(0xC7, 0xA3, 0xD2);
                res["PrimaryButtonFgBrush"] = Brushes.White;
                res["AccentBrush"] = MakeBrush(0x9C, 0x93, 0xE5);
                res["AccentButtonFgBrush"] = Brushes.White;
                res["TextBrush"] = Brushes.White;
                res["TabActiveBrush"] = Brushes.White;
                // ComboBox: чёрный фон, белый текст, обводка #7F4EA8
                res["ComboBoxBgBrush"] = MakeBrush(0x00, 0x00, 0x00);
                res["ComboBoxBorderBrush"] = MakeBrush(0x7F, 0x4E, 0xA8);
                res["ComboBoxFgBrush"] = Brushes.White;
                // Обводка кнопок
                res["ButtonBorderBrush"] = MakeBrush(0x9C, 0x93, 0xE5);
                // Карточки
                res["CardBgBrush"] = MakeBrush(0xC7, 0xA3, 0xD2);
                res["CardBorderBrush"] = MakeBrush(0x9C, 0x93, 0xE5);
                res["CardHeaderBrush"] = MakeBrush(0x9C, 0x93, 0xE5);
            }
            else
            {
                res["WindowBgBrush"] = MakeBrush(0xF5, 0xED, 0xF0);
                res["PanelBrush"] = MakeBrush(0xDC, 0xBD, 0xDF);
                res["PrimaryButtonBrush"] = MakeBrush(0x9A, 0x99, 0xE1);
                res["PrimaryButtonFgBrush"] = Brushes.White;
                res["AccentBrush"] = MakeBrush(0x00, 0x8B, 0xD0);
                res["AccentButtonFgBrush"] = Brushes.White;
                res["TextBrush"] = MakeBrush(0x3A, 0x2B, 0x45);
                res["TabActiveBrush"] = MakeBrush(0x00, 0x8B, 0xD0);
                // ComboBox: белый фон, текст #333333, обводка #F5EDF0
                res["ComboBoxBgBrush"] = Brushes.White;
                res["ComboBoxBorderBrush"] = MakeBrush(0xF5, 0xED, 0xF0);
                res["ComboBoxFgBrush"] = MakeBrush(0x33, 0x33, 0x33);
                // Обводка кнопок
                res["ButtonBorderBrush"] = Brushes.Black;
                // Карточки
                res["CardBgBrush"] = Brushes.White;
                res["CardBorderBrush"] = MakeBrush(0x99, 0x99, 0x99);
                res["CardHeaderBrush"] = MakeBrush(0x9A, 0x99, 0xE1);
            }
        }

        private static SolidColorBrush MakeBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        #endregion

        #region Подключение

        /// <summary>
        /// Кнопка подключения/отключения COM-порта.
        /// </summary>
        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            // Если уже подключены — отключаемся
            if (_mainController.IsConnected)
            {
                _mainController.Disconnect();
                ConnectButton.Content = "Подключиться";
                StatusIndicator.Fill = MakeBrush(0x9E, 0x9E, 0x9E); // серый
                return;
            }

            // Не подключены — пытаемся подключиться
            // Берём выбранный порт из ComboBox
            ComboBoxItem selectedItem = PortComboBox.SelectedItem as ComboBoxItem;
            string portName = selectedItem != null
                ? selectedItem.Content as string
                : null;

            if (string.IsNullOrEmpty(portName))
                return;

            // Подключаемся через контроллер
            if (_mainController.ConnectToPort(portName))
            {
                // Успех — меняем текст кнопки и индикатор на зелёный
                ConnectButton.Content = "Отключиться";
                StatusIndicator.Fill = MakeBrush(0x00, 0xC8, 0x53);
            }
            else
            {
                // Ошибка — оставляем текст «Подключиться» и индикатор серый
                ConnectButton.Content = "Подключиться";
                StatusIndicator.Fill = MakeBrush(0x9E, 0x9E, 0x9E);
            }
        }

        #endregion

        #region Кнопки управления

        private void FreqUpButton_Click(object sender, RoutedEventArgs e)
        {
            _mainController.SendCommand("UP");
        }

        private void FreqDownButton_Click(object sender, RoutedEventArgs e)
        {
            _mainController.SendCommand("DOWN");
        }

        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            _mainController.SendCommand("SCAN");
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _mainController.SendCommand("SAVE");
        }

        #endregion

        #region Навигация

        private void FavoritesButton_Click(object sender, RoutedEventArgs e)
        {
            // Передаём _mainController в окно — чтобы оно могло читать данные и журнал
            FavoritesWindow favoritesWindow = new FavoritesWindow(_mainController) { Owner = this };
            favoritesWindow.ShowDialog();
        }

        private void LogButton_Click(object sender, RoutedEventArgs e)
        {
            // Передаём _mainController в окно — чтобы оно могло показывать журнал
            LogWindow logWindow = new LogWindow(_mainController) { Owner = this };
            logWindow.ShowDialog();
        }

        #endregion

        #region Управление окном

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion
    }
}
