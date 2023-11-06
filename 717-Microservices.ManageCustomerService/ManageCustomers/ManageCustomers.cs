using _717_Microservices.Shared;
using ManageCustomers.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using System.Fabric;

namespace ManageCustomers
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class ManageCustomers : StatelessService, IManageCustomerService
    {
        private readonly ManageCustomerDbContext _dbContext;

        public ManageCustomers(StatelessServiceContext context, ManageCustomerDbContext dbContext)
            : base(context)
        {
            _dbContext = dbContext;
        }

        public async Task<(int? custId, string? error)> AddCustomerAsync(string name, string email)
        {
            try
            {
                var existingCust = await _dbContext.Customers.Where(x => x.Email == email).ToListAsync();

                if (existingCust != null && existingCust.Any())
                {
                    return (null, $"customer exists: {existingCust.First().Id}");
                }

                _ = await _dbContext.Customers.AddAsync(new() {FullName = name, Email = email});
                _ = await _dbContext.SaveChangesAsync();
                var newCust = await _dbContext.Customers.Where(x => x.Email == email).FirstAsync();

                return (newCust.Id, null);
            }
            catch (Exception e)
            {
                return (null, e.Message);
            }
        }

        public async Task<(int? custId, string? err)> GetCustomerIdByEmailAsync(string email)
        {
            try
            {
                var customer = await _dbContext.Customers.Where(c => c.Email == email).FirstAsync();

                if (customer is null)
                {
                    return (null, $"unable to find customer record for {email}");
                }

                return (customer.Id, null);
            }
            catch (Exception e)
            {
                return (null, e.Message);
            }
        }

        public async Task<(List<(int id, string name, string email)>? custs, string? error)> GetCustomersAsync()
        {
            try
            {
                var customers = await _dbContext.Customers.ToListAsync();

                var retList = customers.Select(c => (c.Id, c.FullName, c.Email)).ToList();
                return (retList, null);
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
}
