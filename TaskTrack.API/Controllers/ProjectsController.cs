using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTrack.Service.Dtos;
using TaskTrack.Service.Interfaces;

namespace TaskTrack.API.Controllers;

[ApiController, Route("api/projects")]
public class ProjectsController(IProjectService service) : ApiControllerBase
{
    [HttpGet] public Task<ActionResult<IReadOnlyList<ProjectResponse>>> GetAll(CancellationToken ct) => Run(() => service.GetAllAsync(ct));
    [HttpGet("search")] public Task<ActionResult<IReadOnlyList<ProjectResponse>>> Search([FromQuery] string? name, [FromQuery] short? status, [FromQuery] int? departmentId, CancellationToken ct) => Run(() => service.SearchAsync(name, status, departmentId, ct));
    [HttpGet("department/{departmentId:int}")] public Task<ActionResult<IReadOnlyList<ProjectResponse>>> ByDepartment(int departmentId, CancellationToken ct) => Run(() => service.GetByDepartmentAsync(departmentId, ct));
    [HttpGet("{id:int}")] public Task<ActionResult<ProjectResponse>> GetById(int id, CancellationToken ct) => Run(() => service.GetByIdAsync(id, ct));
    [Authorize, HttpPost] public Task<ActionResult<ProjectResponse>> Create(ProjectRequest request, CancellationToken ct) => Run(() => service.CreateAsync(request, ct));
    [Authorize, HttpPut("{id:int}")] public Task<ActionResult<ProjectResponse>> Update(int id, ProjectRequest request, CancellationToken ct) => Run(() => service.UpdateAsync(id, request, ct));
    [Authorize, HttpDelete("{id:int}")] public Task<IActionResult> Delete(int id, CancellationToken ct) => Run(() => service.DeleteAsync(id, ct));
}
