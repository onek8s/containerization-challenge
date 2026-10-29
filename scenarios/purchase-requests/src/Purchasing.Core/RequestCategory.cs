namespace Contoso.Purchasing.Core
{
    public static class RequestCategory
    {
        public const string Consumables = "CONSUMABLES";
        public const string Services = "SERVICES";
        public const string ItEquipment = "IT";
        public const string Capital = "CAPEX";

        public static readonly string[] All = { Consumables, Services, ItEquipment, Capital };

        public static string DisplayName(string category)
        {
            switch (category)
            {
                case Consumables: return "Consumables and supplies";
                case Services: return "Services and contractors";
                case ItEquipment: return "IT equipment and software";
                case Capital: return "Capital investment";
                default: return category;
            }
        }
    }
}
