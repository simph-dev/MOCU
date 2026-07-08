using System;
using UnityEngine;


public class InputLogic : ManagedMonoBehaviour
{
    private UiHandler _uiHandler;
    private ExperimentTabHandler _experimentTabHandler;

    private AnswerHandler _answerHandler;
    private AudioHandler _audioHandler;

    public event Action startIncomingIntercomStream;
    public event Action startOutgoingIntercomStream;
    public event Action stopIncomingIntercomStream;
    public event Action stopOutgoingIntercomStream;

    public override void ManagedAwake()
    {
        _uiHandler = GetComponent<UiHandler>(); // todo: shouldn't be here, change all to "_experimentTabHandler"
        _experimentTabHandler = GetComponent<ExperimentTabHandler>();

        _answerHandler = GetComponent<AnswerHandler>();
        _audioHandler = GetComponent<AudioHandler>();
    }

    public void TestMethod(string text)
    {
        //_uiHandler.PrintToWarnings(text);
    }
    


    public void IntercomFromResearcherStarted()
    {
        if (_audioHandler.stateTracker.Status != ModuleStatus.FullyOperational) return;

        startOutgoingIntercomStream?.Invoke();
        _uiHandler.mainUiScreen.GetElement("controls-intercom-part").AddToClassList("isActive");
        _uiHandler.mainUiScreen.GetElement("controls-intercom-part").AddToClassList("isOutgoing");
    }
    public void IntercomFromResearcherStopped()
    {
        if (_audioHandler.stateTracker.Status != ModuleStatus.FullyOperational) return;

        stopOutgoingIntercomStream?.Invoke();
        if (!_uiHandler.mainUiScreen.GetElement("controls-intercom-part").ClassListContains("isIncoming"))
        {
            // the check is necessary so that the highlight does not disappear completely when pressed simultaneously
            _uiHandler.mainUiScreen.GetElement("controls-intercom-part").RemoveFromClassList("isActive");
        }
        _uiHandler.mainUiScreen.GetElement("controls-intercom-part").RemoveFromClassList("isOutgoing");
    }

    public void IntercomFromParticipantStarted()
    {
        if (_audioHandler.stateTracker.Status != ModuleStatus.FullyOperational) return;

        startIncomingIntercomStream?.Invoke();
        _uiHandler.mainUiScreen.GetElement("controls-intercom-part").AddToClassList("isActive");
        _uiHandler.mainUiScreen.GetElement("controls-intercom-part").AddToClassList("isIncoming");
    }
    public void IntercomFromParticipantStopped()
    {
        if (_audioHandler.stateTracker.Status != ModuleStatus.FullyOperational) return;

        stopIncomingIntercomStream?.Invoke();
        if (!_uiHandler.mainUiScreen.GetElement("controls-intercom-part").ClassListContains("isOutgoing"))
        {
            // the check is necessary so that the highlight does not disappear completely when pressed simultaneously
            _uiHandler.mainUiScreen.GetElement("controls-intercom-part").RemoveFromClassList("isActive");
        }
        _uiHandler.mainUiScreen.GetElement("controls-intercom-part").RemoveFromClassList("isIncoming");
    }



    public void GotPressSignalFromInputSystem(AnswerFromParticipant signalFromParticipant)
    {
        // All this is temp and dirty

        switch (signalFromParticipant)
        {
            case AnswerFromParticipant.Up:
                AnswerUpPressed(source: "inputSystem");
                break;
            case AnswerFromParticipant.Left:
                AnswerLeftPressed(source: "inputSystem");
                break;
            case AnswerFromParticipant.Center:
                AnswerCenterPressed(source: "inputSystem");
                break;
            case AnswerFromParticipant.Right:
                AnswerRightPressed(source: "inputSystem");
                break;
            case AnswerFromParticipant.Down:
                AnswerDownPressed(source: "inputSystem");
                break;
        }
        _answerHandler.AddAnswer(signalFromParticipant);
    }

    public void GotReleaseSignalFromInputSystem(AnswerFromParticipant signalFromParticipant)
    {
        // All this is temp and dirty

        switch (signalFromParticipant)
        {
            case AnswerFromParticipant.Up:
                AnswerUpReleased();
                break;
            case AnswerFromParticipant.Left:
                AnswerLeftReleased();
                break;
            case AnswerFromParticipant.Center:
                AnswerCenterReleased();
                break;
            case AnswerFromParticipant.Right:
                AnswerRightReleased();
                break;
            case AnswerFromParticipant.Down:
                AnswerDownReleased();
                break;
        }
    }

    /*public void GotAnswerFromCedrus(AnswerFromParticipant signalFromParticipant)
    {
        // All this is temp and dirty

        switch (signalFromParticipant)
        {
            case AnswerFromParticipant.Up:
                AnswerUpPressed(source: "cedrus");
                break;
            case AnswerFromParticipant.Left:
                AnswerLeftPressed(source: "cedrus");
                break;
            case AnswerFromParticipant.Center:
                AnswerCenterPressed(source: "cedrus");
                break;
            case AnswerFromParticipant.Right:
                AnswerRightPressed(source: "cedrus");
                break;
            case AnswerFromParticipant.Down:
                AnswerDownPressed(source: "cedrus");
                break;
        }
        _answerHandler.AddAnswer(signalFromParticipant);
    }*/




    public void AnswerLeftPressed(string source)
    {
        _experimentTabHandler.ControllerButtonWasPressed("controller-left-btn");
        if (source == "ui" || source == "cedrus") { Invoke("AnswerLeftReleased", 0.1f); }       // with delay 0.5sec if clicked with mouse    // todo later to all
    }
    public void AnswerLeftReleased()
    {
        _experimentTabHandler.ControllerButtonWasReleased("controller-left-btn");
    }
    public void AnswerRightPressed(string source)
    {
        _experimentTabHandler.ControllerButtonWasPressed("controller-right-btn");
        if (source == "ui" || source == "cedrus") { Invoke("AnswerRightReleased", 0.1f); }
    }
    public void AnswerRightReleased()
    {
        _experimentTabHandler.ControllerButtonWasReleased("controller-right-btn");
    }
    public void AnswerUpPressed(string source)
    {
        _experimentTabHandler.ControllerButtonWasPressed("controller-up-btn");
        if (source == "ui" || source == "cedrus") { Invoke("AnswerUpReleased", 0.1f); }
    }
    public void AnswerUpReleased()
    {
        _experimentTabHandler.ControllerButtonWasReleased("controller-up-btn");
    }
    public void AnswerDownPressed(string source)
    {
        _experimentTabHandler.ControllerButtonWasPressed("controller-down-btn");
        if (source == "ui" || source == "cedrus") { Invoke("AnswerDownReleased", 0.1f); }
    }
    public void AnswerDownReleased()
    {
        _experimentTabHandler.ControllerButtonWasReleased("controller-down-btn");
    }
    public void AnswerCenterPressed(string source)
    {
        _experimentTabHandler.ControllerButtonWasPressed("controller-center-btn");
        if (source == "ui" || source == "cedrus") { Invoke("AnswerCenterReleased", 0.1f); }
    }
    public void AnswerCenterReleased()
    {
        _experimentTabHandler.ControllerButtonWasReleased("controller-center-btn");
    }

}