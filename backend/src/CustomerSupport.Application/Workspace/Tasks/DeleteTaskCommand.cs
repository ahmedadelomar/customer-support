using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupport.Application.Workspace.Tasks;

[RequirePermission(Permissions.Workspace.ManageTasks)]
public record DeleteTaskCommand(Guid Id) : IRequest;

public class DeleteTaskCommandHandler(IAppDbContext db) : IRequestHandler<DeleteTaskCommand>
{
    public async Task Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await db.AgentTasks.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Workspace.AgentTask), request.Id);

        db.AgentTasks.Remove(task);
        await db.SaveChangesAsync(cancellationToken);
    }
}
