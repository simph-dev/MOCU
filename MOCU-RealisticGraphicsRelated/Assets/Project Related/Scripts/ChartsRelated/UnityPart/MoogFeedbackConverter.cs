using System;
using System.Linq;
using System.Collections.Generic;

using MoogModule;
using System.Reflection;


namespace ChartsModule
{
    public static class MoogFeedbackConverter
    {
        /*public static ChartData ConvertToChartData(MoogFeedback feedback, SpetialChartSettings_Displacement settings, string dofToDisplay = "Surge")
        {
            var seriesList = new List<SeriesData>();

            var allTimestamps = feedback.Commands.Select(i => i.timestamp).Concat(feedback.Responses.Select(i => i.timestamp));

            var startTime = allTimestamps.DefaultIfEmpty(DateTime.UtcNow).Min();

            var commandPoints = feedback.Commands.OrderBy(c => c.timestamp).ToList();
            var responsePoints = feedback.Responses.OrderBy(r => r.timestamp).ToList();

            var stateChangePoints = new List<PointData>();
            var errorPoints = new List<PointData>();

            // Commands
            if (commandPoints.Any())
            {
                var series = new SeriesData { Title = $"command ({dofToDisplay})", LineColor = settings.CommandColor };
                series.Series = commandPoints.Select((c, index) =>
                {
                    var pointColor = settings.CommandColor;

                    // delay
                    if (index > 0)
                    {
                        var timeGap = c.timestamp - commandPoints[index - 1].timestamp;

                        if (timeGap > settings.MaxDelay)
                            pointColor = settings.ExceededDelayColor;
                    }

                    return new PointData
                    {
                        X = (c.timestamp - startTime).TotalMilliseconds,
                        Y = GetDofValueByName(c.position, dofToDisplay),
                        PointColor = pointColor
                    };
                });
                seriesList.Add(series);
            }

            // Responses
            if (responsePoints.Any())
            {
                var series = new SeriesData { Title = $"Response ({dofToDisplay})", LineColor = settings.ResponseColor };
                series.Series = responsePoints.Select((r, index) =>
                {
                    var realTimeState = r.feedback;
                    var yValue = GetDofValueByName(realTimeState.Position, dofToDisplay);
                    var velocity = GetDofValueByName(realTimeState.Velocity, dofToDisplay);
                    var acceleration = GetDofValueByName(realTimeState.Acceleration, dofToDisplay);
                    var pointColor = settings.ResponseColor;
                    var currentX = (r.timestamp - startTime).TotalMilliseconds;

                    // acceleration
                    if (Math.Abs(acceleration) > settings.MaxAcceleration)
                        pointColor = settings.ExceededAccelerationColor;

                    // delay
                    if (index > 0)
                    {
                        var timeGap = r.timestamp - responsePoints[index - 1].timestamp;

                        if (timeGap > settings.MaxDelay)
                            pointColor = settings.ExceededDelayColor;
                    }

                    // state
                    if (index > 0 && realTimeState.EncodedMachineState != responsePoints[index - 1].feedback.EncodedMachineState)
                    {
                        stateChangePoints.Add(new PointData
                        {
                            X = currentX,
                            Y = yValue,
                            Label = realTimeState.EncodedMachineState.ToString(),
                            PointColor = settings.StateLabelColor,
                            PointSize = 8,
                            TooltipText = $"State: {realTimeState.EncodedMachineState}\nTime: {r.timestamp:HH:mm:ss.fff}"
                        });
                    }

                    // error
                    if (!String.IsNullOrEmpty(realTimeState.Faults))
                    {
                        errorPoints.Add(new PointData
                        {
                            X = currentX,
                            Y = yValue,
                            Label = realTimeState.Faults,
                            PointColor = settings.ErrorLabelColor,
                            PointSize = 10,
                            TooltipText = $"Error: {realTimeState.Faults}\nTime: {r.timestamp:HH:mm:ss.fff}"
                        });
                    }

                    return new PointData
                    {
                        X = (r.timestamp - startTime).TotalMilliseconds,
                        Y = yValue,
                        PointColor = pointColor,
                        TooltipText = $"Time: {r.timestamp:HH:mm:ss.fff}\n" +
                                      $"Position: {yValue:F4}\n" +
                                      $"Velocity: {velocity:F4}\n" +
                                      $"Acceleration: {acceleration:F4}"
                    };
                });
                seriesList.Add(series);
            }

            // States
            if (stateChangePoints.Any())
                seriesList.Add(new SeriesData { Title = "State Changes", ConnectPoints = false, Series = stateChangePoints });

            // Errors
            if (errorPoints.Any())
                seriesList.Add(new SeriesData { Title = "Errors", ConnectPoints = false, Series = errorPoints });

            // Final
            return new ChartData
            {
                Title = $"Moog Motion: {dofToDisplay}",
                XLabel = "Time (milliseconds)",
                YLabel = dofToDisplay,
                Series = seriesList
            };
        }

        private static double GetDofValueByName(DofParameters parameters, string dofName)
        {
            return dofName switch
            {
                "Roll" => parameters.Roll,
                "Pitch" => parameters.Pitch,
                "Yaw" => parameters.Yaw,
                "Surge" => parameters.Surge,
                "Sway" => parameters.Sway,
                "Heave" => parameters.Heave,
                _ => double.NaN // Возвращаем NaN если имя неверное
            };
        }*/
    }
}