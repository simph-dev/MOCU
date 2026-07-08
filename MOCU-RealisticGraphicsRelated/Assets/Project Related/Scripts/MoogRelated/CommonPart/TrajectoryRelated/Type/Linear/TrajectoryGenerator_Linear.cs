using System;
using System.Collections.Generic;


namespace MoogModule
{
    public class TrajectoryGenerator_Linear : ITrajectoryGenerator
    {
        private MoveByTrajectoryParameters _trajectoryParameters;
        private ITrajectoryProfile _trajectoryProfile;
        private TrajectoryTypeSettings_Linear _trajectoryTypeSettings;

        public TrajectoryGenerator_Linear(MoveByTrajectoryParameters parameters, ITrajectoryProfile trajectoryProfile)
        {
            _trajectoryParameters = parameters;
            _trajectoryProfile = trajectoryProfile;
            _trajectoryTypeSettings = parameters.TrajectoryTypeSettings.Linear;
        }

        public DofParameters[] GetWholePath(int fps)
        {
            int totalPoints = (int)(_trajectoryParameters.MovementDuration.TotalSeconds * fps);
            DofParameters[] trajectory = new DofParameters[totalPoints];
            float deltaTime = 1.0f / fps;

            for (int i = 0; i < totalPoints; i++)
            {
                float currentTime = i * deltaTime;
                float normalizedTime = currentTime / (float)_trajectoryParameters.MovementDuration.TotalSeconds;
                float progress = _trajectoryProfile.NormalizedDisplacement(normalizedTime);
                trajectory[i] = Interpolate(_trajectoryParameters.StartPoint, _trajectoryParameters.EndPoint, progress);
            }

            return trajectory;
        }

        // 'Jump' scenario
        public DofParameters? GetNextPosition()
        {
            TimeSpan timePassed = DateTime.UtcNow - _trajectoryParameters.ScheduledStartTime;
            float normalizedTime = (float)(timePassed / _trajectoryParameters.MovementDuration);

            if (normalizedTime > 1 || normalizedTime < 0)
                return null;

            float normalizedDisplacement = _trajectoryProfile.NormalizedDisplacement(normalizedTime);
            return Interpolate(_trajectoryParameters.StartPoint, _trajectoryParameters.EndPoint, normalizedDisplacement);
        }

        private DofParameters Interpolate(DofParameters start, DofParameters end, float progress)
        {
            return start + (end - start) * progress;
        }
    } 
}