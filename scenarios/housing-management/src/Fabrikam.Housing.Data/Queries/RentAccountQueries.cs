using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Fabrikam.Housing.Rent;

namespace Fabrikam.Housing.Data.Queries
{
    public class StatementLine
    {
        public DateTime PostedOn { get; set; }
        public DateTime RentWeek { get; set; }
        public TransactionType Type { get; set; }
        public string Description { get; set; }
        public string Reference { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal Balance { get; set; }
    }

    public class RentAccountSummary
    {
        public int TenancyId { get; set; }
        public string TenancyReference { get; set; }
        public decimal Balance { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public decimal LastPaymentAmount { get; set; }
    }

    public class RentAccountQueries
    {
        private readonly ConnectionFactory _connections;

        public RentAccountQueries(ConnectionFactory connections)
        {
            _connections = connections;
        }

        public async Task<decimal> BalanceAsync(int tenancyId)
        {
            using (var c = _connections.Open())
            {
                var value = await c.ExecuteScalarAsync<double?>(
                    $"SELECT SUM(Amount) FROM {_connections.Table("RentTransactions")} WHERE TenancyId = @tenancyId", new { tenancyId });
                return Math.Round((decimal)(value ?? 0), 2);
            }
        }

        public async Task<IDictionary<int, decimal>> BalancesAsync()
        {
            using (var c = _connections.Open())
            {
                var rows = await c.QueryAsync<(int TenancyId, double Balance)>(
                    $"SELECT TenancyId, SUM(Amount) FROM {_connections.Table("RentTransactions")} GROUP BY TenancyId");
                return rows.ToDictionary(r => r.TenancyId, r => Math.Round((decimal)r.Balance, 2));
            }
        }

        public async Task<IList<StatementLine>> StatementAsync(int tenancyId, DateTime from, DateTime to)
        {
            using (var c = _connections.Open())
            {
                var t = _connections.Table("RentTransactions");
                var opening = await c.ExecuteScalarAsync<double?>($"SELECT SUM(Amount) FROM {t} WHERE TenancyId = @tenancyId AND PostedOn < @from", new { tenancyId, from });
                var rows = (await c.QueryAsync<(DateTime PostedOn, DateTime RentWeek, int Type, string Description, string Reference, double Amount)>(
                    $"SELECT PostedOn, RentWeek, Type, Description, Reference, Amount FROM {t} WHERE TenancyId = @tenancyId AND PostedOn >= @from AND PostedOn < @to ORDER BY PostedOn, Id",
                    new { tenancyId, from, to = to.Date.AddDays(1) })).ToList();

                var balance = Math.Round((decimal)(opening ?? 0), 2);
                var lines = new List<StatementLine>
                {
                    new StatementLine { PostedOn = from, RentWeek = from, Description = "Balance brought forward", Balance = balance }
                };
                foreach (var r in rows)
                {
                    var amount = Math.Round((decimal)r.Amount, 2);
                    balance += amount;
                    lines.Add(new StatementLine
                    {
                        PostedOn = r.PostedOn,
                        RentWeek = r.RentWeek,
                        Type = (TransactionType)r.Type,
                        Description = r.Description,
                        Reference = r.Reference,
                        Debit = amount > 0 ? amount : 0,
                        Credit = amount < 0 ? -amount : 0,
                        Balance = balance
                    });
                }
                return lines;
            }
        }
    }
}
