using Microsoft.AspNetCore.Identity;
using TaskTrack.Repo.Models;
using TaskTrack.Repo.Repositories;
using TaskTrack.Service.Dtos;

namespace TaskTrack.Service.Services;

public class AccountService(AccountRepository repo, IPasswordHasher<SystemAccount> hasher)
{
    public async Task<AccountResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await repo.FindByEmailAsync(email, ct) is not null) throw new AccountConflictException("Email already exists.");
        var account = new SystemAccount { FullName = Name(request.FullName), Email = email, Role = 0 };
        account.PasswordHash = hasher.HashPassword(account, request.Password);
        await repo.AddAsync(account, ct);
        await repo.SaveAsync(ct);
        return Map(account);
    }

    public async Task<AccountResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var account = await repo.FindByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);
        if (account is null || hasher.VerifyHashedPassword(account, account.PasswordHash, request.Password) == PasswordVerificationResult.Failed) return null;
        return Map(account);
    }

    public async Task<AccountResponse> GetAsync(int id, CancellationToken ct) => Map(await repo.FindAsync(id, ct) ?? throw new KeyNotFoundException("Account not found."));

    private static string Name(string value) => value.Trim().Length > 0 ? value.Trim() : throw new InvalidOperationException("Full name is required.");
    private static AccountResponse Map(SystemAccount account) => new(account.AccountId, account.FullName, account.Email, account.Role, account.CreatedDate);
}

public class AccountConflictException(string message) : InvalidOperationException(message);
