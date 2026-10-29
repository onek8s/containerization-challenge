using System;
using System.ServiceModel;
using Southridge.Clinic.Contracts;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.Services
{
    /// <summary>Runs service operations and turns exceptions into typed faults.</summary>
    internal static class ServiceCall
    {
        public static T Run<T>(string operation, Func<T> body)
        {
            try
            {
                return body();
            }
            catch (FaultException)
            {
                throw;
            }
            catch (ClinicRuleException ex)
            {
                throw new FaultException<ClinicFault>(new ClinicFault { Code = FaultCodes.Validation, Message = ex.Message }, ex.Message);
            }
            catch (NotFoundException ex)
            {
                throw new FaultException<ClinicFault>(new ClinicFault { Code = FaultCodes.NotFound, Message = ex.Message }, ex.Message);
            }
            catch (NHibernate.StaleObjectStateException ex)
            {
                throw new FaultException<ClinicFault>(new ClinicFault { Code = FaultCodes.Concurrency, Message = "The record was changed by someone else." }, ex.Message);
            }
            catch (Exception ex)
            {
                Exception replaced = ErrorHandler.Handle(ex, ExceptionPolicies.ServiceBoundary);
                string message = replaced != null ? replaced.Message : operation + " failed.";
                throw new FaultException<ClinicFault>(new ClinicFault { Code = FaultCodes.Internal, Message = message }, message);
            }
        }

        public static void Run(string operation, Action body)
        {
            Run<object>(operation, delegate
            {
                body();
                return null;
            });
        }

        public static string CallerName
        {
            get
            {
                ServiceSecurityContext context = ServiceSecurityContext.Current;
                return context == null || context.IsAnonymous ? "anonymous" : context.PrimaryIdentity.Name;
            }
        }
    }

    [Serializable]
    public class NotFoundException : Exception
    {
        public NotFoundException(string message)
            : base(message)
        {
        }

        protected NotFoundException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }
}
