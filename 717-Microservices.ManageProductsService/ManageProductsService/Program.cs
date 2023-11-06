using ManageProductsService.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Runtime;
using System.Diagnostics;

namespace ManageProductsService
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
                services.AddDbContext<ManageProductDbContext>(options => options.UseSqlite(@"Filename=C:/tmp/microservices-products.sqlite"));

                var provider = services.BuildServiceProvider();

                ServiceRuntime.RegisterServiceAsync("ManageProductsServiceType",
                    context => new ManageProductsService(
                        context,
                        provider.GetRequiredService<ManageProductDbContext>()
                    ))
                    .GetAwaiter()
                    .GetResult();

                ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(ManageProductsService).Name);

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
