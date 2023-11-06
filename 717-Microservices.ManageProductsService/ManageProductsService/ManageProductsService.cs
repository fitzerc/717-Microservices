using _717_Microservices.Shared;
using ManageProductsService.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System.Fabric;

namespace ManageProductsService;

/// <summary>
/// An instance of this class is created for each service instance by the Service Fabric runtime.
/// </summary>
internal sealed class ManageProductsService : StatelessService, IManageProductService
{
    private readonly ManageProductDbContext _dbContext;

    public ManageProductsService(StatelessServiceContext context, ManageProductDbContext dbContext)
        : base(context)
    {
        _dbContext = dbContext;
    }

    public async Task<(int? productId, string? error)> AddProductAsync(string name, string description, decimal price)
    {
        try
        {
            var existingProd = await _dbContext.Products.Where(x => x.Name == name).ToListAsync();

            if (existingProd != null && existingProd.Any())
            {
                return (null, $"product already exists: {existingProd.First().Id}");
            }

            _ = await _dbContext.Products.AddAsync(new() { Name = name, Description = description, Price = price});
            _ = _dbContext.SaveChanges();
            var newProd = await _dbContext.Products.Where(x => x.Name == name).FirstAsync();

            return (newProd.Id, null);
        }
        catch (Exception e)
        {
            return (null, e.Message);
        }
    }

    public async Task<(int? id, string? err)> GetProductIdByNameAsync(string name)
    {
        try
        {
            var product = await _dbContext.Products.Where(p => p.Name == name).FirstOrDefaultAsync();

            if (product is null)
            {
                return (null, $"unable to find product {name}");
            }

            return (product.Id, null);
        }
        catch (Exception e)
        {
            return (null, e.Message);
        }
    }

    public async Task<(string? name, string? err)> GetProductNameByIdAsync(int id)
    {
        try
        {
            var product = await _dbContext.Products.Where(p => p.Id == id).FirstAsync();

            if (product is null)
            {
                return (null, $"unable to find product for {id}");
            }

            return (product.Name, null);
        }
        catch (Exception e)
        {
            return (null, e.Message);
        }
    }

    public async Task<(List<(int Id, string Name, string Description, decimal Cost)>? products, string? error)> GetProductsAsync()
    {
        try
        {
            var products = await _dbContext.Products.ToListAsync();

            List<(int, string, string, decimal)> resultList = new();
            resultList.AddRange(products.Select(product => (product.Id, product.Name, product.Description, product.Price)));

            return (resultList, null);
        }
        catch (Exception e)
        {
            return (null, e.Message);
        }
    }

    /// <summary>
    /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
    /// </summary>
    /// <returns>A collection of listeners.</returns>
    protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
    {
        return this.CreateServiceRemotingInstanceListeners();
    }

    /// <summary>
    /// This is the main entry point for your service instance.
    /// </summary>
    /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        // TODO: Replace the following sample code with your own logic 
        //       or remove this RunAsync override if it's not needed in your service.

        long iterations = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ServiceEventSource.Current.ServiceMessage(this.Context, "Working-{0}", ++iterations);

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }
}
