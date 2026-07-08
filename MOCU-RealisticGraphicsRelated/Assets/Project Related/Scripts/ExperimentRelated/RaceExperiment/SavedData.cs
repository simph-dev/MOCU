using System;
using System.IO;
using System.Collections.Generic;

using DaemonsRelated;

namespace RaceExperiment
{
    public class SavedData
    {
        public DateTimeOffset Date;
        public Parameters Parameters;
        public List<Trial> Trials;


        public SavedData()
        {
            Date = DateTimeOffset.Now;
        }

        public void Save(DateTimeOffset startedAt)
        {
            // 1. Подготавливаем данные и имя файла
            var json = JsonHelper.SerializeJson(this);
            string dateTime = startedAt.ToString("yyyy-MM-dd_HH-mm-ss");
            string fileName = $"ExperimentData_{dateTime}.json";

            // === ЧАСТЬ 1: ОСНОВНОЙ ФАЙЛ (Витрина в Документах) ===
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string mainDirectory = Path.Combine(documentsPath, "MocuRE");
            string mainFilePath = Path.Combine(mainDirectory, fileName);

            if (!Directory.Exists(mainDirectory))
                Directory.CreateDirectory(mainDirectory);

            // Временно снимаем защиту для перезаписи (если файл с таким именем почему-то уже есть)
            if (File.Exists(mainFilePath))
                File.SetAttributes(mainFilePath, FileAttributes.Normal);

            // Записываем данные и мгновенно вешаем атрибут "Только чтение"
            File.WriteAllText(mainFilePath, json);
            File.SetAttributes(mainFilePath, FileAttributes.ReadOnly);


            // === ЧАСТЬ 2: НЕЗАВИСИМЫЙ БЭКАП (В корне профиля) ===
            // Получаем корень текущего пользователя (работает динамически для любой учетки)
            string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string backupRootDirectory = Path.Combine(userProfilePath, "MocuRE_Vault");
            string backupFilePath = Path.Combine(backupRootDirectory, fileName);

            if (!Directory.Exists(backupRootDirectory))
            {
                DirectoryInfo di = Directory.CreateDirectory(backupRootDirectory);
                // Делаем папку-сейф системно скрытой
                di.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
            }

            // Перестраховка: снимаем любые системные блокировки с файла бэкапа перед перезаписью
            if (File.Exists(backupFilePath))
                File.SetAttributes(backupFilePath, FileAttributes.Normal);

            // Сохраняем независимую копию (в скрытой папке атрибуты можно не трогать)
            File.WriteAllText(backupFilePath, json);
        }
    }
}