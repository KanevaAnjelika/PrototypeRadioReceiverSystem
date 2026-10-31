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
        private List<LogEntry> _actions = new List<LogEntry>();
        private List<LogEntry> _errors = new List<LogEntry>();
        private bool _showActions = true;

        public LogWindow()
        {
            InitializeComponent();
            LoadDemoData();
            UpdateTabColors();
            LogListBox.ItemsSource = _actions;
        }

        private void LoadDemoData()
        {
            _actions = new List<LogEntry>
            {
                new LogEntry { Date = "28.10.2026", Time = "14:32", Event = "Частота + 101.5 МГц" },
                new LogEntry { Date = "28.10.2026", Time = "14:30", Event = "Подключение COM1" },
                new LogEntry { Date = "27.10.2026", Time = "09:15", Event = "Поиск станции" },
            };
            _errors = new List<LogEntry>
            {
                new LogEntry { Date = "28.10.2026", Time = "14:31", Event = "Таймаут COM-порта" },
                new LogEntry { Date = "26.10.2026", Time = "18:00", Event = "Ошибка подключения" },
            };
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
            if (_showActions)
                _actions.Clear();
            else
                _errors.Clear();

            LogListBox.ItemsSource = null;
            LogListBox.ItemsSource = _showActions ? _actions : _errors;
            ShowToast("Журнал очищен", 0x75, 0x75, 0x75);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ShowToast("Журнал сохранён в файл", 0x4C, 0xAF, 0x50);
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
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
    }

    // Модель строки журнала
    public class LogEntry
    {
        public string Date { get; set; }
        public string Time { get; set; }
        public string Event { get; set; }
    }
}
