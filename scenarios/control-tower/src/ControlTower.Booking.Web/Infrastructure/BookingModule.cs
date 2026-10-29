using Autofac;
using Proseware.ControlTower.Booking.Models;
using Proseware.ControlTower.Booking.Services;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Pricing;

namespace Proseware.ControlTower.Booking.Infrastructure;

public sealed class BookingModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<SystemClock>().As<IClock>().SingleInstance();
        builder.RegisterType<QuoteCalculator>().AsSelf().SingleInstance();
        builder.RegisterType<LegPlanner>().AsSelf().SingleInstance();
        builder.RegisterType<BookingDraftStore>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<QuoteService>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<BookingService>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<ShipmentCommands>().AsSelf().InstancePerLifetimeScope();
        // The wizard steps validate the same draft with different rule sets.
        builder.RegisterType<BookingPartiesValidator>().AsSelf().SingleInstance();
        builder.RegisterType<BookingPackagesValidator>().AsSelf().SingleInstance();
    }
}
