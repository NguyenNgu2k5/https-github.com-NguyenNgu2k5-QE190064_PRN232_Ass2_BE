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

    public async Task<IReadOnlyList<AccountResponse>> GetAllAsync(CancellationToken ct) => (await repo.GetAllAsync(ct)).Select(Map).ToList();
    public async Task<AccountResponse> GetAsync(int id, CancellationToken ct) => Map(await repo.FindAsync(id, ct) ?? throw new KeyNotFoundException("Account not found."));

    public async Task<AccountResponse> UpdateAsync(int id, AccountUpdateRequest request, CancellationToken ct)
    {
        var account = await repo.FindAsync(id, ct) ?? throw new KeyNotFoundException("Account not found.");
        if (request.FullName is null && request.Role is null) throw new InvalidOperationException("Provide a full name or role.");
        if (request.FullName is not null) account.FullName = Name(request.FullName);
        if (request.Role is not null) account.Role = request.Role.Value;
        await repo.SaveAsync(ct);
        return Map(account);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var account = await repo.FindAsync(id, ct) ?? throw new KeyNotFoundException("Account not found.");
        if (await repo.HasTasksAsync(id, ct)) throw new AccountConflictException("This account created tasks and cannot be deleted, including tasks that were soft-deleted.");
        repo.Remove(account);
        await repo.SaveAsync(ct);
    }

    private static string Name(string value) => value.Trim().Length > 0 ? value.Trim() : throw new InvalidOperationException("Full name is required.");
    private static AccountResponse Map(SystemAccount account) => new(account.AccountId, account.FullName, account.Email, account.Role, account.CreatedDate);
}

public class AccountConflictException(string message) : InvalidOperationException(message);
