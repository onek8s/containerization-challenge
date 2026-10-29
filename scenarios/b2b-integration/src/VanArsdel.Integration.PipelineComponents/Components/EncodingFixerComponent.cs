using System;
using System.Runtime.InteropServices;
using Microsoft.BizTalk.Component.Interop;
using Microsoft.BizTalk.Message.Interop;
using VanArsdel.Integration.Messaging;
using VanArsdel.Integration.PipelineComponents.Common;

namespace VanArsdel.Integration.PipelineComponents.Components
{
    /// <summary>
    /// Decode stage: normalises partner files to UTF-8 before the EDI or XML disassembler sees them (BOMs from
    /// Windows editors, Windows-1252 from older translators, stray control characters, 80 column wrapped EDI).
    /// </summary>
    [ComponentCategory(CategoryTypes.CATID_PipelineComponent)]
    [ComponentCategory(CategoryTypes.CATID_Decoder)]
    [Guid("6B7D1E2A-4C1F-4E57-9B7A-2E0D7C3F5A11")]
    public class EncodingFixerComponent : ComponentBase, IComponent
    {
        public EncodingFixerComponent()
        {
            FallbackCodePage = 1252;
            RemoveControlCharacters = true;
        }

        public override string Name
        {
            get { return "VanArsdel Encoding Fixer"; }
        }

        public override string Description
        {
            get { return "Converts partner files to UTF-8 without BOM and cleans control characters."; }
        }

        public int FallbackCodePage { get; set; }
        public bool RemoveControlCharacters { get; set; }
        public bool UnwrapFixedLines { get; set; }
        public bool TransliterateForEdi { get; set; }

        public IBaseMessage Execute(IPipelineContext pContext, IBaseMessage pInMsg)
        {
            if (pInMsg.BodyPart == null)
            {
                return pInMsg;
            }

            var result = EncodingFixer.Fix(MessageBodies.ReadAll(pInMsg), new EncodingFixerSettings
            {
                FallbackCodePage = FallbackCodePage,
                RemoveControlCharacters = RemoveControlCharacters,
                UnwrapFixedLines = UnwrapFixedLines,
                TransliterateForEdi = TransliterateForEdi
            });

            MessageBodies.Replace(pContext, pInMsg, result.Body);
            pInMsg.BodyPart.Charset = "utf-8";
            pInMsg.Context.Write("EncodingFix", Contracts.ContextProperties.VanArsdelNamespace, EncodingFixer.Describe(result));
            return pInMsg;
        }

        protected override void LoadProperties(IPropertyBag bag)
        {
            FallbackCodePage = ReadInt(bag, "FallbackCodePage", 1252);
            RemoveControlCharacters = ReadBool(bag, "RemoveControlCharacters", true);
            UnwrapFixedLines = ReadBool(bag, "UnwrapFixedLines", false);
            TransliterateForEdi = ReadBool(bag, "TransliterateForEdi", false);
        }

        protected override void SaveProperties(IPropertyBag bag)
        {
            WriteProperty(bag, "FallbackCodePage", FallbackCodePage);
            WriteProperty(bag, "RemoveControlCharacters", RemoveControlCharacters);
            WriteProperty(bag, "UnwrapFixedLines", UnwrapFixedLines);
            WriteProperty(bag, "TransliterateForEdi", TransliterateForEdi);
        }

        protected override System.Collections.Generic.IList<string> ValidateConfiguration()
        {
            try
            {
                System.Text.Encoding.GetEncoding(FallbackCodePage);
                return null;
            }
            catch (ArgumentException)
            {
                return new[] { "Code page " + FallbackCodePage + " is not available." };
            }
        }
    }
}
