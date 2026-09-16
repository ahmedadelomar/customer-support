using System.Text.Json;
using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using CustomerSupport.Application.Common.Settings;
using CustomerSupport.Application.Settings.Queries;
using CustomerSupport.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Settings.Commands;

/// <summary>
/// Writes one setting, globally or as a branch override
/// (Security &amp; Administration / System configuration).
/// </summary>
[RequirePermission(Permissions.Administration.ManageSettings)]
public record UpdateSettingCommand(string Key, string? Value, Guid? BranchId = null) : IRequest;

public class UpdateSettingCommandHandler(ISettingsProvider settings, IAuditRecorder audit)
    : IRequestHandler<UpdateSettingCommand>
{
    public async Task Handle(UpdateSettingCommand request, CancellationToken cancellationToken)
    {
        var definition = SettingKeys.Find(request.Key)
            ?? throw new NotFoundException("Setting", request.Key);

        // Resubmitting the mask means "leave it alone" — the form had no way to show the real value,
        // so treating it as a write would overwrite the secret with literal asterisks.
        if (definition.IsSecret && request.Value == SecretMask.Value)
        {
            return;
        }

        if (!IsValidForType(request.Value, definition.DataType))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["value"] = [$"Value must be a valid {definition.DataType}."],
            });
        }

        await settings.SetAsync(request.Key, request.Value, request.BranchId, cancellationToken);

        await audit.RecordAsync(
            AuditAction.ConfigChanged,
            nameof(Domain.Identity.SystemSetting),
            request.Key,

            // Never the value itself for a secret — that would put it in the trail in clear text.
            metadata: new
            {
                request.BranchId,
                Value = definition.IsSecret ? "(secret)" : request.Value,
            },
            ct: cancellationToken);
    }

    /// <summary>Validated before storage, so a bad value is a field error rather than a runtime surprise.</summary>
    private static bool IsValidForType(string? value, string dataType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return dataType switch
        {
            "int" => int.TryParse(value, out _),
            "bool" => bool.TryParse(value, out _),
            "json" => IsValidJson(value),
            _ => true,
        };
    }

    private static bool IsValidJson(string value)
    {
        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

/// <summary>
/// Removes a branch override, restoring inheritance from the global value. The global row itself is
/// never deletable — system settings can be edited but not removed.
/// </summary>
[RequirePermission(Permissions.Administration.ManageSettings)]
public record DeleteSettingOverrideCommand(string Key, Guid BranchId) : IRequest;

public class DeleteSettingOverrideCommandHandler(
    IAppDbContext db, ISettingsProvider settings, IAuditRecorder audit)
    : IRequestHandler<DeleteSettingOverrideCommand>
{
    public async Task Handle(DeleteSettingOverrideCommand request, CancellationToken cancellationToken)
    {
        var row = await db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == request.Key && s.BranchId == request.BranchId, cancellationToken)
            ?? throw new NotFoundException("SettingOverride", request.Key);

        db.SystemSettings.Remove(row);
        await db.SaveChangesAsync(cancellationToken);

        settings.Invalidate(request.Key);

        await audit.RecordAsync(
            AuditAction.ConfigChanged,
            nameof(Domain.Identity.SystemSetting),
            request.Key,
            metadata: new { request.BranchId, Removed = "branch override" },
            ct: cancellationToken);
    }
}
