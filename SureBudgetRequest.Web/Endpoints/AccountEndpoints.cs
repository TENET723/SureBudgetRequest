using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Web.Services;

namespace SureBudgetRequest.Web.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        // POST /account/switch-user
        app.MapPost("/account/switch-user", async (
            [FromForm] Guid targetUserId,
            HttpContext ctx,
            IUserRepository userRepo,
            IDepartmentRepository deptRepo,
            CancellationToken ct) =>
        {
            var currentPrincipal = ctx.User;
            if (currentPrincipal.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();

            // Real user ID is stored in ClaimRealActorUserId if already switched, else in NameIdentifier
            var realActorIdStr = currentPrincipal.FindFirst(CurrentUserService.ClaimRealActorUserId)?.Value
                ?? currentPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(realActorIdStr, out var realUserId))
                return Results.Unauthorized();

            var realUser = await userRepo.GetByIdAsync(realUserId, ct);
            if (realUser is null || !realUser.IsActive)
                return Results.Unauthorized();

            var targetUser = await userRepo.GetByIdAsync(targetUserId, ct);
            if (targetUser is null || !targetUser.IsActive || !targetUser.IsOnLeave || targetUser.FallbackUserId != realUser.Id)
                return Results.Forbid();

            var depts = await deptRepo.ListAsync(includeInactive: false, ct);
            var isDeptHead = depts.Any(d => d.HeadUserId == targetUser.Id);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, targetUser.Id.ToString()),
                new(ClaimTypes.Name, targetUser.FullName),
                new(ClaimTypes.Email, targetUser.Email),
                new(ClaimTypes.Role, targetUser.Role.ToString()),
                new(CurrentUserService.ClaimUsername, targetUser.Username),
                new(CurrentUserService.ClaimDepartmentId, targetUser.DepartmentId.ToString()),
                new(CurrentUserService.ClaimMustChangePassword, "false"),
                new(CurrentUserService.ClaimIsFinanceApprover, targetUser.IsFinanceApprover ? "true" : "false"),
                new(CurrentUserService.ClaimIsDepartmentHead, isDeptHead ? "true" : "false"),
                new(CurrentUserService.ClaimIsFallbackPersonAct, "true"),
                new(CurrentUserService.ClaimRealActorUserId, realUser.Id.ToString()),
                new(CurrentUserService.ClaimRealActorFullName, realUser.FullName)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            return Results.Redirect("/");
        });

        // POST /account/switch-back
        app.MapPost("/account/switch-back", async (
            HttpContext ctx,
            IUserRepository userRepo,
            IDepartmentRepository deptRepo,
            CancellationToken ct) =>
        {
            var currentPrincipal = ctx.User;
            if (currentPrincipal.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();

            var realActorIdStr = currentPrincipal.FindFirst(CurrentUserService.ClaimRealActorUserId)?.Value;
            if (!Guid.TryParse(realActorIdStr, out var realUserId))
                return Results.Redirect("/");

            var realUser = await userRepo.GetByIdAsync(realUserId, ct);
            if (realUser is null || !realUser.IsActive)
            {
                await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.Redirect("/login");
            }

            var depts = await deptRepo.ListAsync(includeInactive: false, ct);
            var isDeptHead = depts.Any(d => d.HeadUserId == realUser.Id);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, realUser.Id.ToString()),
                new(ClaimTypes.Name, realUser.FullName),
                new(ClaimTypes.Email, realUser.Email),
                new(ClaimTypes.Role, realUser.Role.ToString()),
                new(CurrentUserService.ClaimUsername, realUser.Username),
                new(CurrentUserService.ClaimDepartmentId, realUser.DepartmentId.ToString()),
                new(CurrentUserService.ClaimMustChangePassword, realUser.MustChangePassword ? "true" : "false"),
                new(CurrentUserService.ClaimIsFinanceApprover, realUser.IsFinanceApprover ? "true" : "false"),
                new(CurrentUserService.ClaimIsDepartmentHead, isDeptHead ? "true" : "false")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            return Results.Redirect("/");
        });

        return app;
    }
}
