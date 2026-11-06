using System;
using System.Collections.Generic;
using System.Globalization;

namespace PrototypeRadioReceiverSystem
{
    /// <summary>
    /// Основной контроллер — связывает COM-порт, модель данных и журнал операций.
    /// </summary>
    public class MainController
    {
        private readonly SecondController _serialController;
        private readonly RadioData _radioData;

        /// <summary>Журнал операций.</summary>
        public StorageOperationsLog OperationLog { get; } = new StorageOperationsLog();

        /// <summary>Модель данных приёмника.</summary>
        public RadioData Data => _radioData;

        /// <summary>Порт подключён.</summary>
        public bool IsConnected => _serialController.IsConnected;

        /// <summary>Частота обновилась (для MainWindow).</summary>
        public event Action FrequencyChanged;

        public MainController()
        {
            _serialController = new SecondController();
            _radioData = new RadioData();
            _radioData.Reset();

            _serialController.ConnectionLost += () =>
            {
                OperationLog.AddRecord("Потеря соединения с Arduino");
                _radioData.Reset();
            };
        }

        /// <summary>Подключается к COM-порту Arduino.</summary>
        public bool ConnectToPort(string portName)
        {
            bool result = _serialController.Connect(portName);

            if (result)
                OperationLog.AddRecord("Подключение к " + portName + " — успешно");
            else
                OperationLog.AddRecord("Подключение к " + portName + " — ошибка");

            return result;
        }

        /// <summary>Отключается от COM-порта.</summary>
        public void Disconnect()
        {
            _serialController.Disconnect();
            _radioData.Reset();
            OperationLog.AddRecord("Отключение от порта");
        }

        // ---- Команды ----

        public bool StepUp()      { return SendCommand("UP"); }
        public bool StepDown()    { return SendCommand("DOWN"); }
        public bool Scan()        { return SendCommand("SCAN"); }
        public bool SaveStation() { return SendCommand("SAVE"); }
        public bool LoadStation() { return SendCommand("LOAD"); }

        /// <summary>Устанавливает точную частоту.</summary>
        public bool SetStation(float freq)
        {
            string cmd = "SET " + freq.ToString("F1", CultureInfo.InvariantCulture);
            return SendCommand(cmd);
        }

        /// <summary>Отправляет команду в Arduino.</summary>
        public bool SendCommand(string cmd)
        {
            bool sent = _serialController.SendCommand(cmd);

            if (sent)
                OperationLog.AddRecord("→ " + cmd);
            else
                OperationLog.AddRecord("Ошибка отправки: " + cmd);

            return sent;
        }

        // ---- Приём данных от Arduino ----

        /// <summary>
        /// Забирает все накопленные строки из порта и обрабатывает их.
        /// Вызывать из DispatcherTimer.
        /// </summary>
        public void PollArduino()
        {
            if (!_serialController.IsConnected)
                return;

            List<string> lines = _serialController.ReadAvailableLines();
            if (lines == null)
                return;

            foreach (string line in lines)
            {
                ProcessLine(line);
            }
        }

        private void ProcessLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return;

            // EVT,<code>[,<arg>] — событие
            if (line.StartsWith("EVT,"))
            {
                string rest = line.Substring(4);
                int comma = rest.IndexOf(',');
                string code = comma > 0 ? rest.Substring(0, comma) : rest;
                string arg = comma > 0 ? rest.Substring(comma + 1) : null;

                OperationLog.AddRecord("← " + code + (arg != null ? " " + arg : ""));
                return;
            }

            // ERR,<text> — ошибка от устройства
            if (line.StartsWith("ERR,"))
            {
                OperationLog.AddRecord("Ошибка Arduino: " + line.Substring(4));
                return;
            }

            // Строка статуса: «101.5,8,1»
            string[] parts = line.Split(',');
            if (parts.Length >= 3)
            {
                try
                {
                    float freq = float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture);
                    int sig = int.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
                    int stereo = int.Parse(parts[2].Trim(), CultureInfo.InvariantCulture);

                    if (freq >= 76.0f && freq <= 108.0f && sig >= 0 && sig <= 10)
                    {
                        float oldFreq = _radioData.CurrentFrequency;
                        _radioData.UpdateFromArduino(line);

                        // Логируем только при реальном изменении (не спамим каждые 400мс)
                        if (Math.Abs(oldFreq - _radioData.CurrentFrequency) > 0.01f)
                            FrequencyChanged?.Invoke();
                    }
                }
                catch
                {
                    OperationLog.AddRecord("Ошибка парсинга: " + line);
                }
            }
        }

        // ---- Избранное ----

        /// <summary>Сохраняет текущую частоту в избранное с указанным именем.</summary>
        public void AddCurrentToFavorites(string name = null)
        {
            if (_radioData.CurrentFrequency > 0)
            {
                _radioData.AddToFavorites(_radioData.CurrentFrequency, name);
                OperationLog.AddRecord("Добавлено в избранное: " + _radioData.CurrentFrequency.ToString("F1") + " МГц");
            }
        }

        /// <summary>Удаляет станцию из избранного.</summary>
        public void RemoveFavorite(float freq)
        {
            if (_radioData.RemoveFromFavorites(freq))
                OperationLog.AddRecord("Удалено из избранного: " + freq.ToString("F1") + " МГц");
        }

        /// <summary>Выбирает станцию из избранного — отправляет SET-команду.</summary>
        public void SelectFavorite(float freq)
        {
            SetStation(freq);
            OperationLog.AddRecord("Выбрана станция: " + freq.ToString("F1") + " МГц");
        }
    }
}
