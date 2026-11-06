using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace PrototypeRadioReceiverSystem
{
    /// <summary>
    /// Модель данных приёмника с поддержкой привязки к UI.
    /// </summary>
    public class RadioData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private float _currentFrequency = 0f;
        private int _signalLevel = 0;
        private bool _isStereo = false;

        /// <summary>Текущая частота в МГц.</summary>
        public float CurrentFrequency
        {
            get => _currentFrequency;
            set { if (_currentFrequency != value) { _currentFrequency = value; OnPropertyChanged(); } }
        }

        /// <summary>Уровень сигнала от 0 до 10.</summary>
        public int SignalLevel
        {
            get => _signalLevel;
            set { if (_signalLevel != value) { _signalLevel = value; OnPropertyChanged(); } }
        }

        /// <summary>True — стерео, False — моно.</summary>
        public bool IsStereo
        {
            get => _isStereo;
            set { if (_isStereo != value) { _isStereo = value; OnPropertyChanged(); } }
        }

        /// <summary>Строка для отображения уровня сигнала.</summary>
        public string SignalDisplay
        {
            get
            {
                if (CurrentFrequency == 0)
                    return "Ожидание данных";
                string mode = IsStereo ? "Стерео" : "Моно";
                return "Уровень сигнала: " + SignalLevel + "/10 " + mode;
            }
        }

        /// <summary>Форматированная частота для отображения.</summary>
        public string FrequencyDisplay
        {
            get
            {
                if (CurrentFrequency == 0)
                    return "Ожидание подключения";
                return CurrentFrequency.ToString("F1") + " МГц";
            }
        }

        /// <summary>Список избранных станций.</summary>
        public List<FavoriteInfo> Favorites { get; } = new List<FavoriteInfo>();

        /// <summary>Добавляет частоту в избранное.</summary>
        public void AddToFavorites(float freq, string name = null)
        {
            // Проверяем дубликат
            foreach (var fav in Favorites)
                if (Math.Abs(fav.Frequency - freq) < 0.05f)
                    return;

            Favorites.Add(new FavoriteInfo { Frequency = freq, Name = name ?? "", Date = DateTime.Now.ToString("dd.MM.yyyy") });
            OnPropertyChanged(nameof(Favorites));
        }

        /// <summary>Удаляет станцию из избранного.</summary>
        public bool RemoveFromFavorites(float freq)
        {
            for (int i = 0; i < Favorites.Count; i++)
                if (Math.Abs(Favorites[i].Frequency - freq) < 0.05f)
                {
                    Favorites.RemoveAt(i);
                    OnPropertyChanged(nameof(Favorites));
                    return true;
                }
            return false;
        }

        /// <summary>Сбрасывает данные при отключении.</summary>
        public void Reset()
        {
            _currentFrequency = 0f;
            _signalLevel = 0;
            _isStereo = false;
            OnPropertyChanged(nameof(FrequencyDisplay));
            OnPropertyChanged(nameof(SignalDisplay));
        }

        /// <summary>
        /// Обновляет данные из строки протокола Arduino: «101.5,8,1»
        /// </summary>
        public void UpdateFromArduino(string raw)
        {
            string[] parts = raw.Split(',');
            if (parts.Length >= 3)
            {
                float freq = float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture);
                int sig = int.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
                int stereoVal = int.Parse(parts[2].Trim(), CultureInfo.InvariantCulture);

                if (freq >= 76.0f && freq <= 108.0f && sig >= 0 && sig <= 10)
                {
                    _currentFrequency = freq;
                    _signalLevel = sig;
                    _isStereo = stereoVal == 1;
                    OnPropertyChanged(nameof(FrequencyDisplay));
                    OnPropertyChanged(nameof(SignalDisplay));
                }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>Информация об избранной станции.</summary>
    public class FavoriteInfo
    {
        public float Frequency { get; set; }
        public string Name { get; set; }
        public string Date { get; set; }
    }
}
