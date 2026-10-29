using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NHibernate;
using NHibernate.Criterion;
using NHibernate.Linq;
using Southridge.Clinic.Domain;

namespace Southridge.Clinic.Referrals
{
    public class ReferralSearch
    {
        public ReferralStatus? Status { get; set; }
        public string SpecialtyCode { get; set; }
        public string Text { get; set; }
        public bool OpenOnly { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
    }

    public class ReferralRepository
    {
        private readonly ISession _session;

        public ReferralRepository(ISession session)
        {
            _session = session;
        }

        public Referral Get(int id)
        {
            return _session.Get<Referral>(id);
        }

        public Referral GetByNumber(string referralNumber)
        {
            return _session.QueryOver<Referral>().Where(r => r.ReferralNumber == referralNumber).SingleOrDefault();
        }

        public Referral GetByWorkflow(Guid instanceId)
        {
            return _session.Query<Referral>().SingleOrDefault(r => r.WorkflowInstanceId == instanceId);
        }

        public ReferralPatient GetPatient(int patientId)
        {
            return _session.Get<ReferralPatient>(patientId);
        }

        /// <summary>Work queue for the referral coordinators: open items, most urgent and oldest first.</summary>
        public IList<Referral> Queue(string specialtyCode)
        {
            ICriteria criteria = _session.CreateCriteria<Referral>("r")
                .Add(Restrictions.In("Status", new object[] { ReferralStatus.Received, ReferralStatus.InReview, ReferralStatus.Approved }))
                .AddOrder(Order.Desc("Priority"))
                .AddOrder(Order.Asc("DueBy"));
            if (!string.IsNullOrEmpty(specialtyCode))
            {
                criteria.Add(Restrictions.Eq("SpecialtyCode", specialtyCode));
            }

            return criteria.List<Referral>();
        }

        public IList<Referral> Search(ReferralSearch search, out int total)
        {
            ICriteria criteria = _session.CreateCriteria<Referral>("r").CreateAlias("r.Patient", "p");
            if (search.Status.HasValue)
            {
                criteria.Add(Restrictions.Eq("r.Status", search.Status.Value));
            }
            else if (search.OpenOnly)
            {
                criteria.Add(Restrictions.In("r.Status", new object[] { ReferralStatus.Received, ReferralStatus.InReview, ReferralStatus.Approved }));
            }

            if (!string.IsNullOrEmpty(search.SpecialtyCode))
            {
                criteria.Add(Restrictions.Eq("r.SpecialtyCode", search.SpecialtyCode));
            }

            if (!string.IsNullOrEmpty(search.Text))
            {
                criteria.Add(Restrictions.Disjunction()
                    .Add(Restrictions.Eq("r.ReferralNumber", search.Text))
                    .Add(Restrictions.Eq("p.Mrn", search.Text))
                    .Add(Restrictions.InsensitiveLike("p.LastName", search.Text, MatchMode.Start))
                    .Add(Restrictions.InsensitiveLike("r.ReferringProviderName", search.Text, MatchMode.Anywhere)));
            }

            ICriteria count = CriteriaTransformer.TransformToRowCount(criteria);
            int pageSize = search.PageSize > 0 ? search.PageSize : 25;
            IList<Referral> rows = criteria
                .AddOrder(Order.Desc("r.ReceivedOn"))
                .SetFirstResult(search.PageIndex * pageSize)
                .SetMaxResults(pageSize)
                .List<Referral>();
            total = count.UniqueResult<int>();
            return rows;
        }

        /// <summary>Referrals that were received while the workflow host was down and have no workflow yet.</summary>
        public IList<Referral> WithoutWorkflow()
        {
            return _session.Query<Referral>()
                .Where(r => r.WorkflowInstanceId == null && (r.Status == ReferralStatus.Received || r.Status == ReferralStatus.InReview))
                .OrderBy(r => r.ReceivedOn)
                .ToList();
        }

        public IDictionary<ReferralStatus, int> CountByStatus()
        {
            IList<object[]> rows = _session.CreateCriteria<Referral>()
                .SetProjection(Projections.ProjectionList()
                    .Add(Projections.GroupProperty("Status"))
                    .Add(Projections.RowCount()))
                .List<object[]>();
            return rows.ToDictionary(r => (ReferralStatus)r[0], r => Convert.ToInt32(r[1], CultureInfo.InvariantCulture));
        }

        public string NextReferralNumber()
        {
            long value = _session.GetNamedQuery("NextReferralNumber").UniqueResult<long>();
            return "R0" + value.ToString(CultureInfo.InvariantCulture);
        }

        public void Save(Referral referral)
        {
            _session.SaveOrUpdate(referral);
        }
    }
}
