# Cloudsiders.Kinetic2

*While admitting weakness may be a strength, adding resilience and trying again after failure is usually the more rewarding path* ... said no one ever. Well, maybe [***Aaliyah***](https://open.spotify.com/track/4OFbSGqe95wEJTN8Bn9eHF) did. *Dust yourself off and try again*!

Small project which uses a source generator to add [Polly](https://github.com/App-vNext/Polly) reslience pipelines logic to methods using just an attribute.

You can add a resilience pipeline with just the following code to your service method:
```
// the attribute will be used by the source generator
[ResiliencePipeline("NotificationServicePipeline")]
public async ValueTask SendNotification(Message notification, CancellationToken cancellation) { ... }
```
Polly policies support not only Http calls but may be added to almost any logic.

## What does it do?

Consider the following code 

```
internal interface INotificationService {
    ValueTask SendNotification(Message notification, CancellationToken cancellation);
}

internal sealed class NotificationService : INotificationService {
    public async ValueTask SendNotification(Message notification, CancellationToken cancellation) {
        // perform whatever happy path logic you have
        ...

    }
}
```

In your main logic, you register this service with the dependency injection container
```
builder.Services.AddTransient<INotificationService, NotificationService>();
```
and you inject it in some other service which orchestrates the logic
```
internal class UserOnboardingService(INotificationService _notificationService) {

    ValueTask OnboardUser(Request request, CancellationToken cancellation) {
        ...
        var message = ConstructMessage(request);
        await _notificationService.SendNotification(message, cancellation);
        ...
    }
}
```
note: I don't like primary constructors that much ...

## Adding resilience ... how does it work?
First, you add a reference to Polly and possibly related Polly nugets like Polly.RateLimiting ...
```
    <PackageReference Include="Polly" Version="8.3.1" />
    <PackageReference Include="Polly.RateLimiting" Version="8.3.1" />
```

Register the reslience pipeline with the DI container
```
builder.Services.AddResiliencePipeline("NotificationServicePipeline", builder => {
    builder
        .AddRetry(new RetryStrategyOptions() { BackoffType = DelayBackoffType.Exponential, MaxRetryAttempts = 6, UseJitter = true })
        .AddConcurrencyLimiter(1, 5)
        .AddTimeout(TimeSpan.FromSeconds(10));
});
```
Then, add the reference to Kinetic2 
```
    <PackageReference Include="Kinetic2.Analyzers" Version="1.0.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
```

Just add the attribute to the method. You can apply it to the method in the class or the interface or both ...
```
internal sealed class NotificationService : INotificationService {
    [ResiliencePipeline("NotificationServicePipeline")]
    public async ValueTask SendNotification(Message notification, CancellationToken cancellation) {
        // perform whatever happy path logic you have
        // this logic may fail because it sends some message to a remote system or inserts a row into a database table or whatever
        ...

    }
}
```

No other changes are required.

## What does it do?
for a DI registration that uses and interface and an implementation type (`builder.Services.AddTransient<INotificationService, NotificationService>( _ => new NotificationService()`), we
- create a new type which implements the interface type
- inject `IServiceProvider` and an instance of the original `NotificationService` implementation in the ctor
- for each method which has the attribute applied, generate code which intercepts the call, resolves and executes the resilience pipeline and calls the original instance/method inside the pipeline
- modify the DI registration to use the newly generated type instead of the original `NotificationService` (`builder.Services.AddTransient<INotificationService, NewDerivedNotificationService>(...)`)

You can use factory functions in your DI registration, however, you must use the generic extension methods from `Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions` (which you will be most likely anyway - no hardship here. Non generic versions `AddTransient(typeof(NotificationService))` or using `ServiceDescriptor` directly won't work).

We then register the newly generated type instead of the original type under the interface.

For a DI registration which does not use an interface  (`builder.Services.AddTransient<NotificationService>()`), we a) derive a type from NotificationService and implement the interception in this derived type. NotificationService must not be sealed and the method must be virtual and not sealed in this case.

It doesn't support anything, but it should
1. applying the attribute to default implementations in interfaces
2. applying the attribute somewhere in the inheritance hierarchy
3. applying the attribute multiple times in inheritance hierarchy (it will use the most derived).

## Generated source code

In your .csproj, add 
```
<PropertyGroup>
    ...    
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    ...
</PropertyGroup>    
```
you will be able to see the generated source code under the `./obj/$(Configuration)/$(TargetFramework)/generated/Kinetic2.Analyzers/Kinetic2.Analyzers.K2PollyGenerator`

## Anything else?

This is a small sample subset from a larger project injecting more complex logic (distributed, reliable, durable workflows).
