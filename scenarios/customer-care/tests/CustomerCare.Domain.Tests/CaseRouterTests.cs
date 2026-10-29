using Tailspin.CustomerCare.Domain;
using Xunit;

namespace Tailspin.CustomerCare.Domain.Tests
{
    public class CaseRouterTests
    {
        private static CaseRouter Router()
        {
            return new CaseRouter(
                new[]
                {
                    new RoutingRule { Id = 1, Sequence = 10, Segment = "LCO", QueueCode = "KEYACC" },
                    new RoutingRule { Id = 2, Sequence = 20, CategoryCode = "OUTAGE", PostalCodePrefix = "33", QueueCode = "FIELD-WEST" },
                    new RoutingRule { Id = 3, Sequence = 30, CategoryCode = "OUTAGE", QueueCode = "FIELD" }
                },
                new[]
                {
                    new Category { Code = "BILL", QueueCode = "BILLING" },
                    new Category { Code = "MOVE", QueueCode = null }
                },
                "GENERAL");
        }

        [Fact]
        public void Large_commercial_goes_to_key_accounts()
        {
            Assert.Equal("KEYACC", Router().Route("OUTAGE", new Customer { Segment = "LCO", PostalCode = "33100" }));
        }

        [Fact]
        public void Postal_prefix_rule()
        {
            Assert.Equal("FIELD-WEST", Router().Route("OUTAGE", new Customer { Segment = "RES", PostalCode = "33100" }));
            Assert.Equal("FIELD", Router().Route("OUTAGE", new Customer { Segment = "RES", PostalCode = "00100" }));
        }

        [Fact]
        public void Falls_back_to_category_then_default()
        {
            Assert.Equal("BILLING", Router().Route("bill", new Customer { Segment = "RES" }));
            Assert.Equal("GENERAL", Router().Route("MOVE", new Customer { Segment = "RES" }));
            Assert.Equal("GENERAL", Router().Route("UNKNOWN", null));
        }

        [Fact]
        public void Priority_customer_starts_high()
        {
            Assert.Equal(CasePriority.High, CaseRouter.InitialPriority(CasePriority.Low, new Customer { IsPriorityCustomer = true }));
            Assert.Equal(CasePriority.Critical, CaseRouter.InitialPriority(CasePriority.Critical, new Customer { IsPriorityCustomer = true }));
            Assert.Equal(CasePriority.Low, CaseRouter.InitialPriority(CasePriority.Low, new Customer()));
        }
    }
}
