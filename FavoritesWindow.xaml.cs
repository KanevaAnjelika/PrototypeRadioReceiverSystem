using System.Collections.Generic;
using System.Windows;

namespace PrototypeRadioReceiverSystem
{
    /// <summary>
    /// Логика взаимодействия для FavoritesWindow.xaml
    /// </summary>
    public partial class FavoritesWindow : Window
    {
        public FavoritesWindow()
        {
            InitializeComponent();

            // Демо-данные (заглушка до подключения реального хранилища)
            FavoritesGrid.ItemsSource = new List<FavoriteStation>
            {
                new FavoriteStation { Frequency = "101.5 МГц", Name = "Русское радио", Date = "28.10.2026" },
                new FavoriteStation { Frequency = "88.6 МГц",  Name = "Максимум",     Date = "27.10.2026" },
                new FavoriteStation { Frequency = "93.1 МГц",  Name = "Рекорд ФМ",    Date = "25.10.2026" },
            };
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: выбрать станцию и применить её частоту в главном окне
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: удалить выбранную станцию из списка
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    // Модель строки таблицы «Избранные станции»
    public class FavoriteStation
    {
        public string Frequency { get; set; }
        public string Name { get; set; }
        public string Date { get; set; }
    }
}
