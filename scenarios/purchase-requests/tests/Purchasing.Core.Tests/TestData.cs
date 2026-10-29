using System;

namespace Contoso.Purchasing.Core.Tests
{
    internal static class TestData
    {
        public static PurchaseRequest Request(decimal unitPrice, decimal quantity = 1, string category = RequestCategory.Consumables)
        {
            var request = new PurchaseRequest
            {
                Id = 7,
                Number = "PR-2026-00007",
                RequesterAccount = @"CONTOSO\jkoski",
                RequesterName = "Jari Koski",
                CostCenterCode = "4100",
                SupplierId = 3,
                Category = category,
                Title = "Safety gloves for line 2",
                Justification = "Annual budget",
                Status = RequestStatus.Draft,
                CreatedAt = new DateTime(2026, 3, 2, 9, 0, 0)
            };
            request.Lines.Add(new RequestLine { LineNo = 1, Description = "Item", Quantity = quantity, Unit = "pcs", UnitPrice = unitPrice, VatRate = 25.5m });
            return request;
        }

        public static UserContext Manager()
        {
            var user = new UserContext { Account = @"CONTOSO\mlind", DisplayName = "Maria Lind" };
            user.ManagedCostCenters.Add("4100");
            return user;
        }

        public static UserContext Finance()
        {
            return new UserContext { Account = @"CONTOSO\pvirta", DisplayName = "Pekka Virta", IsFinance = true };
        }

        public static UserContext Director()
        {
            return new UserContext { Account = @"CONTOSO\ahalme", DisplayName = "Anna Halme", IsDirector = true };
        }
    }
}
