using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sems.Api.Modules.Iam.Domain.Model;
using Sems.Api.Modules.Iam.Domain.Repositories;
using Sems.Api.Modules.Iam.Interfaces.Acl;
using Sems.Api.Shared.Persistence;

namespace Sems.Api.Modules.Iam.Infrastructure;

public sealed class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("iam_users");
        b.HasKey(u => u.UserId);
        b.Property(u => u.EmailAddress).HasMaxLength(200).IsRequired();
        b.Property(u => u.PasswordHash).HasMaxLength(200).IsRequired();
        b.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        b.Property(u => u.Status).HasMaxLength(20);
        b.HasIndex(u => u.EmailAddress).IsUnique();
    }
}

public sealed class RefreshTokenConfig : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("iam_refresh_tokens");
        b.HasKey(t => t.Id);
        b.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
        b.HasIndex(t => t.UserId);
        b.HasIndex(t => t.TokenHash).IsUnique();
    }
}

public sealed class UserAuthTokenConfig : IEntityTypeConfiguration<UserAuthToken>
{
    public void Configure(EntityTypeBuilder<UserAuthToken> b)
    {
        b.ToTable("iam_user_auth_tokens");
        b.HasKey(t => t.Id);
        b.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
        b.Property(t => t.Purpose).HasMaxLength(40).IsRequired();
        b.HasIndex(t => t.UserId);
        b.HasIndex(t => t.TokenHash).IsUnique();
    }
}

// ---------------------------------------------------------------- adaptadores

public sealed class UserRepository : IUserRepository
{
    private readonly SemsDbContext _db;
    public UserRepository(SemsDbContext db) => _db = db;

    public async Task<User> SaveAsync(User user, CancellationToken ct = default)
    {
        if (_db.Entry(user).State == EntityState.Detached) _db.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }

    public Task<User?> FindByIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.Set<User>().FirstOrDefaultAsync(u => u.UserId == userId, ct);

    public Task<User?> FindByEmailAsync(string emailAddress, CancellationToken ct = default) =>
        _db.Set<User>().FirstOrDefaultAsync(u => u.EmailAddress == emailAddress, ct);

    public Task<bool> ExistsByEmailAsync(string emailAddress, CancellationToken ct = default) =>
        _db.Set<User>().AnyAsync(u => u.EmailAddress == emailAddress, ct);
}

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly SemsDbContext _db;
    public RefreshTokenRepository(SemsDbContext db) => _db = db;

    public async Task<RefreshToken> SaveAsync(RefreshToken token, CancellationToken ct = default)
    {
        if (_db.Entry(token).State == EntityState.Detached) _db.Add(token);
        await _db.SaveChangesAsync(ct);
        return token;
    }

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default) =>
        _db.Set<RefreshToken>().FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await _db.Set<RefreshToken>()
            .Where(t => t.UserId == userId && !t.Revoked).ToListAsync(ct);

        foreach (var token in tokens)
        {
            token.Revoke();
        }
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class UserAuthTokenRepository : IUserAuthTokenRepository
{
    private readonly SemsDbContext _db;
    public UserAuthTokenRepository(SemsDbContext db) => _db = db;

    public async Task<UserAuthToken> SaveAsync(UserAuthToken token, CancellationToken ct = default)
    {
        if (_db.Entry(token).State == EntityState.Detached) _db.Add(token);
        await _db.SaveChangesAsync(ct);
        return token;
    }

    public Task<UserAuthToken?> FindByHashAndPurposeAsync(string tokenHash, string purpose,
        CancellationToken ct = default) =>
        _db.Set<UserAuthToken>()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.Purpose == purpose, ct);
}

/// <summary>Implementacion de la capa anticorrupcion de IAM.</summary>
public sealed class IamAcl : IIamAcl
{
    private readonly IUserRepository _users;

    public IamAcl(IUserRepository users) => _users = users;

    public async Task<string?> EmailOfAsync(Guid userId, CancellationToken ct = default) =>
        (await _users.FindByIdAsync(userId, ct))?.EmailAddress;
}
