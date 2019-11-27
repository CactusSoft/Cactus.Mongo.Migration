using AspnetExample.Migrations;
using Cactus.Mongo.Migration;
using Cactus.Mongo.Migration.Extensions;
using Cactus.Mongo.Migration.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;

namespace AspnetExample
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // IMongoDatabase required to be in a service list
            services.AddSingleton(s => new MongoClient("mongodb://localhost").GetDatabase("testdb"));

            // Add a migration
            services.AddMigrations(
                s => UpgradeSettings.Default,
                s => s.GetRequiredService<IMongoDatabase>(),
                s => null, s => null);
            //services.AddMigrations(
            //    new DbInit(),
            //    new IUpgradeLink[] { new FirstMigration(), new SecondMigration() });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.ApplicationServices.UpgradeMongo();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Hello World!");
                });
            });
        }
    }
}
