using System;
using System.Collections.Generic;
using Xunit;

namespace Wingtip.FieldSales.Core.Tests
{
    public class DeliveryTests
    {
        private static readonly DeliveryCalendar Calendar =
            new DeliveryCalendar(new TimeSpan(14, 0, 0), new[] { new DateTime(2017, 12, 6), new DateTime(2017, 12, 25), new DateTime(2017, 12, 26) });

        [Fact]
        public void Before_cut_off_next_working_day()
        {
            Assert.Equal(new DateTime(2017, 11, 3), Calendar.EarliestDelivery(new DateTime(2017, 11, 2, 13, 59, 0)));
        }

        [Fact]
        public void After_cut_off_one_more_day_and_weekend_is_skipped()
        {
            Assert.Equal(new DateTime(2017, 11, 6), Calendar.EarliestDelivery(new DateTime(2017, 11, 2, 14, 0, 0)));
            Assert.Equal(new DateTime(2017, 11, 6), Calendar.EarliestDelivery(new DateTime(2017, 11, 4, 9, 0, 0)));
        }

        [Fact]
        public void Holidays_are_skipped()
        {
            Assert.Equal(new DateTime(2017, 12, 7), Calendar.EarliestDelivery(new DateTime(2017, 12, 5, 10, 0, 0)));
            Assert.Equal(new DateTime(2017, 12, 28), Calendar.EarliestDelivery(new DateTime(2017, 12, 22, 15, 0, 0)));
        }

        [Fact]
        public void Next_route_day_follows_customer_route()
        {
            var route = DeliveryDays.Tuesday | DeliveryDays.Friday;
            Assert.Equal(new DateTime(2017, 11, 7), Calendar.NextRouteDay(new DateTime(2017, 11, 2, 15, 0, 0), route));
            Assert.True(Calendar.IsValidDelivery(new DateTime(2017, 11, 2, 9, 0, 0), route, new DateTime(2017, 11, 3)));
            Assert.False(Calendar.IsValidDelivery(new DateTime(2017, 11, 2, 15, 0, 0), route, new DateTime(2017, 11, 3)));
            Assert.False(Calendar.IsValidDelivery(new DateTime(2017, 11, 2, 9, 0, 0), route, new DateTime(2017, 11, 6)));
        }

        [Fact]
        public void Validator_collects_all_problems()
        {
            var products = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase)
            {
                { "20410", new Product { ItemNo = "20410", Unit = "CS", MinOrderQty = 2, Active = true } },
                { "20999", new Product { ItemNo = "20999", Unit = "CS", MinOrderQty = 1, Active = false } }
            };
            var customer = new Customer { CustomerNo = "C1001", DeliveryDays = DeliveryDays.Monday, CreditBlocked = true };
            var order = new SalesOrder { CustomerNo = "C1001", RequestedDelivery = new DateTime(2017, 11, 3) };
            order.Lines.Add(new OrderLine { ItemNo = "20410", Quantity = 1 });
            order.Lines.Add(new OrderLine { ItemNo = "20999", Quantity = 4 });
            order.Lines.Add(new OrderLine { ItemNo = "20410", Quantity = 3 });

            var errors = new OrderValidator(Calendar).Validate(order, customer, products, new DateTime(2017, 11, 2, 9, 0, 0));

            Assert.Contains(errors, e => e.Contains("credit control"));
            Assert.Contains(errors, e => e.StartsWith("Delivery"));
            Assert.Contains(errors, e => e.Contains("more than one line"));
            Assert.Contains(errors, e => e.Contains("minimum order quantity is 2 CS"));
            Assert.Contains(errors, e => e.Contains("20999 is not sold"));
        }

        [Fact]
        public void Rep_reference_format()
        {
            Assert.Equal("RJ-171102-004", RepReference.Create("rj", new DateTime(2017, 11, 2), 4));
            Assert.True(RepReference.IsValid("RJ-171102-004"));
            Assert.False(RepReference.IsValid("RJ-1711-4"));
        }

        [Theory]
        [InlineData("3.2.0.41", "3.2.0.40", true)]
        [InlineData("3.1.9.0", "3.2.0.0", false)]
        [InlineData("garbage", "3.2.0.0", false)]
        [InlineData("1.0.0.0", "", true)]
        public void Client_version_policy(string client, string minimum, bool expected)
        {
            Assert.Equal(expected, ClientVersionPolicy.IsSupported(client, minimum));
        }
    }
}
