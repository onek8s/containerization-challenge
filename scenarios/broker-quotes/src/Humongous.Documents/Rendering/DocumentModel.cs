using Humongous.Quoting.Contracts.Messages;
using Humongous.Quoting.Data.Entities;

namespace Humongous.Documents.Rendering;

/// <summary>Everything a document template needs, loaded by the consumer.</summary>
public sealed record DocumentModel(
    DocumentType Type,
    Agency Agency,
    Quote? Quote,
    Policy? Policy,
    PolicyTransaction? Transaction,
    DateTime IssuedUtc);
