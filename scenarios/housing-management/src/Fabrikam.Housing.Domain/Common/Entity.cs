using System;

namespace Fabrikam.Housing.Common
{
    public abstract class Entity
    {
        public int Id { get; set; }
    }

    public abstract class AuditedEntity : Entity
    {
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string ModifiedBy { get; set; }
    }
}
