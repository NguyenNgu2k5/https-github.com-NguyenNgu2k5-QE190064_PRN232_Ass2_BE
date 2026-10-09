using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTrack.Service.Dtos;
using TaskTrack.Service.Interfaces;

namespace TaskTrack.API.Controllers;

[ApiController, Route("api/departments")]
public class DepartmentsController(IDepartmentService service) : ApiControllerBase
{
    [HttpGet] public Task<ActionResult<IReadOnlyList<DepartmentResponse>>> GetAll(CancellationToken ct) => Run(() => service.GetAllAsync(ct));
    [HttpGet("search")] public Task<ActionResult<IReadOnlyList<DepartmentResponse>>> Search([FromQuery] string name = "", CancellationToken ct = default) => Run(() => service.SearchAsync(name, ct));
    [HttpGet("{id:int}")] public Task<ActionResult<DepartmentResponse>> GetById(int id, CancellationToken ct) => Run(() => service.GetByIdAsync(id, ct));
    [Authorize, HttpPost] public Task<ActionResult<DepartmentResponse>> Create(DepartmentRequest request, CancellationToken ct) => Run(() => service.CreateAsync(request, ct));
    [Authorize, HttpPut("{id:int}")] public Task<ActionResult<DepartmentResponse>> Update(int id, DepartmentRequest request, CancellationToken ct) => Run(() => service.UpdateAsync(id, request, ct));
    [Authorize, HttpDelete("{id:int}")] public Task<IActionResult> Delete(int id, CancellationToken ct) => Run(() => service.DeleteAsync(id, ct));
}
