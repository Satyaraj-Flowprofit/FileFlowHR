using FluentValidation;
using FileFlowHR.Application.Common.Behaviors;
using FileFlowHR.Application.Common.Messaging;
using FileFlowHR.Application.Common.Security;
using FileFlowHR.Application.Abstractions;
using FileFlowHR.Application.Features.Organisation;
using FileFlowHR.Application.Features.People;
using Microsoft.Extensions.DependencyInjection;

namespace FileFlowHR.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // Every IRequestHandler<,> in this assembly, found by convention.
        foreach (var type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
        foreach (var contract in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
            services.AddScoped(contract, type);

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // Order matters: errors are caught outermost, then permissions, then validation.
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ExceptionBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<UserContext>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<UserContext>());
        services.AddScoped<ICompanyContext>(sp => sp.GetRequiredService<UserContext>());
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAccessScopeResolver, AccessScopeResolver>();
        services.AddScoped<IRoleResolver, RoleResolver>();
        services.AddScoped<IPositionReader, PositionReader>();
        services.AddScoped<IReportingLineService, ReportingLineService>();
        services.AddScoped<IStaffingService, StaffingService>();
        services.AddScoped<IPersonNumberGenerator, PersonNumberGenerator>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
