using FieldService.Data.Entities;
using FieldService.Data.Services;
using Microsoft.EntityFrameworkCore;

namespace FieldService.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(FieldServiceContext db, bool seedSampleData, CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);

        if (!await db.Users.AnyAsync(ct))
        {
            db.Users.Add(new AppUser
            {
                UserName = "admin",
                DisplayName = "Administrator",
                PasswordHash = PasswordHasher.Hash("ChangeMe!1"),
                Role = Roles.Admin
            });
            await db.SaveChangesAsync(ct);
        }

        if (!seedSampleData || await db.Customers.AnyAsync(ct))
        {
            return;
        }

        db.Customers.AddRange(
            new Customer { AccountCode = "ACME", Name = "Acme Property Management", ContactEmail = "facilities@acme.example", HourlyRate = 85m, SlaHours = 24 },
            new Customer { AccountCode = "RIVERSIDE", Name = "Riverside Medical Centre", ContactEmail = "estates@riverside.example", HourlyRate = 95m, SlaHours = 8 },
            new Customer { AccountCode = "OAKFIELD", Name = "Oakfield Schools Trust", ContactEmail = "site.team@oakfield.example", HourlyRate = 72.5m, SlaHours = 48 },
            new Customer { AccountCode = "HARBOUR", Name = "Harbour Logistics", ContactEmail = "ops@harbour.example", HourlyRate = 80m, SlaHours = 24 });

        var techs = new[]
        {
            new Technician { Name = "Maria Lindqvist", Region = "North", Phone = "+358 40 100 2001" },
            new Technician { Name = "Tom Becker", Region = "North", Phone = "+358 40 100 2002" },
            new Technician { Name = "Aisha Khan", Region = "South", Phone = "+358 40 100 2003" },
            new Technician { Name = "Jari Mäkinen", Region = "South", Phone = "+358 40 100 2004" }
        };
        db.Technicians.AddRange(techs);
        await db.SaveChangesAsync(ct);

        db.Users.AddRange(
            new AppUser { UserName = "dispatch", DisplayName = "Dispatch Desk", PasswordHash = PasswordHasher.Hash("dispatch"), Role = Roles.Dispatcher },
            new AppUser { UserName = "maria", DisplayName = techs[0].Name, PasswordHash = PasswordHasher.Hash("maria"), Role = Roles.Technician, TechnicianId = techs[0].Id },
            new AppUser { UserName = "aisha", DisplayName = techs[2].Name, PasswordHash = PasswordHasher.Hash("aisha"), Role = Roles.Technician, TechnicianId = techs[2].Id });
        await db.SaveChangesAsync(ct);

        var service = new WorkOrderService(db, new NumberSequence(db), TimeProvider.System);
        var samples = new[]
        {
            new NewWorkOrder("ACME", "12 Market Street, Unit 4", "Boiler pressure dropping", "Tenant reports pressure below 1 bar every morning.", WorkOrderPriority.Normal),
            new NewWorkOrder("RIVERSIDE", "Riverside Medical Centre, Block B", "Cold room alarm", "Vaccine fridge alarm sounding intermittently.", WorkOrderPriority.Emergency),
            new NewWorkOrder("OAKFIELD", "Oakfield Primary, Sports Hall", "Replace failed floodlights", null, WorkOrderPriority.Low),
            new NewWorkOrder("HARBOUR", "Dock 3 Warehouse", "Roller door stuck half open", "Door motor hums but does not move.", WorkOrderPriority.High)
        };
        foreach (var s in samples)
        {
            await service.CreateAsync(s, ct);
        }

        var orders = await db.WorkOrders.OrderBy(w => w.Id).ToListAsync(ct);
        await service.AssignAsync(orders[1].Number, techs[2].Id, ct);
        await service.AssignAsync(orders[3].Number, techs[0].Id, ct);
        await service.StartAsync(orders[3].Number, ct);
    }
}
