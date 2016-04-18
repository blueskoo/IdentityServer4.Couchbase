﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Couchbase;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.Linq;
using Couchbase.Management;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Extensions.Logging;
using IdentityServer4.Couchbase;
using Microsoft.Extensions.Configuration;
using Identity.Couchbase;
using IdentityServer4.Core.Models;
using IdentityServer4.Couchbase.Services;
using Microsoft.AspNet.Identity;

namespace IdSvrHost
{
    public class Startup
    {
        readonly IApplicationEnvironment _environment;
        readonly IConfiguration _configuration;

        public Startup(IApplicationEnvironment environment)
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json");
            _configuration = builder.Build();

            _environment = environment;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var cert = new X509Certificate2(Path.Combine(_environment.ApplicationBasePath, "idsrv4test.pfx"), "idsrv3test");
            
            ClusterHelper.Initialize(new ClientConfiguration()
            {
                Servers = new List<Uri>()
                    {
                        new Uri(_configuration.Get<string>("couchbase:server"))
                    }
            });

            services.AddSingleton(p => ClusterHelper.GetBucket(_configuration.Get<string>("couchbase:bucket")));
            services.AddSingleton<IBucketContext>(p => new BucketContext(p.GetService<IBucket>()));
            services.AddSingleton(p => p.GetService<IBucket>().CreateManager(_configuration.Get<string>("couchbase:username"),
                    _configuration.Get<string>("couchbase:password")));

            var builder = services.AddIdentityServer(options =>
            {
                options.SigningCertificate = cert;
            });

            builder.AddCouchbaseClients();
            builder.AddCouchbaseScopes();
            builder.AddCouchbaseUsers<CouchbaseUser>();

            var identityBuilder = services.AddIdentity<CouchbaseUser, CouchbaseRole>();
            identityBuilder.AddCouchbaseStores<CouchbaseUser, CouchbaseRole>();

            // for the UI
            services
                .AddMvc()
                .AddRazorOptions(razor =>
                {
                    razor.ViewLocationExpanders.Add(new UI.CustomViewLocationExpander());
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory, IBucketManager bucketManager, ICouchbaseClientStore clientStore, ICouchbaseScopeStore scopeStore)
        {
            // Create the couchbase index for our N1QL quieres 
            bucketManager.CreateNamedPrimaryIndex("primary", false);
            
            // Our client for the Mvc sample site
            var client = new Client
            {
                ClientId = "mvc_implicit",
                ClientName = "MVC Implicit",
                ClientUri = "http://identityserver.io",

                Flow = Flows.Implicit,
                RedirectUris = new List<string>
                {
                    "http://localhost:64538/signin-oidc"
                },

                AllowedScopes = new List<string>
                {
                    StandardScopes.OpenId.Name,
                    StandardScopes.Profile.Name,
                    StandardScopes.Email.Name,
                    StandardScopes.Roles.Name,

                    "api1",
                    "api2"
                }
            };

            clientStore.StoreClientAsync(client);

            // Scopes supported by IdentityServer
            scopeStore.StoreScopeAsync(StandardScopes.OpenId);
            scopeStore.StoreScopeAsync(StandardScopes.Profile);
            scopeStore.StoreScopeAsync(StandardScopes.Email);
            scopeStore.StoreScopeAsync(StandardScopes.Roles);

            var apiScope = new Scope
            {
                Name = "api1",
                DisplayName = "API 1",
                Description = "API 1 features and data",
                Type = ScopeType.Identity,

                ScopeSecrets = new List<Secret>
                {
                    new Secret("secret".Sha256())
                },
                Claims = new List<ScopeClaim>
                {
                    new ScopeClaim("role"),
                    new ScopeClaim("ross")
                }
            };

            scopeStore.StoreScopeAsync(apiScope);


            var userManager = app.ApplicationServices.GetService<UserManager<CouchbaseUser>>();
            userManager.CreateAsync(new CouchbaseUser()
            {
                Username = "AliceSmith@email.com",
                Email = "AliceSmith@email.com"
            }, "alice");

            app.UseIdentity();

            loggerFactory.AddConsole(LogLevel.Verbose);
            loggerFactory.AddDebug(LogLevel.Verbose);

            app.UseDeveloperExceptionPage();
            app.UseIISPlatformHandler();

            app.UseIdentityServer();

            app.UseStaticFiles();
            app.UseMvcWithDefaultRoute();
        }

        // Entry point for the application.
        public static void Main(string[] args) => WebApplication.Run<Startup>(args);
    }
}