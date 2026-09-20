using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ShortP2P.Discovery.RouteTables;

namespace TorgLink.Web.Api;

/// <summary>
/// ASP.NET Core rejects ShortP2P's default RouteDb registration.
/// <c>AddDbContext</c> puts <see cref="DbContextOptions{TContext}"/> and
/// <see cref="IDbContextOptionsConfiguration{TContext}"/> in the scoped bucket, while
/// <c>AddDbContextFactory</c> is a singleton (EF Core <c>TryAdd</c> keeps the scoped options).
/// MAUI does not validate that graph; the web host must.
/// </summary>
internal static class RouteDbContextAspNetRegistration
{
    public static IServiceCollection AddRouteDbContextForAspNet(
        this IServiceCollection services,
        string sqliteDatabasePath)
    {
        services.AddRouteDbContextWithPeerExpiryCleanup(sqliteDatabasePath, enableDiscovery: true);
        AlignRouteDbContextOptionsWithSingletonFactory<RouteDbContext>(services);
        return services;
    }

    private static void AlignRouteDbContextOptionsWithSingletonFactory<TContext>(IServiceCollection services)
        where TContext : DbContext
    {
        foreach (var descriptor in services
                     .Where(d => d.ServiceType == typeof(IDbContextOptionsConfiguration<TContext>)
                                 && d.Lifetime != ServiceLifetime.Singleton)
                     .ToList())
            services.Remove(descriptor);

        var options = services.FirstOrDefault(d => d.ServiceType == typeof(DbContextOptions<TContext>));
        if (options is null || options.Lifetime == ServiceLifetime.Singleton)
            return;

        services.Remove(options);
        services.Add(CloneWithLifetime(options, ServiceLifetime.Singleton));
    }

    private static ServiceDescriptor CloneWithLifetime(ServiceDescriptor source, ServiceLifetime lifetime)
    {
        if (source.ImplementationFactory is not null)
            return new ServiceDescriptor(source.ServiceType, source.ImplementationFactory, lifetime);
        if (source.ImplementationInstance is not null)
            return new ServiceDescriptor(source.ServiceType, source.ImplementationInstance);
        if (source.ImplementationType is not null)
            return new ServiceDescriptor(source.ServiceType, source.ImplementationType, lifetime);

        throw new InvalidOperationException(
            $"Cannot change lifetime of {source.ServiceType}: descriptor has no implementation.");
    }
}
