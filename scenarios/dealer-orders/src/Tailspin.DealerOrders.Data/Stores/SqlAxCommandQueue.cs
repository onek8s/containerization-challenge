using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.Data.Stores
{
    /// <summary>
    /// AX command queue in ax.Command. Claims are serialized with an application lock so that two connector instances
    /// (or a restart during a claim) never run commands of the same sequence key side by side.
    /// </summary>
    public class SqlAxCommandQueue : IAxCommandQueue
    {
        private const string Columns = "Id, CommandType, State, DealerId, QuoteId, OrderId, SequenceKey, Payload, RequestedBy, RunAsAxUser, CreatedUtc, NextAttemptUtc, Attempts, CompletedUtc, LastError, Result";
        private readonly SqlConnectionFactory _connections;

        public SqlAxCommandQueue(SqlConnectionFactory connections)
        {
            _connections = connections;
        }

        /// <summary>Running commands older than this are considered abandoned (service killed during a call) and retried.</summary>
        public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(15);

        public void Enqueue(AxCommand command)
        {
            using (var c = _connections.Open())
            {
                command.Id = c.ExecuteScalar<long>(@"
INSERT ax.Command (CommandType, State, DealerId, QuoteId, OrderId, SequenceKey, Payload, RequestedBy, RunAsAxUser, CreatedUtc, NextAttemptUtc, Attempts)
OUTPUT inserted.Id
VALUES (@CommandType, @State, @DealerId, @QuoteId, @OrderId, @SequenceKey, @Payload, @RequestedBy, @RunAsAxUser, @CreatedUtc, @NextAttemptUtc, 0);", command);
            }
        }

        public AxCommand ClaimNext(DateTime utcNow)
        {
            using (var c = _connections.Open())
            using (var tx = c.BeginTransaction())
            {
                var lockResult = c.ExecuteScalar<int>(@"
DECLARE @r int;
EXEC @r = sp_getapplock @Resource = 'ax.Command.claim', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
SELECT @r;", transaction: tx);
                if (lockResult < 0) throw new TimeoutException("Could not get the AX command claim lock.");

                c.Execute("UPDATE ax.Command SET State = @retry, NextAttemptUtc = @now, LastError = 'Abandoned while running' WHERE State = @running AND ClaimedUtc < @stale",
                    new { retry = AxCommandState.Retry, running = AxCommandState.Running, now = utcNow, stale = utcNow - StaleAfter }, tx);

                var command = c.QueryFirstOrDefault<AxCommand>($@"
SELECT TOP (1) {Columns}
FROM ax.Command c
WHERE c.State IN (@pending, @retry) AND c.NextAttemptUtc <= @now
  AND NOT EXISTS (SELECT 1 FROM ax.Command o WHERE o.SequenceKey = c.SequenceKey AND o.Id < c.Id AND o.State IN (@pending, @running, @retry, @failed))
  AND NOT EXISTS (SELECT 1 FROM ax.Command r WHERE r.SequenceKey = c.SequenceKey AND r.State IN (@running, @failed))
ORDER BY c.Id;",
                    new { pending = AxCommandState.Pending, retry = AxCommandState.Retry, running = AxCommandState.Running, failed = AxCommandState.Failed, now = utcNow }, tx);

                if (command != null)
                {
                    c.Execute("UPDATE ax.Command SET State = @running, ClaimedUtc = @now, ClaimedBy = HOST_NAME() WHERE Id = @Id",
                        new { running = AxCommandState.Running, now = utcNow, command.Id }, tx);
                    command.State = AxCommandState.Running;
                }
                tx.Commit();
                return command;
            }
        }

        public void Save(AxCommand command)
        {
            using (var c = _connections.Open())
                c.Execute(@"
UPDATE ax.Command SET State = @State, NextAttemptUtc = @NextAttemptUtc, Attempts = @Attempts, CompletedUtc = @CompletedUtc,
       LastError = LEFT(@LastError, 4000), Result = LEFT(@Result, 1000), RunAsAxUser = @RunAsAxUser
WHERE Id = @Id;", command);
        }

        public IList<AxCommand> ForOrder(int orderId)
        {
            using (var c = _connections.Open())
                return c.Query<AxCommand>($"SELECT {Columns} FROM ax.Command WHERE OrderId = @orderId ORDER BY Id", new { orderId }).ToList();
        }

        public IList<AxCommand> Recent(int max)
        {
            using (var c = _connections.Open())
                return c.Query<AxCommand>($"SELECT TOP (@max) {Columns} FROM ax.Command ORDER BY Id DESC", new { max }).ToList();
        }

        public AxCommand Get(long id)
        {
            using (var c = _connections.Open())
                return c.QueryFirstOrDefault<AxCommand>($"SELECT {Columns} FROM ax.Command WHERE Id = @id", new { id });
        }

        /// <summary>Sales desk: puts a failed command back in the queue (after the data has been fixed).</summary>
        public void Requeue(long id, string user, DateTime utcNow)
        {
            using (var c = _connections.Open())
                c.Execute("UPDATE ax.Command SET State = @pending, NextAttemptUtc = @utcNow, Attempts = 0, LastError = CONCAT('Requeued by ', @user, '. ', LastError) WHERE Id = @id AND State = @failed",
                    new { id, user, utcNow, pending = AxCommandState.Pending, failed = AxCommandState.Failed });
        }

        /// <summary>Sales desk: gives up on a failed command so the sequence can continue.</summary>
        public void Cancel(long id, string user)
        {
            using (var c = _connections.Open())
                c.Execute("UPDATE ax.Command SET State = @cancelled, Result = CONCAT('Cancelled by ', @user) WHERE Id = @id AND State IN (@failed, @pending, @retry)",
                    new { id, user, cancelled = AxCommandState.Cancelled, failed = AxCommandState.Failed, pending = AxCommandState.Pending, retry = AxCommandState.Retry });
        }

        public QueueCounts Counts()
        {
            using (var c = _connections.Open())
                return c.QueryFirst<QueueCounts>(@"
SELECT SUM(CASE WHEN State = 0 THEN 1 ELSE 0 END) AS Pending, SUM(CASE WHEN State = 1 THEN 1 ELSE 0 END) AS Running,
       SUM(CASE WHEN State = 3 THEN 1 ELSE 0 END) AS Retrying, SUM(CASE WHEN State = 4 THEN 1 ELSE 0 END) AS Failed,
       MIN(CASE WHEN State IN (0, 3) THEN CreatedUtc END) AS OldestOpenUtc
FROM ax.Command;");
        }
    }

    public class QueueCounts
    {
        public int? Pending { get; set; }
        public int? Running { get; set; }
        public int? Retrying { get; set; }
        public int? Failed { get; set; }
        public DateTime? OldestOpenUtc { get; set; }
    }
}
