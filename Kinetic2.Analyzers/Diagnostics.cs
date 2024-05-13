using Microsoft.CodeAnalysis;

namespace Kinetic2.Analyzers;
/*
 * parts of this code taken from the most excellent https://github.com/DapperLib/DapperAOT/ 
 * licence: The Dapper library and tools are licenced under Apache 2.0: http://www.apache.org/licenses/LICENSE-2.0
 * owner: Marc Gravell
 */

internal sealed class Diagnostics : DiagnosticsBase {
    internal static readonly DiagnosticDescriptor UnableToResolveSymbol = GenerationError("K2P208", "ResiliencePipelineAttribute not found", "ResiliencePipelineAttribute not found on method {1}::{0}");
    internal static readonly DiagnosticDescriptor UnknownError = GenerationError("K2P201", "Generic error", "Dapper.AOT handled {0} of {1} possible call-sites using {2} interceptors, {3} commands and {4} readers");
    internal static readonly DiagnosticDescriptor CodeGenerated = GenerationInfo("K2P099", "Completed", "Code generation completed");

    internal static readonly DiagnosticDescriptor DefaultImplementationOnInterfaceNotSupported = UsageError("K2P201", "Unsupported: Default implementation on interface", "Applying the attribute to default implementation on an interface (method {1}::{0}) is not supported");
    // todo asyncenumerable
    internal static readonly DiagnosticDescriptor MethodMustBeAwaitableAndReturnValueTaskOrValueTaskOfT = UsageError("K2P204", "Invalid return type", "Method {1}::{0} must be awaitable and return ValueTask/ValueTask<T>/Task/Task<T>");
    internal static readonly DiagnosticDescriptor AsyncEnumerableNotSupported = UsageError("K2P209", "Invalid return type", "IAsyncEnumerable not supported for use with resilience pipeline; Method {1}::{0} must be awaitable and return ValueTask/ValueTask<T>/Task/Task<T>");

    internal static readonly DiagnosticDescriptor MethodMayNotBeSealed = UsageError("K2P203", "Method may not be sealed", "When using implementation types without interfaces, the method {1}::{0} may not be sealed");

    internal static readonly DiagnosticDescriptor MethodMustBeVirtual = UsageError("K2P205", "Method must be virtual", "When using implementation types without interfaces, method {1}::{0} must be virtual if attributed with ResiliencePipelineAttribute");

    internal static readonly DiagnosticDescriptor DeclaringTypeMayNotBeSealedOfNoInterface = UsageError("K2P206", "Type may not be sealed", "When using implementation types without interfaces, the containing type {1} may not be sealed");

    internal static readonly DiagnosticDescriptor UnsupportedTypeInDiRegistration = UsageWarning("K2P102", "Unsupported type in dependency injection container registration", "The type {2} registered using {1}.{0} with the DI container is not a supported type; this statement will be ignored (which may be fine)");
    internal static readonly DiagnosticDescriptor UnsupportedTypeInDiRegistration2 = UsageWarning("K2P102", "Unsupported type in dependency injection container registration", "The type {2}/{3} registered using {1}.{0} with the DI container is not a supported type; this statement will be ignored (which may be fine)");

    internal static readonly DiagnosticDescriptor UnsupportedDiRegistrationMethodCalled = UsageWarning("K2P101", "Unsupported method for registering type with dependency injection container", "Method {1}::{0} is unsupported, please use the generic extension methods from Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions");
}
