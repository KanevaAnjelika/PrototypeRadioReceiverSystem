using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace PrototypeRadioReceiverSystem
{
    /// <summary>
    /// Логика взаимодействия для FavoritesWindow.xaml
    /// </summary>
    public partial class FavoritesWindow : Window
    {
        /// <summary>
        /// Контроллер главного окна — даёт доступ к данным и журналу.
        /// </summary>
        private readonly MainController _mainController;

        /// <summary>
        /// Конструктор с параметром: принимает контроллер из MainWindow.
        /// </summary>
        public FavoritesWindow(MainController controller)
        {
            InitializeComponent();

            _mainController = controller;

            LoadFavorites();
        }

        /// <summary>
        /// Загружает список избранных станций.
        /// Если в контроллере избранного ещё нет — показывает тестовые карточки.
        /// </summary>
        private void LoadFavorites()
        {
            List<FavoriteStation> stations = new List<FavoriteStation>();

            // Пробуем взять реальные избранные частоты из модели данных
            if (_mainController != null && _mainController.Data.Favorites.Count > 0)
            {
                foreach (float freq in _mainController.Data.Favorites)
                {
                    stations.Add(new FavoriteStation
                    {
                        Frequency = freq.ToString("F1") + " МГц",
                        Name = "Сохранённая станция",
                        Date = ""
                    });
                }
            }
            else
            {
                // Тестовые карточки, пока избранное пусто
                stations.Add(new FavoriteStation { Frequency = "101.5 МГц", Name = "Русское радио", Date = "28.10.2026" });
                stations.Add(new FavoriteStation { Frequency = "88.6 МГц", Name = "Максимум", Date = "27.10.2026" });
                stations.Add(new FavoriteStation { Frequency = "93.1 МГц", Name = "Рекорд ФМ", Date = "25.10.2026" });
            }

            FavoritesListBox.ItemsSource = stations;
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: выбрать станцию и применить её частоту в главном окне
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: удалить выбранную станцию из списка
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Перетаскивание окна за шапку
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }

    // Модель карточки избранной станции
    public class FavoriteStation
    {
        public string Frequency { get; set; }
        public string Name { get; set; }
        public string Date { get; set; }
    }
}
