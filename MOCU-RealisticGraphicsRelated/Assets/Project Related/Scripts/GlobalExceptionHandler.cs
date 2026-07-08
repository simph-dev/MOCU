using UnityEngine;
using System;


public class GlobalExceptionHandler : ManagedMonoBehaviour
{
    // Событие, на которое сможет подписаться ваш UI-менеджер
    public static event Action<string> OnErrorCaught;

    public override void ManagedOnEnable()
    {
        //Debug.Log("Did you see that? ManagedOnEnable");
        // Подписываемся на события логов
        Application.logMessageReceived += HandleLog;

        // Если вы используете многопоточность (например, тяжелые асинхронные задачи), 
        // лучше использовать потокобезопасную версию:
        // Application.logMessageReceivedThreaded += HandleLog;
    }

    public override void ManagedOnDisable()
    {
        //Debug.Log("Did you see that? ManagedOnDisable");
        // Обязательно отписываемся при уничтожении объекта
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        //Debug.Log("Did you see that? HandleLog");
        // Нас интересуют только реальные ошибки и исключения (игнорируем обычные Debug.Log)
        if (type == LogType.Exception || type == LogType.Error)
        {
            // Формируем читаемое сообщение
            string errorMessage = $"Error: {logString}\n{stackTrace}";
            //Debug.Log("Did you see that?");

            // Передаем сообщение всем, кто слушает (например, скрипту отрисовки UI)
            OnErrorCaught?.Invoke(errorMessage);
        }
    }
}