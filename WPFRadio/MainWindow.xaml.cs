using System;
using System.Collections.Generic;
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
        private readonly MainController _mainController = new MainController();
        private readonly DispatcherTimer _pollTimer;
        private bool _isDarkTheme = false;

        public MainWindow()
        {
            InitializeComponent();

            // Загружаем доступные COM-порты
            LoadPorts();

            // Таймер опроса Arduino — каждые 500мс
            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _pollTimer.Tick += PollTimer_Tick;
            _pollTimer.Start();

            // Событие: частота изменилась
            _mainController.FrequencyChanged += OnFrequencyChanged;

            // При закрытии — отключаемся
            Closed += MainWindow_Closed;
        }

        #region Порты

        private void LoadPorts()
        {
            PortComboBox.Items.Clear();
            string[] ports = SecondController.GetAvailablePorts();
            foreach (string port in ports)
            {
                ComboBoxItem item = new ComboBoxItem { Content = port };
                PortComboBox.Items.Add(item);
            }

            // Если портов нет — показываем подсказку
            if (ports.Length == 0)
            {
                ComboBoxItem hint = new ComboBoxItem { Content = "Нет портов" };
                hint.IsEnabled = false;
                PortComboBox.Items.Add(hint);
            }
        }

        private void RefreshPorts()
        {
            string selected = PortComboBox.SelectedValue as string;
            LoadPorts();
            if (!string.IsNullOrEmpty(selected))
                PortComboBox.SelectedItem = PortComboBox.Items.Contains(selected)
                    ? PortComboBox.Items.GetItemAt(Array.IndexOf(SecondController.GetAvailablePorts(), selected))
                    : null;
        }

        #endregion

        #region Обновление UI

        private void PollTimer_Tick(object sender, EventArgs e)
        {
            if (!_mainController.IsConnected)
                return;

            // Забираем данные из Serial-порта
            _mainController.PollArduino();

            // Обновляем текстовые поля из модели данных (INotifyPropertyChanged)
            FrequencyTextBlock.Text = _mainController.Data.FrequencyDisplay;
            SignalTextBlock.Text = _mainController.Data.SignalDisplay;

            // Обновляем статус-индикатор по уровню сигнала
            int sig = _mainController.Data.SignalLevel;
            if (sig >= 7)
                StatusIndicator.Fill = MakeBrush(0x00, 0xC8, 0x53);   // зелёный
            else if (sig >= 3)
                StatusIndicator.Fill = MakeBrush(0xFF, 0xA0, 0x00);   // оранжевый
            else
                StatusIndicator.Fill = MakeBrush(0xE5, 0x39, 0x35);  // красный
        }

        private void OnFrequencyChanged()
        {
            // Частота изменилась — обновить UI (уже в pollTimer, но можно и отдельно)
            Dispatcher.BeginInvoke(new Action(() =>
            {
                FrequencyTextBlock.Text = _mainController.Data.FrequencyDisplay;
                SignalTextBlock.Text = _mainController.Data.SignalDisplay;
            }));
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            _pollTimer.Stop();
            if (_mainController.IsConnected)
                _mainController.Disconnect();
        }

        #endregion

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
                res["ComboBoxBgBrush"] = MakeBrush(0x00, 0x00, 0x00);
                res["ComboBoxBorderBrush"] = MakeBrush(0x7F, 0x4E, 0xA8);
                res["ComboBoxFgBrush"] = Brushes.White;
                res["ButtonBorderBrush"] = MakeBrush(0x9C, 0x93, 0xE5);
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
                res["ComboBoxBgBrush"] = Brushes.White;
                res["ComboBoxBorderBrush"] = MakeBrush(0xF5, 0xED, 0xF0);
                res["ComboBoxFgBrush"] = MakeBrush(0x33, 0x33, 0x33);
                res["ButtonBorderBrush"] = Brushes.Black;
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

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mainController.IsConnected)
            {
                _mainController.Disconnect();
                ConnectButton.Content = "Подключиться";
                StatusIndicator.Fill = MakeBrush(0x9E, 0x9E, 0x9E);
                SetButtonsEnabled(false);
                return;
            }

            ComboBoxItem selectedItem = PortComboBox.SelectedItem as ComboBoxItem;
            string portName = selectedItem != null
                ? selectedItem.Content as string
                : null;

            if (string.IsNullOrEmpty(portName) || portName == "Нет портов")
                return;

            if (_mainController.ConnectToPort(portName))
            {
                ConnectButton.Content = "Отключиться";
                StatusIndicator.Fill = MakeBrush(0x00, 0xC8, 0x53);
                SetButtonsEnabled(true);
            }
            else
            {
                ConnectButton.Content = "Подключиться";
                StatusIndicator.Fill = MakeBrush(0x9E, 0x9E, 0x9E);
            }
        }

        private void SetButtonsEnabled(bool enabled)
        {
            FreqUpButton.IsEnabled = enabled;
            FreqDownButton.IsEnabled = enabled;
            ScanButton.IsEnabled = enabled;
            SaveButton.IsEnabled = enabled;
            FavoritesButton.IsEnabled = enabled;
        }

        #endregion

        #region Кнопки управления

        private void FreqUpButton_Click(object sender, RoutedEventArgs e)
        {
            _mainController.StepUp();
        }

        private void FreqDownButton_Click(object sender, RoutedEventArgs e)
        {
            _mainController.StepDown();
        }

        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            _mainController.Scan();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Сохраняем текущую частоту в EEPROM (Arduino)
            _mainController.SaveStation();

            // Добавляем в WPF-избранное с именем по умолчанию
            _mainController.AddCurrentToFavorites(
                "Станция " + _mainController.Data.CurrentFrequency.ToString("F1"));
        }

        #endregion

        #region Навигация

        private void FavoritesButton_Click(object sender, RoutedEventArgs e)
        {
            FavoritesWindow favoritesWindow = new FavoritesWindow(_mainController) { Owner = this };
            favoritesWindow.ShowDialog();

            // После закрытия окна избранного — обновить список портов
            // (Arduino мог отключиться/переподключиться)
        }

        private void LogButton_Click(object sender, RoutedEventArgs e)
        {
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
