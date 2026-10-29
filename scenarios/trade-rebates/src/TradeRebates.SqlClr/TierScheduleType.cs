using System;
using System.Data.SqlTypes;
using System.IO;
using ContosoFoods.TradeRebates.Rules;
using Microsoft.SqlServer.Server;

namespace ContosoFoods.TradeRebates.SqlClr
{
    /// <summary>
    /// rebate.TierSchedule column type. Stored with the binary layout of <see cref="TierSchedule.Write"/>.
    /// </summary>
    [Serializable]
    [SqlUserDefinedType(Format.UserDefined, MaxByteSize = 1024, IsByteOrdered = false, IsFixedLength = false, Name = "TierSchedule")]
    public struct TierScheduleType : INullable, IBinarySerialize
    {
        private TierSchedule _schedule;
        private bool _isNull;

        public bool IsNull => _isNull;

        public static TierScheduleType Null
        {
            get
            {
                var value = new TierScheduleType();
                value._isNull = true;
                return value;
            }
        }

        [SqlMethod(OnNullCall = false)]
        public static TierScheduleType Parse(SqlString text)
        {
            if (text.IsNull)
                return Null;

            var value = new TierScheduleType();
            value._schedule = TierSchedule.Parse(text.Value);
            return value;
        }

        public override string ToString() => _isNull ? "NULL" : _schedule.ToString();

        public SqlString Mode => _isNull ? SqlString.Null : new SqlString(_schedule.Mode.ToString());

        public SqlInt32 TierCount => _isNull ? SqlInt32.Null : new SqlInt32(_schedule.Tiers.Count);

        [SqlMethod(IsDeterministic = true, IsPrecise = true, OnNullCall = false)]
        [return: SqlFacet(Precision = 9, Scale = 4)]
        public SqlDecimal RateFor([SqlFacet(Precision = 19, Scale = 4)] SqlDecimal measure)
        {
            if (_isNull || measure.IsNull)
                return SqlDecimal.Null;
            return new SqlDecimal(_schedule.RateFor(measure.Value));
        }

        [SqlMethod(IsDeterministic = true, IsPrecise = true, OnNullCall = false)]
        [return: SqlFacet(Precision = 19, Scale = 2)]
        public SqlDecimal RebateFor([SqlFacet(Precision = 19, Scale = 4)] SqlDecimal measure, [SqlFacet(Precision = 19, Scale = 2)] SqlDecimal value)
        {
            if (_isNull || measure.IsNull || value.IsNull)
                return SqlDecimal.Null;
            return new SqlDecimal(_schedule.RebateFor(measure.Value, value.Value));
        }

        [SqlMethod(IsDeterministic = true, IsPrecise = true, OnNullCall = false)]
        [return: SqlFacet(Precision = 19, Scale = 4)]
        public SqlDecimal ToNextTier([SqlFacet(Precision = 19, Scale = 4)] SqlDecimal measure)
        {
            if (_isNull || measure.IsNull)
                return SqlDecimal.Null;
            decimal? distance = _schedule.ToNextTier(measure.Value);
            return distance.HasValue ? new SqlDecimal(distance.Value) : SqlDecimal.Null;
        }

        public void Read(BinaryReader r)
        {
            _isNull = false;
            _schedule = TierSchedule.Read(r);
        }

        public void Write(BinaryWriter w)
        {
            _schedule.Write(w);
        }
    }
}
