using _717_Microservices.Shared;
using ManageOrdersService.Db;
using ManageOrdersService.Db.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System.Fabric;

namespace ManageOrdersService;

/// <summary>
/// An instance of this class is created for each service instance by the Service Fabric runtime.
/// </summary>
internal sealed class ManageOrdersService : StatelessService, IManageOrderService
{
    private readonly ManageOrderDbContext _dbContext;
    private readonly IManageUserService _userService;
    private readonly IManageCustomerService _customerService;
    private readonly IManageProductService _productService;

    public ManageOrdersService(StatelessServiceContext context, ManageOrderDbContext dbContext, IManageUserService userService, IManageCustomerService customerService, IManageProductService productService)
        : base(context)
    {
        _dbContext = dbContext;
        _userService = userService;
        _customerService = customerService;
        _productService = productService;
    }

    public async Task<((int Id, string UserId, int CustomerId, List<string> Products, DateTime DatePlaced)? orders, string? err)> GetOrderByIdAsync(int orderId)
    {
        try
        {
            var order = await _dbContext.Orders.Where(o => o.Id == orderId).FirstAsync();
            if (order is null)
            {
                return (null, $"no orders found for {orderId}");
            }

            var orderLineItems = await _dbContext.OrderLineItem.Where(l => l.OrderId == orderId).ToListAsync();
            List<string> lineItemsNames = new();
            foreach (var li in orderLineItems)
            {
                var result = await _productService.GetProductNameByIdAsync(li.ProductId);
                lineItemsNames.Add(result.name);
            }

            return (
                (order.Id,
                 order.UserId,
                 order.CustomerId,
                 lineItemsNames,
                 order.DatePlaced),
                null);
        }
        catch (Exception e)
        {
            return (null, e.Message);
        }
    }

    public async Task<(List<(int Id, string UserId, int CustomerId, List<int> Orders, DateTime DatePlaced)>? orders, string? err)> GetOrdersByCustomerAsync(string customerEmail)
    {
        try
        {
            var (customerId, err) = await _customerService.GetCustomerIdByEmailAsync(customerEmail);
            if (err is not null || customerId is null)
            {
                return (null, err ?? $"customer now found for {customerEmail}");
            }

            var orders = await _dbContext.Orders.Where(o => o.CustomerId == customerId).ToListAsync();

            if (orders is null || !orders.Any())
            {
                return (null, $"no orders found for customer {customerId}");
            }

            List<(int, string, int, List<int>, DateTime)> resultList = new();
            resultList.AddRange(orders.Select(o => (o.Id, o.UserId, o.CustomerId, o.LineItems.Select(i => i.Id).ToList(), o.DatePlaced)));

            return (resultList, null);
        }
        catch (Exception e)
        {
            return (null, e.Message);
        }
    }

    public async Task<(List<(int Id, string UserId, int CustomerId, List<int> Orders, DateTime DatePlaced)>? orders, string? err)> GetOrdersByUserAsync(string userEmail)
    {
        try
        {
            var (userId, err) = await _userService.GetUserIdByEmailAsync(userEmail);
            if (err is not null || userId is null)
            {
                return (null, err ?? $"unable to find user for {userEmail}");
            }

            var orders = await _dbContext.Orders.Where(o => o.UserId == userId).ToListAsync();

            if (orders is null || !orders.Any())
            {
                return (null, $"no orders found for customer {userId}");
            }

            List<(int, string, int, List<int>, DateTime)> resultList = new();
            resultList.AddRange(orders.Select(o => (o.Id, o.UserId, o.CustomerId, o.LineItems.Select(i => i.Id).ToList(), o.DatePlaced)));

            return (resultList, null);
        }
        catch (Exception e)
        {
            return (null, e.Message);
        }
    }

    public async Task<(int? orderId, string? error)> PlaceOrderAsync(string userEmail, string customerEmail, string customerName, List<string> products)
    {
        try
        {
            var (userId, getUserErr) = await _userService.GetUserIdByEmailAsync(userEmail);

            if (getUserErr is not null || userId is null)
            {
                return (null, getUserErr ?? $"unable to get user id for {userEmail}");
            }

            var (custId, getCustErr) = await _customerService.GetCustomerIdByEmailAsync(customerEmail);

            if (getCustErr is not null && !getCustErr.Contains("Sequence contains no elements."))
            {
                return (null, getCustErr);
            }

            //TODO: add a db transaction to rollback if any errors
            if (custId is null)
            {
                string? addCustErr;
                (custId, addCustErr) = await _customerService.AddCustomerAsync(customerName, customerEmail);

                if (addCustErr is not null || custId is null)
                {
                    return (null, addCustErr ?? $"unable to find or add customer record for {customerEmail}");
                }
            }

            List<OrderLineItem> lineItems = new();

            foreach (var productName in products)
            {
                var (id, err) = await _productService.GetProductIdByNameAsync(productName);
                //TODO: add error handling - hoping and praying for now
                lineItems.Add(new OrderLineItem { ProductId = id.Value});
            }

            var datePlaced = DateTime.Now;
            var order = new Order
            {
                UserId = userId,
                CustomerId = custId.Value,
                LineItems = lineItems,
                DatePlaced = datePlaced
            };

            _ = await _dbContext.Orders.AddAsync(order);
            _ = await _dbContext.SaveChangesAsync();
            var newOrder = await _dbContext.Orders.Where(o => o.UserId == userId && o.CustomerId == custId && o.DatePlaced == datePlaced).FirstAsync();

            if (newOrder is null)
            {
                return (null, "unable to create order");
            }

            return (newOrder.Id, null);
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
