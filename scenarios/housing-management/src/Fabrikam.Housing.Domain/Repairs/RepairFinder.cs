using System.Collections.Generic;
using System.Linq;

namespace Fabrikam.Housing.Repairs
{
    /// <summary>
    /// The question tree tenants walk through in the portal to describe a repair. Each answer maps to a
    /// Schedule of Rates code, which sets the trade and default priority.
    /// </summary>
    public static class RepairFinder
    {
        public class Option
        {
            public string Key { get; set; }
            public string Area { get; set; }
            public string Text { get; set; }
            public string SorCode { get; set; }
            public bool AskForPhoto { get; set; }
            public string SafetyAdvice { get; set; }
        }

        public static readonly IReadOnlyList<string> Areas = new[] { "Kitchen", "Bathroom", "Heating and hot water", "Electrics", "Doors and windows", "Outside", "Damp and mould" };

        public static readonly IReadOnlyList<Option> Options = new List<Option>
        {
            new Option { Key = "kit-tap", Area = "Kitchen", Text = "Tap is dripping or loose", SorCode = "PL0101" },
            new Option { Key = "kit-sink", Area = "Kitchen", Text = "Sink is blocked", SorCode = "DR0201" },
            new Option { Key = "kit-unit", Area = "Kitchen", Text = "Cupboard door or drawer is broken", SorCode = "CA0301", AskForPhoto = true },
            new Option { Key = "kit-leak", Area = "Kitchen", Text = "Water is leaking and cannot be stopped", SorCode = "PL0901", SafetyAdvice = "Turn off the water at the stop tap, usually under the kitchen sink." },
            new Option { Key = "bath-toilet", Area = "Bathroom", Text = "Toilet will not flush", SorCode = "PL0210" },
            new Option { Key = "bath-blocked", Area = "Bathroom", Text = "Toilet is blocked and it is the only toilet", SorCode = "DR0101", SafetyAdvice = "Do not keep flushing." },
            new Option { Key = "bath-seal", Area = "Bathroom", Text = "Bath or shower seal is mouldy or leaking", SorCode = "PL0305", AskForPhoto = true },
            new Option { Key = "bath-shower", Area = "Bathroom", Text = "Electric shower is not working", SorCode = "EL0410" },
            new Option { Key = "heat-none", Area = "Heating and hot water", Text = "No heating and no hot water", SorCode = "HT0101", SafetyAdvice = "If you smell gas, leave the property and call the National Gas Emergency number 0800 111 999." },
            new Option { Key = "heat-radiator", Area = "Heating and hot water", Text = "One radiator is cold", SorCode = "HT0204" },
            new Option { Key = "heat-pressure", Area = "Heating and hot water", Text = "Boiler is losing pressure", SorCode = "HT0110" },
            new Option { Key = "el-power", Area = "Electrics", Text = "No power to the whole property", SorCode = "EL0101", SafetyAdvice = "Check whether your neighbours have power. If they do not, call your electricity network operator on 105." },
            new Option { Key = "el-socket", Area = "Electrics", Text = "Socket or switch is damaged", SorCode = "EL0205", AskForPhoto = true },
            new Option { Key = "el-light", Area = "Electrics", Text = "Light fitting is not working", SorCode = "EL0301" },
            new Option { Key = "el-smoke", Area = "Electrics", Text = "Smoke alarm is beeping or not working", SorCode = "EL0601" },
            new Option { Key = "dw-lock", Area = "Doors and windows", Text = "Front door will not lock", SorCode = "CA0101", SafetyAdvice = "If the property cannot be secured, tell us now: this is an emergency." },
            new Option { Key = "dw-glass", Area = "Doors and windows", Text = "Window glass is cracked or broken", SorCode = "GL0101", AskForPhoto = true },
            new Option { Key = "dw-handle", Area = "Doors and windows", Text = "Window handle or hinge is broken", SorCode = "GL0201" },
            new Option { Key = "out-gutter", Area = "Outside", Text = "Gutter is leaking or blocked", SorCode = "RF0301", AskForPhoto = true },
            new Option { Key = "out-roof", Area = "Outside", Text = "Roof tiles are missing or roof is leaking", SorCode = "RF0101", AskForPhoto = true },
            new Option { Key = "out-fence", Area = "Outside", Text = "Fence panel is broken", SorCode = "CA0601", AskForPhoto = true },
            new Option { Key = "out-drain", Area = "Outside", Text = "Outside drain is blocked or smells", SorCode = "DR0301" },
            new Option { Key = "damp-mould", Area = "Damp and mould", Text = "Black mould on walls or ceilings", SorCode = "DM0101", AskForPhoto = true },
            new Option { Key = "damp-patch", Area = "Damp and mould", Text = "Damp patch that is getting bigger", SorCode = "DM0201", AskForPhoto = true }
        };

        public static Option Find(string key) => Options.FirstOrDefault(o => o.Key == key);

        public static IEnumerable<Option> ForArea(string area) => Options.Where(o => o.Area == area);
    }
}
