using System;
using System.Collections.Generic;
using System.ComponentModel;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Domain;

namespace Southridge.Clinic.StaffWeb.DataSources
{
    [DataObject]
    public class InterfaceDataSource
    {
        [DataObjectMethod(DataObjectMethodType.Select, true)]
        public List<MessageLogRow> Search(string partner, int? status, DateTime fromDate, DateTime toDate)
        {
            return new InterfaceRepository().Search(string.IsNullOrEmpty(partner) ? null : partner,
                status.HasValue ? (MessageStatus?)status.Value : null, fromDate, toDate.AddDays(1), 1000);
        }
    }
}
