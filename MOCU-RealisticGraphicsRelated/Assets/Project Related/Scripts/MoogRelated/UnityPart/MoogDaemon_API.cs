#nullable enable

using MoogModule.Daemon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace MoogModule
{
    public interface MoogDaemon_API
    {
        void Connect(MachineSettings parameters);
        void Engage();
        void Disengage();
        void Reset();
        void StartReceivingFeedback();
        void StopReceivingFeedback();
        void MoveToPoint(MoveToPointParameters parameters);
        void MoveByTrajectory(MoveByTrajectoryParameters parameters);

        void Test(object? input = null);

        event Action<MoogRealTimeState> State;
        event Action<MoogFeedback> Feedback;
    }
}