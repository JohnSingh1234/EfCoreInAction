﻿using System;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using DataLayer.EfCode;
using DataNoSql;
using EfCoreInAction.Logger;
using EfCoreInAction.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceLayer.DatabaseServices.Concrete;

namespace EfCoreInAction
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            var gitBranchName = DatabaseStartupHelpers.GetWwwRootPath().GetBranchName();

            // Add framework services.
            services.AddMvc();

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            //This makes the Git branch name available via injection
            services.AddSingleton(new AppInformation(gitBranchName));

            var connection = Configuration.GetConnectionString("DefaultConnection");
            var ravenDbConnection = Configuration.GetConnectionString("RavenDbConnection");

            services.AddSingleton(ctr =>
            {
                var logger = ctr.GetService<ILogger<RavenStore>>();
                return new RavenStore(ravenDbConnection, logger);
            });
            services.AddSingleton<IUpdateCreator>(ctr => ctr.GetService<RavenStore>());
            services.AddSingleton<IQueryCreator>(ctr => ctr.GetService<RavenStore>());

            services.AddDbContext<EfCoreContext>(
                options => options.UseMySql(connection,
                    b => b.MigrationsAssembly("DataLayer")
                          .MaxBatchSize(1))); //Needed to overcome https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues/397

            //Add AutoFac
            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterModule<ServiceLayer.Utils.MyAutoFacModule>();
            containerBuilder.Populate(services);
            var container = containerBuilder.Build();
            return new AutofacServiceProvider(container);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, 
            ILoggerFactory loggerFactory, IHttpContextAccessor httpContextAccessor)
        {
            //Remove the standard loggers because they slow the applictaion down
            //loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            //loggerFactory.AddDebug();
            loggerFactory.AddProvider(new RequestTransientLogger(() => httpContextAccessor));
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
