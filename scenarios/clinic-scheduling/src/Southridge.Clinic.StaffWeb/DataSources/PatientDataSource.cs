using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Web;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;

namespace Southridge.Clinic.StaffWeb.DataSources
{
    /// <summary>
    /// ObjectDataSource for patient search with server side paging and sorting (usp_Patient_Search). The total row
    /// count comes back with the page, so it is kept in HttpContext.Items for SearchCount.
    /// </summary>
    [DataObject]
    public class PatientDataSource
    {
        private const string CountKey = "PatientDataSource.Count";

        [DataObjectMethod(DataObjectMethodType.Select, true)]
        public List<PatientSearchRow> Search(string lastName, string firstName, string dateOfBirth, string mrn, string phone,
                                             string sortExpression, int startRowIndex, int maximumRows)
        {
            var criteria = new PatientSearchCriteria
            {
                LastName = Trim(lastName),
                FirstName = Trim(firstName),
                DateOfBirth = ParseDate(dateOfBirth),
                Mrn = Trim(mrn),
                Phone = Trim(phone)
            };
            if (criteria.IsEmpty)
            {
                HttpContext.Current.Items[CountKey] = 0;
                return new List<PatientSearchRow>();
            }

            int total;
            List<PatientSearchRow> rows = new PatientRepository().Search(criteria, sortExpression, startRowIndex, maximumRows, out total);
            HttpContext.Current.Items[CountKey] = total;
            return rows;
        }

        public int SearchCount(string lastName, string firstName, string dateOfBirth, string mrn, string phone)
        {
            object count = HttpContext.Current.Items[CountKey];
            return count == null ? 0 : (int)count;
        }

        private static string Trim(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value.Trim();
        }

        private static DateTime? ParseDate(string value)
        {
            DateTime date;
            return DateTime.TryParse(value, System.Globalization.CultureInfo.GetCultureInfo("en-US"), System.Globalization.DateTimeStyles.None, out date)
                ? date.Date
                : (DateTime?)null;
        }
    }
}
