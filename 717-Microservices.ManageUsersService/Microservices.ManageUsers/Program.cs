using System;
using System.Diagnostics;
using System.Fabric;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microservices.ManageUsers.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.ServiceFabric.Services.Runtime;

namespace Microservices.ManageUsers
{
    internal static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        private static async Task Main()
        {
            try
            {
                // The ServiceManifest.XML file defines one or more service type names.
                // Registering a service maps a service type name to a .NET type.
                // When Service Fabric creates an instance of this service type,
                // an instance of the class is created in this host process.
                var services = new ServiceCollection();
                services.AddDbContext<MicroservicesAuthDbContext>(options => options.UseSqlite(@"Filename=C:/tmp/microservices-users.sqlite"));
                services.AddIdentityCore<IdentityUser>(options => options.User.RequireUniqueEmail = true).AddEntityFrameworkStores<MicroservicesAuthDbContext>()
                    .AddEntityFrameworkStores<MicroservicesAuthDbContext>();

                var provider = services.BuildServiceProvider();

                //add deps to ManageUsers as needed
                ServiceRuntime.RegisterServiceAsync("Microservices.ManageUsersType",
                    context => new ManageUsers(
                        context,
                        provider.GetRequiredService<IUserStore<IdentityUser>>(),
                        provider.GetRequiredService<UserManager<IdentityUser>>()))
                    .GetAwaiter()
                    .GetResult();

                ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(ManageUsers).Name);



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
