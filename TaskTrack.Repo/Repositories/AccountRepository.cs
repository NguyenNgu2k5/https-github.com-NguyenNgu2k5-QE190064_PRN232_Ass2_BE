using Microsoft.EntityFrameworkCore;
using TaskTrack.Repo.Models;

namespace TaskTrack.Repo.Repositories;

public class AccountRepository(TaskManagementDbContext db)
{
    public Task<List<SystemAccount>> GetAllAsync(CancellationToken ct) => db.Accounts.AsNoTracking().OrderBy(x => x.AccountId).ToListAsync(ct);
    public Task<SystemAccount?> FindAsync(int id, CancellationToken ct) => db.Accounts.FirstOrDefaultAsync(x => x.AccountId == id, ct);
    public Task<SystemAccount?> FindByEmailAsync(string email, CancellationToken ct) => db.Accounts.FirstOrDefaultAsync(x => x.Email.ToLower() == email, ct);
    public Task<bool> HasTasksAsync(int id, CancellationToken ct) => db.Tasks.AnyAsync(x => x.CreatedById == id, ct);
    public Task AddAsync(SystemAccount account, CancellationToken ct) => db.Accounts.AddAsync(account, ct).AsTask();
    public void Remove(SystemAccount account) => db.Accounts.Remove(account);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
