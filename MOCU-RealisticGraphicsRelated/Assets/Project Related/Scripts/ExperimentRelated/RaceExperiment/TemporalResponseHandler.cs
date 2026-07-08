/*using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;


namespace Temporal
{
    public class TemporalResponseHandler : ManagedMonoBehaviour
    {
        public event Action GotAnswer_Up;
        public event Action GotAnswer_Down;
        public event Action GotAnswer_Left;
        public event Action GotAnswer_Right;
        public event Action GotSignal_Start;

        //private TemporalControls _input;
        private CedrusHandler _cedrusHandler;
        private Task _cedrusTask;
        private CancellationTokenSource _cts;

        public override void ManagedAwake()
        {
            _cts = new CancellationTokenSource();
            _input = new();
            _cedrusHandler = new CedrusHandler();
            _cedrusTask = Task.Run(() => _cedrusHandler.Init(_cts.Token), _cts.Token);

            _input.Responses.Up.performed += _ => HandleUp();
            _input.Responses.Down.performed += _ => HandleDown();
            _input.Responses.Left.performed += _ => HandleLeft();
            _input.Responses.Right.performed += _ => HandleRight();
            _input.Responses.Start.performed += _ => HandleStart();

            _cedrusHandler.GotAnswer_Up += HandleUp;
            _cedrusHandler.GotAnswer_Down += HandleDown;
            _cedrusHandler.GotAnswer_Left += HandleLeft;
            _cedrusHandler.GotAnswer_Right += HandleRight;
            _cedrusHandler.GotSignal_Start += HandleStart;
        }

        public override void ManagedOnEnable() => _input.Enable();
        public override void ManagedOnDisable()
        {
            _input.Disable();

            if (_cts != null)
            {
                _cts.Cancel(); // Посылаем сигнал всем потокам остановиться
                _cts.Dispose(); // Освобождаем ресурсы
                _cts = null;
            }
        }

        // .................................

        private void HandleUp()
        {
            //Debug.Log("Up arrow pressed");
            GotAnswer_Up?.Invoke();
        }

        private void HandleDown()
        {
            //Debug.Log("Down arrow pressed");
            GotAnswer_Down?.Invoke();
        }

        private void HandleLeft()
        {
            //Debug.Log("Left arrow pressed");
            GotAnswer_Left?.Invoke();
        }

        private void HandleRight()
        {
            //Debug.Log("Right arrow pressed");
            GotAnswer_Right?.Invoke();
        }

        private void HandleStart()
        {
            Debug.Log("Start button pressed");
            GotSignal_Start?.Invoke();
        }
    }
}*/