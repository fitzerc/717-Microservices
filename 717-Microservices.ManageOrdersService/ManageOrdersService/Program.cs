using _717_Microservices.Shared;
using ManageOrdersService.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Runtime;
using System.Diagnostics;

namespace ManageOrdersService
{
    internal static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        private static void Main()
        {
            try
            {
                // The ServiceManifest.XML file defines one or more service type names.
                // Registering a service maps a service type name to a .NET type.
                // When Service Fabric creates an instance of this service type,
                // an instance of the class is created in this host process.
                var services = new ServiceCollection();
                services.AddDbContext<ManageOrderDbContext>(options => options.UseSqlite(@"Filename=C:/tmp/microservices-orders.sqlite"));
                services.AddSingleton<IManageUserService>(ServiceProxy.Create<IManageUserService>(ServiceUris.ManageUsersUri));
                services.AddSingleton<IManageCustomerService>(ServiceProxy.Create<IManageCustomerService>(ServiceUris.ManageCustomersUri));
                services.AddSingleton<IManageProductService>(ServiceProxy.Create<IManageProductService>(ServiceUris.ManageProductsUri));

                var provider = services.BuildServiceProvider();

                ServiceRuntime.RegisterServiceAsync("ManageOrdersServiceType",
                    context => new ManageOrdersService(
                        context,
                        provider.GetRequiredService<ManageOrderDbContext>(),
                        provider.GetRequiredService<IManageUserService>(),
                        provider.GetRequiredService<IManageCustomerService>(),
                        provider.GetRequiredService<IManageProductService>()
                    ))
                    .GetAwaiter()
                    .GetResult();

                ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(ManageOrdersService).Name);

                // Prevents this host process from terminating so services keep running.
                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception e)
            {
                ServiceEventSource.Current.ServiceHostInitializationFailed(e.ToString());
                throw;
            }
        }
    }
}
