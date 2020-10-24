using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SpelosNet.Core.Repositories;
using SpelosNet.Core.Services;
using SpelosNet.Infrastructure;
using SpelosNet.Infrastructure.Repositories;
using SpelosNet.Infrastructure.Spotify;

namespace SpelosNet.WebApp
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();

            services.AddSingleton<JsonStorage>();
            services.AddScoped<IUrlRepository, UrlRepository>();
            services.AddScoped<IUrlShortener, UrlShortener>();

            var spotifyClientId = Configuration.GetValue<string>("Spotify:ClientId");
            var spotifyClientSecret = Configuration.GetValue<string>("Spotify:ClientSecret");
            var spotifyMyUserId = Configuration.GetValue<string>("Spotify:MyUserId");
            var spotifyConfig = new SpotifyConfig(spotifyClientId, spotifyClientSecret, spotifyMyUserId);
            services.AddSingleton<SpotifyApi>(p => new SpotifyApi(spotifyConfig));
            services.AddTransient<ISpotifyService, SpotifyService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
