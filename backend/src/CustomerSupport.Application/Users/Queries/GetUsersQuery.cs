using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Models;
using CustomerSupport.Application.Common.Security;
using MediatR;

namespace CustomerSupport.Application.Users.Queries;

/// <summary>
/// Paged, filtered list of agent accounts (Security &amp; Administration / Users and roles).
/// Follows the <c>GetCustomersQuery</c> shape; the Identity-specific query itself runs in
/// <see cref="IUserAdminService"/> because <c>ApplicationUser</c> is not exposed to this layer.
/// </summary>
[RequirePermission(Permissions.Administration.ViewUsers)]
public class GetUsersQuery : PagedQuery, IRequest<PagedResult<UserListItemDto>>
{
    public Guid? RoleId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? BranchId { get; set; }
    public bool? IsActive { get; set; }
}

public class GetUsersQueryHandler(IUserAdminService users)
    : IRequestHandler<GetUsersQuery, PagedResult<UserListItemDto>>
{
    public Task<PagedResult<UserListItemDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken) =>
        users.ListAsync(
            new UserListFilter
            {
                Search = request.Search,
                RoleId = request.RoleId,
                DepartmentId = request.DepartmentId,
                BranchId = request.BranchId,
                IsActive = request.IsActive,
                SortBy = request.SortBy,
                SortDescending = request.SortDescending,
                Page = request.Page,
                PageSize = request.PageSize,
            },
            cancellationToken);
}

/// <summary>One account's full record, for the edit form.</summary>
[RequirePermission(Permissions.Administration.ViewUsers)]
public record GetUserByIdQuery(Guid Id) : IRequest<UserDetailDto>;

public class GetUserByIdQueryHandler(IUserAdminService users)
    : IRequestHandler<GetUserByIdQuery, UserDetailDto>
{
    public async Task<UserDetailDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken) =>
        await users.GetAsync(request.Id, cancellationToken)
        ?? throw new Common.Exceptions.NotFoundException("User", request.Id);
}
