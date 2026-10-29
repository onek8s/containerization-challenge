using System.Collections.Generic;

namespace StaffOnboarding.Workflows.Services
{
    public interface INotifier
    {
        /// <summary>
        /// Sends mail built from a template. Recipients are account names or "group:&lt;name&gt;".
        /// </summary>
        void Send(string template, IEnumerable<string> recipients, IDictionary<string, string> values);

        /// <summary>Writes the welcome pack for a joiner to the onboarding share and returns its folder.</summary>
        string WriteWelcomePack(string requestNumber, IDictionary<string, string> values);
    }
}
