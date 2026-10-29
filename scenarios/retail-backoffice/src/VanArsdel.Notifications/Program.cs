using VanArsdel.BuildingBlocks.Data;
using VanArsdel.BuildingBlocks.Hosting;
using VanArsdel.BuildingBlocks.Http;
using VanArsdel.BuildingBlocks.Messaging;
using VanArsdel.Integration.Messaging;
using VanArsdel.Notifications.Consumers;
using VanArsdel.Notifications.Data;
using VanArsdel.Notifications.Sending;

return VanArsdelHost.Run("notifications", args, CreateHostBuilder, (host, a) => host.PrepareDatabase<NotificationsDbContext>(a));

static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .UseWindowsService(o => o.ServiceName = "VanArsdelNotifications")
        .UseVanArsdelSerilog("notifications")
        .ConfigureServices((context, services) =>
        {
            var configuration = context.Configuration;
            services.AddServiceDatabase<NotificationsDbContext>(configuration, "Notifications");
            services.Configure<NotificationOptions>(configuration.GetSection("Notifications"));
            services.AddMemoryCache();

            var postiq = configuration["Notifications:Postiq:BaseUrl"];
            services.AddRefitClientFor<IEmailProviderApi>(postiq);
            services.AddRefitClientFor<ISmsProviderApi>(postiq);
            services.AddTransient<PostiqEmailSender>();
            services.AddTransient<SmtpEmailSender>();
            services.AddTransient<IEmailSender, FallbackEmailSender>();
            services.AddTransient<ISmsSender, PostiqSmsSender>();
            services.AddSingleton<TemplateRenderer>();
            services.AddScoped<Notifier>();

            services.AddServiceIdentity(configuration);
            services.AddServiceClient<IStoreOpsApi>(configuration, "StoreOps");

            services.AddVanArsdelMessaging(configuration, "notifications", x =>
            {
                x.AddConsumer<CustomerNotificationConsumers>();
                x.AddConsumer<InternalNotificationConsumers>();
            });
        });
