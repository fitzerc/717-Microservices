using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _717_Microservices.Shared
{
    public class ServiceUris
    {
        public static Uri ManageUsersUri = new Uri("fabric:/717-Microservices.ManageUsersService/Microservices.ManageUsers");
        public static Uri ManageCustomersUri = new Uri("fabric:/717-Microservices.ManageCustomerService/ManageCustomers");
        public static Uri ManageProductsUri = new Uri("fabric:/717-Microservices.ManageProductsService/ManageProductsService");
        public static Uri ManageOrdersUri = new Uri("fabric:/717-Microservices.ManageOrdersService/ManageOrdersService");
    }
}
