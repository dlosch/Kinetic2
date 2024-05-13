using Microsoft.Extensions.DependencyInjection;

namespace Kinetic2;

public static class ServiceCollectionExtensions {
    // marker to ensure the source generator ran. Will be replaced by an interceptor with a no op.
    public static void RegisterKinetic2(this IServiceCollection services) {
        throw new InvalidOperationException("Cloudsiders.Kinetic2 Source Generator did not execute properly.");
    }
}
