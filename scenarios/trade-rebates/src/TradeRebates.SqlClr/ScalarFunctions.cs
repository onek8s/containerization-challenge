using System.Data.SqlTypes;
using ContosoFoods.TradeRebates.Rules;
using Microsoft.SqlServer.Server;

namespace ContosoFoods.TradeRebates.SqlClr
{
    public static class ScalarFunctions
    {
        [SqlFunction(Name = "GtinIsValid", IsDeterministic = true, IsPrecise = true, DataAccess = DataAccessKind.None)]
        public static SqlBoolean GtinIsValid([SqlFacet(MaxSize = 14)] SqlString gtin)
        {
            if (gtin.IsNull)
                return SqlBoolean.Null;
            return Gtin.IsValid(gtin.Value);
        }

        [SqlFunction(Name = "IbanIsValid", IsDeterministic = true, IsPrecise = true, DataAccess = DataAccessKind.None)]
        public static SqlBoolean IbanIsValid([SqlFacet(MaxSize = 42)] SqlString iban)
        {
            if (iban.IsNull)
                return SqlBoolean.Null;
            return Iban.IsValid(iban.Value);
        }

        [SqlFunction(Name = "IbanMask", IsDeterministic = true, IsPrecise = true, DataAccess = DataAccessKind.None)]
        [return: SqlFacet(MaxSize = 42)]
        public static SqlString IbanMask([SqlFacet(MaxSize = 42)] SqlString iban)
        {
            if (iban.IsNull)
                return SqlString.Null;
            return Iban.Mask(iban.Value);
        }

        [SqlFunction(Name = "TierScheduleError", IsDeterministic = true, IsPrecise = true, DataAccess = DataAccessKind.None)]
        [return: SqlFacet(MaxSize = 400)]
        public static SqlString TierScheduleError([SqlFacet(MaxSize = 400)] SqlString text)
        {
            if (text.IsNull)
                return new SqlString("Tier schedule is required.");
            return TierSchedule.TryParse(text.Value, out _, out string error) ? SqlString.Null : new SqlString(error);
        }
    }
}
