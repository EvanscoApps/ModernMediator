index	openLine	lineCount	firstChars
1	144	16	// Program.cs - with assembly scanning (uses reflection) ser
2	165	5	// Singleton (shared instance) - ideal for Pub/Sub across th
3	187	10	// Auto-generated - use instead of assembly scanning service
4	201	2	// Generated extension methods bypass reflection entirely va
5	225	10	services.AddModernMediator(config => { // Eager (default) - 
6	242	16	// Define request and response public record GetUserQuery(in
7	265	14	// Define a ValueTask handler public class GetCachedUserHand
8	284	16	// Define command public record CreateUserCommand(string Nam
9	305	22	// Define stream request public record GetAllUsersRequest(in
10	338	28	services.AddModernMediator(config => { config.RegisterServic
11	373	22	// Transaction behavior that wraps all requests public class
12	422	16	// Behavior for a specific request type public class GetUser
13	445	16	public class CreateOrderHandler : IRequestHandler<CreateOrde
14	468	5	[Endpoint(HttpMethod.Post, "/api/users")] public record Crea
15	478	28	// Pre-processor runs before handler public class Authorizat
16	513	22	// Define an exception handler for a specific exception type
17	542	14	// Define a message public record OrderCreatedEvent(int Orde
18	565	13	public record OrderCreatedNotification(int OrderId) : INotif
19	585	12	// Subscriptions on DI-injected IMediator are scoped to that
20	602	9	// Subscribe async mediator.SubscribeAsync<OrderCreatedEvent
21	618	19	// Define message and response types public record Confirmat
22	642	12	// Multiple async validators mediator.SubscribeAsync<Validat
23	666	10	public record AnimalEvent(string Name); public record DogEve
24	681	7	// Subscribe to specific topics mediator.Subscribe<OrderEven
25	693	5	// Weak reference (default) - handler can be GC'd mediator.S
26	703	12	// Subscribe returns a disposable token var subscription = m
27	720	16	// Set dispatcher once at startup mediator.SetDispatcher(new
28	743	2	// In your Avalonia App.axaml.cs or startup mediator.SetDisp
29	752	10	// Set error policy mediator.ErrorPolicy = ErrorPolicy.LogAn
