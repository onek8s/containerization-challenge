using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Scheduling;

namespace Southridge.Clinic.Tests
{
    [TestClass]
    public class SlotEngineTests
    {
        // Tuesday 2013-03-12
        private static readonly DateTime Day = new DateTime(2013, 3, 12);

        private static TemplateWindow Morning(int providerId, string types)
        {
            return SlotEngine.Resolve(Day, providerId, 1, new TimeSpan(8, 0, 0), new TimeSpan(12, 0, 0), 20, types, 1);
        }

        [TestMethod]
        public void EmptyMorningGivesTwelveTwentyMinuteSlots()
        {
            var engine = new SlotEngine(new ClinicCalendar());
            IList<AvailableSlot> slots = engine.FindSlots(Day, "FU", 20, new[] { Morning(10, null) }, new BusyPeriod[0], Day);

            Assert.AreEqual(12, slots.Count);
            Assert.AreEqual(Day.AddHours(8), slots[0].Start);
            Assert.AreEqual(Day.AddHours(11).AddMinutes(40), slots.Last().Start);
        }

        [TestMethod]
        public void LongerVisitsDoNotRunPastTheWindow()
        {
            var engine = new SlotEngine(new ClinicCalendar());
            IList<AvailableSlot> slots = engine.FindSlots(Day, "NP", 40, new[] { Morning(10, null) }, new BusyPeriod[0], Day);

            Assert.AreEqual(Day.AddHours(11).AddMinutes(20), slots.Last().Start);
            Assert.IsTrue(slots.All(s => s.End <= Day.AddHours(12)));
        }

        [TestMethod]
        public void BookedAppointmentsAndProviderBlocksAreSkipped()
        {
            var engine = new SlotEngine(new ClinicCalendar());
            var busy = new[]
            {
                new BusyPeriod { ProviderId = 10, Start = Day.AddHours(8), End = Day.AddHours(8).AddMinutes(20) },
                new BusyPeriod { ProviderId = 10, Start = Day.AddHours(9), End = Day.AddHours(10), IsBlock = true },
                new BusyPeriod { ProviderId = 11, Start = Day.AddHours(8).AddMinutes(20), End = Day.AddHours(9) }
            };

            IList<AvailableSlot> slots = engine.FindSlots(Day, "FU", 20, new[] { Morning(10, null) }, busy, Day);

            Assert.AreEqual(Day.AddHours(8).AddMinutes(20), slots[0].Start, "another provider's appointment does not block");
            Assert.IsFalse(slots.Any(s => s.Start >= Day.AddHours(9) && s.Start < Day.AddHours(10)));
            Assert.AreEqual(8, slots.Count);
        }

        [TestMethod]
        public void SiteClosureBlocksEveryProviderAtThatSite()
        {
            var engine = new SlotEngine(new ClinicCalendar());
            var closure = new BusyPeriod { SiteId = 1, IsBlock = true, Start = Day.AddHours(10), End = Day.AddHours(12) };

            IList<AvailableSlot> slots = engine.FindSlots(Day, "FU", 20, new[] { Morning(10, null), Morning(11, null) }, new[] { closure }, Day);

            Assert.AreEqual(12, slots.Count);
            Assert.IsTrue(slots.All(s => s.End <= Day.AddHours(10)));
        }

        [TestMethod]
        public void TemplateRestrictsAppointmentTypes()
        {
            var engine = new SlotEngine(new ClinicCalendar());
            TemplateWindow window = Morning(10, "NP, WELL");

            Assert.AreEqual(0, engine.FindSlots(Day, "FU", 20, new[] { window }, new BusyPeriod[0], Day).Count);
            Assert.AreEqual(12, engine.FindSlots(Day, "well", 20, new[] { window }, new BusyPeriod[0], Day).Count);
        }

        [TestMethod]
        public void NothingBeforeTheEarliestStart()
        {
            var engine = new SlotEngine(new ClinicCalendar());
            IList<AvailableSlot> slots = engine.FindSlots(Day, "FU", 20, new[] { Morning(10, null) }, new BusyPeriod[0], Day.AddHours(10).AddMinutes(5));

            Assert.AreEqual(Day.AddHours(10).AddMinutes(20), slots[0].Start);
        }

        [TestMethod]
        public void ClosedDaysHaveNoSlots()
        {
            DateTime independenceDay = new DateTime(2013, 7, 4);
            var engine = new SlotEngine(new ClinicCalendar(new[] { Day }));

            Assert.AreEqual(0, engine.FindSlots(Day, "FU", 20, new[] { Morning(10, null) }, new BusyPeriod[0], Day).Count);
            TemplateWindow holiday = SlotEngine.Resolve(independenceDay, 10, 1, new TimeSpan(8, 0, 0), new TimeSpan(12, 0, 0), 20, null, 0);
            Assert.AreEqual(0, engine.FindSlots(independenceDay, "FU", 20, new[] { holiday }, new BusyPeriod[0], independenceDay).Count);
        }

        [TestMethod]
        public void FirstPerProviderAndDayKeepsEarliestSlot()
        {
            var slots = new[]
            {
                new AvailableSlot { ProviderId = 10, Start = Day.AddHours(9) },
                new AvailableSlot { ProviderId = 10, Start = Day.AddHours(8) },
                new AvailableSlot { ProviderId = 11, Start = Day.AddHours(10) },
                new AvailableSlot { ProviderId = 10, Start = Day.AddDays(1).AddHours(8) }
            };

            IList<AvailableSlot> first = SlotEngine.FirstPerProviderAndDay(slots);

            Assert.AreEqual(3, first.Count);
            Assert.AreEqual(Day.AddHours(8), first[0].Start);
            Assert.AreEqual(11, first[1].ProviderId);
        }
    }
}
