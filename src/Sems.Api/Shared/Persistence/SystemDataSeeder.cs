using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sems.Api.Modules.Iam.Application;

namespace Sems.Api.Shared.Persistence;

public class SystemDataSeeder
{
    private readonly AuthenticationService _auth;
    private readonly SemsDbContext _context;

    public SystemDataSeeder(AuthenticationService auth, SemsDbContext context)
    {
        _auth = auth;
        _context = context;
    }

    public async Task SeedAsync()
    {
        try
        {
            // Seed base user using Application service so it automatically handles hashing and table schema!
            await _auth.RegisterAsync("admin@energix.com", "12345678", "ADMIN");
            
            // Fetch the user to seed their data
            var user = await _context.Database.ExecuteSqlRawAsync("SELECT 1"); // just to test it works
            // Actual data injection happens later
        }
        catch 
        {
            // Usually fails if user already exists, which is fine
        }
    }

    public async Task SeedForUserAsync(string email)
    {
        var userIdStr = await _context.Database.SqlQueryRaw<string>(
            "SELECT \"UserId\"::text FROM iam_users WHERE \"EmailAddress\" = {0} LIMIT 1", email)
            .FirstOrDefaultAsync();

        if (userIdStr == null) return;
        var userId = Guid.Parse(userIdStr);

        var siteId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        
        await _context.Database.ExecuteSqlRawAsync(@"
            INSERT INTO org_sites (id, organization_id, name, address, created_at, updated_at) 
            VALUES ('{0}', '{0}', 'Casa Principal', 'Calle Falsa 123', NOW(), NOW()) ON CONFLICT DO NOTHING;
            
            INSERT INTO org_zones (id, site_id, name, created_at, updated_at) 
            VALUES ('{1}', '{0}', 'Interiores', NOW(), NOW()) ON CONFLICT DO NOTHING;
        ", siteId, zoneId);

        var rng = new Random();
        int deviceCount = rng.Next(5, 12); // Between 5 and 11

        string[] names = { "Televisor Smart", "Refrigeradora", "Aire Acondicionado", "Luces Sala", "Microondas", "Lavadora", "Computadora", "Secadora", "Consola de Juegos", "Termostato", "Luces Cocina", "Calentador de Agua", "Sistema de Audio" };
        string[] types = { "Electronics", "Appliance", "HVAC", "Lighting", "Appliance", "Appliance", "Electronics", "Appliance", "Electronics", "HVAC", "Lighting", "Appliance", "Electronics" };

        for (int i = 0; i < deviceCount; i++)
        {
            var devId = Guid.NewGuid();
            int idx = rng.Next(names.Length);
            
            string name = names[idx];
            string type = types[idx];
            string status = rng.NextDouble() > 0.8 ? "Maintenance" : "Active";
            bool isOnline = rng.NextDouble() > 0.2;
            
            await _context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO dm_devices (id, name, model, ip_address, mac_address, type, status, is_online, user_id, site_id, connection_protocol, registered_at, updated_at, external_device_code)
                VALUES ('{0}', '{1}', 'Modelo X', '192.168.1.{2}', '00:11:22:33:44:{2:D2}', '{3}', '{4}', {5}, '{6}', '{7}', 'WiFi', NOW(), NOW(), 'DEV-{8}') ON CONFLICT DO NOTHING;
            ", devId, name, i + 100, type, status, isOnline, userId, siteId, i);
        }
    }
}
