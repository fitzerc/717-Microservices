using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using _717_Microservices.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Runtime;

namespace Microservices.Api;

/// <summary>
/// The FabricRuntime creates an instance of this class for each service type instance.
/// </summary>
internal sealed class Api : StatelessService
{
    public Api(StatelessServiceContext context)
        : base(context)
    { }

    internal record JwtSettings(string Issuer, string Audience, byte[] Key);
    internal JwtSettings jwtSettings = new("ManageUsersService", "Microservices.Api", Encoding.UTF8.GetBytes("Mysupersecretkeythatishardtoguess"));

    /// <summary>
    /// Optional override to create listeners (like tcp, http) for this service instance.
    /// </summary>
    /// <returns>The collection of listeners.</returns>
    protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
    {
        return new ServiceInstanceListener[]
        {
            new ServiceInstanceListener(serviceContext =>
                new KestrelCommunicationListener(serviceContext, "ServiceEndpoint", (url, listener) =>
                {
                    ServiceEventSource.Current.ServiceMessage(serviceContext, $"Starting Kestrel on {url}");

                    var builder = WebApplication.CreateBuilder();

                    builder.Services.AddSingleton<StatelessServiceContext>(serviceContext);
                    builder.WebHost
                                .UseKestrel(opt =>
                                {
                                    int port = serviceContext.CodePackageActivationContext.GetEndpoint("ServiceEndpoint").Port;
                                    opt.Listen(IPAddress.IPv6Any, port, listenOptions =>
                                    {
                                        listenOptions.UseHttps(GetCertificateFromStore());
                                    });
                                })
                                .UseContentRoot(Directory.GetCurrentDirectory())
                                .UseServiceFabricIntegration(listener, ServiceFabricIntegrationOptions.None)
                                .UseUrls(url);
                    
                    // Add services to the container.
                    builder.Services.AddAuthentication(x =>
                    {
                        x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                        x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                        x.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                    }).AddJwtBearer(x =>
                    {
                        x.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidIssuer = jwtSettings.Issuer,
                            ValidAudience = jwtSettings.Audience,
                            IssuerSigningKey = new SymmetricSecurityKey(jwtSettings.Key),
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true
                        };
                    });
                    builder.Services.AddAuthorization(authOptions => authOptions.AddPolicy("basic", policy =>
                    {
                        policy
                        .RequireAuthenticatedUser()
                        .AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
                    }));

                    builder.Services.AddSingleton<IManageUserService>(ServiceProxy.Create<IManageUserService>(ServiceUris.ManageUsersUri));
                    builder.Services.AddSingleton<IManageProductService>(ServiceProxy.Create<IManageProductService>(ServiceUris.ManageProductsUri));
                    builder.Services.AddSingleton<IManageCustomerService>(ServiceProxy.Create<IManageCustomerService>(ServiceUris.ManageCustomersUri));
                    builder.Services.AddSingleton<IManageOrderService>(ServiceProxy.Create<IManageOrderService>(ServiceUris.ManageOrdersUri));
                    
                    var app = builder.Build();
                    
                    // Configure the HTTP request pipeline.
                    
                    app.UseHttpsRedirection();
                    
                    app.UseAuthorization();
                    

                    //Endpoint Handlers
                    app.MapPost("/token", async ([FromBody] GetTokenRequest loginRequest, IManageUserService managerUserSvc) =>
                    {
                        try
                        {
                            var (email, password) = loginRequest;
                            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || (!email.Contains("ndsu") && !email.Contains("ndus")))
                            {
                                return Results.BadRequest("invalid request");
                            }

                            var (t, err) = await managerUserSvc.GetTokenForUserAsync(loginRequest.email, loginRequest.password);

                            if (err != null)
                            {
                                return Results.BadRequest(err);
                            }

                            return Results.Ok(new {token = t });
                        }
                        catch (Exception e)
                        {
                            //log and cleanse response
                            return Results.BadRequest(e.Message);
                        }
                    });

                    app.MapPost("/orders", async ([FromBody] PlaceOrderRequest request, ClaimsPrincipal cp, IManageOrderService manageOrderSvc) =>
                    {
                        try
                        {
                            var userEmail = cp.Claims.First(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress").Value;
                            var (orderId, err) = await manageOrderSvc.PlaceOrderAsync(userEmail, request.customerEmail, request.customerName, request.products);

                            if (err is not null)
                            {
                                return Results.BadRequest(err);
                            }

                            return Results.Ok(new { Id = orderId } );
                        }
                        catch (Exception e)
                        {
                            //log and cleanse response
                            return Results.BadRequest(e.Message);
                        }
                    }).RequireAuthorization("basic");
                    app.MapGet("/orders/{id}", async (int id, IManageOrderService manageOrderSvc) =>
                    {
                        try
                        {
                            var (orderRes, err) = await manageOrderSvc.GetOrderByIdAsync(id);

                            if (err is not null || orderRes is null)
                            {
                                return Results.BadRequest(err ?? $"unable to find order for {id}");
                            }

                            var order = orderRes.Value;

                            return Results.Ok(new{Id = order.Id, UserId = order.UserId, CustomerId = order.CustomerId, Products = order.Products, DatePlaced = order.DatePlaced});
                        }
                        catch (Exception e)
                        {
                            //log and cleanse response
                            return Results.BadRequest(e.Message);
                        }
                    });
                    //app.MapGet("/orders", () => {});
                    //app.MapDelete("/orders/{id}", (string id) => {});

                    //TODO: maybe change one of these to search by query string
                    app.MapGet("/products", async (IManageProductService manageProductSvc) =>
                    {
                        try
                        {
                            var (products, err) = await manageProductSvc.GetProductsAsync();

                            if (err != null)
                            {
                                return Results.BadRequest(err);
                            }

                            List<Product> results = new();
                            foreach (var p in products)
                            {
                                results.Add(new (p.Id, p.Name, p.Description, p.Cost));
                            }
                            
                            return Results.Ok(results);
                        }
                        catch (Exception e)
                        {
                            //TODO: log and cleanse response
                            return Results.BadRequest(e.Message);
                        }
                    }).RequireAuthorization("basic");
                    //app.MapGet("/products/{id}", (string id) => {});
                    app.MapPost("/products", async ([FromBody] AddProductRequest request, IManageProductService manageProdSvc) =>
                    {
                        try
                        {
                            var (newProdId, err) = await manageProdSvc.AddProductAsync(request.name, request.description, request.price);

                            if (err != null)
                            {
                                return Results.BadRequest(err);
                            }

                            return Results.Ok(newProdId);
                        }
                        catch (Exception e)
                        {
                            //TODO: log and cleanse response
                            return Results.BadRequest(e.Message);
                        }
                    }).RequireAuthorization("basic");
                    //app.MapPut("/products", () => {});
                    //app.MapDelete("/products/{id}", (string id) => {});

                    //TODO: maybe change one of these to search by query string
                    app.MapGet("/customers", async (IManageCustomerService manageCustSvc) =>
                    {
                        try
                        {
                            var (customers, err) = await manageCustSvc.GetCustomersAsync();
                            if (err != null || customers is null)
                            {
                                return Results.BadRequest(err ?? "unable to get customers");
                            }

                            List<Customer> customersList = new();
                            foreach (var c in customers)
                            {
                                customersList.Add(new Customer(c.id, c.name, c.email));
                            }

                            return Results.Ok(customersList);
                        }
                        catch (Exception e)
                        {
                            //TODO: log and cleanse response
                            return Results.BadRequest(e.Message);
                        }
                    }).RequireAuthorization("basic");
                    //app.MapGet("/customers/{id}", (string id) => { });
                    //app.MapDelete("/customers/{id}", (string id) => { });

                    //app.MapGet("/users", () => { });
                    //app.MapGet("/users/{id}", (string id) => { });
                    app.MapPost("/users", async ([FromBody] AddUserRequest req, IManageUserService manageUserSvc) =>
                    {
                        try
                        {
                            if (!req.email.Contains("ndsu") && !req.email.Contains("ndus"))
                            {
                                return Results.BadRequest("Not Available For Your Email");
                            }

                            var addUserResult = await manageUserSvc.AddUserAsync(req.email, req.password);

                            if (addUserResult.IsFailed)
                            {
                                //TODO: log and cleanse error before returning
                                return Results.BadRequest(addUserResult.Errors.First().Message);
                            }

                            return Results.Ok();
                        }
                        catch (Exception e)
                        {
                            //TODO: log and cleanse error before returning
                            return Results.BadRequest(e.Message);
                        }
                    });
                    //app.MapPut("/users", () => {});
                    //app.MapDelete("/users/{id}", (string id) => { });
                    
                    return app;

                }))
        };
    }

    public record AddUserRequest(string email, string password);
    public record GetTokenRequest(string email, string password);
    public record AddProductRequest(string name, string description, decimal price);
    public record PlaceOrderRequest(string customerEmail, string customerName, List<string> products);
    public record Product(int Id, string Name, string Description, decimal Cost);
    public record Customer(int Id, string Name, string Email);

    /// <summary>
    /// Finds the ASP .NET Core HTTPS development certificate in development environment. Update this method to use the appropriate certificate for production environment.
    /// </summary>
    /// <returns>Returns the ASP .NET Core HTTPS development certificate</returns>
    private static X509Certificate2 GetCertificateFromStore()
    {
        string aspNetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (string.Equals(aspNetCoreEnvironment, "Development", StringComparison.OrdinalIgnoreCase))
        {
            const string aspNetHttpsOid = "1.3.6.1.4.1.311.84.1.1";
            const string CNName = "CN=localhost";
            using (X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadOnly);
                var certCollection = store.Certificates;
                var currentCerts = certCollection.Find(X509FindType.FindByExtension, aspNetHttpsOid, true);
                currentCerts = currentCerts.Find(X509FindType.FindByIssuerDistinguishedName, CNName, true);
                return currentCerts.Count == 0 ? null : currentCerts[0];
            }
        }
        else
        {
            throw new NotImplementedException("GetCertificateFromStore should be updated to retrieve the certificate for non Development environment");
        }
    }
}
