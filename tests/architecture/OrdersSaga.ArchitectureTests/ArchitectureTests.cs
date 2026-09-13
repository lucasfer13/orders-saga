using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace OrdersSaga.ArchitectureTests;

/// <summary>
/// Verifies the boundaries this repo claims in ADR-0004 and CLAUDE.md.
/// Every rule asserts that the set it inspects and the set it forbids are both
/// non-empty before checking anything: a query that selects no type passes in
/// silence, and a boundary that is never actually tested is worse than none.
/// </summary>
public class ArchitectureTests
{
    private const string OrdersDbContext = "Orders.Api.Persistence.OrdersDbContext";
    private const string InventoryDbContext = "Inventory.Api.Persistence.InventoryDbContext";
    private const string PaymentsDbContext = "Payments.Api.Persistence.PaymentsDbContext";
    private const string ShippingDbContext = "Shipping.Api.Persistence.ShippingDbContext";

    private static readonly Assembly OrdersDomain = typeof(Orders.Domain.Order).Assembly;
    private static readonly Assembly OrdersApi = typeof(Orders.Api.ApiMarker).Assembly;
    private static readonly Assembly InventoryApi = typeof(Inventory.Api.ApiMarker).Assembly;
    private static readonly Assembly PaymentsApi = typeof(Payments.Api.ApiMarker).Assembly;
    private static readonly Assembly ShippingApi = typeof(Shipping.Api.ApiMarker).Assembly;

    private static readonly string[] OwnProjects =
    [
        "Orders.Domain", "Orders.Api", "Inventory.Api", "Payments.Api", "Shipping.Api"
    ];

    private static readonly string[] OnlyOrdersDomain = ["Orders.Domain"];

    private static readonly Architecture Arch = new ArchLoader()
        .LoadAssemblies(OrdersDomain, OrdersApi, InventoryApi, PaymentsApi, ShippingApi)
        .Build();

    /// <summary>
    /// CLAUDE.md: "Orders.Domain, cero dependencias de infraestructura".
    /// ArchUnitNET only knows the assemblies it was handed, so a fluent rule against
    /// "types in Microsoft.EntityFrameworkCore" would filter an empty set and pass no
    /// matter what the domain does. Dependency targets do carry their real assembly,
    /// so the check runs over those instead.
    /// </summary>
    [Fact]
    public void Orders_domain_depends_on_nothing_but_itself_and_the_base_class_library()
    {
        var dependencies = AssembliesUsedBy(OrdersDomain);

        // Proves the query resolved something: without it an empty result would pass.
        Assert.Contains("System.Private.CoreLib", dependencies);

        var infrastructure = dependencies.Where(assembly => !IsBaseClassLibrary(assembly)).ToArray();
        Assert.Equal(OnlyOrdersDomain, infrastructure);
    }

    /// <summary>
    /// ADR-0004, first rule: the repository implementation is the only type of the
    /// service that reaches the aggregate through the context. The rest of the
    /// allowlist is wiring that cannot avoid it — the context itself, the design-time
    /// factory, the startup migrator, the migrations EF generates and the health check
    /// registration. A handler appearing here means the aggregate can be modified
    /// around its invariants.
    /// </summary>
    [Fact]
    public void Only_the_persistence_plumbing_of_orders_depends_on_its_dbcontext()
    {
        var context = Types().That().HaveFullName(OrdersDbContext);

        var allowed = Types().That()
            .HaveFullName(OrdersDbContext)
            .Or().HaveFullName("Orders.Api.Persistence.OrderRepository")
            .Or().HaveFullName("Orders.Api.Persistence.OrdersDbContextFactory")
            .Or().HaveFullName("Orders.Api.Persistence.DatabaseMigrator")
            .Or().HaveFullName("Orders.Api.HealthChecks.HealthCheckExtensions")
            .Or().ResideInNamespace("Orders.Api.Persistence.Migrations");

        var guarded = Types().That().ResideInAssembly(OrdersApi).And().AreNot(allowed);

        // A typo in either name would leave the rule with nothing to forbid or nothing
        // to inspect, and it would go green while guarding nothing.
        Assert.Single(context.GetObjects(Arch));
        Assert.NotEmpty(allowed.GetObjects(Arch));
        Assert.NotEmpty(guarded.GetObjects(Arch));

        guarded.Should().NotDependOnAny(context).Check(Arch);
    }

    /// <summary>
    /// ADR-0004, second rule: each service owns its schema. Reading another service's
    /// tables through its context turns four deployables into one distributed monolith,
    /// and it is the rule that matters most in the thin slices.
    /// </summary>
    [Theory]
    [InlineData(nameof(OrdersApi), OrdersDbContext)]
    [InlineData(nameof(InventoryApi), InventoryDbContext)]
    [InlineData(nameof(PaymentsApi), PaymentsDbContext)]
    [InlineData(nameof(ShippingApi), ShippingDbContext)]
    public void A_service_never_depends_on_the_dbcontext_of_another_service(string service, string ownDbContext)
    {
        var foreign = Types().That().HaveNameEndingWith("DbContext").And().DoNotHaveFullName(ownDbContext);
        var guarded = Types().That().ResideInAssembly(AssemblyOf(service));

        Assert.Equal(3, foreign.GetObjects(Arch).Count());
        Assert.NotEmpty(guarded.GetObjects(Arch));

        guarded.Should().NotDependOnAny(foreign).Check(Arch);
    }

    /// <summary>
    /// The four services deploy separately, so no project may reach into another.
    /// Orders.Api referencing its own domain is the single allowed edge, and it points
    /// inwards. Broader than the rule above: it also catches a leak that is not a
    /// context, such as a handler or an entity borrowed from a neighbouring slice.
    /// </summary>
    [Theory]
    [InlineData(nameof(OrdersDomain), null)]
    [InlineData(nameof(OrdersApi), "Orders.Domain")]
    [InlineData(nameof(InventoryApi), null)]
    [InlineData(nameof(PaymentsApi), null)]
    [InlineData(nameof(ShippingApi), null)]
    public void A_project_only_depends_on_the_project_its_architecture_allows(string project, string? allowed)
    {
        var itself = AssemblyOf(project);
        var dependencies = AssembliesUsedBy(itself);

        Assert.NotEmpty(dependencies);

        var siblings = dependencies
            .Where(assembly => OwnProjects.Contains(assembly))
            .Where(assembly => assembly != itself.GetName().Name && assembly != allowed)
            .ToArray();
        Assert.Empty(siblings);
    }

    private static Assembly AssemblyOf(string project) => project switch
    {
        nameof(OrdersDomain) => OrdersDomain,
        nameof(OrdersApi) => OrdersApi,
        nameof(InventoryApi) => InventoryApi,
        nameof(PaymentsApi) => PaymentsApi,
        nameof(ShippingApi) => ShippingApi,
        _ => throw new ArgumentOutOfRangeException(nameof(project), project, "Unknown project.")
    };

    private static string[] AssembliesUsedBy(Assembly assembly)
    {
        var name = assembly.GetName().Name;

        return Arch.Types
            .Where(type => SimpleName(type.Assembly.FullName) == name)
            .SelectMany(type => type.Dependencies)
            .Select(dependency => SimpleName(dependency.Target.Assembly.FullName))
            .Distinct()
            .OrderBy(dependency => dependency, StringComparer.Ordinal)
            .ToArray();
    }

    private static string SimpleName(string assemblyFullName) => assemblyFullName.Split(',')[0];

    private static bool IsBaseClassLibrary(string assembly) =>
        assembly.StartsWith("System.", StringComparison.Ordinal)
        || assembly is "System" or "mscorlib" or "netstandard";
}
