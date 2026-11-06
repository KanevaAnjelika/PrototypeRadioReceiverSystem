using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace PrototypeRadioReceiverSystem
{
    /// <summary>
    /// Логика взаимодействия для FavoritesWindow.xaml
    /// </summary>
    public partial class FavoritesWindow : Window
    {
        private readonly MainController _mainController;
        private const string FavoritesFileName = "favorites.txt";

        public FavoritesWindow(MainController controller)
        {
            InitializeComponent();

            _mainController = controller;
            LoadFavorites();
        }

        /// <summary>Загружает избранное из файла и из модели данных.</summary>
        private void LoadFavorites()
        {
            List<FavoriteStation> stations = new List<FavoriteStation>();

            // 1. Из модели данных (пополняется при нажатии SAVE в MainWindow)
            foreach (FavoriteInfo fav in _mainController.Data.Favorites)
            {
                stations.Add(new FavoriteStation
                {
                    Frequency = fav.Frequency.ToString("F1") + " МГц",
                    Name = string.IsNullOrEmpty(fav.Name) ? "—" : fav.Name,
                    Date = fav.Date
                });
            }

            FavoritesListBox.ItemsSource = stations;

            if (stations.Count == 0)
            {
                FavoritesListBox.Items.Add(new TextBlock
                {
                    Text = "Нет избранных станций. Нажмите «Сохранить» на частоте.",
                    Foreground = Brushes.Gray,
                    FontSize = 14,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0)
                });
            }
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            FavoriteStation selected = FavoritesListBox.SelectedItem as FavoriteStation;
            if (selected == null) return;

            // Парсим частоту из строки «101.5 МГц»
            string freqStr = selected.Frequency.Replace(" МГц", "").Trim();
            if (float.TryParse(freqStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float freq))
            {
                _mainController.SelectFavorite(freq);
                MessageBox.Show("Выбрана: " + selected.Frequency, "Избранное", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            FavoriteStation selected = FavoritesListBox.SelectedItem as FavoriteStation;
            if (selected == null) return;

            // Парсим частоту
            string freqStr = selected.Frequency.Replace(" МГц", "").Trim();
            if (float.TryParse(freqStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float freq))
            {
                _mainController.RemoveFavorite(freq);
                LoadFavorites();
                MessageBox.Show("Удалено: " + selected.Frequency, "Избранное", MessageBoxButton.OK, MessageBoxImage.Information);
            }
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
    }

    /// <summary>Модель карточки избранной станции (для DataBinding).</summary>
    public class FavoriteStation
    {
        public string Frequency { get; set; }
        public string Name { get; set; }
        public string Date { get; set; }
    }
}
