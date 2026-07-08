using System;
using System.Collections.Concurrent;
using UnityEngine;


public class AnswerHandler : ManagedMonoBehaviour
{
    public ConcurrentStack<ParticipantAnswerStruct> answers;    // make later IEnumerable for public use

    public override void ManagedAwake()
    {
        answers = new();
    }

    public override void ManagedStart()
    {
        CanUseUpdateMethod = true;
    }



    public void AddAnswer(AnswerFromParticipant signalFromParticipant)
    {
        answers.Push(new ParticipantAnswerStruct(answer: signalFromParticipant, timestamp: DateTimeOffset.Now));
    }
}