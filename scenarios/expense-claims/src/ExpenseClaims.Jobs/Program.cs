using ExpenseClaims.Data;
using ExpenseClaims.Data.Setup;
using ExpenseClaims.Jobs;
using System.Net;
using ExpenseClaims.Core.Accounting;
using ExpenseClaims.Jobs.Bank;
using ExpenseClaims.Jobs.Cards;
using ExpenseClaims.Jobs.Erp;
using ExpenseClaims.Jobs.Hr;
using ExpenseClaims.Jobs.Jobs;
using ExpenseClaims.Jobs.Mail;
using ExpenseClaims.Jobs.Status;
using Microsoft.Extensions.Options;
using Quartz;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, logger) => logger
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddExpenseClaimsDatabase(builder.Configuration);
    builder.Services.Configure<BankOptions>(builder.Configuration.GetSection(BankOptions.SectionName));
    builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
    builder.Services.Configure<HrImportOptions>(builder.Configuration.GetSection(HrImportOptions.SectionName));
    builder.Services.Configure<CardImportOptions>(builder.Configuration.GetSection(CardImportOptions.SectionName));
    builder.Services.Configure<ErpOptions>(builder.Configuration.GetSection(ErpOptions.SectionName));
    builder.Services.Configure<AccountingOptions>(builder.Configuration.GetSection(AccountingOptions.SectionName));
    builder.Services.AddSingleton(sp => new JournalBuilder(sp.GetRequiredService<IOptions<AccountingOptions>>().Value));
    builder.Services.AddScoped<HrEmployeeImporter>();
    builder.Services.AddScoped<CardTransactionImporter>();
    builder.Services.AddScoped<PaymentRunService>();
    builder.Services.AddSingleton<IMailSender, MailSender>();

    builder.Services.AddHttpClient(ExchangeRateJob.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(60))
        .ConfigurePrimaryHttpMessageHandler(() => ProxySettings.CreateHandler(builder.Configuration));

    builder.Services.AddHttpClient<BankGatewayClient>((sp, c) =>
        {
            var bank = sp.GetRequiredService<IOptions<BankOptions>>().Value;
            c.BaseAddress = new Uri(bank.GatewayUrl);
            c.Timeout = TimeSpan.FromMinutes(2);
        })
        .ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var bank = sp.GetRequiredService<IOptions<BankOptions>>().Value;
            var handler = ProxySettings.CreateHandler(builder.Configuration);
            if (bank.UsesGateway)
                handler.ClientCertificates.Add(BankGatewayClient.LoadClientCertificate(bank.ClientCertificateThumbprint));
            return handler;
        });

    builder.Services.AddHttpClient<NavJournalClient>((sp, c) =>
        {
            var erp = sp.GetRequiredService<IOptions<ErpOptions>>().Value;
            c.BaseAddress = new Uri(erp.ODataUrl);
            c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        })
        .ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var erp = sp.GetRequiredService<IOptions<ErpOptions>>().Value;
            // NAV service tier uses Windows credential type (NTLM/Kerberos).
            ICredentials credentials = string.IsNullOrEmpty(erp.UserName)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(erp.UserName, erp.Password, erp.Domain);
            return new HttpClientHandler { Credentials = credentials, PreAuthenticate = true, UseProxy = false };
        });

    var quartz = builder.Configuration.GetSection("Quartz");
    builder.Services.AddQuartz(q =>
    {
        q.SchedulerId = "AUTO";
        q.SchedulerName = quartz["SchedulerName"] ?? "ExpenseClaims";
        q.UseMicrosoftDependencyInjectionJobFactory();
        q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 4);

        if (string.Equals(quartz["JobStore"], "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            q.UsePersistentStore(store =>
            {
                store.UseProperties = true;
                store.UseSqlServer(sql =>
                {
                    sql.ConnectionString = builder.Configuration.GetConnectionString("Quartz")
                        ?? builder.Configuration.GetConnectionString("ExpenseClaims")!;
                    sql.TablePrefix = "quartz.QRTZ_";
                });
                store.UseJsonSerializer();
                store.UseClustering(c => c.CheckinInterval = TimeSpan.FromSeconds(20));
            });
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(quartz["TimeZone"] ?? "FLE Standard Time");
        void Schedule<TJob>(string name, string defaultCron) where TJob : IJob
        {
            var cron = builder.Configuration[$"Jobs:{name}:Cron"] ?? defaultCron;
            if (string.Equals(cron, "off", StringComparison.OrdinalIgnoreCase))
                return;
            var key = new JobKey(name);
            q.AddJob<TJob>(key, j => j.StoreDurably().WithDescription(name));
            q.AddTrigger(t => t
                .ForJob(key)
                .WithIdentity(name + "-trigger")
                .WithCronSchedule(cron, c => c.InTimeZone(timeZone).WithMisfireHandlingInstructionFireAndProceed()));
        }

        Schedule<ExchangeRateJob>("ExchangeRates", "0 30 17 ? * MON-FRI");
        Schedule<HrEmployeeImportJob>("HrEmployeeImport", "0 15 5 * * ?");
        Schedule<ApproverReminderJob>("ApproverReminders", "0 0 8 ? * MON-FRI");
        Schedule<PaymentRunJob>("PaymentRun", "0 0 13 ? * MON-FRI");
        Schedule<PaymentStatusJob>("PaymentStatus", "0 0/15 7-20 ? * MON-FRI");
        Schedule<CardTransactionImportJob>("CardTransactionImport", "0 0 6 * * ?");
        Schedule<AccountingExportJob>("AccountingExport", "0 30 18 ? * MON-FRI");
    });
    builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);
    builder.Services.AddHealthChecks().AddDbContextCheck<ExpenseClaimsDbContext>("database");

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.MapJobsStatus();
    app.MapHealthChecks("/health");

    if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
        await app.Services.MigrateExpenseClaimsDatabaseAsync();

    await app.RunAsync();
}
catch (Exception ex) when (ex.GetType().Name != "StopTheHostException")
{
    Log.Fatal(ex, "Expense Claims jobs host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
