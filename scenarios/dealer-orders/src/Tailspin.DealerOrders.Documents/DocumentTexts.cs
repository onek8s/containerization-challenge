using System;
using System.Collections.Generic;
using System.Globalization;

namespace Tailspin.DealerOrders.Documents
{
    /// <summary>Fixed texts and number/date formats per document language.</summary>
    public static class DocumentTexts
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Texts = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new Dictionary<string, string>
            {
                ["quote.title"] = "Quotation",
                ["confirmation.title"] = "Order confirmation",
                ["contract.title"] = "Dealer agreement",
                ["validity"] = "This quotation is valid until {0}.",
                ["delivery.exw"] = "Ex works Kontiolahti, Finland (Incoterms 2010 EXW)",
                ["delivery.fca"] = "Free carrier, loaded at Kontiolahti factory (Incoterms 2010 FCA)",
                ["delivery.cpt"] = "Carriage paid to dealer yard (Incoterms 2010 CPT)",
                ["delivery.dap"] = "Delivered at place (Incoterms 2010 DAP)",
                ["leadtime"] = "Estimated delivery week {0}/{1}, subject to confirmation.",
                ["warranty"] = "Warranty 12 months or 2,000 operating hours, whichever comes first, according to Tailspin Machinery warranty terms TSM-W-2016.",
                ["confirmed"] = "We confirm your order {0} as follows."
            },
            ["fi"] = new Dictionary<string, string>
            {
                ["quote.title"] = "Tarjous",
                ["confirmation.title"] = "Tilausvahvistus",
                ["contract.title"] = "Jälleenmyyjäsopimus",
                ["validity"] = "Tarjous on voimassa {0} asti.",
                ["delivery.exw"] = "Noudettuna tehtaalta Kontiolahti (Incoterms 2010 EXW)",
                ["delivery.fca"] = "Vapaasti rahdinkuljettajalla, lastattuna Kontiolahden tehtaalla (Incoterms 2010 FCA)",
                ["delivery.cpt"] = "Rahti maksettuna jälleenmyyjän pihaan (Incoterms 2010 CPT)",
                ["delivery.dap"] = "Toimitettuna määräpaikkaan (Incoterms 2010 DAP)",
                ["leadtime"] = "Arvioitu toimitusviikko {0}/{1}, vahvistetaan erikseen.",
                ["warranty"] = "Takuu 12 kuukautta tai 2 000 käyttötuntia sen mukaan, kumpi täyttyy ensin, Tailspin Machineryn takuuehtojen TSM-W-2016 mukaisesti.",
                ["confirmed"] = "Vahvistamme tilauksenne {0} seuraavasti."
            },
            ["sv"] = new Dictionary<string, string>
            {
                ["quote.title"] = "Offert",
                ["confirmation.title"] = "Orderbekräftelse",
                ["contract.title"] = "Återförsäljaravtal",
                ["validity"] = "Offerten gäller till och med {0}.",
                ["delivery.exw"] = "Fritt fabrik Kontiolahti (Incoterms 2010 EXW)",
                ["delivery.fca"] = "Fritt transportören, lastat vid fabriken i Kontiolahti (Incoterms 2010 FCA)",
                ["delivery.cpt"] = "Fraktfritt till återförsäljarens gård (Incoterms 2010 CPT)",
                ["delivery.dap"] = "Levererat till plats (Incoterms 2010 DAP)",
                ["leadtime"] = "Beräknad leveransvecka {0}/{1}, bekräftas separat.",
                ["warranty"] = "Garanti 12 månader eller 2 000 drifttimmar, det som inträffar först, enligt Tailspin Machinerys garantivillkor TSM-W-2016.",
                ["confirmed"] = "Vi bekräftar er order {0} enligt följande."
            },
            ["de"] = new Dictionary<string, string>
            {
                ["quote.title"] = "Angebot",
                ["confirmation.title"] = "Auftragsbestätigung",
                ["contract.title"] = "Händlervertrag",
                ["validity"] = "Dieses Angebot ist gültig bis {0}.",
                ["delivery.exw"] = "Ab Werk Kontiolahti, Finnland (Incoterms 2010 EXW)",
                ["delivery.fca"] = "Frei Frachtführer, verladen im Werk Kontiolahti (Incoterms 2010 FCA)",
                ["delivery.cpt"] = "Frachtfrei Händlerhof (Incoterms 2010 CPT)",
                ["delivery.dap"] = "Geliefert benannter Ort (Incoterms 2010 DAP)",
                ["leadtime"] = "Voraussichtliche Lieferwoche {0}/{1}, Bestätigung folgt.",
                ["warranty"] = "Gewährleistung 12 Monate oder 2.000 Betriebsstunden, je nachdem was zuerst eintritt, gemäß den Garantiebedingungen TSM-W-2016.",
                ["confirmed"] = "Wir bestätigen Ihren Auftrag {0} wie folgt."
            }
        };

        public static string Get(string language, string key)
        {
            if (!Texts.TryGetValue(language ?? "en", out var texts)) texts = Texts["en"];
            return texts.TryGetValue(key, out var text) ? text : Texts["en"][key];
        }

        public static CultureInfo Culture(string language)
        {
            switch ((language ?? "en").ToLowerInvariant())
            {
                case "fi": return CultureInfo.GetCultureInfo("fi-FI");
                case "sv": return CultureInfo.GetCultureInfo("sv-SE");
                case "de": return CultureInfo.GetCultureInfo("de-DE");
                default: return CultureInfo.GetCultureInfo("en-GB");
            }
        }

        public static string Amount(decimal amount, string currency, string language) =>
            amount.ToString(Domain.Currencies.Decimals(currency) == 0 ? "N0" : "N2", Culture(language)) + " " + currency;

        public static string Date(DateTime date, string language) => date.ToString("d", Culture(language));
    }
}
