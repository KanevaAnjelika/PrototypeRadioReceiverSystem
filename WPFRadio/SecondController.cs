using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrototypeRadioReceiverSystem
{
    /// <summary>
    /// Контроллер работы с COM-портом: подключение, отправка команд, чтение данных.
    /// </summary>
    internal class SecondController
    {
        /// <summary>
        /// Подключённый последовательный порт.
        /// </summary>
        private SerialPort _port;

        /// <summary>
        /// Возвращает true, если порт открыт.
        /// </summary>
        public bool IsConnected => _port != null && _port.IsOpen;

        /// <summary>
        /// Открывает COM-порт с заданным именем и скоростью.
        /// </summary>
        /// <param name="portName">Имя порта, например «COM3».</param>
        /// <param name="baudRate">Скорость передачи (по умолчанию 9600).</param>
        /// <returns>True — успешно; false — ошибка (порт занят, не существует и т.д.).</returns>
        public bool Connect(string portName, int baudRate = 9600)
        {
            try
            {
                // Если порт уже открыт — сначала закроем
                Disconnect();

                _port = new SerialPort(portName, baudRate)
                {
                    Parity = Parity.None,
                    DataBits = 8,
                    StopBits = StopBits.One,
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };

                _port.Open();
                return true;
            }
            catch
            {
                // Порт может быть занят, не существовать, параметры недопустимы — возвращаем false
                _port = null;
                return false;
            }
        }

        /// <summary>
        /// Закрывает COM-порт.
        /// </summary>
        public void Disconnect()
        {
            if (_port != null)
            {
                try
                {
                    if (_port.IsOpen)
                        _port.Close();
                }
                catch { /* игнорируем ошибки при отключении */ }
                finally
                {
                    _port.Dispose();
                    _port = null;
                }
            }
        }

        /// <summary>
        /// Отправляет команду в порт. В конец автоматически добавляется '\n'.
        /// </summary>
        /// <param name="command">Текст команды, например «UP» или «101.5».</param>
        /// <returns>True — успешно отправлено; false — порт не открыт.</returns>
        public bool SendCommand(string command)
        {
            if (_port == null || !_port.IsOpen)
                return false;

            try
            {
                // Arduino ожидает команды, завершённые '\n'
                _port.WriteLine(command + "\n");
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Читает строку из порта. Если данные не пришли за 1 секунду — возвращает null.
        /// </summary>
        /// <returns>Прочитанная строка или null.</returns>
        public string ReadData()
        {
            if (_port == null || !_port.IsOpen)
                return null;

            try
            {
                return _port.ReadLine();
            }
            catch
            {
                // Таймаут или ошибка чтения — возвращаем null
                return null;
            }
        }
    }
}
