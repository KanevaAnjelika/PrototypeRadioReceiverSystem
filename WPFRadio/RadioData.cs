using System;
using System.Collections.Generic;

namespace PrototypeRadioReceiverSystem
{
    /// <summary>
    /// Модель данных приёмника: текущая частота, уровень сигнала, стерео и список избранного.
    /// </summary>
    public class RadioData
    {
        /// <summary>
        /// Текущая частота в МГц (диапазон TEA5767 — 87.5 … 108.0).
        /// </summary>
        public float CurrentFrequency { get; set; }

        /// <summary>
        /// Уровень сигнала от 0 (плохо) до 10 (отлично).
        /// </summary>
        public int SignalLevel { get; set; }

        /// <summary>
        /// True — стерео, False — моно.
        /// </summary>
        public bool IsStereo { get; set; }

        /// <summary>
        /// Список избранных частот.
        /// </summary>
        public List<float> Favorites { get; } = new List<float>();

        /// <summary>
        /// Добавляет частоту в избранное. Если частота уже есть — ничего не делает.
        /// </summary>
        /// <param name="freq">Частота в МГц.</param>
        public void AddToFavorites(float freq)
        {
            if (!Favorites.Contains(freq))
                Favorites.Add(freq);
        }

        /// <summary>
        /// Устанавливает начальные данные приёмника.
        /// </summary>
        public void Initialize(float frequency, int signalLevel, bool stereo)
        {
            CurrentFrequency = frequency;
            SignalLevel = signalLevel;
            IsStereo = stereo;
        }

        /// <summary>
        /// Обновляет все три свойства из строки в формате «101.5,8,1».
        /// </summary>
        public void UpdateFromArduino(string raw)
        {
            string[] parts = raw.Split(',');
            if (parts.Length >= 3)
            {
                CurrentFrequency = float.Parse(parts[0].Trim());
                SignalLevel = int.Parse(parts[1].Trim());
                IsStereo = (int.Parse(parts[2].Trim()) == 1);
            }
        }

        /// <summary>
        /// Форматирует текущую частоту для отображения: «101.5 МГц».
        /// </summary>
        public string GetFrequencyDisplay()
        {
            return CurrentFrequency.ToString("F1") + " МГц";
        }

        /// <summary>
        /// Форматирует строку уровня сигнала: «Уровень сигнала: 8/10 Стерео».
        /// </summary>
        public string GetSignalDisplay()
        {
            string mode = IsStereo ? "Стерео" : "Моно";
            return "Уровень сигнала: " + SignalLevel + "/10 " + mode;
        }
    }
}
