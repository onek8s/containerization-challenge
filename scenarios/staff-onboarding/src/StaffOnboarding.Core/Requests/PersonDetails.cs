using System.Runtime.Serialization;

namespace StaffOnboarding.Core.Requests
{
    [DataContract(Namespace = OnboardingNamespaces.Data)]
    public class PersonDetails
    {
        [DataMember(Order = 1)] public string FirstName { get; set; }
        [DataMember(Order = 2)] public string LastName { get; set; }
        [DataMember(Order = 3)] public string PreferredName { get; set; }
        [DataMember(Order = 4)] public string EmployeeId { get; set; }
        [DataMember(Order = 5)] public string MobilePhone { get; set; }

        public string DisplayFirstName
        {
            get { return string.IsNullOrWhiteSpace(PreferredName) ? FirstName : PreferredName; }
        }

        public string DisplayName
        {
            get { return (DisplayFirstName + " " + LastName).Trim(); }
        }
    }
}
