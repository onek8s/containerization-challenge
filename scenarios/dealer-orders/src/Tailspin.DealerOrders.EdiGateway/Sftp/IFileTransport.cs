using System;
using System.Collections.Generic;
using Tailspin.DealerOrders.Domain;

namespace Tailspin.DealerOrders.EdiGateway.Sftp
{
    /// <summary>One connection to a partner's file drop.</summary>
    public interface IFileSession : IDisposable
    {
        IList<string> ListInbound();
        string Read(string name);
        void Archive(string name);
        void Write(string name, string content);
    }

    public interface IFileTransport
    {
        IFileSession Open(EdiPartner partner);
    }
}
