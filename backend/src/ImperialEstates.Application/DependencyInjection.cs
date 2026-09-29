using FluentValidation;
using ImperialEstates.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ImperialEstates.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<Validators.UpsertBlockRequestValidator>();
        services.AddScoped<BlockService>();
        services.AddScoped<PropertyService>();
        services.AddScoped<UserManagementService>();
        services.AddScoped<EnquiryService>();
        services.AddScoped<RecruitmentService>();
        services.AddScoped<AuthService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<RentSyncService>();
        services.AddScoped<CommissionService>();
        services.AddScoped<AuditLogService>();
        return services;
    }
}
