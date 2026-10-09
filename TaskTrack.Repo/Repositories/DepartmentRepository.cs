using Microsoft.EntityFrameworkCore;
using TaskTrack.Repo.Models;

namespace TaskTrack.Repo.Repositories;

public class DepartmentRepository(TaskManagementDbContext db)
{
    public Task<List<Department>> GetActiveAsync(CancellationToken ct = default) =>
        db.Departments.AsNoTracking().Include(x => x.Projects.Where(p => p.IsActive)).Where(x => x.IsActive).OrderBy(x => x.DepartmentName).ToListAsync(ct);

    public Task<Department?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.Departments.AsNoTracking().Include(x => x.Projects.Where(p => p.IsActive)).FirstOrDefaultAsync(x => x.DepartmentId == id, ct);

    public Task<List<Department>> SearchAsync(string name, CancellationToken ct = default) =>
        db.Departments.AsNoTracking().Include(x => x.Projects.Where(p => p.IsActive)).Where(x => x.IsActive && EF.Functions.ILike(x.DepartmentName, $"%{name}%")).OrderBy(x => x.DepartmentName).ToListAsync(ct);

    public Task<bool> HasProjectsAsync(int id, CancellationToken ct = default) => db.Projects.AnyAsync(x => x.DepartmentId == id, ct);
    public Task<bool> ExistsAsync(int id, CancellationToken ct = default) => db.Departments.AnyAsync(x => x.DepartmentId == id, ct);
    public Task AddAsync(Department item, CancellationToken ct = default) => db.Departments.AddAsync(item, ct).AsTask();
    public void Update(Department item) => db.Departments.Update(item);
    public void Remove(Department item) => db.Departments.Remove(item);
    public Task SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
    public Task<Department?> FindTrackedAsync(int id, CancellationToken ct = default) => db.Departments.FirstOrDefaultAsync(x => x.DepartmentId == id, ct);
}
