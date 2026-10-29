using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.ServiceModel.Web;
using System.Web;
using System.Web.Caching;
using Southridge.Clinic.Contracts;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Infrastructure;
using Southridge.Clinic.Scheduling;
using SchedulingEngine = Southridge.Clinic.Scheduling.SchedulingService;

namespace Southridge.Clinic.Services
{
    /// <summary>JSON API for the waiting room displays and the phone app pilot (WebHttpBinding).</summary>
    [ServiceBehavior(Namespace = Namespaces.Api, InstanceContextMode = InstanceContextMode.PerCall)]
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    public class ClinicApi : IClinicApi
    {
        public List<SiteInfo> Sites()
        {
            RequireKey(null);
            return ClinicLookups.Sites().Select(s => SchedulingService.ToInfo(s)).ToList();
        }

        public WaitingBoard Board(string siteCode)
        {
            RequireKey(null);
            Site site = ClinicLookups.SiteByCode(siteCode);
            if (site == null)
            {
                throw new WebFaultException<string>("Unknown site " + siteCode, HttpStatusCode.NotFound);
            }

            // The displays poll every 30 seconds; one query per site per 30 seconds is enough.
            string cacheKey = "board:" + site.Code;
            var board = HttpRuntime.Cache[cacheKey] as WaitingBoard;
            if (board == null)
            {
                DateTime now = DateTime.Now;
                List<DayAppointment> day = new AppointmentRepository().ListByDay(site.SiteId, now.Date, null, false);
                board = new WaitingBoard
                {
                    Site = site.Name,
                    Updated = now.ToString("s", CultureInfo.InvariantCulture),
                    Providers = day.GroupBy(a => a.ProviderName).Select(g => new BoardProvider
                    {
                        Name = g.Key,
                        Waiting = g.Count(a => a.Status == (byte)AppointmentStatus.CheckedIn),
                        RunningLateMinutes = g.Where(a => a.Status == (byte)AppointmentStatus.CheckedIn && a.StartsAt < now)
                            .Select(a => (int)(now - a.StartsAt).TotalMinutes).DefaultIfEmpty(0).Max()
                    }).OrderBy(p => p.Name).ToList()
                };
                HttpRuntime.Cache.Insert(cacheKey, board, null, DateTime.UtcNow.AddSeconds(30), Cache.NoSlidingExpiration);
            }

            return board;
        }

        public List<SlotInfo> Slots(string appointmentTypeId, string from, string days, string siteId)
        {
            RequireKey(null);
            int typeId, dayCount, site;
            DateTime start;
            if (!int.TryParse(appointmentTypeId, out typeId))
            {
                throw new WebFaultException<string>("type is required", HttpStatusCode.BadRequest);
            }

            if (!DateTime.TryParseExact(from ?? string.Empty, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start))
            {
                start = DateTime.Today;
            }

            return new AvailabilityService().FindSlots(new SlotQuery
            {
                AppointmentTypeId = typeId,
                FromDate = start,
                Days = int.TryParse(days, out dayCount) ? Math.Min(Math.Max(dayCount, 1), 14) : 7,
                SiteId = int.TryParse(siteId, out site) ? site : (int?)null,
                OnlineOnly = true,
                MaxResults = 100
            }, DateTime.Now).Select(s => SchedulingService.ToInfo(s)).ToList();
        }

        public AppointmentInfo Appointment(string confirmationCode)
        {
            RequireKey(null);
            PatientAppointment a = ClinicLookups.AppointmentByConfirmation(confirmationCode);
            if (a == null)
            {
                throw new WebFaultException<string>("Not found", HttpStatusCode.NotFound);
            }

            return SchedulingService.ToInfo(a);
        }

        public CheckInResult CheckIn(string confirmationCode, CheckInRequest request)
        {
            ApiClientInfo client = RequireKey("Kiosk");
            PatientAppointment a = ClinicLookups.AppointmentByConfirmation(confirmationCode);
            DateTime dob;
            if (a == null || request == null
                || !DateTime.TryParseExact(request.DateOfBirth ?? string.Empty, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dob))
            {
                return new CheckInResult { SeeFrontDesk = true, Message = "Please see the front desk." };
            }

            CheckInMatch match = new AppointmentRepository().FindForCheckIn(a.SiteId, request.LastName, dob, confirmationCode, DateTime.Now)
                .FirstOrDefault(m => m.AppointmentId == a.AppointmentId);
            if (match == null || (client.SiteId.HasValue && client.SiteId.Value != a.SiteId) || !AppointmentRules.IsKioskCheckInWindow(a.StartsAt, DateTime.Now))
            {
                return new CheckInResult { SeeFrontDesk = true, Message = "Please see the front desk." };
            }

            try
            {
                new SchedulingEngine().CheckIn(a.AppointmentId, null, "api:" + client.Name);
            }
            catch (ClinicRuleException ex)
            {
                return new CheckInResult { SeeFrontDesk = true, Message = ex.Message };
            }

            return new CheckInResult { CheckedIn = true, Copay = match.Copay, Message = "You are checked in. Please have a seat." };
        }

        public HealthStatus Health()
        {
            string database;
            try
            {
                using (var connection = new SqlConnection(ClinicSettings.ConnectionString("Clinic")))
                using (var command = new SqlCommand("SELECT 1", connection))
                {
                    connection.Open();
                    command.ExecuteScalar();
                    database = "ok";
                }
            }
            catch (SqlException ex)
            {
                database = "error: " + ex.Number;
            }

            return new HealthStatus
            {
                Status = database == "ok" ? "ok" : "degraded",
                Database = database,
                Version = typeof(ClinicApi).Assembly.GetName().Version.ToString(),
                Time = DateTime.Now.ToString("s", CultureInfo.InvariantCulture)
            };
        }

        private static ApiClientInfo RequireKey(string clientType)
        {
            WebOperationContext context = WebOperationContext.Current;
            string key = context == null ? null : context.IncomingRequest.Headers["X-Api-Key"];
            if (string.IsNullOrEmpty(key))
            {
                throw new WebFaultException<string>("X-Api-Key header is required", HttpStatusCode.Unauthorized);
            }

            string hash = ApiKeys.Hash(key);
            string cacheKey = "apikey:" + (clientType ?? "*") + ":" + hash;
            var client = HttpRuntime.Cache[cacheKey] as ApiClientInfo;
            if (client == null)
            {
                client = new SystemRepository().ValidateApiKey(hash, clientType);
                if (client == null)
                {
                    throw new WebFaultException<string>("API key rejected", HttpStatusCode.Forbidden);
                }

                HttpRuntime.Cache.Insert(cacheKey, client, null, DateTime.UtcNow.AddMinutes(5), Cache.NoSlidingExpiration);
            }

            return client;
        }
    }
}
