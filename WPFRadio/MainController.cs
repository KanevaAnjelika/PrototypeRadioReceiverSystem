using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrototypeRadioReceiverSystem
{
    /// <summary>
    /// Основной контроллер — связывает COM-порт, модель данных и журнал операций.
    /// </summary>
    public class MainController
    {
        /// <summary>
        /// Контроллер COM-порта.
        /// </summary>
        private readonly SecondController _serialController;

        /// <summary>
        /// Модель данных приёмника.
        /// </summary>
        private readonly RadioData _radioData;

        /// <summary>
        /// Журнал операций (доступен из окон для отображения).
        /// </summary>
        public StorageOperationsLog OperationLog { get; } = new StorageOperationsLog();

        /// <summary>
        /// Свойство для удобного доступа к модели данных.
        /// </summary>
        public RadioData Data => _radioData;

        /// <summary>
        /// Подключён ли порт.
        /// </summary>
        public bool IsConnected => _serialController.IsConnected;

        /// <summary>
        /// Создаёт контроллер с начальными настройками.
        /// </summary>
        public MainController()
        {
            _serialController = new SecondController();
            _radioData = new RadioData();

            // Начальные данные: частота 101.5 МГц, сигнал 8/10, стерео
            _radioData.Initialize(101.5f, 8, true);
        }

        /// <summary>
        /// Подключается к COM-порту Arduino.
        /// </summary>
        /// <param name="portName">Имя порта, например «COM3».</param>
        /// <returns>True — успешно подключились.</returns>
        public bool ConnectToPort(string portName)
        {
            bool result = _serialController.Connect(portName);

            // Логируем результат
            if (result)
                OperationLog.AddRecord("Подключение к " + portName + " — успешно");
            else
                OperationLog.AddRecord("Подключение к " + portName + " — ошибка");

            return result;
        }

        /// <summary>
        /// Отправляет команду в Arduino (например, «UP», «DOWN», «SCAN»).
        /// </summary>
        /// <param name="cmd">Команда без завершающего '\n' — контроллер сам добавит.</param>
        /// <returns>True — команда отправлена.</returns>
        public bool SendCommand(string cmd)
        {
            bool sent = _serialController.SendCommand(cmd);

            if (sent)
                OperationLog.AddRecord("Команда отправлена: " + cmd);
            else
                OperationLog.AddRecord("Ошибка отправки команды: " + cmd);

            return sent;
        }

        /// <summary>
        /// Читает данные из порта и обновляет модель.
        /// Ожидаемый формат строки: «101.5,8,1»
        /// где 101.5 — частота, 8 — уровень сигнала, 1 — стерео (1=да, 0=нет).
        /// </summary>
        public void UpdateDataFromArduino()
        {
            string raw = _serialController.ReadData();

            if (string.IsNullOrEmpty(raw))
                return; // Нет данных или таймаут

            // Разбиваем строку по запятым
            string[] parts = raw.Split(',');

            if (parts.Length < 3)
            {
                OperationLog.AddRecord("Ошибка формата данных от Arduino: " + raw);
                return;
            }

            try
            {
                // Обновляем модель данных
                _radioData.UpdateFromArduino(raw);

                // Логируем успешное обновление
                OperationLog.AddRecord(
                    "Данные: " + _radioData.GetFrequencyDisplay() +
                    ", сигнал " + _radioData.SignalLevel + "/10, " +
                    (_radioData.IsStereo ? "стерео" : "моно"));
            }
            catch (Exception)
            {
                OperationLog.AddRecord("Ошибка парсинга данных: " + raw);
            }
        }

        /// <summary>
        /// Отключается от COM-порта.
        /// </summary>
        public void Disconnect()
        {
            _serialController.Disconnect();
            OperationLog.AddRecord("Отключение от порта");
        }
    }
}
