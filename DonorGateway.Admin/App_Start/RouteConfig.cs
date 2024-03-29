﻿using System.Web.Mvc;
using System.Web.Routing;

namespace DonorGateway.Admin
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute("HomeActions", "{action}", new { controller = "Home" });

            routes.MapRoute(
                          name: "Default",
                          url: "{controller}/{action}/{id}",
                          defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
                      );
        }
    }
}
