//using RGB.NET.Core;
//using RGB.NET.Devices.Asus;

namespace AuraScheduler.Worker
{
    public class LightOptions
    {
        public class LEDSchedule
        {

            public TimeOnly LightsOn { get; set; }

            public TimeOnly LightsOff { get; set; }
        }

        public const string SectionName = nameof(LightOptions);

        public bool LightsOn { get; set; }

        public bool ScheduleEnabled { get; set; }

        public LEDSchedule Schedule { get; set; } = new LEDSchedule();

        public bool ShouldLightsBeOn(TimeOnly timeToCheck)
        {
            if (ScheduleEnabled)
            {
                if (Schedule.LightsOn < Schedule.LightsOff)
                    return Schedule.LightsOn <= timeToCheck && Schedule.LightsOff >= timeToCheck;
                else
                    return Schedule.LightsOff <= timeToCheck && Schedule.LightsOn <= timeToCheck;
            }

            return LightsOn;
        }

        public double SecondsUntilNextScheduledTime(TimeOnly fromTime)
        {
            if (ScheduleEnabled)
            {
                if (Schedule.LightsOn > fromTime)
                    return (Schedule.LightsOn - fromTime).TotalSeconds;

                if (Schedule.LightsOff < fromTime)
                    return (Schedule.LightsOn - fromTime).TotalSeconds;

                if (Schedule.LightsOff > fromTime)
                    return (Schedule.LightsOff - fromTime).TotalSeconds;

                if (Schedule.LightsOff < fromTime)
                    return (fromTime - Schedule.LightsOff).TotalSeconds;
            }

            return -1;
        }
    }
}
