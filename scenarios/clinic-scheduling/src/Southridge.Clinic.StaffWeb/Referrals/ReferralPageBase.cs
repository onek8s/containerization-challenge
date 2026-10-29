using System;
using NHibernate;
using Southridge.Clinic.Infrastructure;
using Southridge.Clinic.Referrals;

namespace Southridge.Clinic.StaffWeb.Referrals
{
    /// <summary>Referral pages use the request's NHibernate session (ReferralSessionModule) and commit explicitly.</summary>
    public abstract class ReferralPageBase : ClinicPage
    {
        protected ISession Db
        {
            get { return ReferralSessions.Current; }
        }

        protected ReferralService Service
        {
            get { return new ReferralService(ReferralSessions.Current, ClinicContainer.Resolve<IClock>()); }
        }

        protected ReferralRepository Repository
        {
            get { return new ReferralRepository(ReferralSessions.Current); }
        }

        /// <summary>Runs the change in a transaction. Returns false (and shows the error) when it fails.</summary>
        protected bool Commit(Action work)
        {
            ITransaction tx = Db.BeginTransaction();
            try
            {
                work();
                tx.Commit();
                return true;
            }
            catch (StaleObjectStateException)
            {
                tx.Rollback();
                ShowError("Someone else changed this referral. Reload the page and try again.");
            }
            catch (Exception ex)
            {
                if (tx.IsActive)
                {
                    tx.Rollback();
                }

                HandleError(ex);
            }

            return false;
        }
    }
}
