using DentaCore.Application.Common;
using DentaCore.Application.DTOs;
using DentaCore.Application.Interfaces;
using DentaCore.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentaCore.API.Controllers;

[ApiController]
[Route("api/v1/audit-logs")]
public class AuditLogsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public AuditLogsController(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> GetAllAuditLogs([FromQuery] string? entityName, [FromQuery] string? action)
    {
        var query = _context.AuditLogs
            .Include(a => a.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            query = query.Where(a => a.EntityName.ToLower() == entityName.Trim().ToLower());
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action.ToLower() == action.Trim().ToLower());
        }

        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(200)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                UserId = a.UserId,
                UserEmail = a.User != null ? a.User.Email : null,
                Action = a.Action,
                EntityName = a.EntityName,
                EntityId = a.EntityId,
                OldValue = a.OldValue,
                NewValue = a.NewValue,
                IpAddress = a.IpAddress,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        return Ok(logs);
    }

    [HttpGet("doctor/me")]
    [Authorize(Roles = AppRoles.Doctor)]
    public async Task<IActionResult> GetDoctorLogs()
    {
        var doctorId = _currentUserService.DoctorId;
        var userId = _currentUserService.UserId;
        if (!doctorId.HasValue || !userId.HasValue) return Forbid();

        // Doctor's logs: where action was taken by doctor or related to doctor's appointments
        var doctorAppointmentIds = await _context.Appointments
            .Where(a => a.DoctorId == doctorId.Value)
            .Select(a => (Guid?)a.Id)
            .ToListAsync();

        var logs = await _context.AuditLogs
            .Include(a => a.User)
            .Where(a => a.UserId == userId.Value || (a.EntityId.HasValue && doctorAppointmentIds.Contains(a.EntityId)))
            .OrderByDescending(a => a.CreatedAt)
            .Take(100)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                UserId = a.UserId,
                UserEmail = a.User != null ? a.User.Email : null,
                Action = a.Action,
                EntityName = a.EntityName,
                EntityId = a.EntityId,
                OldValue = a.OldValue,
                NewValue = a.NewValue,
                IpAddress = a.IpAddress,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        return Ok(logs);
    }
}
