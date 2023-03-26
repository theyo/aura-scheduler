namespace AuraScheduler.Worker.Tests
{
    [TestClass]
    public class LightOptions_Tests
    {
        private LightOptions GetDefaultOptions()
        {
            return new LightOptions()
            {
                LightsOn = true,
                ScheduleEnabled = true,
                Schedule = new LightOptions.LEDSchedule()
                {
                    LightsOn = new TimeOnly(7, 0, 0),
                    LightsOff = new TimeOnly(22, 30, 0)
                }
            };
        }

        [TestMethod]
        public void ShouldLightsBeOn_WhenLightsOffAndScheduleOff_ReturnsFalse()
        {
            var lightOptions = GetDefaultOptions();
            lightOptions.ScheduleEnabled = false;
            lightOptions.LightsOn = false;

            var shouldLightsBeOn = lightOptions.ShouldLightsBeOn(new TimeOnly(7, 30, 0));

            Assert.IsFalse(shouldLightsBeOn);
        }

        [TestMethod]
        public void ShouldLightsBeOn_WhenLightsOnAndScheduleOff_ReturnsTrue()
        {
            var lightOptions = GetDefaultOptions();
            lightOptions.ScheduleEnabled = false;

            var shouldLightsBeOn = lightOptions.ShouldLightsBeOn(new TimeOnly(7, 30, 0));

            Assert.IsTrue(shouldLightsBeOn);
        }

        [TestMethod]
        public void ShouldLightsBeOn_WhenTimeIsExactScheduleStart_ReturnsTrue()
        {
            var lightOptions = GetDefaultOptions();

            var shouldLightsBeOn = lightOptions.ShouldLightsBeOn(new TimeOnly(7, 0, 0));

            Assert.IsTrue(shouldLightsBeOn);
        }

        [TestMethod]
        public void ShouldLightsBeOn_WhenTimeDuringSchedule_ReturnsTrue()
        {
            var lightOptions = GetDefaultOptions();

            var shouldLightsBeOn = lightOptions.ShouldLightsBeOn(new TimeOnly(7, 30, 0));

            Assert.IsTrue(shouldLightsBeOn);
        }

        [TestMethod]
        public void ShouldLightsBeOn_WhenTimeIsExactScheduleEnd_ReturnsTrue()
        {
            var lightOptions = GetDefaultOptions();

            var shouldLightsBeOn = lightOptions.ShouldLightsBeOn(new TimeOnly(22, 30, 0));

            Assert.IsTrue(shouldLightsBeOn);
        }

        [TestMethod]
        public void ShouldLightsBeOn_WhenTimeBeforeScheduleStart_ReturnsFalse()
        {
            var lightOptions = GetDefaultOptions();

            var shouldLightsBeOn = lightOptions.ShouldLightsBeOn(new TimeOnly(6, 0, 0));

            Assert.IsFalse(shouldLightsBeOn);
        }

        [TestMethod]
        public void ShouldLightsBeOn_WhenTimeAfterScheduleEnd_ReturnsFalse()
        {
            var lightOptions = GetDefaultOptions();

            var shouldLightsBeOn = lightOptions.ShouldLightsBeOn(new TimeOnly(23, 0, 0));

            Assert.IsFalse(shouldLightsBeOn);
        }

        [TestMethod]
        public void ShouldLightsBeOn_WhenScheduleStartTimeIsAfterEndTime_ReturnsTrue()
        {
            var lightOptions = GetDefaultOptions();
            lightOptions.Schedule.LightsOn = new TimeOnly(14, 0, 0);
            lightOptions.Schedule.LightsOff = new TimeOnly(13, 59, 0);

            var shouldLightsBeOn = lightOptions.ShouldLightsBeOn(new TimeOnly(14, 58, 0));

            Assert.IsTrue(shouldLightsBeOn);
        }

        [TestMethod]
        public void SecondsUntilNextScheduledTime_WhenFromTimeBeforeScheduledStart_ReturnsSecondsUntilScheduledStart()
        {
            var lightOptions = GetDefaultOptions();

            var expectedSeconds = TimeSpan.FromMinutes(30).TotalSeconds;

            var secondsUntilNextScheduledTime = lightOptions.SecondsUntilNextScheduledTime(new TimeOnly(6,30,0));

            Assert.AreEqual(expectedSeconds, secondsUntilNextScheduledTime);
        }

        [TestMethod]
        public void SecondsUntilNextScheduledTime_WhenFromTimeAfterScheduledStart_ReturnsSecondsUntilScheduleEnd()
        {
            var lightOptions = GetDefaultOptions();

            var expectedSeconds = TimeSpan.FromMinutes(30).TotalSeconds;

            var secondsUntilNextScheduledTime = lightOptions.SecondsUntilNextScheduledTime(new TimeOnly(22, 0, 0));

            Assert.AreEqual(expectedSeconds, secondsUntilNextScheduledTime);
        }

        [TestMethod]
        public void SecondsUntilNextScheduledTime_WhenFromTimeAfterScheduledEnd_ReturnsSecondsUntilScheduledStart()
        {
            var lightOptions = GetDefaultOptions();

            var expectedSeconds = TimeSpan.FromHours(8).TotalSeconds;

            var secondsUntilNextScheduledTime = lightOptions.SecondsUntilNextScheduledTime(new TimeOnly(23, 0, 0));

            Assert.AreEqual(expectedSeconds, secondsUntilNextScheduledTime);
        }

        [TestMethod]
        public void SecondsUntilNextScheduledTime_WhenScheduleStartTimeIsAfterEndTime_ReturnsSecondsUntilScheduledStart()
        {
            var lightOptions = GetDefaultOptions();
            lightOptions.Schedule.LightsOn = new TimeOnly(14, 00, 0);
            lightOptions.Schedule.LightsOff = new TimeOnly(13, 00, 0);

            var expectedSeconds = TimeSpan.FromHours(23).TotalSeconds;

            var secondsUntilNextScheduledTime = lightOptions.SecondsUntilNextScheduledTime(new TimeOnly(15, 00, 0));

            Assert.AreEqual(expectedSeconds, secondsUntilNextScheduledTime);
        }
    }
}