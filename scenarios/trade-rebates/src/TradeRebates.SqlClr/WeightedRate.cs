using System;
using System.Data.SqlTypes;
using System.IO;
using Microsoft.SqlServer.Server;

namespace ContosoFoods.TradeRebates.SqlClr
{
    /// <summary>rebate.WeightedRate(rate, weight): value-weighted average of rebate rates.</summary>
    [Serializable]
    [SqlUserDefinedAggregate(Format.UserDefined, MaxByteSize = 64, IsInvariantToDuplicates = false, IsInvariantToNulls = true,
        IsInvariantToOrder = true, IsNullIfEmpty = true, Name = "WeightedRate")]
    public struct WeightedRate : IBinarySerialize
    {
        private decimal _weightedSum;
        private decimal _weight;

        public void Init()
        {
            _weightedSum = 0m;
            _weight = 0m;
        }

        public void Accumulate([SqlFacet(Precision = 9, Scale = 4)] SqlDecimal rate, [SqlFacet(Precision = 19, Scale = 2)] SqlDecimal weight)
        {
            if (rate.IsNull || weight.IsNull)
                return;
            _weightedSum += rate.Value * weight.Value;
            _weight += weight.Value;
        }

        public void Merge(WeightedRate other)
        {
            _weightedSum += other._weightedSum;
            _weight += other._weight;
        }

        [return: SqlFacet(Precision = 9, Scale = 4)]
        public SqlDecimal Terminate()
        {
            if (_weight == 0m)
                return SqlDecimal.Null;
            return new SqlDecimal(Math.Round(_weightedSum / _weight, 4, MidpointRounding.AwayFromZero));
        }

        public void Read(BinaryReader r)
        {
            _weightedSum = r.ReadDecimal();
            _weight = r.ReadDecimal();
        }

        public void Write(BinaryWriter w)
        {
            w.Write(_weightedSum);
            w.Write(_weight);
        }
    }
}
