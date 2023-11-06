using FluentResults;
using Microsoft.ServiceFabric.Services.Remoting;

namespace _717_Microservices.Shared;

public interface IManageUserService : IService
{
    Task<(string? UserId, string? err)> GetUserIdByEmailAsync(string email);
    Task<Result> AddUserAsync(string email, string password);
    Task<(string? token, string? err)> GetTokenForUserAsync(string email, string password);
}

public interface IManageCustomerService : IService
{
    Task<(int? custId, string? error)> AddCustomerAsync(string name, string email);
    Task<(List<(int id, string name, string email)>? custs, string? error)> GetCustomersAsync();
    Task<(int? custId, string? err)> GetCustomerIdByEmailAsync(string emial);
}

public interface IManageProductService : IService
{
    Task<(int? productId, string? error)> AddProductAsync(string name, string description, decimal price);
    Task<(List<(int Id, string Name, string Description, decimal Cost)>? products,  string? error)> GetProductsAsync();
    Task<(int? id, string? err)> GetProductIdByNameAsync(string name);
    Task<(string? name, string? err)> GetProductNameByIdAsync(int id);
}

public interface IManageOrderService : IService
{
    Task<(int? orderId, string? error)> PlaceOrderAsync(string userEmail, string customerEmail, string CustomerName, List<string> products);
    Task<((int Id, string UserId, int CustomerId, List<string> Products, DateTime DatePlaced)? orders, string? err)> GetOrderByIdAsync(int orderId);
    Task<(List<(int Id, string UserId, int CustomerId, List<int> Orders, DateTime DatePlaced)>? orders, string? err)> GetOrdersByCustomerAsync(string customerEmail);
    Task<(List<(int Id, string UserId, int CustomerId, List<int> Orders, DateTime DatePlaced)>? orders, string? err)> GetOrdersByUserAsync(string userEmail);
}