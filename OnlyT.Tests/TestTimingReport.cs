﻿namespace OnlyT.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using OnlyT.Report.Services;
    using OnlyT.Services.Report;

    [TestClass]
    public class TestTimingReport
    {
        private const int TotalMtgLengthMins = 105;

        // 3 mins 20 secs for interim song
        private readonly TimeSpan _interimDuration = new TimeSpan(0, 3, 20);

        private readonly Random _random = new Random();

        private bool _useRandomTimes = false;

        [TestMethod]
        public void TestReportGeneration()
        {
            var dateTimeService = new DateTimeServiceForTests();

            const int weekCount = 20;
            var dateOfFirstMeeting = GetNearestDayOnOrAfter(DateTime.Today.AddDays(-weekCount * 7), DayOfWeek.Sunday).Date;

            for (int wk = 0; wk < weekCount; ++wk)
            {
                var dateOfWeekendMtg = dateOfFirstMeeting.AddDays(wk * 7);
                var dateOfMidweekMtg = dateOfWeekendMtg.AddDays(4);

                StoreWeekendData(wk, dateOfWeekendMtg, dateTimeService);
                StoreMidweekData(wk, weekCount, dateOfMidweekMtg, dateTimeService);
            }
        }

        private void StoreWeekendData(
            int week,
            DateTime dateOfWeekendMtg, 
            DateTimeServiceForTests dateTimeService)
        {
            var startOfMtg = dateOfWeekendMtg + TimeSpan.FromHours(10);
            var plannedEnd = startOfMtg.AddMinutes(TotalMtgLengthMins);

            var service = new LocalTimingDataStoreService(null, dateTimeService);

            if (week == 0)
            {
                service.DeleteAllData();
            }

            dateTimeService.Set(startOfMtg);

            service.InsertMeetingStart(startOfMtg);
            service.InsertPlannedMeetingEnd(plannedEnd);

            dateTimeService.Add(TimeSpan.FromSeconds(1));

            // song and prayer
            InsertTimer(service, dateTimeService, "Introductory Segment", false, 5, false);

            // public talk...
            InsertTimer(service, dateTimeService, "Public Talk", false, 30);

            // song
            InsertTimer(service, dateTimeService, "Interim Segment", false, _interimDuration, false);

            // WT...
            InsertTimer(service, dateTimeService, "Watchtower Study", false, 60);

            InsertTimer(service, dateTimeService, "Concluding Segment", false, 5, false);

            service.InsertActualMeetingEnd();

            service.Save();
        }

        private void StoreMidweekData(
            int week,
            int weekCount,
            DateTime dateOfMidweekMtg,
            DateTimeServiceForTests dateTimeService)
        {
            var startOfMtg = dateOfMidweekMtg + TimeSpan.FromHours(19);
            var plannedEnd = startOfMtg.AddMinutes(TotalMtgLengthMins);

            var service = new LocalTimingDataStoreService(null, dateTimeService);

            dateTimeService.Set(startOfMtg);

            service.InsertMeetingStart(startOfMtg);
            service.InsertPlannedMeetingEnd(plannedEnd);
            
            InsertTimer(service, dateTimeService, "Introductory Segment", false, 5, false);

            InsertTimer(service, dateTimeService, "Opening Comments", false, 3);

            InsertTimer(service, dateTimeService, "Treasures", false, 10);

            InsertTimer(service, dateTimeService, "Digging for Spiritual Gems", false, 8);

            InsertTimer(service, dateTimeService, "Bible Reading", true, 4);
            dateTimeService.Add(GetCounselDuration());

            InsertTimer(service, dateTimeService, "Ministry Talk 1", true, 2);
            dateTimeService.Add(GetCounselDuration());

            InsertTimer(service, dateTimeService, "Ministry Talk 2", true, 4);
            dateTimeService.Add(GetCounselDuration());

            InsertTimer(service, dateTimeService, "Ministry Talk 3", true, 6);
            dateTimeService.Add(GetCounselDuration());

            InsertTimer(service, dateTimeService, "Interim Segment", false, _interimDuration, false);

            InsertTimer(service, dateTimeService, "Living Item 1", false, 15);

            InsertTimer(service, dateTimeService, "Congregation Bible Study", false, 30);

            InsertTimer(service, dateTimeService, "Review", false, 3);

            InsertTimer(service, dateTimeService, "Concluding Segment", false, 5, false);

            service.InsertActualMeetingEnd();

            service.Save();

            if (week == weekCount - 1)
            {
                var file = TimingReportGeneration.ExecuteAsync(service, null).Result;
                Assert.IsNotNull(file);
            }
        }

        private TimeSpan GetCounselDuration()
        {
            if (_useRandomTimes)
            {
                return TimeSpan.FromSeconds(_random.Next(70, 90));
            }

            return TimeSpan.FromSeconds(80);
        }

        private void InsertTimer(
            LocalTimingDataStoreService service,
            DateTimeServiceForTests dateTimeService,
            string talkDescription, 
            bool isStudentTalk, 
            TimeSpan target,
            bool addChangeoverTime = true)
        {
            service.InsertTimerStart(talkDescription, isStudentTalk, target, target);
            dateTimeService.Add(GetDuration(target.TotalMinutes));
            service.InsertTimerStop();

            if (addChangeoverTime)
            {
                // add an amount for speaker swap
                AddSpeakerChangeoverTime(dateTimeService, isStudentTalk);
            }
        }

        private void AddSpeakerChangeoverTime(DateTimeServiceForTests dateTimeService, bool isStudentTalk)
        {
            if (_useRandomTimes)
            {
                dateTimeService.Add(GetDuration(0.3));
            }
            else
            {
                if (!isStudentTalk)
                {
                    dateTimeService.Add(new TimeSpan(0, 0, 20));
                }
            }
        }

        private void InsertTimer(
            LocalTimingDataStoreService service,
            DateTimeServiceForTests dateTimeService,
            string talkDescription,
            bool isStudentTalk,
            int targetMins,
            bool addChangeoverTime = true)
        {
            InsertTimer(service, dateTimeService, talkDescription, isStudentTalk, TimeSpan.FromMinutes(targetMins), addChangeoverTime);
        }

        private DateTime GetNearestDayOnOrAfter(DateTime dt, DayOfWeek dayOfWeek)
        {
            return dt.AddDays(((int)dayOfWeek - (int)dt.DayOfWeek + 7) % 7);
        }

        private TimeSpan GetDuration(double targetMinutes)
        {
            if (_useRandomTimes)
            {
                int variationSecs = (int)((targetMinutes / 20.0) * 60);
                var secsToAdd = _random.Next(-variationSecs, variationSecs);

                var result = targetMinutes + (secsToAdd / 60.0);
                return TimeSpan.FromMinutes(result);
            }

            return TimeSpan.FromMinutes(targetMinutes);
        }

        private class DateTimeServiceForTests : IDateTimeService
        {
            private DateTime _value;

            public void Set(DateTime dt)
            {
                _value = dt;
            }

            public void Add(TimeSpan timeSpan)
            {
                _value = _value + timeSpan;
            }

            public DateTime Now()
            {
                if (_value == default(DateTime))
                {
                    throw new ArgumentException();
                }

                return _value;
            }
        }
    }
}
