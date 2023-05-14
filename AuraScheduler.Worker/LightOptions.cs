namespace AuraScheduler.Worker
{
    public enum LightMode
    {
        On,
        Off,
        Schedule
    }

    public class LightOptions
    {
        public class LEDSchedule
        {

            public TimeOnly LightsOn { get; set; }

            public TimeOnly LightsOff { get; set; }
        }

        public const string SectionName = nameof(LightOptions);

        public LightMode LightMode { get; set; } = LightMode.Schedule;

        public bool ScheduleEnabled => LightMode == LightMode.Schedule;

        public LEDSchedule Schedule { get; set; } = new LEDSchedule();

        public bool ShouldLightsBeOn(TimeOnly timeToCheck)
        {
            bool lightsOn;

            switch (LightMode)
            {
                case LightMode.On:
                    lightsOn = true;
                    break;
                case LightMode.Off:
                    lightsOn = false;
                    break;
                case LightMode.Schedule:
                    if (Schedule.LightsOn < Schedule.LightsOff)
                        lightsOn = Schedule.LightsOn <= timeToCheck && Schedule.LightsOff >= timeToCheck;
                    else
                        lightsOn = Schedule.LightsOff <= timeToCheck && Schedule.LightsOn <= timeToCheck;
                    break;
                default:
                    lightsOn = false;
                    break;
            }

            return lightsOn;
        }

        public double SecondsUntilNextScheduledTime(TimeOnly fromTime)
        {
            if (LightMode == LightMode.Schedule)
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
