using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;

using MoogModule.Daemon;


namespace MoogModule
{
    public class TrajectoryManager
    {
        private MoveByTrajectoryParameters _trajectoryParameters;

        private ITrajectoryGenerator _trajectoryGenerator;
        private ITrajectoryProfile _trajectoryProfile;
        private DofParameters[] _trajectory;

        private int _currentPositionIndex;
        private DateTime _startTime;
        

        public TrajectoryManager(MoveByTrajectoryParameters trajectoryParameters)
        {
            _trajectoryParameters = trajectoryParameters;

            _trajectoryProfile = trajectoryParameters.TrajectoryProfile switch
            {
                TrajectoryProfile.CDF => new TrajectoryProfile_CDF(trajectoryParameters.TrajectoryProfileSettings.CDF),
                _ => throw new NotImplementedException()
            };

            _trajectoryGenerator = trajectoryParameters.TrajectoryType switch
            {
                TrajectoryType.Linear => new TrajectoryGenerator_Linear(_trajectoryParameters, _trajectoryProfile),
                _ => throw new NotImplementedException()
            };

            _trajectory = _trajectoryGenerator.GetWholePath(trajectoryParameters.DesiredFps);
            _currentPositionIndex = 0;
            _startTime = _trajectoryParameters.ScheduledStartTime == DateTime.MinValue ? DateTime.UtcNow : _trajectoryParameters.ScheduledStartTime;
        }

        public string GetDevLog()
        {
            return
                @$"
                TrajectoryManager.constructor info:
                    _trajectory.Length = {_trajectory.Length}
                    {_trajectoryParameters.MovementDuration.TotalMilliseconds}
                    {_trajectoryParameters.StartPoint.Surge}
                    {_trajectoryParameters.EndPoint.Surge}
                    {_trajectoryParameters.TrajectoryType}
                    {_trajectoryParameters.TrajectoryProfile}
                    {_trajectoryParameters.DelayHandling}
                    _startTime = {_startTime}

                    _trajectory = {string.Join(", ", _trajectory.Select(p => p.Surge))}
                ";
        }

        public IEnumerable<DofParameters> GetTrajectoryArray()
        {
            return _trajectory;
        }

        public DofParameters? GetNextPosition()
        {
            if (_currentPositionIndex == _trajectory.Length)
                return null;

            int indexesLeft = _trajectory.Length - _currentPositionIndex - 1;
            int totalIndexes = _trajectory.Length;
            TimeSpan totalTime = _trajectoryParameters.MovementDuration;
            TimeSpan timePassed = DateTime.UtcNow - _startTime;
            TimeSpan timeLeft = totalTime - timePassed;

            // todo: resolve problem with "index overflow" and devision by 0

            /*Console.WriteLine(@$"
                TrajectoryManager.GetNextPosition info:
                    indexesLeft = {indexesLeft}
                    totalIndexes = {totalIndexes}
                    totalTime = {totalTime}
                    timePassed = {timePassed}
                    timeLeft = {timeLeft}
                    surge position = {_trajectory[_currentPositionIndex].Surge}
            ");*/

            return _trajectoryParameters.DelayHandling switch
            {
                DelayCompensationStrategy.None => throw new Exception("You didn't choose a 'DelayCompensationStrategy'"),
                DelayCompensationStrategy.Ignore => _trajectory[_currentPositionIndex++],
                DelayCompensationStrategy.Repair => _trajectory[Math.Min(_currentPositionIndex += (int)(indexesLeft / timeLeft.TotalMilliseconds), totalIndexes - 1)],
                DelayCompensationStrategy.Jump => _trajectory[Math.Min((int)Math.Round(totalIndexes * timePassed / totalTime), totalIndexes - 1)],
                _ => throw new NotImplementedException()
            };
        }

        public bool TooEarly()
        {
            return (DateTime.UtcNow - _trajectoryParameters.ScheduledStartTime).TotalSeconds < 0;
        }

        public bool TooLate()
        {
            return (DateTime.UtcNow - _trajectoryParameters.ScheduledStartTime) > _trajectoryParameters.MovementDuration;
        }
    }
}