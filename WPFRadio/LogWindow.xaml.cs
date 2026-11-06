using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace PrototypeRadioReceiverSystem
{
    /// <summary>
    /// Логика взаимодействия для LogWindow.xaml
    /// </summary>
    public partial class LogWindow : Window
    {
        private readonly MainController _mainController;
        private readonly DispatcherTimer _refreshTimer;
        private bool _showActions = true;

        public LogWindow(MainController controller)
        {
            InitializeComponent();

            _mainController = controller;

            // Таймер автообновления журнала пока окно открыто
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _refreshTimer.Tick += (s, e) => RefreshLog();
            _refreshTimer.Start();

            UpdateTabColors();
            RefreshLog();
        }

        /// <summary>Обновляет карточки журнала из реального контроллера.</summary>
        private void RefreshLog()
        {
            if (_mainController == null) return;

            List<LogEntry> actions = new List<LogEntry>();
            List<LogEntry> errors = new List<LogEntry>();

            string today = DateTime.Now.ToString("dd.MM.yyyy");

            // Берём все записи из журнала контроллера
            List<string> logs = _mainController.OperationLog.Logs;

            foreach (string record in logs)
            {
                LogEntry entry = ParseRecord(record, today);

                // Записи со словом «ошибка» — на вкладку Ошибки
                if (entry.Event.IndexOf("ошибк", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    entry.Event.StartsWith("Ошибка Arduino:") ||
                    entry.Event.StartsWith("Ошибка парсинга:"))
                    errors.Add(entry);
                else
                    actions.Add(entry);
            }

            _actions = actions;
            _errors = errors;

            LogListBox.ItemsSource = _showActions ? _actions : _errors;
        }

        private static LogEntry ParseRecord(string record, string date)
        {
            int closeBracket = record.IndexOf(']');

            if (record.StartsWith("[") && closeBracket > 0)
            {
                return new LogEntry
                {
                    Date = date,
                    Time = record.Substring(1, closeBracket - 1),
                    Event = record.Substring(closeBracket + 1).Trim()
                };
            }

            return new LogEntry { Date = date, Time = "", Event = record };
        }

        private void UpdateTabColors()
        {
            var grayBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));

            if (_showActions)
            {
                ActionsTab.SetResourceReference(Button.ForegroundProperty, "TabActiveBrush");
                ErrorsTab.SetValue(Button.ForegroundProperty, grayBrush);
            }
            else
            {
                ActionsTab.SetValue(Button.ForegroundProperty, grayBrush);
                ErrorsTab.SetResourceReference(Button.ForegroundProperty, "TabActiveBrush");
            }
        }

        private void ActionsTab_Click(object sender, RoutedEventArgs e)
        {
            _showActions = true;
            LogListBox.ItemsSource = _actions;
            UpdateTabColors();
        }

        private void ErrorsTab_Click(object sender, RoutedEventArgs e)
        {
            _showActions = false;
            LogListBox.ItemsSource = _errors;
            UpdateTabColors();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mainController != null)
                _mainController.OperationLog.Clear();

            LogListBox.ItemsSource = null;
            RefreshLog();
            ShowToast("Журнал очищен", 0x75, 0x75, 0x75);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"),
                "operations_log.txt");

            // Создаём папку logs, если нет
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            if (_mainController != null && _mainController.OperationLog.SaveToFile(path))
                ShowToast("Журнал сохранён: " + path, 0x4C, 0xAF, 0x50);
            else
                ShowToast("Не удалось сохранить журнал", 0xE5, 0x39, 0x35);
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            _refreshTimer.Stop();
            Close();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void ShowToast(string message, byte r, byte g, byte b)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xCC, r, g, b)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 4, 0, 0),
                Opacity = 0,
                Child = new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.White,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold
                }
            };
            ToastPanel.Children.Add(border);

            var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(300));
            border.BeginAnimation(Border.OpacityProperty, fadeIn);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300));
                fadeOut.Completed += (s2, ev2) => ToastPanel.Children.Remove(border);
                border.BeginAnimation(Border.OpacityProperty, fadeOut);
            };
            timer.Start();
        }

        // Поля для списков (нужны для переключения вкладок)
        private List<LogEntry> _actions = new List<LogEntry>();
        private List<LogEntry> _errors = new List<LogEntry>();
    }

    public class LogEntry
    {
        public string Date { get; set; }
        public string Time { get; set; }
        public string Event { get; set; }
    }
}
