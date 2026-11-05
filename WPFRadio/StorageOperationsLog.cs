using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrototypeRadioReceiverSystem
{
    /// <summary>
    /// Журнал операций приёмника: запись событий с меткой времени и сохранение в файл.
    /// </summary>
    public class StorageOperationsLog
    {
        /// <summary>
        /// Список записей. Каждая запись — строка вида «[14:35] Подключение...».
        /// </summary>
        public List<string> Logs { get; } = new List<string>();

        /// <summary>
        /// Добавляет запись с текущим временем.
        /// Формат: «[ЧЧ:ММ] текст», например «[14:35] Подключение к COM3».
        /// </summary>
        /// <param name="text">Текст события (без метки времени — она добавится автоматически).</param>
        public void AddRecord(string text)
        {
            // Берём часы и минуты из текущего времени
            string time = DateTime.Now.ToString("HH:mm");
            Logs.Add("[" + time + "] " + text);
        }

        /// <summary>
        /// Сохраняет все записи в текстовый файл.
        /// </summary>
        /// <param name="path">Полный путь к файлу, например «C:\logs\radio.txt».</param>
        /// <returns>True — успешно; false — ошибка (файл занят, путь недействителен и т.д.).</returns>
        public bool SaveToFile(string path)
        {
            try
            {
                // StreamWriter перезаписывает файл (false = не добавлять в конец).
                // UTF-8 без BOM — для максимальной совместимости.
                using (StreamWriter writer = new StreamWriter(path, false, Encoding.UTF8))
                {
                    foreach (string record in Logs)
                    {
                        writer.WriteLine(record);
                    }
                }
                return true;
            }
            catch
            {
                // Файл может быть занят, путь — недопустим, нет прав на запись — возвращаем false
                return false;
            }
        }

        /// <summary>
        /// Очищает журнал.
        /// </summary>
        public void Clear()
        {
            Logs.Clear();
        }

        /// <summary>
        /// Количество записей в журнале.
        /// </summary>
        public int Count => Logs.Count;
    }
}
