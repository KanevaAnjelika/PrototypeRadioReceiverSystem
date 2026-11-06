using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;

namespace PrototypeRadioReceiverSystem
{
    /// <summary>
    /// Контроллер COM-порта: подключение, отправка команд, асинхронное чтение строк.
    /// Использует событие DataReceived — UI-поток никогда не блокируется.
    /// </summary>
    internal class SecondController : IDisposable
    {
        private SerialPort _port;
        private readonly StringBuilder _rxBuffer = new StringBuilder();
        private readonly object _rxLock = new object();
        private readonly List<string> _pendingLines = new List<string>();

        /// <summary>Порт открыт.</summary>
        public bool IsConnected => _port != null && _port.IsOpen;

        /// <summary>Порт был открыт, но закрылся unexpectedly (Arduino отключили).</summary>
        public event Action ConnectionLost;

        /// <summary>
        /// Список доступных COM-портов системы.
        /// </summary>
        public static string[] GetAvailablePorts()
        {
            try { return SerialPort.GetPortNames(); }
            catch { return new string[0]; }
        }

        /// <summary>
        /// Открывает COM-порт.
        /// </summary>
        public bool Connect(string portName, int baudRate = 9600)
        {
            Disconnect();

            try
            {
                _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 500,
                    WriteTimeout = 500,
                    NewLine = "\n",
                    Encoding = Encoding.ASCII,
                    DtrEnable = true,
                    RtsEnable = true,
                    Handshake = Handshake.None
                };

                _port.DataReceived += OnDataReceived;
                _port.ErrorReceived += OnErrorReceived;

                _port.Open();

                lock (_rxLock)
                {
                    _rxBuffer.Clear();
                    _pendingLines.Clear();
                }

                // Arduino Uno сбрасывается при открытии порта (DTR) — ждём перезагрузки
                System.Threading.Thread.Sleep(2000);
                DrainBuffer();

                return true;
            }
            catch
            {
                DisposePort();
                return false;
            }
        }

        /// <summary>
        /// Закрывает порт.
        /// </summary>
        public void Disconnect()
        {
            DisposePort();
        }

        /// <summary>
        /// Отправляет команду в порт. Автоматически добавляет '\n'.
        /// </summary>
        public bool SendCommand(string command)
        {
            SerialPort port = _port;
            if (port == null || !port.IsOpen)
                return false;

            try
            {
                port.WriteLine(command);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Забирает все накопленные строки из буфера. НЕ блокирует поток.
        /// Вызывать из UI-таймера.
        /// </summary>
        public List<string> ReadAvailableLines()
        {
            lock (_rxLock)
            {
                if (_pendingLines.Count == 0)
                    return null;

                List<string> result = new List<string>(_pendingLines);
                _pendingLines.Clear();
                return result;
            }
        }

        /// <summary>
        /// Очищает входящий буфер.
        /// </summary>
        public void DrainBuffer()
        {
            try
            {
                if (_port != null && _port.IsOpen)
                    _port.DiscardInBuffer();
            }
            catch { }

            lock (_rxLock)
            {
                _rxBuffer.Clear();
                _pendingLines.Clear();
            }
        }

        // ---- event handlers (работают на фоновом потоке SerialPort) ----

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;

            try
            {
                string chunk = sp.ReadExisting();
                if (string.IsNullOrEmpty(chunk))
                    return;

                lock (_rxLock)
                {
                    foreach (char c in chunk)
                    {
                        if (c == '\n')
                        {
                            string line = _rxBuffer.ToString().Trim();
                            _rxBuffer.Clear();
                            if (line.Length > 0)
                                _pendingLines.Add(line);
                        }
                        else if (c != '\r')
                        {
                            if (_rxBuffer.Length < 64)
                                _rxBuffer.Append(c);
                            else
                                _rxBuffer.Clear();
                        }
                    }
                }
            }
            catch
            {
                // Порт мог закрыться во время чтения
            }
        }

        private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            // Бэкап: если порт закрылся или возникла ошибка — уведомляем
            ConnectionLost?.Invoke();
        }

        private void DisposePort()
        {
            SerialPort port = _port;
            _port = null;

            if (port == null)
                return;

            try
            {
                port.DataReceived -= OnDataReceived;
                port.ErrorReceived -= OnErrorReceived;

                if (port.IsOpen)
                    port.Close();
            }
            catch { }
            finally
            {
                try { port.Dispose(); } catch { }
            }
        }

        public void Dispose()
        {
            DisposePort();
        }
    }
}
